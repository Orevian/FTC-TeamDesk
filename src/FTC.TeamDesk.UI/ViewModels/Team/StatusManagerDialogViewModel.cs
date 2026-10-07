using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Team;

public sealed class StatusRow
{
    public StatusRow(MemberStatus s, string name) { Status = s; Name = name; IsSystem = s.IsSystem; Color = s.Color; }
    public MemberStatus Status { get; }
    public string Name { get; }
    public string Color { get; }
    public bool IsSystem { get; }
}

/// <summary>Lets the user add custom member statuses (system statuses are localized and cannot be deleted).</summary>
public sealed class StatusManagerDialogViewModel : DialogViewModel
{
    private static readonly string[] Palette = { "#2456D6", "#067647", "#B54708", "#B42318", "#7A3EC8", "#0E7490", "#6B7280" };
    private readonly IMemberService _members;
    private string _newName = "";
    private string _newColor = Palette[0];

    public StatusManagerDialogViewModel(UiContext ui, IMemberService members) : base(ui)
    {
        _members = members;
        AddCommand = new AsyncRelayCommand(AddAsync, null, e => Error = ui.Errors.Describe(e));
        DeleteCommand = new AsyncRelayCommand<StatusRow>(DeleteAsync, r => r is { IsSystem: false }, e => Error = ui.Errors.Describe(e));
    }

    public override string Title => L["Team.Statuses.Title"];
    public override string ConfirmText => L["Common.Close"];
    public override bool ShowCancel => false;
    public ObservableCollection<StatusRow> Rows { get; } = new();
    public IReadOnlyList<string> Colors => Palette;
    public string NewName { get => _newName; set => SetProperty(ref _newName, value); }
    public string NewColor { get => _newColor; set => SetProperty(ref _newColor, value); }
    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }

    public override Task InitializeAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        Rows.Clear();
        foreach (var s in await _members.ListStatusesAsync()) Rows.Add(new StatusRow(s, Ui.Format.StatusName(s)));
    }

    private async Task AddAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(_newName)) { Error = L["Team.Statuses.NameRequired"]; return; }
        await _members.SaveStatusAsync(null, _newName.Trim(), _newColor);
        NewName = "";
        await ReloadAsync();
    }

    private async Task DeleteAsync(StatusRow? row)
    {
        if (row is null) return;
        Error = null;
        await _members.DeleteStatusAsync(row.Status.Id);
        await ReloadAsync();
    }
}
