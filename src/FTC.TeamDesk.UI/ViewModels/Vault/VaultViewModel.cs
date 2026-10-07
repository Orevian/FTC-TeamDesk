using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Vault;

public enum VaultScreen { Create, Locked, Unlocked }

public sealed class VaultRow
{
    public VaultRow(VaultItem item, string category) { Item = item; Id = item.Id; Title = item.Title; Username = item.Username ?? ""; Category = category; }
    public VaultItem Item { get; }
    public Guid Id { get; }
    public string Title { get; }
    public string Username { get; }
    public string Category { get; }
}

/// <summary>
/// Password vault page. Starts locked. The master password is only held in memory long enough to derive the key and is cleared right after.
/// Passwords are masked; revealing requires a confirmation and auto-hides.
/// </summary>
public sealed class VaultViewModel : PageViewModel, IDisposable
{
    private const string Mask = "••••••••••";
    private readonly IVaultService _vault;
    private readonly ISettingsService _settings;
    private readonly ISecureClipboard _clipboard;
    private VaultScreen _screen;
    private string _master = "", _confirm = "", _search = "", _message = "";
    private VaultRow? _selected;
    private string _passwordText = Mask;
    private bool _revealed;
    private Timer? _lockoutTimer, _hideTimer;
    private DateTime _lockedUntil;
    private IReadOnlyList<VaultItem> _items = Array.Empty<VaultItem>();
    private string _strengthText = "";
    private int _strengthScore;

    public VaultViewModel(UiContext ui, IVaultService vault, ISettingsService settings, ISecureClipboard clipboard) : base(ui)
    {
        _vault = vault; _settings = settings; _clipboard = clipboard;
        CreateCommand = Cmd(CreateAsync);
        UnlockCommand = Cmd(UnlockAsync);
        LockCommand = Cmd(() => _vault.Lock("manual"));
        AddCommand = Cmd(AddAsync, () => _screen == VaultScreen.Unlocked);
        EditCommand = Cmd(EditAsync, () => _selected is not null);
        DeleteCommand = Cmd(DeleteAsync, () => _selected is not null);
        RevealCommand = Cmd(ToggleRevealAsync, () => _selected is not null);
        CopyPasswordCommand = Cmd(CopyPasswordAsync, () => _selected is not null);
        CopyTextCommand = Cmd<string>(CopyText);
        ChangeMasterCommand = Cmd(ChangeMasterAsync, () => _screen == VaultScreen.Unlocked);
        _vault.StateChanged += OnStateChanged;
    }

    public override string Section => "vault";
    public override ICommand? PrimaryAction => AddCommand;

    public VaultScreen Screen
    {
        get => _screen;
        private set
        {
            if (!SetProperty(ref _screen, value)) return;
            Raise(nameof(IsCreate), nameof(IsLocked), nameof(IsUnlocked));
            foreach (var c in new[] { AddCommand, ChangeMasterCommand }) (c as IRaiseCanExecute)?.RaiseCanExecuteChanged();
        }
    }
    public bool IsCreate => _screen == VaultScreen.Create;
    public bool IsLocked => _screen == VaultScreen.Locked;
    public bool IsUnlocked => _screen == VaultScreen.Unlocked;

    public string MasterPassword { get => _master; set { if (SetProperty(ref _master, value)) UpdateStrength(); } }
    public string ConfirmPassword { get => _confirm; set => SetProperty(ref _confirm, value); }
    public string Message { get => _message; private set { if (SetProperty(ref _message, value)) Raise(nameof(HasMessage)); } }
    public bool HasMessage => !string.IsNullOrEmpty(_message);
    public string StrengthText { get => _strengthText; private set => SetProperty(ref _strengthText, value); }
    public int StrengthScore { get => _strengthScore; private set => SetProperty(ref _strengthScore, value); }

