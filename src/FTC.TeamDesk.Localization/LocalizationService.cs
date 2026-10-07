using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace FTC.TeamDesk.Localization;

/// <summary>JSON-backed localization with instant language switching. Resource files are embedded in the assembly.</summary>
public sealed class LocalizationService : ILocalizationService
{
    public const string English = "en";
    public const string Turkish = "tr";

    private static readonly LanguageInfo[] Languages =
    {
        new(English, "English"),
        new(Turkish, "Türkçe")
    };

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _tables = new();
    private IReadOnlyDictionary<string, string> _active;
    private IReadOnlyDictionary<string, string> _fallback;

    public LocalizationService(string initialLanguage)
    {
        foreach (var lang in Languages) _tables[lang.Code] = Load(lang.Code);
        _fallback = _tables[English];
        CurrentLanguage = Normalize(initialLanguage);
        _active = _tables[CurrentLanguage];
        Culture = CultureFor(CurrentLanguage);
        ApplyCulture();
    }

    public string CurrentLanguage { get; private set; }
    public CultureInfo Culture { get; private set; }
    public IReadOnlyList<LanguageInfo> SupportedLanguages => Languages;
    public event EventHandler? LanguageChanged;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_active.TryGetValue(key, out var v)) return v;
        if (_fallback.TryGetValue(key, out v)) return v;
        return key;
    }

    public string Format(string key, params object?[] args)
    {
        try { return string.Format(Culture, Get(key), args); }
        catch (FormatException) { return Get(key); }
    }

    public void SetLanguage(string code)
    {
        var normalized = Normalize(code);
        if (normalized == CurrentLanguage) return;
        CurrentLanguage = normalized;
        _active = _tables[normalized];
        Culture = CultureFor(normalized);
        ApplyCulture();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resolves the language to use at start-up: an explicit "tr"/"en" preference wins, "auto" (or anything else)
    /// follows the Windows display language, and unsupported languages fall back to English.
    /// </summary>
    public static string ResolveInitial(string? preference, CultureInfo? systemUiCulture = null)
    {
        if (!string.IsNullOrWhiteSpace(preference) && !preference.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return Normalize(preference);
        var culture = systemUiCulture ?? CultureInfo.InstalledUICulture;
        return Normalize(culture.TwoLetterISOLanguageName);
    }

    public static string Normalize(string? code)
    {
        var two = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (two.Length > 2) two = two[..2];
        return Languages.Any(l => l.Code == two) ? two : English;
    }

    private static CultureInfo CultureFor(string code) => code == Turkish ? new CultureInfo("tr-TR") : new CultureInfo("en-US");

    private void ApplyCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    private static IReadOnlyDictionary<string, string> Load(string code)
    {
        var asm = typeof(LocalizationService).Assembly;
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($".{code}.json", StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"Missing localization resource for '{code}'.");
        using var stream = asm.GetManifestResourceStream(name)!;
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                   ?? throw new InvalidOperationException($"Invalid localization resource '{name}'.");
        return dict;
    }
}
