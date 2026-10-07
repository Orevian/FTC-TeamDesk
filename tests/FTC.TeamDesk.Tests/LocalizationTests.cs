using System.Globalization;
using FTC.TeamDesk.Localization;
using Xunit;

namespace FTC.TeamDesk.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData("tr-TR", "tr")]
    [InlineData("tr", "tr")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("de-DE", "en")]   // unsupported -> English
    [InlineData("ja-JP", "en")]
    public void DetectsWindowsLanguage(string windows, string expected)
        => Assert.Equal(expected, LocalizationService.ResolveInitial("auto", new CultureInfo(windows)));

    [Fact]
    public void ExplicitPreferenceWinsOverSystem()
        => Assert.Equal("en", LocalizationService.ResolveInitial("en", new CultureInfo("tr-TR")));

    [Fact]
    public void SwitchesLanguageInstantlyAndRaisesEvent()
    {
        var loc = new LocalizationService("en");
        var raised = 0;
        loc.LanguageChanged += (_, _) => raised++;
        var en = loc.Get("Nav.Dashboard");
        loc.SetLanguage("tr");
        Assert.Equal(1, raised);
        Assert.NotEqual(en, loc.Get("Nav.Team") == "Nav.Team" ? "x" : "y");
        Assert.Equal("tr", loc.CurrentLanguage);
        loc.SetLanguage("tr");
        Assert.Equal(1, raised); // no-op when unchanged
    }

    [Fact]
    public void MissingKeyFallsBackToKey() => Assert.Equal("No.Such.Key", new LocalizationService("tr").Get("No.Such.Key"));

    [Fact]
    public void EnglishAndTurkishHaveExactlyTheSameKeys()
    {
        var (en, tr) = LoadTables();
        Assert.Empty(en.Keys.Except(tr.Keys));
        Assert.Empty(tr.Keys.Except(en.Keys));
    }

    [Fact]
    public void NoTranslationIsEmpty()
    {
        var (en, tr) = LoadTables();
        Assert.All(en.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
        Assert.All(tr.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public void PlaceholdersMatchBetweenLanguages()
    {
        var (en, tr) = LoadTables();
        foreach (var (key, value) in en)
        {
            var a = System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToList();
            var b = System.Text.RegularExpressions.Regex.Matches(tr[key], @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToList();
            Assert.True(a.SequenceEqual(b));
        }
    }

    private static (Dictionary<string, string> en, Dictionary<string, string> tr) LoadTables()
    {
        var asm = typeof(LocalizationService).Assembly;
        Dictionary<string, string> Read(string code)
        {
            var name = asm.GetManifestResourceNames().Single(n => n.EndsWith($".{code}.json", StringComparison.OrdinalIgnoreCase));
            using var s = asm.GetManifestResourceStream(name)!;
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(s)!;
        }
        return (Read("en"), Read("tr"));
    }
}
