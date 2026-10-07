using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data.Repositories;

public sealed class MemberRepository : IMemberRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public MemberRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Member>> ListAsync(MemberFilter filter, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        IQueryable<Member> q = db.Members.AsNoTracking()
            .Include(m => m.Status)
            .Include(m => m.MemberAreas).ThenInclude(a => a.Area)
            .Include(m => m.MemberSkills).ThenInclude(s => s.Skill);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            q = q.Where(m => EF.Functions.Like(m.FullName, pattern)
                             || (m.Email != null && EF.Functions.Like(m.Email, pattern))
                             || m.MemberSkills.Any(s => EF.Functions.Like(s.Skill!.Name, pattern)));
        }
        if (filter.StatusId is { } sid) q = q.Where(m => m.StatusId == sid);
        if (filter.AreaId is { } aid) q = q.Where(m => m.MemberAreas.Any(a => a.AreaId == aid));

        return await q.OrderBy(m => m.FullName).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Member?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Members.AsNoTracking().AsSplitQuery()
            .Include(m => m.Status)
            .Include(m => m.MemberAreas).ThenInclude(a => a.Area)
            .Include(m => m.MemberSkills).ThenInclude(s => s.Skill)
            .Include(m => m.CustomFields)
            .Include(m => m.PerformanceRecords).ThenInclude(r => r.Area)
            .FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Guid> SaveAsync(MemberEditModel model, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        Member member;
        if (model.Id is { } id)
        {
            member = await db.Members
                .Include(m => m.MemberAreas).Include(m => m.MemberSkills).Include(m => m.CustomFields)
                .FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false)
                ?? throw new DomainException("Error.NotFound");
        }
        else
        {
            member = new Member();
            db.Members.Add(member);
        }

        member.FullName = model.FullName.Trim();
        member.Email = Clean(model.Email);
        member.Phone = Clean(model.Phone);
        member.AvatarPath = Clean(model.AvatarPath);
        member.StatusId = model.StatusId;
        member.JoinDate = model.JoinDate.Date;
        member.Responsibilities = Clean(model.Responsibilities);
        member.Notes = Clean(model.Notes);
        member.UpdatedAt = DateTime.UtcNow;

        // Areas
        var wantedAreas = model.AreaIds.Distinct().ToHashSet();
        member.MemberAreas.RemoveAll(a => !wantedAreas.Contains(a.AreaId));
        foreach (var areaId in wantedAreas.Where(a => member.MemberAreas.All(x => x.AreaId != a)))
            db.MemberAreas.Add(new MemberArea { MemberId = member.Id, AreaId = areaId });

        // Skills (created on demand, matched case-insensitively)
        var names = model.SkillNames.Select(n => n.Trim()).Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingSkills = await db.Skills.ToListAsync(ct).ConfigureAwait(false);
        var wantedSkillIds = new HashSet<Guid>();
        foreach (var name in names)
        {
            var skill = existingSkills.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (skill is null)
            {
                skill = new Skill { Name = name };
                db.Skills.Add(skill);
                existingSkills.Add(skill);
            }
            wantedSkillIds.Add(skill.Id);
        }
        member.MemberSkills.RemoveAll(s => !wantedSkillIds.Contains(s.SkillId));
        foreach (var skillId in wantedSkillIds.Where(s => member.MemberSkills.All(x => x.SkillId != s)))
            db.MemberSkills.Add(new MemberSkill { MemberId = member.Id, SkillId = skillId });

        // Custom fields: replace wholesale
        db.MemberCustomFields.RemoveRange(member.CustomFields);
        foreach (var f in model.CustomFields.Where(f => !string.IsNullOrWhiteSpace(f.Key)))
            db.MemberCustomFields.Add(new MemberCustomField { MemberId = member.Id, Name = f.Key.Trim(), Value = f.Value?.Trim() ?? string.Empty });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return member.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Members.Where(m => m.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MemberStatus>> ListStatusesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.MemberStatuses.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<MemberStatus> SaveStatusAsync(MemberStatus status, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.MemberStatuses.FirstOrDefaultAsync(s => s.Id == status.Id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            status.SortOrder = (await db.MemberStatuses.MaxAsync(s => (int?)s.SortOrder, ct).ConfigureAwait(false) ?? 0) + 1;
            db.MemberStatuses.Add(status);
            existing = status;
        }
        else
        {
            if (!existing.IsSystem) existing.Name = status.Name;
            existing.Color = status.Color;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task DeleteStatusAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var status = await db.MemberStatuses.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false)
                     ?? throw new DomainException("Error.NotFound");
        if (status.IsSystem) throw new DomainException("Error.SystemItemProtected");
        if (await db.Members.AnyAsync(m => m.StatusId == id, ct).ConfigureAwait(false)) throw new DomainException("Error.StatusInUse");
        db.MemberStatuses.Remove(status);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Area>> ListAreasAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Areas.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Area> SaveAreaAsync(Area area, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        area.SortOrder = (await db.Areas.MaxAsync(a => (int?)a.SortOrder, ct).ConfigureAwait(false) ?? 0) + 1;
        db.Areas.Add(area);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return area;
    }

    public async Task<IReadOnlyList<Skill>> ListSkillsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Skills.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<PerformanceRecord> AddRecordAsync(PerformanceRecord record, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.PerformanceRecords.Add(record);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return record;
    }

    public async Task DeleteRecordAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.PerformanceRecords.Where(r => r.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PerformanceRecord>> ListRecordsAsync(Guid? memberId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var q = db.PerformanceRecords.AsNoTracking().Include(r => r.Area).AsQueryable();
        if (memberId is { } id) q = q.Where(r => r.MemberId == id);
        return await q.OrderBy(r => r.RecordedOn).ToListAsync(ct).ConfigureAwait(false);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
