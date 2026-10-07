using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Dashboard;

public sealed class TaskDialogViewModel : DialogViewModel
{
    private readonly ITaskService _tasks;
    private readonly Guid? _id;
    private string _title = "";
    private DateTime? _due;
    private string? _notes;

    public TaskDialogViewModel(Abstractions.UiContext ui, ITaskService tasks, TeamTask? existing) : base(ui)
    {
        _tasks = tasks;
        if (existing is not null) { _id = existing.Id; _title = existing.Title; _due = existing.DueDate; _notes = existing.Notes; }
    }

    public override string Title => L[_id is null ? "Task.New" : "Task.Edit"];
    public string TaskTitle { get => _title; set => SetProperty(ref _title, value); }
    public DateTime? DueDate { get => _due; set => SetProperty(ref _due, value); }
    public string? Notes { get => _notes; set => SetProperty(ref _notes, value); }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_title)) { Error = L["Task.Error.TitleRequired"]; return false; }
        await _tasks.SaveAsync(_id, _title.Trim(), _due, _notes, ct);
        return true;
    }
}
