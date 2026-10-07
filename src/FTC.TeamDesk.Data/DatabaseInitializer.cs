using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data;

/// <summary>Creates/migrates the database and seeds the built-in reference data (statuses, areas, categories, AI defaults).</summary>
public sealed class DatabaseInitializer
{
    private readonly string _databasePath;
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;

    public DatabaseInitializer(string databasePath, IDbContextFactory<TeamDeskDbContext> factory)
    {
        _databasePath = databasePath;
        _factory = factory;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await SchemaMigrator.MigrateAsync(DataServiceCollectionExtensions.BuildConnectionString(_databasePath), ct).ConfigureAwait(false);
        await SeedAsync(ct).ConfigureAwait(false);
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        if (!await db.MemberStatuses.AnyAsync(ct).ConfigureAwait(false))
        {
            var order = 0;
            db.MemberStatuses.AddRange(SystemKeys.MemberStatuses.Select(s => new MemberStatus
            { Key = s.Key, Color = s.Color, IsSystem = true, SortOrder = order++ }));
        }
        if (!await db.Areas.AnyAsync(ct).ConfigureAwait(false))
        {
            var order = 0;
            db.Areas.AddRange(SystemKeys.Areas.Select(a => new Area { Key = a, IsSystem = true, SortOrder = order++ }));
        }
        if (!await db.BudgetCategories.AnyAsync(ct).ConfigureAwait(false))
        {
            var order = 0;
            db.BudgetCategories.AddRange(SystemKeys.BudgetCategories.Select(c => new BudgetCategory
            { Key = c, IsSystem = true, SortOrder = order++ }));
        }
        if (!await db.Budgets.AnyAsync(ct).ConfigureAwait(false))
            db.Budgets.Add(new Budget { Name = "Season budget", TotalAmount = 0m, Currency = "USD" });

        if (!await db.AiSettings.AnyAsync(ct).ConfigureAwait(false))
            db.AiSettings.Add(new AiSettings { Id = 1, IsEnabled = false });

        var existing = (await db.AiPermissions.Select(p => p.Key).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        foreach (var key in Enum.GetValues<AiPermissionKey>())
        {
            if (existing.Contains(key.ToString())) continue;
            // Safe defaults: read-only access to team data; every write and vault access is OFF.
            var readOnlyDefault = key is AiPermissionKey.ReadMembers or AiPermissionKey.ReadApplications
                or AiPermissionKey.ReadBudget or AiPermissionKey.ReadPurchases or AiPermissionKey.ReadAnalytics;
            db.AiPermissions.Add(new AiPermission { Key = key.ToString(), IsEnabled = readOnlyDefault });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
