using System.Diagnostics;
using System.Windows;
using FTC.TeamDesk.UI.Abstractions;

namespace FTC.TeamDesk.UI.Services;

public sealed class AppLifecycle : IAppLifecycle
{
    public void Restart()
    {
        var exe = Environment.ProcessPath;
        if (exe is not null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        Application.Current.Shutdown();
    }

    public void OpenFolder(string path)
    {
        if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public void OpenUrl(string url)
    {
        // Only web links; never launch arbitrary schemes from stored data.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
