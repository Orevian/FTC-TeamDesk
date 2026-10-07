using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.Services;

public sealed class TaskService : ITaskService
{
    private readonly ITaskRepository _repo;
    private readonly IActivityLogService _log;
    public TaskService(ITaskRepository repo, IActivityLogService log) { _repo = repo; _log = log; }

    public Task<IReadOnlyList<TeamTask>> ListAsync(bool includeCompleted, CancellationToken ct = default) => _repo.ListAsync(includeCompleted, ct);

    public async Task<TeamTask> SaveAsync(Guid? id, string title, DateTime? dueDate, string? notes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Error.TitleRequired");
        var task = new TeamTask
        {
            Id = id ?? Guid.NewGuid(), Title = title.Trim(), DueDate = dueDate?.Date,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        if (id is { } existingId)
        {
            var current = (await _repo.ListAsync(true, ct).ConfigureAwait(false)).FirstOrDefault(t => t.Id == existingId);
            task.IsCompleted = current?.IsCompleted ?? false;
        }
        var saved = await _repo.SaveAsync(task, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.TaskChanged, saved.Title, ActivityActor.User, ct).ConfigureAwait(false);
        return saved;
    }

    public async Task SetCompletedAsync(Guid id, bool completed, CancellationToken ct = default)
    {
        var task = (await _repo.ListAsync(true, ct).ConfigureAwait(false)).FirstOrDefault(t => t.Id == id) ?? throw new DomainException("Error.NotFound");
        task.IsCompleted = completed;
        await _repo.SaveAsync(task, ct).ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
