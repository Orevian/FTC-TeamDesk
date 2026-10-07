namespace FTC.TeamDesk.Core.Common;

/// <summary>Outcome of an operation that can fail with a user-presentable error code.</summary>
public sealed class OperationResult
{
    private OperationResult(bool ok, string? errorKey, string? detail)
    {
        Succeeded = ok; ErrorKey = errorKey; Detail = detail;
    }

    public bool Succeeded { get; }
    /// <summary>Localization key of the error message.</summary>
    public string? ErrorKey { get; }
    /// <summary>Optional non-sensitive technical detail (never secrets).</summary>
    public string? Detail { get; }

    public static OperationResult Ok() => new(true, null, null);
    public static OperationResult Fail(string errorKey, string? detail = null) => new(false, errorKey, detail);
}

public sealed class OperationResult<T>
{
    private OperationResult(bool ok, T? value, string? errorKey, string? detail)
    {
        Succeeded = ok; Value = value; ErrorKey = errorKey; Detail = detail;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public string? ErrorKey { get; }
    public string? Detail { get; }

    public static OperationResult<T> Ok(T value) => new(true, value, null, null);
    public static OperationResult<T> Fail(string errorKey, string? detail = null) => new(false, default, errorKey, detail);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Exception carrying a localization key, for validation and domain errors.</summary>
public class DomainException : Exception, ILocalizedError
{
    public DomainException(string errorKey, string? detail = null) : base(detail ?? errorKey)
    {
        ErrorKey = errorKey;
    }
    public string ErrorKey { get; }
}

/// <summary>Implemented by exceptions that carry a localization key the UI can show directly.</summary>
public interface ILocalizedError
{
    string ErrorKey { get; }
}
