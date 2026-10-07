using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;
using FTC.TeamDesk.UI.ViewModels.Dialogs;

namespace FTC.TeamDesk.UI;

/// <summary>In-window modal dialogs: the shell binds to <see cref="Current"/> and shows an overlay. Dialogs queue up.</summary>
public sealed class DialogService : ObservableObject, IDialogService
{
    private readonly Queue<DialogViewModel> _queue = new();
    private DialogViewModel? _current;
    private readonly Func<UiContext> _ui;

    public DialogService(Func<UiContext> ui) { _ui = ui; }

    public DialogViewModel? Current
    {
        get => _current;
        private set { if (SetProperty(ref _current, value)) Raise(nameof(IsOpen)); }
    }

    public bool IsOpen => _current is not null;

    public async Task<bool> ShowAsync(DialogViewModel dialog)
    {
        Diag.Mark("dialog open: " + dialog.GetType().Name);
        if (Current is null) Current = dialog; else _queue.Enqueue(dialog);
        _ = dialog.InitializeAsync();
        var result = await dialog.Completion.ConfigureAwait(true);
        Diag.Mark("dialog closed: " + dialog.GetType().Name + " result=" + result);
        if (ReferenceEquals(Current, dialog)) Current = _queue.Count > 0 ? _queue.Dequeue() : null;
        return result;
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false)
        => ShowAsync(new ConfirmDialogViewModel(_ui(), title, message, confirmText, destructive));
}
