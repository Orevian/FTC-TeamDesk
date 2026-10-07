using FTC.TeamDesk.AI.Providers;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.Services;
using Xunit;

namespace FTC.TeamDesk.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task RoundTripsAndClampsValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"td-settings-{Guid.NewGuid():N}.json");
        try
        {
            var svc = new SettingsService(path, null, new NullAppLogger());
            Assert.Equal("auto", svc.Current.Language);
            await svc.SaveAsync(new UserPreferences { Language = "tr", Theme = ThemeMode.Dark, VaultAutoLockMinutes = 0, ClipboardClearSeconds = 99999 });

            var reloaded = new SettingsService(path, null, new NullAppLogger());
            Assert.Equal("tr", reloaded.Current.Language);
            Assert.Equal(ThemeMode.Dark, reloaded.Current.Theme);
            Assert.Equal(1, reloaded.Current.VaultAutoLockMinutes);
            Assert.Equal(300, reloaded.Current.ClipboardClearSeconds);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CorruptFileFallsBackToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"td-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ this is not json");
        try { Assert.Equal("auto", SettingsService.ReadFile(path).Language); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task SettingsFileNeverContainsSecrets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"td-settings-{Guid.NewGuid():N}.json");
        try
        {
            var svc = new SettingsService(path, null, new NullAppLogger());
            await svc.SaveAsync(new UserPreferences { GoogleClientId = "client-id-123.apps.googleusercontent.com" });
            var text = File.ReadAllText(path).ToLowerInvariant();
            Assert.DoesNotContain("apikey", text);
            Assert.DoesNotContain("token", text);
            Assert.DoesNotContain("secret", text);
            Assert.DoesNotContain("password", text.Replace("vaultautolock", ""));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public class AiProviderInfoTests
{
    [Fact]
    public void ResolvesDefaultsAndOverrides()
    {
        Assert.Equal("https://api.openai.com/v1", AiProviderInfo.For(AiProviderKind.OpenAI).ResolveBaseUrl(null));
        Assert.Equal("https://api.openai.com/v1", AiProviderInfo.For(AiProviderKind.OpenAI).ResolveBaseUrl("http://evil.example")); // fixed-host providers ignore overrides
        Assert.Equal("http://localhost:11434", AiProviderInfo.For(AiProviderKind.Ollama).ResolveBaseUrl(null));
        Assert.Equal("http://gpu-box:11434", AiProviderInfo.For(AiProviderKind.Ollama).ResolveBaseUrl("http://gpu-box:11434/v1/"));
        Assert.Equal("https://llm.example.com/v1", AiProviderInfo.For(AiProviderKind.OpenAiCompatible).ResolveBaseUrl("https://llm.example.com/v1/"));
    }

    [Fact]
    public void CompatibleProviderRequiresBaseUrl()
        => Assert.Throws<FTC.TeamDesk.AI.Chat.AiProviderException>(() => AiProviderInfo.For(AiProviderKind.OpenAiCompatible).ResolveBaseUrl(null));

    [Fact]
    public void AllSixProvidersAreRegistered() => Assert.Equal(6, AiProviderInfo.All.Count);
}

public class MemberValidationTests
{
    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("a@b", false)]
    [InlineData("a b@c.com", false)]
    [InlineData("@c.com", false)]
    [InlineData("a@@c.com", false)]
    public void EmailCheck(string email, bool ok) => Assert.Equal(ok, MemberService.LooksLikeEmail(email));
}
