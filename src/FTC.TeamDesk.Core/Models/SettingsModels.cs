using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.Core.Models;

/// <summary>Machine-local, non-sensitive preferences persisted as JSON (not in the SQLite database).</summary>
public sealed class UserPreferences
{
    /// <summary>"auto" (detect from Windows on first run), "tr" or "en".</summary>
    public string Language { get; set; } = "auto";
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public StartupPage Startup { get; set; } = StartupPage.Dashboard;
    public bool NotificationsEnabled { get; set; } = true;
    public string? LastPage { get; set; }
    public string? DatabasePath { get; set; }
    public int VaultAutoLockMinutes { get; set; } = 5;
    public int ClipboardClearSeconds { get; set; } = 20;
    public bool LockVaultOnMinimize { get; set; } = true;
    public bool LockVaultOnSessionLock { get; set; } = true;
    public string? GoogleClientId { get; set; }
}
