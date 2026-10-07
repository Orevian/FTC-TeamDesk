using FTC.TeamDesk.UI.Abstractions;
using Microsoft.Win32;

namespace FTC.TeamDesk.UI.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? PickOpenFile(string title, string filter)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string suggestedName)
    {
        var dlg = new SaveFileDialog { Title = title, Filter = filter, FileName = suggestedName, OverwritePrompt = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
