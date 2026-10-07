using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;
using FTC.TeamDesk.UI.ViewModels.Assistant;

namespace FTC.TeamDesk.UI.ViewModels.Applications;

public sealed class ApplicationRow
{
    public ApplicationRow(TeamApplication a, DisplayFormatter f)
    {
        Application = a; Id = a.Id; Name = a.ApplicantName; Email = a.Email ?? "";
        Submitted = f.DateText(a.SubmittedAt.ToLocalTime()); Status = f.EnumText(a.Status); Tone = StatusTones.For(a.Status);
        Skills = a.Skills ?? "";
    }
    public TeamApplication Application { get; }
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string Submitted { get; }
    public string Status { get; }
    public StatusTone Tone { get; }
    public string Skills { get; }
}

public sealed record AnswerRow(string Question, string Answer);

public sealed class ApplicationsViewModel : PageViewModel
{
    private readonly IApplicationService _apps;
    private readonly IGoogleSheetsService _google;
    private readonly INavigationService _nav;
    private string _search = "";
    private SelectOption<ApplicationStatus?> _statusFilter = null!;
    private SelectOption<ApplicationSortField> _sort = null!;
    private bool _descending = true;
    private DateTime? _from, _to;
    private int _page = 1, _pageCount = 1, _total;
    private ApplicationRow? _selected;
    private TeamApplication? _detail;
    private string _notes = "";
    private bool _suppress;
    private CancellationTokenSource? _debounce;
    private bool _syncing;
    private string? _syncStatus;

    public ApplicationsViewModel(UiContext ui, IApplicationService apps, IGoogleSheetsService google, INavigationService nav) : base(ui)
    {
        _apps = apps; _google = google; _nav = nav;
        StatusOptions.Add(new(null, L["Applications.Filter.AllStatuses"]));
        foreach (var s in Enum.GetValues<ApplicationStatus>()) StatusOptions.Add(new(s, Format.EnumText(s)));
        SortOptions.Add(new(ApplicationSortField.SubmittedAt, L["Applications.Sort.Date"]));
        SortOptions.Add(new(ApplicationSortField.ApplicantName, L["Applications.Sort.Name"]));
        SortOptions.Add(new(ApplicationSortField.Status, L["Applications.Sort.Status"]));
        _statusFilter = StatusOptions[0]; _sort = SortOptions[0];

        SetStatusCommand = Cmd<ApplicationStatus>(SetStatusAsync);
        SaveNotesCommand = Cmd(SaveNotesAsync);
        DeleteCommand = Cmd(DeleteAsync);
        SyncCommand = Cmd(SyncAsync, () => !_syncing);
        AnalyzeCommand = Cmd(AnalyzeAsync);
        NextPageCommand = Cmd(() => GoToPage(_page + 1), () => _page < _pageCount);
        PreviousPageCommand = Cmd(() => GoToPage(_page - 1), () => _page > 1);
        ToggleDirectionCommand = Cmd(() => { Descending = !Descending; });
        ClearFiltersCommand = Cmd(ClearFilters);
        RefreshCommand = Cmd(() => ReloadAsync());
    }

    public override string Section => "applications";
    public override ICommand? PrimaryAction => SyncCommand;
    public override ICommand? RefreshCommand { get; }

    public ObservableCollection<ApplicationRow> Rows { get; } = new();
    public ObservableCollection<SelectOption<ApplicationStatus?>> StatusOptions { get; } = new();
    public ObservableCollection<SelectOption<ApplicationSortField>> SortOptions { get; } = new();
    public ObservableCollection<AnswerRow> Answers { get; } = new();
    public IReadOnlyList<SelectOption<ApplicationStatus>> StatusChoices { get; } =
        Enum.GetValues<ApplicationStatus>().Select(s => new SelectOption<ApplicationStatus>(s, s.ToString())).ToList();

    public string Search { get => _search; set { if (SetProperty(ref _search, value)) QueueReload(300); } }
    public SelectOption<ApplicationStatus?> StatusFilter { get => _statusFilter; set { if (SetProperty(ref _statusFilter, value)) QueueReload(0); } }
    public SelectOption<ApplicationSortField> Sort { get => _sort; set { if (SetProperty(ref _sort, value)) QueueReload(0); } }
    public bool Descending { get => _descending; set { if (SetProperty(ref _descending, value)) { Raise(nameof(SortGlyph)); QueueReload(0); } } }
    public string SortGlyph => _descending ? "" : "";
    public DateTime? From { get => _from; set { if (SetProperty(ref _from, value)) QueueReload(0); } }
    public DateTime? To { get => _to; set { if (SetProperty(ref _to, value)) QueueReload(0); } }

    public int Page => _page;
    public string PageText => L.Format("Common.PageOf", _page, Math.Max(1, _pageCount));
    public string CountText => L.Format("Applications.Count", _total);
    public bool IsEmpty => IsLoaded && Rows.Count == 0;

