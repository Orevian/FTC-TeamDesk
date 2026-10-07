using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.AI;
using FTC.TeamDesk.AI.Chat;
using FTC.TeamDesk.AI.Tools;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;
using FTC.TeamDesk.UI.ViewModels.Settings;

namespace FTC.TeamDesk.UI.ViewModels.Assistant;

public enum ChatSide { User, Assistant, System, Error }

public sealed class ChatBubble
{
    public ChatBubble(ChatSide side, string text, string? tools = null) { Side = side; Text = text; Tools = tools; Time = DateTime.Now.ToString("HH:mm"); }
    public ChatSide Side { get; }
    public string Text { get; }
    public string? Tools { get; }
    public string Time { get; }
    public bool IsUser => Side == ChatSide.User;
    public bool HasTools => !string.IsNullOrEmpty(Tools);
}

public sealed class AssistantViewModel : PageViewModel
{
    private readonly IAssistantService _assistant;
    private readonly INavigationService _nav;
    private readonly List<ChatMessage> _history = new();
    private string _input = "";
    private bool _thinking;
    private AssistantAvailability _availability;
    private CancellationTokenSource? _cts;

    public AssistantViewModel(UiContext ui, IAssistantService assistant, INavigationService nav) : base(ui)
    {
        _assistant = assistant; _nav = nav;
        SendCommand = Cmd(() => SendAsync(_input), () => !_thinking && _availability == AssistantAvailability.Ready);
        SuggestCommand = Cmd<string>(s => { if (s is not null) Input = s; });
        CancelCommand = Cmd(() => _cts?.Cancel(), () => _thinking);
        ClearCommand = Cmd(() => { _history.Clear(); Messages.Clear(); });
        OpenSettingsCommand = Cmd(() => _nav.NavigateAsync<SettingsViewModel>("ai"));
        Suggestions = new[] { "Assistant.Suggest.Budget", "Assistant.Suggest.Applications", "Assistant.Suggest.Team", "Assistant.Suggest.Purchases" };
    }

    public override string Section => "assistant";
    public ObservableCollection<ChatBubble> Messages { get; } = new();
    public IReadOnlyList<string> Suggestions { get; }
    public string Input { get => _input; set => SetProperty(ref _input, value); }
    public bool IsThinking { get => _thinking; private set { if (SetProperty(ref _thinking, value)) RefreshCommands(); } }
    public bool IsReady => _availability == AssistantAvailability.Ready;
    public bool IsUnavailable => !IsReady;
    public string UnavailableText => L[_availability == AssistantAvailability.Disabled ? "Assistant.Disabled" : "Assistant.NotConfigured"];
    public bool ShowWelcome => Messages.Count == 0 && IsReady;

    public ICommand SendCommand { get; }
    public ICommand SuggestCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(async () =>
    {
        _availability = await _assistant.GetAvailabilityAsync(ct);
        Raise(nameof(IsReady), nameof(IsUnavailable), nameof(UnavailableText), nameof(ShowWelcome));
        RefreshCommands();
        IsLoaded = true;
        if (parameter is string prompt && IsReady) await SendAsync(prompt);
    });

    private void RefreshCommands()
    {
        (SendCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged();
        (CancelCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged();
    }

    private async Task SendAsync(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || _thinking) return;
        Input = "";
        Messages.Add(new ChatBubble(ChatSide.User, text));
        Raise(nameof(ShowWelcome));
        IsThinking = true;
        _cts = new CancellationTokenSource();
        try
        {
            var reply = await _assistant.AskAsync(_history.ToList(), text, L.CurrentLanguage, _cts.Token);
            _history.Add(ChatMessage.User(text));
            if (reply.ErrorKey is not null)
            {
                var msg = L[reply.ErrorKey];
                Messages.Add(new ChatBubble(ChatSide.Error, msg));
            }
            else
            {
                _history.Add(ChatMessage.Assistant(reply.Text));
                var tools = reply.Tools.Count == 0 ? null : string.Join(", ", reply.Tools.Select(t => t.ToolName + (t.Succeeded ? "" : " ✗")));
                Messages.Add(new ChatBubble(ChatSide.Assistant, reply.Text, tools is null ? null : L.Format("Assistant.ToolsUsed", tools)));
            }
            foreach (var action in reply.PendingActions) await ReviewAsync(action);
        }
        catch (OperationCanceledException) { Messages.Add(new ChatBubble(ChatSide.System, L["Assistant.Cancelled"])); }
        catch (Exception ex) { Messages.Add(new ChatBubble(ChatSide.Error, Ui.Errors.Describe(ex))); }
        finally { IsThinking = false; _cts?.Dispose(); _cts = null; }
    }

    /// <summary>Shows the ACTION REQUEST dialog. Nothing is written unless the user approves (deletes need a second confirmation).</summary>
    private async Task ReviewAsync(PendingAction action)
    {
        var dlg = new ActionRequestDialogViewModel(Ui, action);
        var approved = await Ui.Dialogs.ShowAsync(dlg);
        if (approved && action.IsDestructive)
            approved = await Ui.Dialogs.ConfirmAsync(L["AiAction.ConfirmDelete.Title"], L["AiAction.ConfirmDelete.Message"], L["AiAction.Approve"], true);

        if (!approved)
        {
            await _assistant.RejectAsync(action);
            Messages.Add(new ChatBubble(ChatSide.System, L["AiAction.Rejected"]));
            return;
        }
        var result = await _assistant.ConfirmAsync(action);
        if (result.Succeeded)
        {
            Messages.Add(new ChatBubble(ChatSide.System, L["AiAction.Applied"]));
            Ui.Toasts.Show(L["AiAction.Applied"], ToastKind.Success);
        }
        else Messages.Add(new ChatBubble(ChatSide.Error, L[result.ErrorKey ?? "Error.Unexpected"]));
    }
}
