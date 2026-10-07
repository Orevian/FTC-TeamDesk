namespace FTC.TeamDesk.UI;

/// <summary>
/// Tiny in-memory breadcrumb trail ("dialog opened", "save finished", ...). When the UI thread stops responding the watchdog writes the
/// last steps to the log, so a freeze can be located without a debugger. Never contains user data (only step names).
/// </summary>
public static class Diag
{
    private const int Capacity = 24;
    private static readonly Queue<string> Steps = new();
    private static readonly object Gate = new();

    public static Action<string>? Sink { get; set; }

    public static void Mark(string step)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {step}";
        lock (Gate) { Steps.Enqueue(line); while (Steps.Count > Capacity) Steps.Dequeue(); }
    }

    public static string Recent() { lock (Gate) return string.Join(" | ", Steps); }
}
