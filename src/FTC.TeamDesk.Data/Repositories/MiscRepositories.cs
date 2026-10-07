using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public TaskRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<TeamTask>> ListAsync(bool includeCompleted, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var q = db.Tasks.AsNoTracking();
        if (!includeCompleted) q = q.Where(t => !t.IsCompleted);
        var list = await q.ToListAsync(ct).ConfigureAwait(false);
        return list.OrderBy(t => t.IsCompleted).ThenBy(t => t.DueDate ?? DateTime.MaxValue).ThenBy(t => t.CreatedAt).ToList();
    }

    public async Task<TeamTask> SaveAsync(TeamTask task, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.Tasks.FirstOrDefaultAsync(t => t.Id == task.Id, ct).ConfigureAwait(false);
        if (existing is null) { db.Tasks.Add(task); existing = task; }
        else
        {
            existing.Title = task.Title;
            existing.Notes = task.Notes;
            existing.DueDate = task.DueDate;
            existing.IsCompleted = task.IsCompleted;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}

public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public ActivityLogRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task AddAsync(ActivityLogEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ActivityLog.Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int count, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ActivityLog.AsNoTracking().OrderByDescending(a => a.Timestamp).Take(count).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityQuery query, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        IQueryable<ActivityLogEntry> q = db.ActivityLog.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var p = $"%{query.Search.Trim()}%";
            q = q.Where(a => EF.Functions.Like(a.Description, p) || EF.Functions.Like(a.ActionType, p));
        }
        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 500);
        var items = await q.OrderByDescending(a => a.Timestamp).Skip((page - 1) * size).Take(size).ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<ActivityLogEntry>(items, total, page, size);
    }
}

public sealed class AiConfigRepository : IAiConfigRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public AiConfigRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<AiSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var s = await db.AiSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct).ConfigureAwait(false);
        if (s is not null) return s;
        s = new AiSettings { Id = 1 };
        db.AiSettings.Add(s);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return s;
    }

    public async Task SaveSettingsAsync(AiSettings settings, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var s = await db.AiSettings.FirstOrDefaultAsync(x => x.Id == 1, ct).ConfigureAwait(false);
        if (s is null) { s = new AiSettings { Id = 1 }; db.AiSettings.Add(s); }
        s.Provider = settings.Provider;
        s.Model = settings.Model;
        s.Temperature = settings.Temperature;
        s.MaxTokens = settings.MaxTokens;
        s.BaseUrl = settings.BaseUrl;
        s.IsEnabled = settings.IsEnabled;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<AiPermissionKey, bool>> GetPermissionsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.AiPermissions.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var result = Enum.GetValues<AiPermissionKey>().ToDictionary(k => k, _ => false);
        foreach (var row in rows)
            if (Enum.TryParse<AiPermissionKey>(row.Key, out var key)) result[key] = row.IsEnabled;
        return result;
    }

    public async Task SetPermissionAsync(AiPermissionKey key, bool enabled, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var name = key.ToString();
        var row = await db.AiPermissions.FirstOrDefaultAsync(p => p.Key == name, ct).ConfigureAwait(false);
        if (row is null) db.AiPermissions.Add(new AiPermission { Key = name, IsEnabled = enabled });
        else row.IsEnabled = enabled;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

public sealed class VaultRepository : IVaultRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public VaultRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<VaultMeta?> GetMetaAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.VaultMeta.AsNoTracking().FirstOrDefaultAsync(m => m.Id == 1, ct).ConfigureAwait(false);
    }

    public async Task SaveMetaAsync(VaultMeta meta, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.VaultMeta.FirstOrDefaultAsync(m => m.Id == 1, ct).ConfigureAwait(false);
        if (existing is null) { meta.Id = 1; db.VaultMeta.Add(meta); }
        else
        {
            existing.Salt = meta.Salt;
            existing.Iterations = meta.Iterations;
            existing.Verifier = meta.Verifier;
            existing.FailedAttempts = meta.FailedAttempts;
            existing.LockedUntil = meta.LockedUntil;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VaultEntry>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.VaultEntries.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<VaultEntry?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.VaultEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(VaultEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.VaultEntries.Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(VaultEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.VaultEntries.FirstOrDefaultAsync(e => e.Id == entry.Id, ct).ConfigureAwait(false)
                       ?? throw new DomainException("Error.NotFound");
        existing.EncryptedPayload = entry.EncryptedPayload;
        existing.UpdatedAt = entry.UpdatedAt;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.VaultEntries.Where(e => e.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    public async Task ReplaceAllAsync(VaultMeta meta, IReadOnlyList<VaultEntry> entries, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var existingMeta = await db.VaultMeta.FirstOrDefaultAsync(m => m.Id == 1, ct).ConfigureAwait(false);
        if (existingMeta is null) { meta.Id = 1; db.VaultMeta.Add(meta); }
        else
        {
            existingMeta.Salt = meta.Salt;
            existingMeta.Iterations = meta.Iterations;
            existingMeta.Verifier = meta.Verifier;
            existingMeta.FailedAttempts = meta.FailedAttempts;
            existingMeta.LockedUntil = meta.LockedUntil;
        }
        var byId = await db.VaultEntries.ToDictionaryAsync(e => e.Id, ct).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            if (byId.TryGetValue(entry.Id, out var row))
            {
                row.EncryptedPayload = entry.EncryptedPayload;
                row.UpdatedAt = entry.UpdatedAt;
            }
            else db.VaultEntries.Add(entry);
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }
}

public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public AppSettingsRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct).ConfigureAwait(false);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else row.Value = value;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
