using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Core.Abstractions.Services;

public interface IMemberService
{
    Task<IReadOnlyList<Member>> ListAsync(MemberFilter filter, CancellationToken ct = default);
    Task<Member?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> SaveAsync(MemberEditModel model, ActivityActor actor = ActivityActor.User, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MemberStatus>> ListStatusesAsync(CancellationToken ct = default);
    Task<MemberStatus> SaveStatusAsync(Guid? id, string name, string color, CancellationToken ct = default);
    Task DeleteStatusAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Area>> ListAreasAsync(CancellationToken ct = default);
    Task<Area> AddAreaAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Skill>> ListSkillsAsync(CancellationToken ct = default);
    Task<PerformanceRecord> AddRecordAsync(Guid memberId, Guid? areaId, DateTime date, int score, string? note, CancellationToken ct = default);
    Task DeleteRecordAsync(Guid memberId, Guid recordId, CancellationToken ct = default);
}

public interface IApplicationService
{
    Task<PagedResult<TeamApplication>> QueryAsync(ApplicationQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<TeamApplication>> ListAllAsync(CancellationToken ct = default);
    Task<TeamApplication?> GetAsync(Guid id, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, ApplicationStatus status, ActivityActor actor = ActivityActor.User, CancellationToken ct = default);
    Task SetNotesAsync(Guid id, string? notes, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> ImportAsync(IReadOnlyList<TeamApplication> items, CancellationToken ct = default);
}

public interface IBudgetService
{
    Task<BudgetSummary> GetSummaryAsync(CancellationToken ct = default);
    Task SetBudgetAsync(decimal total, string currency, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetCategory>> ListCategoriesAsync(CancellationToken ct = default);
    Task<BudgetCategory> SaveCategoryAsync(CategoryEditModel model, CancellationToken ct = default);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Purchase>> ListPurchasesAsync(CancellationToken ct = default);
    Task<Purchase> SavePurchaseAsync(PurchaseEditModel model, ActivityActor actor = ActivityActor.User, CancellationToken ct = default);
    Task DeletePurchaseAsync(Guid id, ActivityActor actor = ActivityActor.User, CancellationToken ct = default);
    Task<WhatIfResult> SimulatePurchaseAsync(PurchaseEditModel model, CancellationToken ct = default);
}

public interface ITaskService
{
    Task<IReadOnlyList<TeamTask>> ListAsync(bool includeCompleted, CancellationToken ct = default);
    Task<TeamTask> SaveAsync(Guid? id, string title, DateTime? dueDate, string? notes, CancellationToken ct = default);
    Task SetCompletedAsync(Guid id, bool completed, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardData> GetAsync(CancellationToken ct = default);
}

public interface IAnalyticsService
{
    Task<AnalyticsData> GetAsync(AnalyticsFilter filter, CancellationToken ct = default);
    Task<TeamSummary> GetTeamSummaryAsync(CancellationToken ct = default);
}

public sealed record TeamSummary(
    DashboardKpis Kpis, BudgetSummary Budget, IReadOnlyList<NamedCount> MemberDistribution,
    IReadOnlyDictionary<ApplicationStatus, int> ApplicationsByStatus, int OpenTasks);

public interface IActivityLogService
{
    /// <summary>Records an event. Descriptions are redacted for secrets before persisting.</summary>
    Task LogAsync(string actionType, string description, ActivityActor actor = ActivityActor.User, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int count, CancellationToken ct = default);
    Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityQuery query, CancellationToken ct = default);
}

public interface ISettingsService
{
    UserPreferences Current { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(UserPreferences preferences, CancellationToken ct = default);
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
    Task SetValueAsync(string key, string? value, CancellationToken ct = default);
    event EventHandler? PreferencesChanged;
}

/// <summary>Stores secrets (API keys, OAuth tokens) encrypted at rest for the current Windows user.</summary>
public interface ISecretStore
{
    Task SetAsync(string name, string secret, CancellationToken ct = default);
    Task<string?> GetAsync(string name, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsAsync(string name, CancellationToken ct = default);
}

public static class SecretNames
{
    public const string AiApiKey = "ai.apikey";          // suffixed per provider: ai.apikey.{Provider}
    public const string GoogleTokens = "google.tokens";
    public const string GoogleClientSecret = "google.clientsecret";
    public static string AiKeyFor(AiProviderKind kind) => $"{AiApiKey}.{kind}";
}

public interface IVaultService
{
    bool IsInitialized { get; }
    bool IsUnlocked { get; }
    event EventHandler? StateChanged;

    Task InitializeAsync(CancellationToken ct = default);
    Task CreateAsync(string masterPassword, CancellationToken ct = default);
    Task<VaultUnlockResult> UnlockAsync(string masterPassword, CancellationToken ct = default);
    void Lock(string reason = "manual");
    /// <summary>Resets the auto-lock timer. Call on user activity in the vault.</summary>
    void Touch();

    Task<IReadOnlyList<VaultItem>> ListAsync(CancellationToken ct = default);
    Task<Guid> SaveAsync(VaultItemEdit edit, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Decrypts and returns a password. Callers must have obtained explicit user confirmation.</summary>
    Task<string> RevealPasswordAsync(Guid id, CancellationToken ct = default);
    Task ChangeMasterPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
}

public interface IGoogleSheetsService
{
    Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken ct = default);
    /// <summary>Runs the OAuth consent flow in the default browser (loopback + PKCE).</summary>
    Task<OperationResult> ConnectAsync(string clientId, string? clientSecret, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<OperationResult<IReadOnlyList<SpreadsheetInfo>>> ListSpreadsheetsAsync(CancellationToken ct = default);
    Task<OperationResult<IReadOnlyList<string>>> ListTabsAsync(string spreadsheetIdOrUrl, CancellationToken ct = default);
    Task<OperationResult> SelectSheetAsync(string spreadsheetIdOrUrl, string tab, CancellationToken ct = default);
    Task<OperationResult> TestConnectionAsync(CancellationToken ct = default);
    Task<OperationResult<SheetSyncResult>> SyncApplicationsAsync(CancellationToken ct = default);
}

public interface IBackupService
{
    Task<string> BackupAsync(string destinationFile, CancellationToken ct = default);
    Task RestoreAsync(string backupFile, CancellationToken ct = default);
    Task ExportAsync(string destinationFile, CancellationToken ct = default);
    Task<int> ImportAsync(string sourceFile, CancellationToken ct = default);
}
