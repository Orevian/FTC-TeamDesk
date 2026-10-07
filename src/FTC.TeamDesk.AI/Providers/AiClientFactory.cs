using FTC.TeamDesk.AI.Chat;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.AI.Providers;

public interface IAiClientFactory
{
    /// <summary>Builds a client. When <paramref name="apiKeyOverride"/> is null the key comes from the secret store.</summary>
    Task<IAiChatClient> CreateAsync(AiSettings settings, string? apiKeyOverride, CancellationToken ct);
    Task<OperationResult> TestConnectionAsync(AiSettings settings, string? apiKeyOverride, CancellationToken ct);
}

public sealed class AiClientFactory : IAiClientFactory
{
    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly IModelCatalog _catalog;

    public AiClientFactory(HttpClient http, ISecretStore secrets, IModelCatalog catalog)
    {
        _http = http; _secrets = secrets; _catalog = catalog;
    }

    public async Task<IAiChatClient> CreateAsync(AiSettings settings, string? apiKeyOverride, CancellationToken ct)
    {
        var info = AiProviderInfo.For(settings.Provider);
        var key = apiKeyOverride ?? await _secrets.GetAsync(SecretNames.AiKeyFor(settings.Provider), ct).ConfigureAwait(false);
        if (info.RequiresApiKey && string.IsNullOrWhiteSpace(key)) throw new AiProviderException("Ai.Error.ApiKeyMissing");
        var baseUrl = info.ResolveBaseUrl(settings.BaseUrl);

        return settings.Provider switch
        {
            AiProviderKind.Anthropic => new AnthropicClient(_http, baseUrl, key!),
            AiProviderKind.Gemini => new GeminiClient(_http, baseUrl, key!),
            _ => new OpenAiCompatibleClient(_http, settings.Provider, baseUrl, key)
        };
    }

    public async Task<OperationResult> TestConnectionAsync(AiSettings settings, string? apiKeyOverride, CancellationToken ct)
    {
        try
        {
            var key = apiKeyOverride ?? await _secrets.GetAsync(SecretNames.AiKeyFor(settings.Provider), ct).ConfigureAwait(false);
            var models = await _catalog.ListModelsAsync(settings.Provider, settings.BaseUrl, key, ct).ConfigureAwait(false);
            if (models.Succeeded) return OperationResult.Ok();
            // Listing may be unsupported by a compatible server; a tiny completion is the real proof.
            if (models.ErrorKey is "Ai.Error.Auth" or "Ai.Error.ApiKeyMissing" or "Ai.Error.Network" or "Ai.Error.BaseUrlRequired")
                return OperationResult.Fail(models.ErrorKey!, models.Detail);
            if (string.IsNullOrWhiteSpace(settings.Model)) return OperationResult.Fail(models.ErrorKey!, models.Detail);

            var client = await CreateAsync(settings, apiKeyOverride, ct).ConfigureAwait(false);
            await client.CompleteAsync(new ChatRequest(new[] { ChatMessage.User("ping") }, Array.Empty<ToolDefinition>(), settings.Model, 0, 8), ct).ConfigureAwait(false);
            return OperationResult.Ok();
        }
        catch (AiProviderException ex) { return OperationResult.Fail(ex.ErrorKey, ex.Message); }
        catch (OperationCanceledException) { return OperationResult.Fail("Error.Cancelled"); }
    }
}
