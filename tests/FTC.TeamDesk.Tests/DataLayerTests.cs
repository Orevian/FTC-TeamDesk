using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.Data;
using FTC.TeamDesk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FTC.TeamDesk.Tests;

/// <summary>Integration tests against a real temporary SQLite file: proves the SQL migration and the EF model agree.</summary>
public sealed class DataLayerTests : IAsyncLifetime
{
    private string _path = null!;
    private IDbContextFactory<TeamDeskDbContext> _factory = null!;

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"td-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<TeamDeskDbContext>().UseSqlite(DataServiceCollectionExtensions.BuildConnectionString(_path)).Options;
        _factory = new TestFactory(options);
        await new DatabaseInitializer(_path, _factory).InitializeAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" }) if (File.Exists(f)) File.Delete(f);
        return Task.CompletedTask;
    }

    private sealed class TestFactory : IDbContextFactory<TeamDeskDbContext>
    {
        private readonly DbContextOptions<TeamDeskDbContext> _o;
        public TestFactory(DbContextOptions<TeamDeskDbContext> o) => _o = o;
        public TeamDeskDbContext CreateDbContext() => new(_o);
    }

    [Fact]
    public async Task MigrationIsIdempotentAndSeedsReferenceData()
    {
        await new DatabaseInitializer(_path, _factory).InitializeAsync(); // second run must be a no-op
        var members = new MemberRepository(_factory);
        Assert.Equal(5, (await members.ListStatusesAsync()).Count);
        Assert.Equal(7, (await members.ListAreasAsync()).Count);
        Assert.Equal(10, (await new BudgetRepository(_factory).ListCategoriesAsync()).Count);
    }

    [Fact]
    public async Task AiDefaultsAreSafe()
    {
        var cfg = new AiConfigRepository(_factory);
        Assert.False((await cfg.GetSettingsAsync()).IsEnabled);
        var perms = await cfg.GetPermissionsAsync();
        Assert.False(perms[AiPermissionKey.AccessPasswordVault]);
        Assert.False(perms[AiPermissionKey.AddPurchase]);
        Assert.False(perms[AiPermissionKey.EditPurchase]);
        Assert.False(perms[AiPermissionKey.DeletePurchase]);
        Assert.False(perms[AiPermissionKey.EditMember]);
        Assert.False(perms[AiPermissionKey.ChangeApplicationStatus]);
    }

    [Fact]
    public async Task MemberSaveRoundTripWithAreasSkillsAndCustomFields()
    {
        var repo = new MemberRepository(_factory);
        var statusId = (await repo.ListStatusesAsync()).First().Id;
        var areaId = (await repo.ListAreasAsync()).First().Id;
        var id = await repo.SaveAsync(new MemberEditModel
        {
            FullName = "Ada", StatusId = statusId, AreaIds = { areaId }, SkillNames = { "Java", "java", "CAD" },
            CustomFields = { new("Shirt size", "M") }
        });
        var m = (await repo.GetAsync(id))!;
        Assert.Equal(2, m.MemberSkills.Count); // case-insensitive de-duplication
        Assert.Single(m.MemberAreas);
        Assert.Equal("M", m.CustomFields.Single().Value);

        // Edit: replace skills/areas/custom fields
        await repo.SaveAsync(new MemberEditModel { Id = id, FullName = "Ada S.", StatusId = statusId, SkillNames = { "CAD" }, CustomFields = { new("Size", "L") } });
        m = (await repo.GetAsync(id))!;
        Assert.Equal("Ada S.", m.FullName);
        Assert.Single(m.MemberSkills);
        Assert.Empty(m.MemberAreas);
        Assert.Equal("Size", m.CustomFields.Single().Name);
    }

    [Fact]
    public async Task CustomStatusInUseCannotBeDeleted()
    {
        var repo = new MemberRepository(_factory);
        var custom = await repo.SaveStatusAsync(new() { Key = "custom-x", Name = "On leave", IsSystem = false, Color = "#123456" });
        await repo.SaveAsync(new MemberEditModel { FullName = "Bo", StatusId = custom.Id });
        await Assert.ThrowsAsync<FTC.TeamDesk.Core.Common.DomainException>(() => repo.DeleteStatusAsync(custom.Id));
        var system = (await repo.ListStatusesAsync()).First(s => s.IsSystem);
        await Assert.ThrowsAsync<FTC.TeamDesk.Core.Common.DomainException>(() => repo.DeleteStatusAsync(system.Id));
    }

    [Fact]
    public async Task PurchasesPersistDecimalsExactly()
    {
        var repo = new BudgetRepository(_factory);
        var cat = (await repo.ListCategoriesAsync()).First();
        await repo.SavePurchaseAsync(new() { Product = "Motor", CategoryId = cat.Id, UnitPrice = 119.99m, Quantity = 3, Status = PurchaseStatus.Planned });
        var p = (await repo.ListPurchasesAsync()).Single();
        Assert.Equal(119.99m, p.UnitPrice);
        Assert.Equal(359.97m, p.Total);
        Assert.Equal(cat.Id, p.Category!.Id);
        await Assert.ThrowsAsync<FTC.TeamDesk.Core.Common.DomainException>(() => repo.DeleteCategoryAsync(cat.Id)); // system + in use
    }

    [Fact]
    public async Task ApplicationImportSkipsKnownExternalKeys()
    {
        var repo = new ApplicationRepository(_factory);
        TeamAppFactory Make() => new();
        Assert.Equal(2, await repo.AddImportedAsync(new[] { Make().Create("k1"), Make().Create("k2") }));
        Assert.Equal(1, await repo.AddImportedAsync(new[] { Make().Create("k1"), Make().Create("k3") }));
        Assert.Equal(3, await repo.CountAsync());
        var page = await repo.QueryAsync(new ApplicationQuery { Search = "answer" });
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, (await repo.GetAsync(page.Items[0].Id))!.Answers.Count);
    }

    private sealed class TeamAppFactory
    {
        public Core.Entities.TeamApplication Create(string key)
        {
            var a = new Core.Entities.TeamApplication { ApplicantName = "N " + key, ExternalKey = key };
            a.Answers.Add(new() { ApplicationId = a.Id, Question = "Q1", Answer = "answer 1", SortOrder = 0 });
            a.Answers.Add(new() { ApplicationId = a.Id, Question = "Q2", Answer = "answer 2", SortOrder = 1 });
            return a;
        }
    }

    [Fact]
    public async Task BackupRestoreAndSnapshotRoundTrip()
    {
        var maintenance = new DatabaseMaintenance(_path, _factory);
        Assert.True(await maintenance.SeedDemoDataAsync());
        Assert.False(await maintenance.SeedDemoDataAsync()); // never seeds twice

        var backup = Path.Combine(Path.GetTempPath(), $"td-backup-{Guid.NewGuid():N}.db");
        try
        {
            await maintenance.BackupToAsync(backup);
            Assert.True(new FileInfo(backup).Length > 0);
            var snapshot = await maintenance.ExportSnapshotAsync();
            Assert.NotEmpty(snapshot.Members);
            Assert.Empty(snapshot.EncryptedVaultEntries); // no vault created
        }
        finally { if (File.Exists(backup)) File.Delete(backup); }
    }

    [Fact]
    public async Task VaultRowsHoldOnlyBlobs()
    {
        var repo = new VaultRepository(_factory);
        await repo.SaveMetaAsync(new() { Salt = new byte[32], Iterations = 100000, Verifier = new byte[40] });
        await repo.AddAsync(new() { EncryptedPayload = new byte[] { 1, 2, 3 } });
        await using var conn = new SqliteConnection(DataServiceCollectionExtensions.BuildConnectionString(_path));
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('PasswordVault')";
        var columns = new List<string>();
        await using (var r = await cmd.ExecuteReaderAsync()) while (await r.ReadAsync()) columns.Add(r.GetString(0));
        Assert.DoesNotContain(columns, c => c.Contains("assword", StringComparison.OrdinalIgnoreCase) || c.Equals("Username", StringComparison.OrdinalIgnoreCase));
    }
}
