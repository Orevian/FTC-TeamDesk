using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Team;

public sealed class MemberRow
{
    public MemberRow(Member m, DisplayFormatter f)
    {
        Member = m;
        Id = m.Id;
        Name = m.FullName;
        Initials = DisplayFormatter.Initials(m.FullName);
        Tone = DisplayFormatter.AvatarTone(m.FullName);
        Email = m.Email ?? "";
        StatusName = f.StatusName(m.Status);
        StatusColor = m.Status?.Color ?? "#6B7280";
        Areas = string.Join(", ", m.MemberAreas.Where(a => a.Area is not null).Select(a => f.AreaName(a.Area!)));
        Skills = string.Join(", ", m.MemberSkills.Where(s => s.Skill is not null).Select(s => s.Skill!.Name));
        Joined = f.DateText(m.JoinDate);
        AvatarPath = m.AvatarPath;
    }
    public Member Member { get; }
    public Guid Id { get; }
    public string Name { get; }
    public string Initials { get; }
    public int Tone { get; }
    public string Email { get; }
    public string StatusName { get; }
    public string StatusColor { get; }
    public string Areas { get; }
    public string Skills { get; }
    public string Joined { get; }
    public string? AvatarPath { get; }
}

public sealed class TeamViewModel : PageViewModel
{
    private readonly IMemberService _members;
    private readonly INavigationService _nav;
    private readonly IFileDialogService _files;
    private readonly IAvatarStore _avatars;
    private string _search = "";
    private SelectOption<Guid?> _statusFilter = null!;
    private SelectOption<Guid?> _areaFilter = null!;
    private MemberRow? _selected;
    private CancellationTokenSource? _debounce;
    private bool _suppress;

    public TeamViewModel(UiContext ui, IMemberService members, INavigationService nav, IFileDialogService files, IAvatarStore avatars) : base(ui)
    {
        _members = members; _nav = nav; _files = files; _avatars = avatars;
        AddCommand = Cmd(AddAsync);
        EditCommand = Cmd<MemberRow>(EditAsync);
        DeleteCommand = Cmd<MemberRow>(DeleteAsync);
        OpenCommand = Cmd<MemberRow>(OpenAsync);
        ManageStatusesCommand = Cmd(ManageStatusesAsync);
        ClearFiltersCommand = Cmd(ClearFilters);
        RefreshCommand = Cmd(() => ReloadAsync(CancellationToken.None));
    }

    public override string Section => "team";
    public override ICommand? PrimaryAction => AddCommand;
    public override ICommand? RefreshCommand { get; }

    public ObservableCollection<MemberRow> Rows { get; } = new();
    public ObservableCollection<SelectOption<Guid?>> StatusOptions { get; } = new();
    public ObservableCollection<SelectOption<Guid?>> AreaOptions { get; } = new();

    public string Search { get => _search; set { if (SetProperty(ref _search, value)) QueueReload(); } }
    public SelectOption<Guid?> StatusFilter { get => _statusFilter; set { if (SetProperty(ref _statusFilter, value)) QueueReload(0); } }
    public SelectOption<Guid?> AreaFilter { get => _areaFilter; set { if (SetProperty(ref _areaFilter, value)) QueueReload(0); } }
    public MemberRow? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public string CountText => L.Format("Team.Count", Rows.Count);
    public bool IsEmpty => IsLoaded && Rows.Count == 0;

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ManageStatusesCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(async () =>
    {
        await LoadFiltersAsync(ct);
        await ReloadAsync(ct);
    });

    private async Task LoadFiltersAsync(CancellationToken ct)
    {
        var statuses = await _members.ListStatusesAsync(ct);
        var areas = await _members.ListAreasAsync(ct);
        _suppress = true;
        StatusOptions.Clear(); AreaOptions.Clear();
        StatusOptions.Add(new(null, L["Team.Filter.AllStatuses"]));
        foreach (var s in statuses) StatusOptions.Add(new(s.Id, Format.StatusName(s)));
        AreaOptions.Add(new(null, L["Team.Filter.AllAreas"]));
        foreach (var a in areas) AreaOptions.Add(new(a.Id, Format.AreaName(a)));
        _statusFilter = StatusOptions[0]; _areaFilter = AreaOptions[0];
        Raise(nameof(StatusFilter), nameof(AreaFilter));
        _suppress = false;
    }

    private void QueueReload(int delayMs = 250)
    {
        if (_suppress) return;
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delayMs, cts.Token); PostToUi(() => _ = RunAsync(() => ReloadAsync(cts.Token))); }
            catch (OperationCanceledException) { }
        });
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        Diag.Mark("team reload start");
        var list = await _members.ListAsync(new MemberFilter(string.IsNullOrWhiteSpace(_search) ? null : _search.Trim(),
            _statusFilter?.Value, _areaFilter?.Value), ct);
        Rows.Clear();
        foreach (var m in list) Rows.Add(new MemberRow(m, Format));
        IsLoaded = true;
        Raise(nameof(CountText), nameof(IsEmpty));
        Diag.Mark("team reload done rows=" + Rows.Count);
    }

    private void ClearFilters()
    {
        _suppress = true;
        _search = ""; _statusFilter = StatusOptions[0]; _areaFilter = AreaOptions[0];
        Raise(nameof(Search), nameof(StatusFilter), nameof(AreaFilter));
        _suppress = false;
        _ = RunAsync(() => ReloadAsync(CancellationToken.None));
    }

    private async Task AddAsync()
    {
        var dlg = new MemberEditDialogViewModel(Ui, _members, _files, _avatars, null);
        if (await Ui.Dialogs.ShowAsync(dlg)) { Ui.Toasts.Show(L["Team.Saved"], ToastKind.Success); await ReloadAsync(CancellationToken.None); }
    }

    private async Task EditAsync(MemberRow? row)
    {
        if (row is null) return;
        var dlg = new MemberEditDialogViewModel(Ui, _members, _files, _avatars, row.Member);
        if (await Ui.Dialogs.ShowAsync(dlg)) { Ui.Toasts.Show(L["Team.Saved"], ToastKind.Success); await ReloadAsync(CancellationToken.None); }
    }

    private async Task DeleteAsync(MemberRow? row)
    {
        if (row is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Team.Delete.Title"], L.Format("Team.Delete.Message", row.Name), L["Common.Delete"], true)) return;
        await _members.DeleteAsync(row.Id);
        Ui.Toasts.Show(L["Team.Deleted"], ToastKind.Success);
        await ReloadAsync(CancellationToken.None);
    }

    private Task OpenAsync(MemberRow? row) => row is null ? Task.CompletedTask : _nav.NavigateAsync<MemberDetailViewModel>(row.Id);

    private async Task ManageStatusesAsync()
    {
        await Ui.Dialogs.ShowAsync(new StatusManagerDialogViewModel(Ui, _members));
        await LoadFiltersAsync(CancellationToken.None);
        await ReloadAsync(CancellationToken.None);
    }
}
