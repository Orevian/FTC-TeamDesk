namespace FTC.TeamDesk.Security;

/// <summary>Failed-attempt protection: no delay for the first two failures, then exponential back-off (max 15 min).</summary>
public static class AttemptPolicy
{
    public const int FreeAttempts = 2;
    public static readonly TimeSpan MaxLockout = TimeSpan.FromMinutes(15);

    public static TimeSpan LockoutFor(int failedAttempts)
    {
        if (failedAttempts <= FreeAttempts) return TimeSpan.Zero;
        var exponent = Math.Min(failedAttempts - FreeAttempts - 1, 12);
        var seconds = 5 * Math.Pow(2, exponent);
        var span = TimeSpan.FromSeconds(seconds);
        return span > MaxLockout ? MaxLockout : span;
    }

    public static int RemainingFreeAttempts(int failedAttempts) => Math.Max(0, FreeAttempts - failedAttempts);
}
