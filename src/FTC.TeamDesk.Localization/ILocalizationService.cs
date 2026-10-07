using System.Globalization;

namespace FTC.TeamDesk.Localization;

public sealed record LanguageInfo(string Code, string NativeName);

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    CultureInfo Culture { get; }
    IReadOnlyList<LanguageInfo> SupportedLanguages { get; }

    /// <summary>Looks up a key in the active language, falling back to English, then to the key itself.</summary>
    string this[string key] { get; }
    string Get(string key);
    string Format(string key, params object?[] args);

    /// <summary>Switches language immediately and raises <see cref="LanguageChanged"/>. Unsupported codes fall back to English.</summary>
    void SetLanguage(string code);

    event EventHandler? LanguageChanged;
}
