using System.Globalization;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

public sealed class MemberService : IMemberService
{
    private readonly IMemberRepository _repo;
    private readonly IActivityLogService _log;

    public MemberService(IMemberRepository repo, IActivityLogService log)
    {
        _repo = repo; _log = log;
    }

    public Task<IReadOnlyList<Member>> ListAsync(MemberFilter filter, CancellationToken ct = default) => _repo.ListAsync(filter, ct);
    public Task<Member?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetAsync(id, ct);

    public async Task<Guid> SaveAsync(MemberEditModel model, ActivityActor actor = ActivityActor.User, CancellationToken ct = default)
    {
        Validate(model);
        var isNew = model.Id is null;
        var id = await _repo.SaveAsync(model, ct).ConfigureAwait(false);
        await _log.LogAsync(isNew ? ActivityActions.MemberAdded : ActivityActions.MemberEdited, model.FullName.Trim(), actor, ct).ConfigureAwait(false);
        return id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var member = await _repo.GetAsync(id, ct).ConfigureAwait(false);
        await _repo.DeleteAsync(id, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.MemberDeleted, member?.FullName ?? id.ToString(), ActivityActor.User, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<MemberStatus>> ListStatusesAsync(CancellationToken ct = default) => _repo.ListStatusesAsync(ct);

    public async Task<MemberStatus> SaveStatusAsync(Guid? id, string name, string color, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) && id is null) throw new DomainException("Error.NameRequired");
        var status = new MemberStatus
        {
            Id = id ?? Guid.NewGuid(),
            Key = "custom-" + Guid.NewGuid().ToString("N")[..8],
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Color = NormalizeColor(color),
            IsSystem = false
        };
        return await _repo.SaveStatusAsync(status, ct).ConfigureAwait(false);
    }

    public Task DeleteStatusAsync(Guid id, CancellationToken ct = default) => _repo.DeleteStatusAsync(id, ct);
    public Task<IReadOnlyList<Area>> ListAreasAsync(CancellationToken ct = default) => _repo.ListAreasAsync(ct);

    public async Task<Area> AddAreaAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Error.NameRequired");
        var trimmed = name.Trim();
        var existing = await _repo.ListAreasAsync(ct).ConfigureAwait(false);
        if (existing.Any(a => string.Equals(a.Name, trimmed, StringComparison.OrdinalIgnoreCase))) throw new DomainException("Error.Duplicate");
        return await _repo.SaveAreaAsync(new Area { Key = "custom-" + Guid.NewGuid().ToString("N")[..8], Name = trimmed, IsSystem = false }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Skill>> ListSkillsAsync(CancellationToken ct = default) => _repo.ListSkillsAsync(ct);

    public async Task<PerformanceRecord> AddRecordAsync(Guid memberId, Guid? areaId, DateTime date, int score, string? note, CancellationToken ct = default)
    {
        if (score is < 1 or > 10) throw new DomainException("Error.ScoreRange");
        var record = await _repo.AddRecordAsync(new PerformanceRecord
        {
            MemberId = memberId, AreaId = areaId, RecordedOn = date.Date, Score = score,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        }, ct).ConfigureAwait(false);
        var member = await _repo.GetAsync(memberId, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.DevelopmentRecordAdded, member?.FullName ?? memberId.ToString(), ActivityActor.User, ct).ConfigureAwait(false);
        return record;
    }

    public Task DeleteRecordAsync(Guid memberId, Guid recordId, CancellationToken ct = default) => _repo.DeleteRecordAsync(recordId, ct);

    private static void Validate(MemberEditModel m)
    {
        if (string.IsNullOrWhiteSpace(m.FullName)) throw new DomainException("Error.NameRequired");
        if (m.FullName.Trim().Length > 200) throw new DomainException("Error.TextTooLong");
        if (m.StatusId == Guid.Empty) throw new DomainException("Error.StatusRequired");
        if (!string.IsNullOrWhiteSpace(m.Email) && !LooksLikeEmail(m.Email)) throw new DomainException("Error.InvalidEmail");
    }

    public static bool LooksLikeEmail(string value)
    {
        var v = value.Trim();
        var at = v.IndexOf('@');
        return at > 0 && at == v.LastIndexOf('@') && v.IndexOf('.', at) > at + 1 && !v.Contains(' ') && !v.EndsWith('.');
    }

    private static string NormalizeColor(string color)
    {
        var c = (color ?? string.Empty).Trim();
        if (c.Length == 7 && c[0] == '#' && c[1..].All(Uri.IsHexDigit)) return c.ToUpper(CultureInfo.InvariantCulture);
        return "#6B7280";
    }
}
