using FTC.TeamDesk.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FTC.TeamDesk.Data;

/// <summary>
/// EF Core model. The physical schema is owned by the SQL scripts in /Migrations (see <see cref="SchemaMigrator"/>);
/// keep both in sync when changing entities.
/// </summary>
public sealed class TeamDeskDbContext : DbContext
{
    public TeamDeskDbContext(DbContextOptions<TeamDeskDbContext> options) : base(options) { }

    public DbSet<MemberStatus> MemberStatuses => Set<MemberStatus>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberSkill> MemberSkills => Set<MemberSkill>();
    public DbSet<MemberArea> MemberAreas => Set<MemberArea>();
    public DbSet<MemberCustomField> MemberCustomFields => Set<MemberCustomField>();
    public DbSet<PerformanceRecord> PerformanceRecords => Set<PerformanceRecord>();
    public DbSet<TeamApplication> Applications => Set<TeamApplication>();
    public DbSet<ApplicationAnswer> ApplicationAnswers => Set<ApplicationAnswer>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<AiSettings> AiSettings => Set<AiSettings>();
    public DbSet<AiPermission> AiPermissions => Set<AiPermission>();
    public DbSet<VaultEntry> VaultEntries => Set<VaultEntry>();
    public DbSet<VaultMeta> VaultMeta => Set<VaultMeta>();
    public DbSet<ActivityLogEntry> ActivityLog => Set<ActivityLogEntry>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<TeamTask> Tasks => Set<TeamTask>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MemberStatus>(e => { e.ToTable("MemberStatuses"); e.HasKey(x => x.Id); });
        b.Entity<Area>(e => { e.ToTable("Areas"); e.HasKey(x => x.Id); });
        b.Entity<Skill>(e => { e.ToTable("Skills"); e.HasKey(x => x.Id); });

        b.Entity<Member>(e =>
        {
            e.ToTable("Members");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.MemberSkills).WithOne(x => x.Member).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.MemberAreas).WithOne(x => x.Member).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.PerformanceRecords).WithOne().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.CustomFields).WithOne().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MemberSkill>(e =>
        {
            e.ToTable("MemberSkills");
            e.HasKey(x => new { x.MemberId, x.SkillId });
            e.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MemberArea>(e =>
        {
            e.ToTable("MemberAreas");
            e.HasKey(x => new { x.MemberId, x.AreaId });
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MemberCustomField>(e => { e.ToTable("MemberCustomFields"); e.HasKey(x => x.Id); });
        b.Entity<PerformanceRecord>(e =>
        {
            e.ToTable("PerformanceRecords");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<TeamApplication>(e =>
        {
            e.ToTable("Applications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ApplicationAnswer>(e => { e.ToTable("ApplicationAnswers"); e.HasKey(x => x.Id); });

        b.Entity<Budget>(e => { e.ToTable("Budgets"); e.HasKey(x => x.Id); });
        b.Entity<BudgetCategory>(e => { e.ToTable("BudgetCategories"); e.HasKey(x => x.Id); });
        b.Entity<Purchase>(e =>
        {
            e.ToTable("Purchases");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Total);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AiSettings>(e =>
        {
            e.ToTable("AiSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Provider).HasConversion<string>();
        });
        b.Entity<AiPermission>(e => { e.ToTable("AiPermissions"); e.HasKey(x => x.Key); });
        b.Entity<VaultEntry>(e => { e.ToTable("PasswordVault"); e.HasKey(x => x.Id); });
        b.Entity<VaultMeta>(e =>
        {
            e.ToTable("PasswordVaultMeta");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });
        b.Entity<ActivityLogEntry>(e => { e.ToTable("ActivityLog"); e.HasKey(x => x.Id); });
        b.Entity<AppSetting>(e => { e.ToTable("AppSettings"); e.HasKey(x => x.Key); });
        b.Entity<TeamTask>(e => { e.ToTable("Tasks"); e.HasKey(x => x.Id); });

        // Guid keys are always assigned by the application, never by the store. This makes graph inserts
        // (new child entities reached through navigation properties) consistently 'Added'.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var key = entity.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
                key.Properties[0].ValueGenerated = ValueGenerated.Never;
        }

        // Audit timestamps are stored and returned as UTC.
        var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                var isUtc = prop.Name is "CreatedAt" or "UpdatedAt" or "Timestamp" or "LockedUntil";
                if (!isUtc) continue;
                if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
                else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullable);
            }
        }
    }
}
