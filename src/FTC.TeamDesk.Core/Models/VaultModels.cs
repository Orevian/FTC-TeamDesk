namespace FTC.TeamDesk.Core.Models;

/// <summary>Decrypted vault record WITHOUT the password. Passwords are fetched explicitly via IVaultService.</summary>
public sealed record VaultItem(Guid Id, string Title, string Category, string? Username, string? Url, string? Notes,
    DateTime UpdatedAt);

public sealed class VaultItemEdit
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    /// <summary>New password. Null on edit means "keep the existing password".</summary>
    public string? Password { get; set; }
}

public enum VaultUnlockOutcome { Success, WrongPassword, LockedOut, NotInitialized }

public sealed record VaultUnlockResult(VaultUnlockOutcome Outcome, TimeSpan? RetryAfter = null, int RemainingAttempts = 0);
