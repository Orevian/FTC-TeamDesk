using System.Windows.Input;
using FTC.TeamDesk.Localization;
using FTC.TeamDesk.UI.Abstractions;

namespace FTC.TeamDesk.UI.Mvvm;

/// <summary>Base for navigable pages.</summary>
public abstract class PageViewModel : ObservableObject
{
    private bool _isBusy;
    private bool _isLoaded;
    private readonly SynchronizationContext? _sync = SynchronizationContext.Current;

    protected PageViewModel(UiContext ui) { Ui = ui; }

    protected UiContext Ui { get; }
    public ILocalizationService L => Ui.Loc;
    public DisplayFormatter Format => Ui.Format;

    /// <summary>Navigation section this page belongs to (highlights the sidebar item).</summary>
    public virtual string Section => string.Empty;
    /// <summary>Command bound to Ctrl+N (the page's main "create" action), if any.</summary>
    public virtual ICommand? PrimaryAction => null;
    /// <summary>Command bound to F5.</summary>
    public virtual ICommand? RefreshCommand => null;

    public bool IsBusy { get => _isBusy; protected set => SetProperty(ref _isBusy, value); }
    /// <summary>True after the first successful load; lets views show a skeleton/empty state instead of stale zeros.</summary>
    public bool IsLoaded { get => _isLoaded; protected set => SetProperty(ref _isLoaded, value); }

    /// <summary>Called by navigation each time the page is shown.</summary>
    public virtual Task LoadAsync(object? parameter, CancellationToken ct) => Task.CompletedTask;

    protected void OnError(Exception ex) => Ui.Errors.Show(ex);

    protected ICommand Cmd(Func<Task> action, Func<bool>? canExecute = null) => new AsyncRelayCommand(action, canExecute, OnError);
    protected ICommand Cmd(Action action, Func<bool>? canExecute = null) => new RelayCommand(action, canExecute);
    protected ICommand Cmd<T>(Func<T?, Task> action, Func<T?, bool>? canExecute = null) => new AsyncRelayCommand<T>(action, canExecute, OnError);
    protected ICommand Cmd<T>(Action<T?> action, Func<T?, bool>? canExecute = null) => new RelayCommand<T>(action, canExecute);

    /// <summary>Runs a load/save operation with the busy flag and centralized error handling.</summary>
    protected async Task RunAsync(Func<Task> work)
    {
        IsBusy = true;
        try { await work().ConfigureAwait(true); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { OnError(ex); }
        finally { IsBusy = false; }
    }

    /// <summary>Marshals a callback raised on a background thread (timers, events) back to the UI thread.</summary>
    protected void PostToUi(Action action)
    {
        if (_sync is null) action(); else _sync.Post(_ => action(), null);
    }
}

/// <summary>Base for modal dialogs shown by <see cref="IDialogService"/>.</summary>
public abstract class DialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _error;

    protected DialogViewModel(UiContext ui) { Ui = ui; }

    protected UiContext Ui { get; }
    public ILocalizationService L => Ui.Loc;
    public abstract string Title { get; }
    public virtual string ConfirmText => L["Common.Save"];
    public virtual string CancelText => L["Common.Cancel"];
    public virtual bool ShowCancel => true;
    public virtual bool IsDestructive => false;
    public virtual double Width => 520;

    public string? Error { get => _error; protected set { if (SetProperty(ref _error, value)) Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public Task<bool> Completion => _tcs.Task;

    private ICommand? _confirm, _cancel;
    public ICommand ConfirmCommand => _confirm ??= new AsyncRelayCommand(ConfirmInternalAsync);
    public ICommand CancelCommand => _cancel ??= new RelayCommand(() => Close(false));

    /// <summary>Validates and persists. Return false (after setting <see cref="Error"/>) to keep the dialog open.</summary>
    protected virtual Task<bool> OnConfirmAsync(CancellationToken ct) => Task.FromResult(true);

    private async Task ConfirmInternalAsync()
    {
        Error = null;
        Diag.Mark("confirm start: " + GetType().Name);
        try
        {
            var ok = await OnConfirmAsync(CancellationToken.None).ConfigureAwait(true);
            Diag.Mark("confirm done: " + GetType().Name + " ok=" + ok);
            if (ok) Close(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error = Ui.Errors.Describe(ex); }
    }

    public void Close(bool result) => _tcs.TrySetResult(result);

    /// <summary>Lets views initialize async data after being shown.</summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;
}
