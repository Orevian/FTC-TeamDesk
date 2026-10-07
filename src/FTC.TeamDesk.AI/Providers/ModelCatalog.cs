using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using FTC.TeamDesk.AI.Chat;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.AI.Providers;

public interface IModelCatalog
{
    /// <summary>Lists chat-capable models from the provider API. Fails with a localization key.</summary>
    Task<OperationResult<IReadOnlyList<string>>> ListModelsAsync(AiProviderKind kind, string? baseUrl, string? apiKey, CancellationToken ct);
}

public sealed class ModelCatalog : IModelCatalog
{
    private static readonly string[] NonChatMarkers =
        { "embedding", "embed", "whisper", "tts", "dall-e", "moderation", "audio", "realtime", "transcribe", "image", "davinci", "babbage", "ocr" };

    private readonly HttpClient _http;
    public ModelCatalog(HttpClient http) { _http = http; }

    public async Task<OperationResult<IReadOnlyList<string>>> ListModelsAsync(AiProviderKind kind, string? baseUrl, string? apiKey, CancellationToken ct)
    {
        try
        {
            var info = AiProviderInfo.For(kind);
            if (info.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey)) return OperationResult<IReadOnlyList<string>>.Fail("Ai.Error.ApiKeyMissing");
            var url = info.ResolveBaseUrl(baseUrl);
            var models = kind switch
            {
                AiProviderKind.Anthropic => await ListAnthropicAsync(url, apiKey!, ct).ConfigureAwait(false),
                AiProviderKind.Gemini => await ListGeminiAsync(url, apiKey!, ct).ConfigureAwait(false),
                AiProviderKind.Ollama => await ListOllamaAsync(url, ct).ConfigureAwait(false),
                _ => await ListOpenAiStyleAsync(url, apiKey, kind == AiProviderKind.OpenAI, ct).ConfigureAwait(false)
            };
            return OperationResult<IReadOnlyList<string>>.Ok(models.Distinct().OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList());
        }
        catch (AiProviderException ex) { return OperationResult<IReadOnlyList<string>>.Fail(ex.ErrorKey, ex.Message); }
    }

    private async Task<List<string>> ListOpenAiStyleAsync(string baseUrl, string? key, bool filter, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
        if (!string.IsNullOrEmpty(key)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var root = await ProviderHttp.SendAsync(_http, req, ct).ConfigureAwait(false);
        var ids = (root["data"] as JsonArray ?? new JsonArray()).Select(n => n?["id"]?.GetValue<string>()).OfType<string>();
        if (filter) ids = ids.Where(id => !NonChatMarkers.Any(m => id.Contains(m, StringComparison.OrdinalIgnoreCase)));
        return ids.ToList();
    }

    private async Task<List<string>> ListAnthropicAsync(string baseUrl, string key, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models?limit=100");
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        var root = await ProviderHttp.SendAsync(_http, req, ct).ConfigureAwait(false);
        return (root["data"] as JsonArray ?? new JsonArray()).Select(n => n?["id"]?.GetValue<string>()).OfType<string>().ToList();
    }

    private async Task<List<string>> ListGeminiAsync(string baseUrl, string key, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models?pageSize=200");
        req.Headers.Add("x-goog-api-key", key);
        var root = await ProviderHttp.SendAsync(_http, req, ct).ConfigureAwait(false);
        var result = new List<string>();
        foreach (var m in root["models"] as JsonArray ?? new JsonArray())
        {
            var name = m?["name"]?.GetValue<string>();
            var methods = m?["supportedGenerationMethods"] as JsonArray;
            if (name is null) continue;
            if (methods is not null && !methods.Any(x => x?.GetValue<string>() == "generateContent")) continue;
            result.Add(name.StartsWith("models/", StringComparison.Ordinal) ? name[7..] : name);
        }
        return result;
    }

    private async Task<List<string>> ListOllamaAsync(string baseUrl, CancellationToken ct)
    {
        var root = await ProviderHttp.SendAsync(_http, new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/tags"), ct).ConfigureAwait(false);
        return (root["models"] as JsonArray ?? new JsonArray()).Select(n => n?["name"]?.GetValue<string>()).OfType<string>().ToList();
    }
}
