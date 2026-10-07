using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI;

public sealed class ToastService : ObservableObject, IToastService
{
    private readonly SynchronizationContext? _sync = SynchronizationContext.Current;
    // Compare threads, not SynchronizationContext instances: WPF can hand out different (but equivalent) context objects on the same
    // UI thread, which made "Current != _sync" always true and re-posted Show() to itself forever (UI thread flooded = frozen app).
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private string? _message;
    private ToastKind _kind;
    private int _version;

    public string? Message { get => _message; private set { if (SetProperty(ref _message, value)) Raise(nameof(IsVisible)); } }
    public ToastKind Kind { get => _kind; private set => SetProperty(ref _kind, value); }
    public bool IsVisible => !string.IsNullOrEmpty(_message);

    public void Show(string message, ToastKind kind = ToastKind.Info)
    {
        if (_sync is not null && Environment.CurrentManagedThreadId != _ownerThread) { _sync.Post(_ => ShowCore(message, kind), null); return; }
        ShowCore(message, kind);
    }

    private void ShowCore(string message, ToastKind kind)
    {
        Kind = kind;
        Message = message;
        var mine = ++_version;
        _ = HideLaterAsync(mine, kind == ToastKind.Error ? 7000 : 4000);
    }

    public void Dismiss() { Message = null; }

    private async Task HideLaterAsync(int version, int ms)
    {
        await Task.Delay(ms).ConfigureAwait(true);
        if (version == _version) Message = null;
    }
}
