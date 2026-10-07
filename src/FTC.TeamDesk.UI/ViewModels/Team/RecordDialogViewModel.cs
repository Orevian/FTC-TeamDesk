using System.Collections.ObjectModel;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Team;

/// <summary>Adds a development observation (score 1-10) for one area, or "general".</summary>
public sealed class RecordDialogViewModel : DialogViewModel
{
    private readonly IMemberService _members;
    private readonly Guid _memberId;
    private SelectOption<Guid?>? _area;
    private DateTime _date = DateTime.Today;
    private int _score = 5;
    private string _note = "";

    public RecordDialogViewModel(UiContext ui, IMemberService members, Guid memberId) : base(ui) { _members = members; _memberId = memberId; }

    public override string Title => L["Team.Record.New"];
    public ObservableCollection<SelectOption<Guid?>> AreaOptions { get; } = new();
    public SelectOption<Guid?>? Area { get => _area; set => SetProperty(ref _area, value); }
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
    public int Score { get => _score; set => SetProperty(ref _score, Math.Clamp(value, 1, 10)); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    public override async Task InitializeAsync()
    {
        AreaOptions.Add(new(null, L["Team.Record.General"]));
        foreach (var a in await _members.ListAreasAsync()) AreaOptions.Add(new(a.Id, Ui.Format.AreaName(a)));
        Area = AreaOptions[0];
    }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        await _members.AddRecordAsync(_memberId, _area?.Value, _date, _score, string.IsNullOrWhiteSpace(_note) ? null : _note.Trim(), ct);
        return true;
    }
}
