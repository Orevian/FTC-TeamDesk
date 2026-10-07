using System.Windows.Input;

namespace FTC.TeamDesk.UI.Mvvm;

/// <summary>Implemented by every command so view models can refresh button state without knowing the concrete command type.</summary>
public interface IRaiseCanExecute { void RaiseCanExecuteChanged(); }

public sealed class RelayCommand : ICommand, IRaiseCanExecute
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand, IRaiseCanExecute
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(Cast(parameter)) ?? true;
    public void Execute(object? parameter) => _execute(Cast(parameter));
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    private static T? Cast(object? p) => p is T t ? t : default;
}

/// <summary>
/// Async command: prevents re-entrancy while running, supports cancellation, and routes every exception to a handler
/// (so a failing operation shows a friendly message instead of crashing the app).
/// </summary>
public sealed class AsyncRelayCommand : ICommand, IRaiseCanExecute
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private CancellationTokenSource? _cts;

    public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null, Action<Exception>? onError = null)
    { _execute = execute; _canExecute = canExecute; _onError = onError; }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onError = null)
        : this(_ => execute(), canExecute, onError) { }

    public event EventHandler? CanExecuteChanged;
    public bool IsRunning { get; private set; }
    public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter) => await ExecuteAsync().ConfigureAwait(true);

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        RaiseCanExecuteChanged();
        try { await _execute(_cts.Token).ConfigureAwait(true); }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex) when (_onError is not null) { _onError(ex); }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
            RaiseCanExecuteChanged();
        }
    }

    public void Cancel() => _cts?.Cancel();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand<T> : ICommand, IRaiseCanExecute
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _running;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null)
    { _execute = execute; _canExecute = canExecute; _onError = onError; }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke(Cast(parameter)) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(Cast(parameter)).ConfigureAwait(true); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (_onError is not null) { _onError(ex); }
        finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }

    private static T? Cast(object? p) => p is T t ? t : default;
}
