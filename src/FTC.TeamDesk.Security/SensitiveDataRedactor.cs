using System.Text.RegularExpressions;

namespace FTC.TeamDesk.Security;

/// <summary>Removes secrets from any text before it reaches a log file or the activity log.</summary>
public static partial class SensitiveDataRedactor
{
    public const string Mask = "[REDACTED]";

    [GeneratedRegex("""(?<key>["']?(?:password|passwd|pwd|passphrase|master[_ -]?password|secret|client[_-]?secret|token|access[_-]?token|refresh[_-]?token|api[_-]?key|apikey|authorization|auth)["']?\s*[:=]\s*)(?<val>"[^"]*"|'[^']*'|[^\s,;&}]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"\b(sk-[A-Za-z0-9_\-]{12,}|AIza[0-9A-Za-z_\-]{20,}|ya29\.[0-9A-Za-z_\-]{10,}|1//[0-9A-Za-z_\-]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|xox[baprs]-[A-Za-z0-9\-]{10,})")]
    private static partial Regex KnownKeyPattern();

    [GeneratedRegex(@"([?&](?:key|api_key|access_token|token|client_secret|code)=)[^&\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex QueryPattern();

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        // Bearer tokens first, so "Authorization: Bearer abc" cannot leave "abc" behind after the key/value pass.
        var result = BearerPattern().Replace(text, "Bearer " + Mask);
        result = KeyValuePattern().Replace(result, m => m.Groups["key"].Value + Mask);
        result = QueryPattern().Replace(result, m => m.Groups[1].Value + Mask);
        result = KnownKeyPattern().Replace(result, Mask);
        return result;
    }
}
