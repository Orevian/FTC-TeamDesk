using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data;

public sealed class DatabaseMaintenance : IDatabaseMaintenance
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;

    public DatabaseMaintenance(string databasePath, IDbContextFactory<TeamDeskDbContext> factory)
    {
        DatabasePath = databasePath;
        _factory = factory;
    }

    public string DatabasePath { get; }

    public async Task BackupToAsync(string destinationFile, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(destinationFile)) File.Delete(destinationFile);

        await using (var source = new SqliteConnection(DataServiceCollectionExtensions.BuildConnectionString(DatabasePath)))
        await using (var target = new SqliteConnection($"Data Source={destinationFile};Pooling=False"))
        {
            await source.OpenAsync(ct).ConfigureAwait(false);
            await target.OpenAsync(ct).ConfigureAwait(false);
            source.BackupDatabase(target); // consistent online backup, safe with WAL
        }
        SqliteConnection.ClearAllPools();
    }

    public async Task RestoreFromAsync(string sourceFile, CancellationToken ct = default)
    {
        if (!File.Exists(sourceFile)) throw new DomainException("Error.FileNotFound");

        // Validate that the file is a TeamDesk database before touching the live one.
        await using (var probe = new SqliteConnection($"Data Source={sourceFile};Mode=ReadOnly;Pooling=False"))
        {
            try
            {
                await probe.OpenAsync(ct).ConfigureAwait(false);
                await using var cmd = probe.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('SchemaVersion','Members','PasswordVault');";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                if (count < 3) throw new DomainException("Error.InvalidBackup");
            }
            catch (SqliteException)
            {
                throw new DomainException("Error.InvalidBackup");
            }
        }

        SqliteConnection.ClearAllPools();
        if (File.Exists(DatabasePath))
        {
            var safety = Path.Combine(Path.GetDirectoryName(DatabasePath)!,
                $"pre-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db.bak");
            File.Copy(DatabasePath, safety, overwrite: true);
        }
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = DatabasePath + suffix;
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }
        File.Copy(sourceFile, DatabasePath, overwrite: true);
    }

    public async Task<DataSnapshot> ExportSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var snap = new DataSnapshot();

        snap.MemberStatuses = await db.MemberStatuses.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        snap.Areas = await db.Areas.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        snap.Skills = await db.Skills.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

        var members = await db.Members.AsNoTracking().AsSplitQuery()
            .Include(m => m.MemberSkills).Include(m => m.MemberAreas)
            .Include(m => m.PerformanceRecords).Include(m => m.CustomFields).ToListAsync(ct).ConfigureAwait(false);
        foreach (var m in members)
        {
            var ms = new MemberSnapshot
            {
                SkillIds = m.MemberSkills.Select(x => x.SkillId).ToList(),
                AreaIds = m.MemberAreas.Select(x => x.AreaId).ToList(),
                PerformanceRecords = m.PerformanceRecords.Select(r => new PerformanceRecord
                { Id = r.Id, MemberId = r.MemberId, AreaId = r.AreaId, RecordedOn = r.RecordedOn, Score = r.Score, Note = r.Note }).ToList(),
                CustomFields = m.CustomFields.Select(f => new MemberCustomField { Id = f.Id, MemberId = f.MemberId, Name = f.Name, Value = f.Value }).ToList(),
                Member = new Member
                {
                    Id = m.Id, FullName = m.FullName, Email = m.Email, Phone = m.Phone, AvatarPath = null, StatusId = m.StatusId,
                    JoinDate = m.JoinDate, Responsibilities = m.Responsibilities, Notes = m.Notes, CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
                }
            };
            snap.Members.Add(ms);
        }

        snap.Applications = await db.Applications.AsNoTracking().Include(a => a.Answers).ToListAsync(ct).ConfigureAwait(false);
        snap.Budgets = await db.Budgets.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        snap.BudgetCategories = await db.BudgetCategories.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        snap.Purchases = await db.Purchases.AsNoTracking().ToListAsync(ct).ConfigureAwait(false); // Category nav not loaded
        snap.Tasks = await db.Tasks.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        snap.Activity = await db.ActivityLog.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        // Vault: ciphertext only. Never decrypted, never plaintext.
        snap.EncryptedVaultEntries = await db.VaultEntries.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        snap.VaultMeta = await db.VaultMeta.AsNoTracking().FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (snap.VaultMeta is not null)
        {
            snap.VaultMeta = new VaultMeta
            {
                Id = 1, Salt = snap.VaultMeta.Salt, Iterations = snap.VaultMeta.Iterations, Verifier = snap.VaultMeta.Verifier,
                FailedAttempts = 0, LockedUntil = null
            };
        }
        return snap;
    }

    public async Task<int> ImportSnapshotAsync(DataSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot.FormatVersion > 1) throw new DomainException("Error.UnsupportedFormat");
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var added = 0;

        // Reference data is matched by Key/Name so imported records get re-pointed at local rows.
        var statusMap = new Dictionary<Guid, Guid>();
        var localStatuses = await db.MemberStatuses.ToListAsync(ct).ConfigureAwait(false);
        foreach (var s in snapshot.MemberStatuses)
        {
            var local = localStatuses.FirstOrDefault(x => x.Key == s.Key);
            if (local is null)
            {
                local = new MemberStatus { Id = s.Id, Key = s.Key, Name = s.Name, Color = s.Color, IsSystem = s.IsSystem, SortOrder = s.SortOrder };
                db.MemberStatuses.Add(local); localStatuses.Add(local); added++;
            }
            statusMap[s.Id] = local.Id;
        }
        var areaMap = new Dictionary<Guid, Guid>();
        var localAreas = await db.Areas.ToListAsync(ct).ConfigureAwait(false);
        foreach (var a in snapshot.Areas)
        {
            var local = localAreas.FirstOrDefault(x => x.Key == a.Key);
            if (local is null)
            {
                local = new Area { Id = a.Id, Key = a.Key, Name = a.Name, IsSystem = a.IsSystem, SortOrder = a.SortOrder };
                db.Areas.Add(local); localAreas.Add(local); added++;
            }
            areaMap[a.Id] = local.Id;
        }
        var skillMap = new Dictionary<Guid, Guid>();
        var localSkills = await db.Skills.ToListAsync(ct).ConfigureAwait(false);
        foreach (var s in snapshot.Skills)
        {
            var local = localSkills.FirstOrDefault(x => x.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            if (local is null)
            {
                local = new Skill { Id = s.Id, Name = s.Name };
                db.Skills.Add(local); localSkills.Add(local); added++;
            }
            skillMap[s.Id] = local.Id;
        }
        var categoryMap = new Dictionary<Guid, Guid>();
        var localCategories = await db.BudgetCategories.ToListAsync(ct).ConfigureAwait(false);
        foreach (var c in snapshot.BudgetCategories)
        {
            var local = localCategories.FirstOrDefault(x => x.Key == c.Key);
            if (local is null)
            {
                local = new BudgetCategory { Id = c.Id, Key = c.Key, Name = c.Name, IsSystem = c.IsSystem, PlannedAmount = c.PlannedAmount, SortOrder = c.SortOrder };
                db.BudgetCategories.Add(local); localCategories.Add(local); added++;
            }
            categoryMap[c.Id] = local.Id;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var knownMembers = (await db.Members.Select(m => m.Id).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        var fallbackStatus = localStatuses.OrderBy(s => s.SortOrder).First().Id;
        foreach (var ms in snapshot.Members.Where(m => !knownMembers.Contains(m.Member.Id)))
        {
            var m = ms.Member;
            m.StatusId = statusMap.GetValueOrDefault(m.StatusId, fallbackStatus);
            m.Status = null;
            m.MemberSkills = new(); m.MemberAreas = new(); m.PerformanceRecords = new(); m.CustomFields = new();
            db.Members.Add(m);
            foreach (var sid in ms.SkillIds.Where(skillMap.ContainsKey).Select(id => skillMap[id]).Distinct())
                db.MemberSkills.Add(new MemberSkill { MemberId = m.Id, SkillId = sid });
            foreach (var aid in ms.AreaIds.Where(areaMap.ContainsKey).Select(id => areaMap[id]).Distinct())
                db.MemberAreas.Add(new MemberArea { MemberId = m.Id, AreaId = aid });
            foreach (var r in ms.PerformanceRecords)
            {
                r.MemberId = m.Id;
                r.AreaId = r.AreaId is { } ra && areaMap.TryGetValue(ra, out var mapped) ? mapped : null;
                r.Area = null;
                db.PerformanceRecords.Add(r);
            }
            foreach (var f in ms.CustomFields) { f.MemberId = m.Id; db.MemberCustomFields.Add(f); }
            added++;
        }

        var knownApps = (await db.Applications.Select(a => a.Id).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        var knownKeys = (await db.Applications.Where(a => a.ExternalKey != null).Select(a => a.ExternalKey!).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        foreach (var a in snapshot.Applications.Where(a => !knownApps.Contains(a.Id) && (a.ExternalKey == null || !knownKeys.Contains(a.ExternalKey))))
        { db.Applications.Add(a); added++; }

        var knownPurchases = (await db.Purchases.Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        var fallbackCategory = localCategories.OrderBy(c => c.SortOrder).Last().Id;
        foreach (var p in snapshot.Purchases.Where(p => !knownPurchases.Contains(p.Id)))
        {
            p.CategoryId = categoryMap.GetValueOrDefault(p.CategoryId, fallbackCategory);
            p.Category = null;
            db.Purchases.Add(p); added++;
        }

        var knownTasks = (await db.Tasks.Select(t => t.Id).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        foreach (var t in snapshot.Tasks.Where(t => !knownTasks.Contains(t.Id))) { db.Tasks.Add(t); added++; }

        var knownActivity = (await db.ActivityLog.Select(t => t.Id).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        foreach (var e in snapshot.Activity.Where(e => !knownActivity.Contains(e.Id))) db.ActivityLog.Add(e);

        // Encrypted vault data can only be adopted when this database has no vault yet (different vaults use different keys).
        if (snapshot.VaultMeta is not null && !await db.VaultMeta.AnyAsync(ct).ConfigureAwait(false))
        {
            snapshot.VaultMeta.Id = 1;
            db.VaultMeta.Add(snapshot.VaultMeta);
            foreach (var v in snapshot.EncryptedVaultEntries) db.VaultEntries.Add(v);
            added += snapshot.EncryptedVaultEntries.Count;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return added;
    }

    public async Task<bool> SeedDemoDataAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        if (await db.Members.AnyAsync(ct).ConfigureAwait(false)) return false;
        await DemoDataSeeder.SeedAsync(db, ct).ConfigureAwait(false);
        return true;
    }
}
