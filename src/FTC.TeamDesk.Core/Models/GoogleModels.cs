using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.Core.Models;

public sealed record GoogleConnectionStatus(
    ConnectionState State, string? AccountEmail, string? SpreadsheetId, string? SpreadsheetTitle, string? SheetTab,
    string? ErrorKey, DateTime? LastSyncUtc);

public sealed record SpreadsheetInfo(string Id, string Name);

public sealed record SheetSyncResult(int RowsRead, int Added, int Skipped);

/// <summary>Optional manual override of which sheet column feeds which application field (0-based index, -1 = auto).</summary>
public sealed class ColumnMapping
{
    public int Timestamp { get; set; } = -1;
    public int Name { get; set; } = -1;
    public int Email { get; set; } = -1;
    public int Skills { get; set; } = -1;
    public int Experience { get; set; } = -1;
}
