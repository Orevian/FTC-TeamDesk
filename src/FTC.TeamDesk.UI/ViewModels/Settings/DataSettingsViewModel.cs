using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Settings;

public sealed class DataSettingsViewModel : SettingsSection
{
    private readonly IBackupService _backup;
    private readonly IDatabaseMaintenance _db;
    private readonly IFileDialogService _files;
    private readonly IAppLifecycle _app;
    private string _message = "";

    public DataSettingsViewModel(UiContext ui, IBackupService backup, IDatabaseMaintenance db, IFileDialogService files, IAppLifecycle app, IActivityLogService log) : base(ui)
    {
        Log = log;
        _backup = backup; _db = db; _files = files; _app = app;
        BackupCommand = Cmd(BackupAsync);
        RestoreCommand = Cmd(RestoreAsync);
        ExportCommand = Cmd(ExportAsync);
        ImportCommand = Cmd(ImportAsync);
        SeedCommand = Cmd(SeedAsync);
        OpenFolderCommand = Cmd(() => { _app.OpenFolder(Path.GetDirectoryName(_db.DatabasePath) ?? _db.DatabasePath); return Task.CompletedTask; });
        ActivityLogCommand = Cmd(() => Ui.Dialogs.ShowAsync(new ActivityLogDialogViewModel(Ui, Log)));
    }

    private IActivityLogService Log { get; }

    public string DatabasePath => _db.DatabasePath;
    public string Message { get => _message; private set { if (SetProperty(ref _message, value)) Raise(nameof(HasMessage)); } }
    public bool HasMessage => !string.IsNullOrEmpty(_message);

    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand SeedCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ActivityLogCommand { get; }

    public override Task LoadAsync(CancellationToken ct) { Raise(nameof(DatabasePath)); return Task.CompletedTask; }

    private async Task BackupAsync()
    {
        var file = _files.PickSaveFile(L["Settings.Data.Backup"], "FTC TeamDesk backup|*.tdbackup", $"TeamDesk-{DateTime.Now:yyyyMMdd-HHmm}.tdbackup");
        if (file is null) return;
        await _backup.BackupAsync(file);
        Message = L.Format("Settings.Data.BackupDone", file);
        Ui.Toasts.Show(L["Settings.Data.BackupDone.Short"], ToastKind.Success);
    }

    private async Task RestoreAsync()
    {
        var file = _files.PickOpenFile(L["Settings.Data.Restore"], "FTC TeamDesk backup|*.tdbackup");
        if (file is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Settings.Data.Restore.Title"], L["Settings.Data.Restore.Message"], L["Settings.Data.Restore"], true)) return;
        await _backup.RestoreAsync(file);
        if (await Ui.Dialogs.ConfirmAsync(L["Settings.Data.Restart.Title"], L["Settings.Data.Restart.Message"], L["Settings.Data.Restart"])) _app.Restart();
    }

    private async Task ExportAsync()
    {
        var file = _files.PickSaveFile(L["Settings.Data.Export"], "JSON|*.json", $"TeamDesk-export-{DateTime.Now:yyyyMMdd}.json");
        if (file is null) return;
        await _backup.ExportAsync(file);
        Message = L.Format("Settings.Data.ExportDone", file);
        Ui.Toasts.Show(L["Settings.Data.ExportDone.Short"], ToastKind.Success);
    }

    private async Task ImportAsync()
    {
        var file = _files.PickOpenFile(L["Settings.Data.Import"], "JSON|*.json");
        if (file is null) return;
        var added = await _backup.ImportAsync(file);
        Message = L.Format("Settings.Data.ImportDone", added);
        Ui.Toasts.Show(Message, ToastKind.Success);
    }

    private async Task SeedAsync()
    {
        if (!await Ui.Dialogs.ConfirmAsync(L["Settings.Data.Demo.Title"], L["Settings.Data.Demo.Message"], L["Settings.Data.Demo"])) return;
        var done = await _db.SeedDemoDataAsync();
        Message = L[done ? "Settings.Data.Demo.Done" : "Settings.Data.Demo.Skipped"];
        Ui.Toasts.Show(Message, done ? ToastKind.Success : ToastKind.Warning);
    }
}

public sealed record LogRow(string When, string Actor, string Text);

public sealed class ActivityLogDialogViewModel : DialogViewModel
{
    private readonly IActivityLogService _log;
    private string _search = "";
    private int _page = 1, _pages = 1;
    private CancellationTokenSource? _debounce;

    public ActivityLogDialogViewModel(UiContext ui, IActivityLogService log) : base(ui)
    {
        _log = log;
        NextCommand = new AsyncRelayCommand(() => GoAsync(_page + 1), () => _page < _pages, ui.Errors.Show);
        PreviousCommand = new AsyncRelayCommand(() => GoAsync(_page - 1), () => _page > 1, ui.Errors.Show);
    }

    public override string Title => L["Settings.ActivityLog"];
    public override string ConfirmText => L["Common.Close"];
    public override bool ShowCancel => false;
    public override double Width => 760;
    public ObservableCollection<LogRow> Rows { get; } = new();
    public string Search { get => _search; set { if (SetProperty(ref _search, value)) Queue(); } }
    public string PageText => L.Format("Common.PageOf", _page, Math.Max(1, _pages));
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }

    public override Task InitializeAsync() => GoAsync(1);

    private void Queue()
    {
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(300, cts.Token); await GoAsync(1); } catch (OperationCanceledException) { }
        });
    }

    private async Task GoAsync(int page)
    {
        var r = await _log.QueryAsync(new ActivityQuery(string.IsNullOrWhiteSpace(_search) ? null : _search.Trim(), Math.Max(1, page), 50));
        _page = r.Page; _pages = Math.Max(1, r.PageCount);
        Rows.Clear();
        foreach (var e in r.Items) Rows.Add(new LogRow(Ui.Format.DateTimeText(e.Timestamp.ToLocalTime()), e.Actor, Ui.Format.ActivityText(e)));
        Raise(nameof(PageText));
        (NextCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged();
        (PreviousCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged();
    }
}