    public ApplicationRow? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) _ = RunAsync(LoadDetailAsync); } }
    public bool HasDetail => _detail is not null;
    public string DetailName => _detail?.ApplicantName ?? "";
    public string DetailEmail => _detail?.Email ?? "-";
    public string DetailSubmitted => _detail is null ? "" : Format.DateTimeText(_detail.SubmittedAt.ToLocalTime());
    public string DetailStatus => _detail is null ? "" : Format.EnumText(_detail.Status);
    public StatusTone DetailTone => _detail is null ? StatusTone.Neutral : StatusTones.For(_detail.Status);
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string? SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public bool IsSyncing { get => _syncing; private set { if (SetProperty(ref _syncing, value)) (SyncCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged(); } }

    public ICommand SetStatusCommand { get; }
    public ICommand SaveNotesCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SyncCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand ToggleDirectionCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(() => ReloadAsync());

    private void QueueReload(int delayMs)
    {
        if (_suppress) return;
        _page = 1;
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delayMs, cts.Token); PostToUi(() => _ = RunAsync(() => ReloadAsync())); }
            catch (OperationCanceledException) { }
        });
    }

    private async Task ReloadAsync()
    {
        var q = new ApplicationQuery
        {
            Search = string.IsNullOrWhiteSpace(_search) ? null : _search.Trim(), Status = _statusFilter?.Value,
            From = _from, To = _to?.Date.AddDays(1).AddTicks(-1), SortBy = _sort?.Value ?? ApplicationSortField.SubmittedAt,
            Direction = _descending ? SortDirection.Descending : SortDirection.Ascending, Page = _page, PageSize = 40
        };
        var result = await _apps.QueryAsync(q);
        _total = result.TotalCount;
        _pageCount = Math.Max(1, result.PageCount);
        var keep = _selected?.Id;
        Rows.Clear();
        foreach (var a in result.Items) Rows.Add(new ApplicationRow(a, Format));
        _selected = Rows.FirstOrDefault(r => r.Id == keep);
        Raise(nameof(Selected));
        if (_selected is null) { _detail = null; Answers.Clear(); RaiseDetail(); }
        IsLoaded = true;
        Raise(nameof(PageText), nameof(CountText), nameof(IsEmpty), nameof(Page));
        (NextPageCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged();
        (PreviousPageCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged();
    }

    private void GoToPage(int page)
    {
        _page = Math.Clamp(page, 1, _pageCount);
        _ = RunAsync(() => ReloadAsync());
    }

    private void ClearFilters()
    {
        _suppress = true;
        _search = ""; _from = null; _to = null; _statusFilter = StatusOptions[0]; _page = 1;
        Raise(nameof(Search), nameof(From), nameof(To), nameof(StatusFilter));
        _suppress = false;
        _ = RunAsync(() => ReloadAsync());
    }

    private async Task LoadDetailAsync()
    {
        if (_selected is null) { _detail = null; Answers.Clear(); RaiseDetail(); return; }
        _detail = await _apps.GetAsync(_selected.Id);
        Answers.Clear();
        if (_detail is not null) foreach (var a in _detail.Answers.OrderBy(a => a.SortOrder)) Answers.Add(new AnswerRow(a.Question, a.Answer));
        Notes = _detail?.Notes ?? "";
        RaiseDetail();
    }

    private void RaiseDetail() => Raise(nameof(HasDetail), nameof(DetailName), nameof(DetailEmail), nameof(DetailSubmitted), nameof(DetailStatus), nameof(DetailTone));

    private async Task SetStatusAsync(ApplicationStatus status)
    {
        if (_detail is null) return;
        await _apps.SetStatusAsync(_detail.Id, status);
        Ui.Toasts.Show(L["Applications.StatusUpdated"], ToastKind.Success);
        await ReloadAsync();
        await LoadDetailAsync();
    }

    private async Task SaveNotesAsync()
    {
        if (_detail is null) return;
        await _apps.SetNotesAsync(_detail.Id, string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim());
        Ui.Toasts.Show(L["Applications.NotesSaved"], ToastKind.Success);
    }

    private async Task DeleteAsync()
    {
        if (_detail is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Applications.Delete.Title"], L.Format("Applications.Delete.Message", _detail.ApplicantName), L["Common.Delete"], true)) return;
        await _apps.DeleteAsync(_detail.Id);
        _selected = null; _detail = null;
        await ReloadAsync();
    }

    private async Task SyncAsync()
    {
        IsSyncing = true; SyncStatus = L["Applications.Sync.Running"];
        try
        {
            var status = await _google.GetStatusAsync();
            if (status.State != ConnectionState.Connected || status.SpreadsheetId is null)
            {
                SyncStatus = null;
                Ui.Toasts.Show(L["Applications.Sync.NotConnected"], ToastKind.Warning);
                return;
            }
            var r = await _google.SyncApplicationsAsync();
            if (!r.Succeeded) { SyncStatus = L[r.ErrorKey ?? "Error.Unexpected"]; Ui.Toasts.Show(SyncStatus, ToastKind.Error); return; }
            SyncStatus = L.Format("Applications.Sync.Result", r.Value!.Added, r.Value.Skipped);
            Ui.Toasts.Show(SyncStatus, ToastKind.Success);
            await ReloadAsync();
        }
        finally { IsSyncing = false; }
    }

    /// <summary>Opens the assistant with a prompt asking for decision support (never a final decision).</summary>
    private Task AnalyzeAsync() => _detail is null
        ? Task.CompletedTask
        : _nav.NavigateAsync<AssistantViewModel>(L.Format("Applications.AiPrompt", _detail.ApplicantName));
}
