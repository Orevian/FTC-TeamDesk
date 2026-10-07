using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.Security;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Vault;

public sealed class VaultItemDialogViewModel : DialogViewModel
{
    public static readonly string[] Categories = { "General", "Social", "Email", "Platform", "Hardware", "Other" };
    private readonly IVaultService _vault;
    private readonly VaultItem? _existing;
    private string _title = "", _username = "", _url = "", _notes = "", _password = "", _category = "General";
    private int _length = 20;
    private bool _symbols = true, _digits = true, _upper = true, _lower = true, _ambiguous = true;
    private bool _show;

    public VaultItemDialogViewModel(UiContext ui, IVaultService vault, VaultItem? existing) : base(ui)
    {
        _vault = vault; _existing = existing;
        if (existing is not null)
        {
            _title = existing.Title; _username = existing.Username ?? ""; _url = existing.Url ?? ""; _notes = existing.Notes ?? "";
            _category = existing.Category;
        }
        foreach (var c in Categories) CategoryOptions.Add(new(c, L.Get("Vault.Category." + c)));
        GenerateCommand = new RelayCommand(Generate);
    }

    public override string Title => L[_existing is null ? "Vault.New" : "Vault.Edit"];
    public override double Width => 580;
    public ObservableCollection<SelectOption<string>> CategoryOptions { get; } = new();
    public string ItemTitle { get => _title; set => SetProperty(ref _title, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Url { get => _url; set => SetProperty(ref _url, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string Category { get => _category; set => SetProperty(ref _category, value); }
    /// <summary>When editing, leaving this empty keeps the existing password.</summary>
    public string Password { get => _password; set { if (SetProperty(ref _password, value)) Raise(nameof(StrengthText), nameof(StrengthScore)); } }
    public bool ShowPassword { get => _show; set => SetProperty(ref _show, value); }
    public bool IsEditing => _existing is not null;
    public int StrengthScore => PasswordGenerator.Estimate(_password).Score;
    public string StrengthText => string.IsNullOrEmpty(_password) ? "" : L["Vault.Strength." + StrengthScore];

    public int Length { get => _length; set => SetProperty(ref _length, Math.Clamp(value, 8, 64)); }
    public bool Lower { get => _lower; set => SetProperty(ref _lower, value); }
    public bool Upper { get => _upper; set => SetProperty(ref _upper, value); }
    public bool Digits { get => _digits; set => SetProperty(ref _digits, value); }
    public bool Symbols { get => _symbols; set => SetProperty(ref _symbols, value); }
    public bool AvoidAmbiguous { get => _ambiguous; set => SetProperty(ref _ambiguous, value); }
    public ICommand GenerateCommand { get; }

    private void Generate()
    {
        if (!(_lower || _upper || _digits || _symbols)) { Error = L["Vault.Error.NoCharset"]; return; }
        Error = null;
        Password = PasswordGenerator.Generate(new PasswordOptions(_length, _lower, _upper, _digits, _symbols, _ambiguous));
        ShowPassword = true;
    }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_title)) { Error = L["Vault.Error.TitleRequired"]; return false; }
        if (_existing is null && string.IsNullOrEmpty(_password)) { Error = L["Vault.Error.PasswordRequired"]; return false; }
        await _vault.SaveAsync(new VaultItemEdit
        {
            Id = _existing?.Id, Title = _title.Trim(), Category = string.IsNullOrWhiteSpace(_category) ? "General" : _category.Trim(),
            Username = N(_username), Url = N(_url), Notes = N(_notes), Password = string.IsNullOrEmpty(_password) ? null : _password
        }, ct);
        Password = "";
        return true;
    }

    private static string? N(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed class ChangeMasterDialogViewModel : DialogViewModel
{
    private readonly IVaultService _vault;
    private string _current = "", _new = "", _confirm = "";

    public ChangeMasterDialogViewModel(UiContext ui, IVaultService vault) : base(ui) { _vault = vault; }

    public override string Title => L["Vault.ChangeMaster.Title"];
    public string CurrentPassword { get => _current; set => SetProperty(ref _current, value); }
    public string NewPassword { get => _new; set { if (SetProperty(ref _new, value)) Raise(nameof(StrengthText), nameof(StrengthScore)); } }
    public string ConfirmPassword { get => _confirm; set => SetProperty(ref _confirm, value); }
    public int StrengthScore => PasswordGenerator.Estimate(_new).Score;
    public string StrengthText => string.IsNullOrEmpty(_new) ? "" : L["Vault.Strength." + StrengthScore];

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        if (_new.Length < 10) { Error = L["Vault.Error.TooShort"]; return false; }
        if (_new != _confirm) { Error = L["Vault.Error.Mismatch"]; return false; }
        await _vault.ChangeMasterPasswordAsync(_current, _new, ct);
        CurrentPassword = ""; NewPassword = ""; ConfirmPassword = "";
        return true;
    }
}
