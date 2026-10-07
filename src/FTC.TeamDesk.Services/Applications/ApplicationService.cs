using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

public sealed class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _repo;
    private readonly IActivityLogService _log;

    public ApplicationService(IApplicationRepository repo, IActivityLogService log)
    {
        _repo = repo; _log = log;
    }

    public Task<PagedResult<TeamApplication>> QueryAsync(ApplicationQuery query, CancellationToken ct = default) => _repo.QueryAsync(query, ct);
    public Task<IReadOnlyList<TeamApplication>> ListAllAsync(CancellationToken ct = default) => _repo.ListAllAsync(ct);
    public Task<TeamApplication?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetAsync(id, ct);

    public async Task SetStatusAsync(Guid id, ApplicationStatus status, ActivityActor actor = ActivityActor.User, CancellationToken ct = default)
    {
        var app = await _repo.GetAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException("Error.NotFound");
        if (app.Status == status) return;
        await _repo.UpdateStatusAsync(id, status, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.ApplicationStatusChanged, $"{app.ApplicantName}: {app.Status} -> {status}", actor, ct).ConfigureAwait(false);
    }

    public Task SetNotesAsync(Guid id, string? notes, CancellationToken ct = default) => _repo.UpdateNotesAsync(id, notes, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);

    public async Task<int> ImportAsync(IReadOnlyList<TeamApplication> items, CancellationToken ct = default)
    {
        var added = await _repo.AddImportedAsync(items, ct).ConfigureAwait(false);
        if (added > 0) await _log.LogAsync(ActivityActions.ApplicationsImported, added.ToString(System.Globalization.CultureInfo.InvariantCulture), ActivityActor.System, ct).ConfigureAwait(false);
        return added;
    }
}
