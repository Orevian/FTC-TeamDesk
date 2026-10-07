using FTC.TeamDesk.Core.Entities;

namespace FTC.TeamDesk.Core.Models;

/// <summary>Portable export of team data. The vault is only ever included in its encrypted form.</summary>
public sealed class DataSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<MemberStatus> MemberStatuses { get; set; } = new();
    public List<Area> Areas { get; set; } = new();
    public List<Skill> Skills { get; set; } = new();
    public List<MemberSnapshot> Members { get; set; } = new();
    public List<TeamApplication> Applications { get; set; } = new();
    public List<Budget> Budgets { get; set; } = new();
    public List<BudgetCategory> BudgetCategories { get; set; } = new();
    public List<Purchase> Purchases { get; set; } = new();
    public List<TeamTask> Tasks { get; set; } = new();
    public List<ActivityLogEntry> Activity { get; set; } = new();
    /// <summary>Encrypted blobs only. Useless without the master password.</summary>
    public List<VaultEntry> EncryptedVaultEntries { get; set; } = new();
    public VaultMeta? VaultMeta { get; set; }
}

public sealed class MemberSnapshot
{
    public Member Member { get; set; } = new();
    public List<Guid> SkillIds { get; set; } = new();
    public List<Guid> AreaIds { get; set; } = new();
    public List<PerformanceRecord> PerformanceRecords { get; set; } = new();
    public List<MemberCustomField> CustomFields { get; set; } = new();
}
