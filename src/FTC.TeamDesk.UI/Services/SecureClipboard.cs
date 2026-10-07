using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using FTC.TeamDesk.Core.Abstractions.Services;

namespace FTC.TeamDesk.UI.Services;

/// <summary>Copies secrets, keeps them out of Windows clipboard history/cloud sync, and clears them after a delay.</summary>
public sealed class SecureClipboard : ISecureClipboard
{
    private DispatcherTimer? _timer;
    private string? _lastCopied;

    public void CopySensitive(string text, TimeSpan clearAfter)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess()) { dispatcher.Invoke(() => CopySensitive(text, clearAfter)); return; }

        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        // Documented Windows opt-outs so the secret is not recorded by clipboard history or synced to other devices.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new byte[] { 1, 0, 0, 0 });
        data.SetData("CanIncludeInClipboardHistory", new byte[] { 0, 0, 0, 0 });
        data.SetData("CanUploadToCloudClipboard", new byte[] { 0, 0, 0, 0 });
        SetWithRetry(data);
        _lastCopied = text;

        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = clearAfter };
        _timer.Tick += (_, _) =>
        {
            _timer?.Stop();
            try { if (Clipboard.ContainsText() && Clipboard.GetText() == _lastCopied) Clipboard.Clear(); }
            catch (COMException) { /* clipboard busy; nothing else we can do */ }
            _lastCopied = null;
        };
        _timer.Start();
    }

    private static void SetWithRetry(DataObject data)
    {
        for (var i = 0; i < 5; i++)
        {
            try { Clipboard.SetDataObject(data, true); return; }
            catch (COMException) { Thread.Sleep(50); }
        }
    }
}
