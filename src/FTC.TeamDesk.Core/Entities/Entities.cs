using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.Core.Entities;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>A member status. System statuses are localized by <see cref="Key"/>; custom ones use <see cref="Name"/>.</summary>
public class MemberStatus : Entity
{
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Color { get; set; } = "#6B7280";
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A working area (Software, Mechanical, CAD...). System areas are localized by <see cref="Key"/>.</summary>
public class Area : Entity
{
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
}

public class Skill : Entity
{
    public string Name { get; set; } = string.Empty;
}

public class Member : Entity
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarPath { get; set; }
    public Guid StatusId { get; set; }
    public MemberStatus? Status { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.Today;
    public string? Responsibilities { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<MemberSkill> MemberSkills { get; set; } = new();
    public List<MemberArea> MemberAreas { get; set; } = new();
    public List<PerformanceRecord> PerformanceRecords { get; set; } = new();
    public List<MemberCustomField> CustomFields { get; set; } = new();
}

public class MemberSkill
{
    public Guid MemberId { get; set; }
    public Member? Member { get; set; }
    public Guid SkillId { get; set; }
    public Skill? Skill { get; set; }
}

public class MemberArea
{
    public Guid MemberId { get; set; }
    public Member? Member { get; set; }
    public Guid AreaId { get; set; }
    public Area? Area { get; set; }
}

public class MemberCustomField : Entity
{
    public Guid MemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>A development data point. <see cref="AreaId"/> null means a general (not area-specific) record.</summary>
public class PerformanceRecord : Entity
{
    public Guid MemberId { get; set; }
    public Guid? AreaId { get; set; }
    public Area? Area { get; set; }
    public DateTime RecordedOn { get; set; } = DateTime.Today;
    public int Score { get; set; }
    public string? Note { get; set; }
}

public class TeamApplication : Entity
{
    public string ApplicantName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public string? Skills { get; set; }
    public string? Experience { get; set; }
    public string? Notes { get; set; }
    /// <summary>Stable key for de-duplicating rows imported from Google Sheets.</summary>
    public string? ExternalKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ApplicationAnswer> Answers { get; set; } = new();
}

public class ApplicationAnswer : Entity
{
    public Guid ApplicationId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class Budget : Entity
{
    public string Name { get; set; } = "Season budget";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class BudgetCategory : Entity
{
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool IsSystem { get; set; }
    /// <summary>Amount planned for this category (optional allocation).</summary>
    public decimal PlannedAmount { get; set; }
    public int SortOrder { get; set; }
}

public class Purchase : Entity
{
    public string Product { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public BudgetCategory? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Planned;
    public string? Seller { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal Total => UnitPrice * Quantity;
}

/// <summary>Single-row AI configuration. The API key is NOT stored here; it lives in the secret store.</summary>
public class AiSettings
{
    public int Id { get; set; } = 1;
    public AiProviderKind Provider { get; set; } = AiProviderKind.OpenAI;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 1024;
    public string? BaseUrl { get; set; }
    public bool IsEnabled { get; set; }
}

public class AiPermission
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

/// <summary>An encrypted vault record. Everything sensitive (title, username, password...) is inside the encrypted payload.</summary>
public class VaultEntry : Entity
{
    public byte[] EncryptedPayload { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class VaultMeta
{
    public int Id { get; set; } = 1;
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public int Iterations { get; set; }
    /// <summary>Encrypted known constant used to verify the master password.</summary>
    public byte[] Verifier { get; set; } = Array.Empty<byte>();
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public class ActivityLogEntry : Entity
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Actor { get; set; } = "User";
}

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class TeamTask : Entity
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
