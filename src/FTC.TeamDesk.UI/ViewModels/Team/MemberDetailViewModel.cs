using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Team;

public sealed record RecordRow(Guid Id, string Date, string Area, int Score, string? Note);

/// <summary>
/// Member profile. Development is shown as one line per area over time; there is deliberately no single overall "good/bad" score.
/// </summary>
public sealed class MemberDetailViewModel : PageViewModel
{
    private readonly IMemberService _members;
    private readonly INavigationService _nav;
    private readonly IFileDialogService _files;
    private readonly IAvatarStore _avatars;
    private Member? _member;
    private Guid _id;

    public MemberDetailViewModel(UiContext ui, IMemberService members, INavigationService nav, IFileDialogService files, IAvatarStore avatars) : base(ui)
    {
        _members = members; _nav = nav; _files = files; _avatars = avatars;
        BackCommand = Cmd(() => _nav.NavigateAsync<TeamViewModel>());
        EditCommand = Cmd(EditAsync);
        AddRecordCommand = Cmd(AddRecordAsync);
        DeleteRecordCommand = Cmd<RecordRow>(DeleteRecordAsync);
    }

    public override string Section => "team";
    public override ICommand? PrimaryAction => AddRecordCommand;

    public string Name => _member?.FullName ?? "";
    public string Initials => DisplayFormatter.Initials(Name);
    public int Tone => DisplayFormatter.AvatarTone(Name);
    public string? AvatarPath => _member?.AvatarPath;
    public string StatusName => Format.StatusName(_member?.Status);
    public string StatusColor => _member?.Status?.Color ?? "#6B7280";
    public string Email => _member?.Email ?? "-";
    public string Phone => _member?.Phone ?? "-";
    public string Joined => _member is null ? "" : Format.DateText(_member.JoinDate);
    public string Responsibilities => string.IsNullOrWhiteSpace(_member?.Responsibilities) ? L["Common.None"] : _member!.Responsibilities!;
    public string Notes => string.IsNullOrWhiteSpace(_member?.Notes) ? L["Common.None"] : _member!.Notes!;

    public ObservableCollection<string> Areas { get; } = new();
    public ObservableCollection<string> Skills { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> CustomFields { get; } = new();
    public ObservableCollection<RecordRow> Records { get; } = new();
    public ObservableCollection<LineSeriesData> Development { get; } = new();
    public bool HasDevelopment => Development.Count > 0;
    public bool HasCustomFields => CustomFields.Count > 0;

    public ICommand BackCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand AddRecordCommand { get; }
    public ICommand DeleteRecordCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct)
    {
        if (parameter is Guid id) _id = id;
        return RunAsync(() => ReloadAsync(ct));
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        Diag.Mark("member detail reload start");
        _member = await _members.GetAsync(_id, ct);
        if (_member is null) { await _nav.NavigateAsync<TeamViewModel>(); return; }

        Fill(Areas, _member.MemberAreas.Where(a => a.Area is not null).Select(a => Format.AreaName(a.Area!)));
        Fill(Skills, _member.MemberSkills.Where(s => s.Skill is not null).Select(s => s.Skill!.Name));
        Fill(CustomFields, _member.CustomFields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        var ordered = _member.PerformanceRecords.OrderByDescending(r => r.RecordedOn).ToList();
        Fill(Records, ordered.Select(r => new RecordRow(r.Id, Format.DateText(r.RecordedOn),
            r.Area is null ? L["Team.Record.General"] : Format.AreaName(r.Area), r.Score, r.Note)));

        Development.Clear();
        var groups = _member.PerformanceRecords.GroupBy(r => r.AreaId).ToList();
        var i = 0;
        foreach (var g in groups)
        {
            var name = g.First().Area is { } a ? Format.AreaName(a) : L["Team.Record.General"];
            var points = g.OrderBy(r => r.RecordedOn).Select(r => new ChartDatum(r.RecordedOn.ToString("yyyy-MM-dd"), r.Score, i)).ToList();
            Development.Add(new LineSeriesData(name, i, points));
            i++;
        }
        IsLoaded = true;
        RaiseAll();
        Diag.Mark("member detail reload done");
    }

    private void RaiseAll() => Raise(nameof(Name), nameof(Initials), nameof(Tone), nameof(AvatarPath), nameof(StatusName), nameof(StatusColor),
        nameof(Email), nameof(Phone), nameof(Joined), nameof(Responsibilities), nameof(Notes), nameof(HasDevelopment), nameof(HasCustomFields));

    private static void Fill<T>(ObservableCollection<T> c, IEnumerable<T> items) { c.Clear(); foreach (var x in items) c.Add(x); }

    private async Task EditAsync()
    {
        if (_member is null) return;
        var dlg = new MemberEditDialogViewModel(Ui, _members, _files, _avatars, _member);
        if (await Ui.Dialogs.ShowAsync(dlg)) { Ui.Toasts.Show(L["Team.Saved"], ToastKind.Success); await ReloadAsync(CancellationToken.None); }
    }

    private async Task AddRecordAsync()
    {
        if (_member is null) return;
        var dlg = new RecordDialogViewModel(Ui, _members, _id);
        if (await Ui.Dialogs.ShowAsync(dlg)) await ReloadAsync(CancellationToken.None);
    }

    private async Task DeleteRecordAsync(RecordRow? row)
    {
        if (row is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Team.Record.Delete"], L["Team.Record.DeleteMessage"], L["Common.Delete"], true)) return;
        await _members.DeleteRecordAsync(_id, row.Id);
        await ReloadAsync(CancellationToken.None);
    }
}
