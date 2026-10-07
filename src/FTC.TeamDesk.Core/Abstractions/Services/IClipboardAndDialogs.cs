namespace FTC.TeamDesk.Core.Abstractions.Services;

/// <summary>Clipboard access with automatic clearing. Implemented by the UI layer.</summary>
public interface ISecureClipboard
{
    /// <summary>Copies text and clears the clipboard after <paramref name="clearAfter"/> if it still holds that text.</summary>
    void CopySensitive(string text, TimeSpan clearAfter);
}
