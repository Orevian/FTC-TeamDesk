using System.Text;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Security;

namespace FTC.TeamDesk.Services.Logging;

/// <summary>Developer log written to %LOCALAPPDATA%\FTC TeamDesk\logs. Every line is passed through the redactor first.</summary>
public sealed class FileAppLogger : IAppLogger
{
    private readonly string _directory;
    private readonly object _gate = new();
    private const int RetainDays = 14;

    public FileAppLogger(string directory)
    {
        _directory = directory;
        try { Directory.CreateDirectory(_directory); PurgeOld(); } catch { /* logging must never break the app */ }
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warning(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ').Append(level).Append(' ')
              .Append(SensitiveDataRedactor.Redact(message));
            if (ex is not null)
            {
                sb.AppendLine().Append(ex.GetType().FullName).Append(": ").Append(SensitiveDataRedactor.Redact(ex.Message));
                if (ex.StackTrace is { } st) sb.AppendLine().Append(SensitiveDataRedactor.Redact(st));
            }
            var path = Path.Combine(_directory, $"teamdesk-{DateTime.Now:yyyyMMdd}.log");
            lock (_gate) File.AppendAllText(path, sb.AppendLine().ToString(), Encoding.UTF8);
        }
        catch { /* ignore */ }
    }

    private void PurgeOld()
    {
        foreach (var file in Directory.EnumerateFiles(_directory, "teamdesk-*.log"))
            if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-RetainDays)) File.Delete(file);
    }
}
