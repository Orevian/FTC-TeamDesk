namespace FTC.TeamDesk.Core.Common;

/// <summary>Developer log abstraction. Implementations MUST redact secrets before writing.</summary>
public interface IAppLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

public sealed class NullAppLogger : IAppLogger
{
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
}
