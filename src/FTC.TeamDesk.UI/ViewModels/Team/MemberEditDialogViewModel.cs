using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Team;

public sealed class CheckOption : ObservableObject
{
    private bool _checked;
    public CheckOption(Guid id, string label, bool isChecked) { Id = id; Label = label; _checked = isChecked; }
    public Guid Id { get; }
    public string Label { get; }
    public bool IsChecked { get => _checked; set => SetProperty(ref _checked, value); }
}

public sealed class CustomFieldRow : ObservableObject
{
    private string _name = "", _value = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Value { get => _value; set => SetProperty(ref _value, value); }
}

public sealed class MemberEditDialogViewModel : DialogViewModel
{
    private readonly IMemberService _members;
    private readonly IFileDialogService _files;
    private readonly IAvatarStore _avatars;
    private readonly Member? _existing;
    private string _fullName = "", _email = "", _phone = "", _skills = "", _responsibilities = "", _notes = "", _newArea = "";
    private DateTime _joinDate = DateTime.Today;
    private string? _avatarPath;
    private SelectOption<Guid>? _status;

    public MemberEditDialogViewModel(UiContext ui, IMemberService members, IFileDialogService files, IAvatarStore avatars, Member? existing) : base(ui)
    {
        _members = members; _files = files; _avatars = avatars; _existing = existing;
        if (existing is not null)
        {
            _fullName = existing.FullName; _email = existing.Email ?? ""; _phone = existing.Phone ?? "";
            _responsibilities = existing.Responsibilities ?? ""; _notes = existing.Notes ?? ""; _joinDate = existing.JoinDate;
            _avatarPath = existing.AvatarPath;
            _skills = string.Join(", ", existing.MemberSkills.Where(s => s.Skill is not null).Select(s => s.Skill!.Name));
            foreach (var f in existing.CustomFields) CustomFields.Add(new CustomFieldRow { Name = f.Name, Value = f.Value });
        }
        PickAvatarCommand = new AsyncRelayCommand(PickAvatarAsync, null, ui.Errors.Show);
        RemoveAvatarCommand = new RelayCommand(() => AvatarPath = null);
        AddFieldCommand = new RelayCommand(() => CustomFields.Add(new CustomFieldRow()));
        RemoveFieldCommand = new RelayCommand<CustomFieldRow>(f => { if (f is not null) CustomFields.Remove(f); });
        AddAreaCommand = new AsyncRelayCommand(AddAreaAsync, null, ui.Errors.Show);
    }

    public override string Title => L[_existing is null ? "Team.New" : "Team.Edit"];
    public override double Width => 640;

    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public DateTime JoinDate { get => _joinDate; set => SetProperty(ref _joinDate, value); }
    /// <summary>Comma-separated skills; new names are created automatically.</summary>
    public string Skills { get => _skills; set => SetProperty(ref _skills, value); }
    public string Responsibilities { get => _responsibilities; set => SetProperty(ref _responsibilities, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string? AvatarPath { get => _avatarPath; set { if (SetProperty(ref _avatarPath, value)) Raise(nameof(HasAvatar)); } }
    public bool HasAvatar => !string.IsNullOrEmpty(_avatarPath);
    public string NewArea { get => _newArea; set => SetProperty(ref _newArea, value); }
    public SelectOption<Guid>? Status { get => _status; set => SetProperty(ref _status, value); }

    public ObservableCollection<SelectOption<Guid>> Statuses { get; } = new();
    public ObservableCollection<CheckOption> Areas { get; } = new();
    public ObservableCollection<CustomFieldRow> CustomFields { get; } = new();

    public ICommand PickAvatarCommand { get; }
    public ICommand RemoveAvatarCommand { get; }
    public ICommand AddFieldCommand { get; }
    public ICommand RemoveFieldCommand { get; }
    public ICommand AddAreaCommand { get; }

    public override async Task InitializeAsync()
    {
        try
        {
            var statuses = await _members.ListStatusesAsync();
            foreach (var s in statuses) Statuses.Add(new(s.Id, Format(s)));
            Status = Statuses.FirstOrDefault(s => s.Value == _existing?.StatusId) ?? Statuses.FirstOrDefault();
            var chosen = _existing?.MemberAreas.Select(a => a.AreaId).ToHashSet() ?? new HashSet<Guid>();
            foreach (var a in await _members.ListAreasAsync()) Areas.Add(new(a.Id, Ui.Format.AreaName(a), chosen.Contains(a.Id)));
        }
        catch (Exception ex) { Error = Ui.Errors.Describe(ex); }
    }

    private string Format(MemberStatus s) => Ui.Format.StatusName(s);

    private async Task PickAvatarAsync()
    {
        var file = _files.PickOpenFile(L["Team.Avatar.Pick"], "Images|*.png;*.jpg;*.jpeg;*.bmp");
        if (file is null) return;
        AvatarPath = await _avatars.ImportAsync(file);
    }

    private async Task AddAreaAsync()
    {
        if (string.IsNullOrWhiteSpace(_newArea)) return;
        var area = await _members.AddAreaAsync(_newArea.Trim());
        if (Areas.All(a => a.Id != area.Id)) Areas.Add(new(area.Id, Ui.Format.AreaName(area), true));
        NewArea = "";
    }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_fullName)) { Error = L["Team.Error.NameRequired"]; return false; }
        if (!string.IsNullOrWhiteSpace(_email) && !_email.Contains('@')) { Error = L["Team.Error.EmailInvalid"]; return false; }
        if (_status is null) { Error = L["Team.Error.StatusRequired"]; return false; }

        var model = new MemberEditModel
        {
            Id = _existing?.Id, FullName = _fullName.Trim(), Email = NullIfEmpty(_email), Phone = NullIfEmpty(_phone),
            AvatarPath = _avatarPath, StatusId = _status.Value, JoinDate = _joinDate,
            Responsibilities = NullIfEmpty(_responsibilities), Notes = NullIfEmpty(_notes),
            AreaIds = Areas.Where(a => a.IsChecked).Select(a => a.Id).ToList(),
            SkillNames = _skills.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            CustomFields = CustomFields.Where(f => !string.IsNullOrWhiteSpace(f.Name)).Select(f => new KeyValuePair<string, string>(f.Name.Trim(), f.Value.Trim())).ToList()
        };
        await _members.SaveAsync(model, Core.Enums.ActivityActor.User, ct);
        return true;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
