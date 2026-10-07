namespace FTC.TeamDesk.Core.Common;

public interface IClock
{
    DateTime UtcNow { get; }
    DateTime Today { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Today => DateTime.Today;
}
