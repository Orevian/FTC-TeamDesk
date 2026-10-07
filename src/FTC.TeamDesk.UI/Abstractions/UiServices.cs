using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Localization;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.Abstractions;

public enum ToastKind { Info, Success, Warning, Error }

public interface IToastService
{
    void Show(string message, ToastKind kind = ToastKind.Info);
}

/// <summary>Turns exceptions into friendly localized messages. Technical details go to the developer log only.</summary>
public interface IErrorPresenter
{
    string Describe(Exception exception);
    void Show(Exception exception);
}

public interface IDialogService
{
    /// <summary>Shows a modal in-window dialog and completes when it closes. Returns true if the user confirmed.</summary>
    Task<bool> ShowAsync(DialogViewModel dialog);
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false);
}

public interface INavigationService
{
    PageViewModel? Current { get; }
    event EventHandler? Navigated;
    Task NavigateAsync<TPage>(object? parameter = null) where TPage : PageViewModel;
    /// <summary>Reloads the current page (used after language/theme changes and data restore).</summary>
    Task RefreshAsync();
}

public interface IViewModelFactory
{
    T Create<T>() where T : class;
}

public interface IFileDialogService
{
    string? PickOpenFile(string title, string filter);
    string? PickSaveFile(string title, string filter, string suggestedName);
    string? PickFolder(string title);
}

public interface IAppLifecycle
{
    void Restart();
    void OpenFolder(string path);
    void OpenUrl(string url);
}

public interface IThemeService
{
    void Apply(ThemeMode mode);
}

public interface IAvatarStore
{
    /// <summary>Copies the chosen image into the app's data folder and returns the stored path.</summary>
    Task<string> ImportAsync(string sourceFile, CancellationToken ct = default);
}

/// <summary>Shared services every view model needs, passed as one parameter object (not a service locator).</summary>
public sealed class UiContext
{
    public UiContext(ILocalizationService loc, IDialogService dialogs, IToastService toasts, IErrorPresenter errors, DisplayFormatter format)
    {
        Loc = loc; Dialogs = dialogs; Toasts = toasts; Errors = errors; Format = format;
    }
    public ILocalizationService Loc { get; }
    public IDialogService Dialogs { get; }
    public IToastService Toasts { get; }
    public IErrorPresenter Errors { get; }
    public DisplayFormatter Format { get; }
}