    public string Search { get => _search; set { if (SetProperty(ref _search, value)) ApplyFilter(); } }
    public ObservableCollection<VaultRow> Rows { get; } = new();
    public bool IsEmpty => IsUnlocked && Rows.Count == 0;
    public VaultRow? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            HidePassword();
            Raise(nameof(HasSelection), nameof(DetailTitle), nameof(DetailUsername), nameof(DetailUrl), nameof(DetailNotes), nameof(DetailCategory), nameof(DetailUpdated));
            foreach (var c in new[] { EditCommand, DeleteCommand, RevealCommand, CopyPasswordCommand }) (c as IRaiseCanExecute)?.RaiseCanExecuteChanged();
            _vault.Touch();
        }
    }
    public bool HasSelection => _selected is not null;
    public string DetailTitle => _selected?.Title ?? "";
    public string DetailUsername => _selected?.Item.Username ?? "-";
    public string DetailUrl => _selected?.Item.Url ?? "-";
    public string DetailNotes => string.IsNullOrWhiteSpace(_selected?.Item.Notes) ? "-" : _selected!.Item.Notes!;
    public string DetailCategory => _selected?.Category ?? "";
    public string DetailUpdated => _selected is null ? "" : Format.DateTimeText(_selected.Item.UpdatedAt.ToLocalTime());
    public string PasswordText { get => _passwordText; private set => SetProperty(ref _passwordText, value); }
    public bool IsRevealed { get => _revealed; private set => SetProperty(ref _revealed, value); }

    public ICommand CreateCommand { get; }
    public ICommand UnlockCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand CopyPasswordCommand { get; }
    public ICommand CopyTextCommand { get; }
    public ICommand ChangeMasterCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(async () =>
    {
        await _vault.InitializeAsync(ct);
        await ApplyStateAsync();
        IsLoaded = true;
    });

    private void OnStateChanged(object? sender, EventArgs e) => PostToUi(() => _ = RunAsync(ApplyStateAsync));

    private async Task ApplyStateAsync()
    {
        if (!_vault.IsInitialized) { Screen = VaultScreen.Create; ClearSecrets(); return; }
        if (!_vault.IsUnlocked)
        {
            Screen = VaultScreen.Locked;
            ClearSecrets();
            _items = Array.Empty<VaultItem>();
            Rows.Clear();
            Selected = null;
            return;
        }
        Screen = VaultScreen.Unlocked;
        Message = "";
        _items = await _vault.ListAsync();
        ApplyFilter();
        foreach (var c in new[] { AddCommand, ChangeMasterCommand }) (c as IRaiseCanExecute)?.RaiseCanExecuteChanged();
    }

    private void ClearSecrets()
    {
        MasterPassword = ""; ConfirmPassword = "";
        HidePassword();
    }

    public string CategoryLabel(string category)
    {
        var key = "Vault.Category." + category;
        var text = L.Get(key);
        return text == key ? category : text;
    }

    private void ApplyFilter()
    {
        var keep = _selected?.Id;
        IEnumerable<VaultItem> q = _items;
        if (!string.IsNullOrWhiteSpace(_search))
        {
            var s = _search.Trim();
            q = q.Where(i => i.Title.Contains(s, StringComparison.CurrentCultureIgnoreCase)
                          || (i.Username?.Contains(s, StringComparison.CurrentCultureIgnoreCase) ?? false)
                          || (i.Url?.Contains(s, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }
        Rows.Clear();
        foreach (var i in q.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)) Rows.Add(new VaultRow(i, CategoryLabel(i.Category)));
        Selected = Rows.FirstOrDefault(r => r.Id == keep);
        Raise(nameof(IsEmpty));
    }

    private void UpdateStrength()
    {
        if (!IsCreate) return;
        var (_, score) = Security.PasswordGenerator.Estimate(_master);
        StrengthScore = score;
        StrengthText = string.IsNullOrEmpty(_master) ? "" : L["Vault.Strength." + score];
    }

    private async Task CreateAsync()
    {
        Message = "";
        if (_master.Length < 10) { Message = L["Vault.Error.TooShort"]; return; }
        if (_master != _confirm) { Message = L["Vault.Error.Mismatch"]; return; }
        await RunAsync(async () =>
        {
            await _vault.CreateAsync(_master);
            ClearSecrets();
            await ApplyStateAsync();
        });
    }

    private async Task UnlockAsync()
    {
        Message = "";
        if (string.IsNullOrEmpty(_master)) return;
        var pw = _master;
        MasterPassword = "";
        VaultUnlockResult r = null!;
        await RunAsync(async () => r = await _vault.UnlockAsync(pw));
        pw = string.Empty;
        if (r is null) return;
        switch (r.Outcome)
        {
            case VaultUnlockOutcome.Success: await ApplyStateAsync(); break;
            case VaultUnlockOutcome.LockedOut: StartLockout(r.RetryAfter ?? TimeSpan.FromSeconds(5)); break;
            case VaultUnlockOutcome.WrongPassword:
                if (r.RetryAfter is { } wait && wait > TimeSpan.Zero) StartLockout(wait);
                else Message = L["Vault.Error.Wrong"];
                break;
            default: Message = L["Vault.Error.NotInitialized"]; break;
        }
    }

    private void StartLockout(TimeSpan wait)
    {
        _lockedUntil = DateTime.UtcNow + wait;
        _lockoutTimer?.Dispose();
        void Tick()
        {
            var left = _lockedUntil - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) { Message = ""; _lockoutTimer?.Dispose(); _lockoutTimer = null; return; }
            Message = L.Format("Vault.Error.LockedOut", (int)Math.Ceiling(left.TotalSeconds));
        }
        Tick();
        _lockoutTimer = new Timer(_ => PostToUi(Tick), null, 1000, 1000);
    }

    private async Task AddAsync()
    {
        _vault.Touch();
        if (await Ui.Dialogs.ShowAsync(new VaultItemDialogViewModel(Ui, _vault, null))) await ApplyStateAsync();
    }

    private async Task EditAsync()
    {
        if (_selected is null) return;
        _vault.Touch();
        if (await Ui.Dialogs.ShowAsync(new VaultItemDialogViewModel(Ui, _vault, _selected.Item))) await ApplyStateAsync();
    }

    private async Task DeleteAsync()
    {
        if (_selected is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Vault.Delete.Title"], L.Format("Vault.Delete.Message", _selected.Title), L["Common.Delete"], true)) return;
        await _vault.DeleteAsync(_selected.Id);
        await ApplyStateAsync();
    }

    private async Task ToggleRevealAsync()
    {
        if (_selected is null) return;
        if (IsRevealed) { HidePassword(); return; }
        // Extra confirmation before a password is shown on screen.
        if (!await Ui.Dialogs.ConfirmAsync(L["Vault.Reveal.Title"], L.Format("Vault.Reveal.Message", _selected.Title), L["Vault.Reveal.Confirm"])) return;
        _vault.Touch();
        PasswordText = await _vault.RevealPasswordAsync(_selected.Id);
        IsRevealed = true;
        _hideTimer?.Dispose();
        _hideTimer = new Timer(_ => PostToUi(HidePassword), null, 15000, Timeout.Infinite);
    }

    private void HidePassword()
    {
        _hideTimer?.Dispose(); _hideTimer = null;
        PasswordText = Mask;
        IsRevealed = false;
    }

    private async Task CopyPasswordAsync()
    {
        if (_selected is null) return;
        _vault.Touch();
        var pw = await _vault.RevealPasswordAsync(_selected.Id);
        var secs = Math.Max(5, _settings.Current.ClipboardClearSeconds);
        _clipboard.CopySensitive(pw, TimeSpan.FromSeconds(secs));
        Ui.Toasts.Show(L.Format("Vault.Copied", secs), ToastKind.Success);
    }

    private void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text) || text == "-") return;
        _clipboard.CopySensitive(text, TimeSpan.FromSeconds(Math.Max(5, _settings.Current.ClipboardClearSeconds)));
        Ui.Toasts.Show(L["Common.Copied"], ToastKind.Success);
    }

    private async Task ChangeMasterAsync()
    {
        _vault.Touch();
        if (await Ui.Dialogs.ShowAsync(new ChangeMasterDialogViewModel(Ui, _vault))) Ui.Toasts.Show(L["Vault.MasterChanged"], ToastKind.Success);
    }

    public void Dispose()
    {
        _vault.StateChanged -= OnStateChanged;
        _lockoutTimer?.Dispose(); _hideTimer?.Dispose();
    }
}
