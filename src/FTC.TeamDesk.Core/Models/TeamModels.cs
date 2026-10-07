using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.Core.Models;

public sealed record MemberFilter(string? Search = null, Guid? StatusId = null, Guid? AreaId = null);

public sealed class MemberEditModel
{
    public Guid? Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarPath { get; set; }
    public Guid StatusId { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.Today;
    public string? Responsibilities { get; set; }
    public string? Notes { get; set; }
    public List<Guid> AreaIds { get; set; } = new();
    public List<string> SkillNames { get; set; } = new();
    public List<KeyValuePair<string, string>> CustomFields { get; set; } = new();
}

public sealed class ApplicationQuery
{
    public string? Search { get; set; }
    public ApplicationStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public ApplicationSortField SortBy { get; set; } = ApplicationSortField.SubmittedAt;
    public SortDirection Direction { get; set; } = SortDirection.Descending;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
