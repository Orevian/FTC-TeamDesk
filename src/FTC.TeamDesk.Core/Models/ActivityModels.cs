namespace FTC.TeamDesk.Core.Models;

public sealed record ActivityQuery(string? Search = null, int Page = 1, int PageSize = 50);
