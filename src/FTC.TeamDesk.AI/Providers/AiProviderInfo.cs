using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.AI.Providers;

public sealed record AiProviderInfo(AiProviderKind Kind, string DisplayName, string? DefaultBaseUrl, bool RequiresApiKey, bool UsesBaseUrl)
{
    public static readonly IReadOnlyList<AiProviderInfo> All = new[]
    {
        new AiProviderInfo(AiProviderKind.OpenAI, "OpenAI", "https://api.openai.com/v1", true, false),
        new AiProviderInfo(AiProviderKind.Gemini, "Google Gemini", "https://generativelanguage.googleapis.com/v1beta", true, false),
        new AiProviderInfo(AiProviderKind.Anthropic, "Anthropic", "https://api.anthropic.com/v1", true, false),
        new AiProviderInfo(AiProviderKind.Mistral, "Mistral AI", "https://api.mistral.ai/v1", true, false),
        new AiProviderInfo(AiProviderKind.Ollama, "Ollama", "http://localhost:11434", false, true),
        new AiProviderInfo(AiProviderKind.OpenAiCompatible, "OpenAI-compatible", null, false, true)
    };

    public static AiProviderInfo For(AiProviderKind kind) => All.First(p => p.Kind == kind);

    /// <summary>Base URL to use: the user's override for providers that allow it, otherwise the provider default.</summary>
    public string ResolveBaseUrl(string? userBaseUrl)
    {
        var candidate = UsesBaseUrl && !string.IsNullOrWhiteSpace(userBaseUrl) ? userBaseUrl.Trim() : DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(candidate)) throw new Chat.AiProviderException("Ai.Error.BaseUrlRequired");
        candidate = candidate.TrimEnd('/');
        // Ollama's native API and its OpenAI-compatible API share a host; users often paste the '/v1' form.
        if (Kind == AiProviderKind.Ollama && candidate.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) candidate = candidate[..^3];
        return candidate;
    }
}
