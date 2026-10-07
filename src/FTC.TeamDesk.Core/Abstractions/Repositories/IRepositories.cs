using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Core.Abstractions.Repositories;

public interface IMemberRepository
{
    Task<IReadOnlyList<Member>> ListAsync(MemberFilter filter, CancellationToken ct = default);
    Task<Member?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> SaveAsync(MemberEditModel model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<MemberStatus>> ListStatusesAsync(CancellationToken ct = default);
    Task<MemberStatus> SaveStatusAsync(MemberStatus status, CancellationToken ct = default);
    /// <summary>Deletes a custom status. Throws <see cref="DomainException"/> if it is a system status or in use.</summary>
    Task DeleteStatusAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Area>> ListAreasAsync(CancellationToken ct = default);
    Task<Area> SaveAreaAsync(Area area, CancellationToken ct = default);
    Task<IReadOnlyList<Skill>> ListSkillsAsync(CancellationToken ct = default);

    Task<PerformanceRecord> AddRecordAsync(PerformanceRecord record, CancellationToken ct = default);
    Task DeleteRecordAsync(Guid id, CancellationToken ct = default);
    /// <summary>All development records (optionally for one member), including the Area navigation.</summary>
    Task<IReadOnlyList<PerformanceRecord>> ListRecordsAsync(Guid? memberId, CancellationToken ct = default);
}

public interface IApplicationRepository
{
    Task<PagedResult<TeamApplication>> QueryAsync(ApplicationQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<TeamApplication>> ListAllAsync(CancellationToken ct = default);
    Task<TeamApplication?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TeamApplication>> RecentAsync(int count, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, ApplicationStatus status, CancellationToken ct = default);
    Task UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default);
    /// <summary>Adds applications whose ExternalKey is not yet known. Returns the number added.</summary>
    Task<int> AddImportedAsync(IReadOnlyList<TeamApplication> items, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IBudgetRepository
{
    Task<Budget> GetBudgetAsync(CancellationToken ct = default);
    Task SaveBudgetAsync(decimal totalAmount, string currency, CancellationToken ct = default);

    Task<IReadOnlyList<BudgetCategory>> ListCategoriesAsync(CancellationToken ct = default);
    Task<BudgetCategory> SaveCategoryAsync(BudgetCategory category, CancellationToken ct = default);
    /// <summary>Throws <see cref="DomainException"/> for system categories or categories that still have purchases.</summary>
    Task DeleteCategoryAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Purchase>> ListPurchasesAsync(CancellationToken ct = default);
    Task<Purchase?> GetPurchaseAsync(Guid id, CancellationToken ct = default);
    Task<Purchase> SavePurchaseAsync(Purchase purchase, CancellationToken ct = default);
    Task DeletePurchaseAsync(Guid id, CancellationToken ct = default);
}

public interface ITaskRepository
{
    Task<IReadOnlyList<TeamTask>> ListAsync(bool includeCompleted, CancellationToken ct = default);
    Task<TeamTask> SaveAsync(TeamTask task, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IActivityLogRepository
{
    Task AddAsync(ActivityLogEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int count, CancellationToken ct = default);
    Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityQuery query, CancellationToken ct = default);
}

public interface IAiConfigRepository
{
    Task<AiSettings> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(AiSettings settings, CancellationToken ct = default);
    Task<IReadOnlyDictionary<AiPermissionKey, bool>> GetPermissionsAsync(CancellationToken ct = default);
    Task SetPermissionAsync(AiPermissionKey key, bool enabled, CancellationToken ct = default);
}

public interface IVaultRepository
{
    Task<VaultMeta?> GetMetaAsync(CancellationToken ct = default);
    Task SaveMetaAsync(VaultMeta meta, CancellationToken ct = default);
    Task<IReadOnlyList<VaultEntry>> ListAsync(CancellationToken ct = default);
    Task<VaultEntry?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(VaultEntry entry, CancellationToken ct = default);
    Task UpdateAsync(VaultEntry entry, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Atomically replaces the meta row and all entries (used when the master password changes).</summary>
    Task ReplaceAllAsync(VaultMeta meta, IReadOnlyList<VaultEntry> entries, CancellationToken ct = default);
}

public interface IAppSettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
}

/// <summary>Whole-database operations: backup, restore, snapshot export/import.</summary>
public interface IDatabaseMaintenance
{
    string DatabasePath { get; }
    Task BackupToAsync(string destinationFile, CancellationToken ct = default);
    /// <summary>Replaces the live database file with the given SQLite file. The app must be restarted afterwards.</summary>
    Task RestoreFromAsync(string sourceFile, CancellationToken ct = default);
    Task<DataSnapshot> ExportSnapshotAsync(CancellationToken ct = default);
    /// <summary>Merges a snapshot into the database, adding records whose Id is unknown. Returns records added.</summary>
    Task<int> ImportSnapshotAsync(DataSnapshot snapshot, CancellationToken ct = default);
    /// <summary>Inserts sample data for demos. Returns false (and does nothing) if the database already contains members.</summary>
    Task<bool> SeedDemoDataAsync(CancellationToken ct = default);
}
