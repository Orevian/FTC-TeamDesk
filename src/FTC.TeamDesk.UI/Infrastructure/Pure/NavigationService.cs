using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI;

public sealed class NavigationService : INavigationService
{
    private readonly IViewModelFactory _factory;
    private readonly IErrorPresenter _errors;
    private CancellationTokenSource? _cts;
    private Func<Task>? _reload;

    public NavigationService(IViewModelFactory factory, IErrorPresenter errors) { _factory = factory; _errors = errors; }

    public PageViewModel? Current { get; private set; }
    public event EventHandler? Navigated;

    public Task NavigateAsync<TPage>(object? parameter = null) where TPage : PageViewModel
    {
        _reload = () => NavigateCoreAsync<TPage>(parameter);
        return NavigateCoreAsync<TPage>(parameter);
    }

    public Task RefreshAsync() => _reload?.Invoke() ?? Task.CompletedTask;

    private async Task NavigateCoreAsync<TPage>(object? parameter) where TPage : PageViewModel
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var previous = Current;
        var page = _factory.Create<TPage>();
        Current = page;
        (previous as IDisposable)?.Dispose(); // e.g. the vault page unsubscribes and stops its timers
        Navigated?.Invoke(this, EventArgs.Empty);
        try { await page.LoadAsync(parameter, token).ConfigureAwait(true); }
        catch (OperationCanceledException) { /* superseded by another navigation */ }
        catch (Exception ex) { _errors.Show(ex); }
    }
}
