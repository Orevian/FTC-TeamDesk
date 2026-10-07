namespace FTC.TeamDesk.Core.Common;

/// <summary>Stable action-type keys stored in the activity log. UI maps them to localized text ("Activity.{key}").</summary>
public static class ActivityActions
{
    public const string MemberAdded = "MemberAdded";
    public const string MemberEdited = "MemberEdited";
    public const string MemberDeleted = "MemberDeleted";
    public const string DevelopmentRecordAdded = "DevelopmentRecordAdded";
    public const string ApplicationStatusChanged = "ApplicationStatusChanged";
    public const string ApplicationsImported = "ApplicationsImported";
    public const string PurchaseAdded = "PurchaseAdded";
    public const string PurchaseEdited = "PurchaseEdited";
    public const string PurchaseDeleted = "PurchaseDeleted";
    public const string BudgetChanged = "BudgetChanged";
    public const string CategoryChanged = "CategoryChanged";
    public const string AiActionConfirmed = "AiActionConfirmed";
    public const string AiActionRejected = "AiActionRejected";
    public const string VaultCreated = "VaultCreated";
    public const string VaultUnlocked = "VaultUnlocked";
    public const string VaultLocked = "VaultLocked";
    public const string VaultUnlockFailed = "VaultUnlockFailed";
    public const string VaultEntryAdded = "VaultEntryAdded";
    public const string VaultEntryEdited = "VaultEntryEdited";
    public const string VaultEntryDeleted = "VaultEntryDeleted";
    public const string VaultPasswordRevealed = "VaultPasswordRevealed";
    public const string GoogleConnected = "GoogleConnected";
    public const string GoogleDisconnected = "GoogleDisconnected";
    public const string SettingsChanged = "SettingsChanged";
    public const string BackupCreated = "BackupCreated";
    public const string BackupRestored = "BackupRestored";
    public const string DataExported = "DataExported";
    public const string DataImported = "DataImported";
    public const string TaskChanged = "TaskChanged";
}
