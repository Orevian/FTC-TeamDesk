using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.Security;

namespace FTC.TeamDesk.Services;

public sealed class ActivityLogService : IActivityLogService
{
    private readonly IActivityLogRepository _repo;
    private readonly IAppLogger _logger;
    private readonly IClock _clock;

    public ActivityLogService(IActivityLogRepository repo, IAppLogger logger, IClock clock)
    {
        _repo = repo; _logger = logger; _clock = clock;
    }

    public async Task LogAsync(string actionType, string description, ActivityActor actor = ActivityActor.User, CancellationToken ct = default)
    {
        try
        {
            var safe = SensitiveDataRedactor.Redact(description);
            if (safe.Length > 500) safe = safe[..500];
            await _repo.AddAsync(new ActivityLogEntry
            {
                Timestamp = _clock.UtcNow, ActionType = actionType, Description = safe, Actor = actor.ToString()
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An audit failure must never break the operation being audited.
            _logger.Error("Failed to write activity log entry.", ex);
        }
    }

    public Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int count, CancellationToken ct = default) => _repo.RecentAsync(count, ct);

    public Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityQuery query, CancellationToken ct = default) => _repo.QueryAsync(query, ct);
}
