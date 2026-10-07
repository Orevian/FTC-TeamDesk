using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using FTC.TeamDesk.AI.Chat;
using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.AI.Providers;

/// <summary>Chat Completions with native tool calling. Serves OpenAI, Mistral, Ollama (/v1) and any compatible endpoint.</summary>
public sealed class OpenAiCompatibleClient : IAiChatClient
{
    private readonly HttpClient _http;
    private readonly AiProviderKind _kind;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public OpenAiCompatibleClient(HttpClient http, AiProviderKind kind, string baseUrl, string? apiKey)
    {
        _http = http; _kind = kind; _baseUrl = baseUrl; _apiKey = apiKey;
    }

    private string ChatUrl => _kind == AiProviderKind.Ollama ? $"{_baseUrl}/v1/chat/completions" : $"{_baseUrl}/chat/completions";

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        var useTemperature = true;
        var tokenParam = _kind == AiProviderKind.OpenAI ? "max_completion_tokens" : "max_tokens";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var node = await SendOnceAsync(request, useTemperature, tokenParam, ct).ConfigureAwait(false);
                return Parse(node);
            }
            catch (AiProviderException ex) when (attempt < 2 && ex.ErrorKey == "Ai.Error.BadRequest")
            {
                // Some models reject temperature or the token-limit parameter name; adapt once and retry.
                var msg = ex.Message;
                if (useTemperature && msg.Contains("temperature", StringComparison.OrdinalIgnoreCase)) { useTemperature = false; continue; }
                if (msg.Contains("max_completion_tokens", StringComparison.OrdinalIgnoreCase) && tokenParam == "max_tokens") { tokenParam = "max_completion_tokens"; continue; }
                if (msg.Contains("max_tokens", StringComparison.OrdinalIgnoreCase) && tokenParam == "max_completion_tokens") { tokenParam = "max_tokens"; continue; }
                throw;
            }
        }
    }

    private Task<JsonNode> SendOnceAsync(ChatRequest req, bool useTemperature, string tokenParam, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = req.Model,
            ["messages"] = BuildMessages(req.Messages),
            [tokenParam] = req.MaxTokens
        };
        if (useTemperature) body["temperature"] = req.Temperature;
        if (req.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var t in req.Tools)
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.ParametersSchema.DeepClone() }
                });
            body["tools"] = tools;
        }

        var http = new HttpRequestMessage(HttpMethod.Post, ChatUrl) { Content = ProviderHttp.JsonBody(body) };
        if (!string.IsNullOrEmpty(_apiKey)) http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        return ProviderHttp.SendAsync(_http, http, ct);
    }

    private static JsonArray BuildMessages(IReadOnlyList<ChatMessage> messages)
    {
        var arr = new JsonArray();
        foreach (var m in messages)
        {
            var o = new JsonObject();
            switch (m.Role)
            {
                case ChatRole.System: o["role"] = "system"; o["content"] = m.Content; break;
                case ChatRole.User: o["role"] = "user"; o["content"] = m.Content; break;
                case ChatRole.Assistant:
                    o["role"] = "assistant";
                    o["content"] = m.Content;
                    if (m.ToolCalls is { Count: > 0 })
                    {
                        var calls = new JsonArray();
                        foreach (var c in m.ToolCalls)
                            calls.Add(new JsonObject
                            {
                                ["id"] = c.Id, ["type"] = "function",
                                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments.ToJsonString() }
                            });
                        o["tool_calls"] = calls;
                    }
                    break;
                case ChatRole.Tool: o["role"] = "tool"; o["tool_call_id"] = m.ToolCallId; o["content"] = m.Content; break;
            }
            arr.Add(o);
        }
        return arr;
    }

    private static ChatCompletion Parse(JsonNode root)
    {
        var message = root["choices"]?[0]?["message"] ?? throw new AiProviderException("Ai.Error.BadResponse");
        var text = message["content"]?.GetValue<string>();
        var calls = new List<ToolCall>();
        if (message["tool_calls"] is JsonArray arr)
        {
            foreach (var c in arr)
            {
                var name = c?["function"]?["name"]?.GetValue<string>();
                if (name is null) continue;
                var argsText = c?["function"]?["arguments"]?.GetValue<string>();
                JsonObject args;
                try { args = string.IsNullOrWhiteSpace(argsText) ? new JsonObject() : (JsonNode.Parse(argsText) as JsonObject ?? new JsonObject()); }
                catch (JsonException) { args = new JsonObject(); }
                calls.Add(new ToolCall(c?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"), name, args));
            }
        }
        return new ChatCompletion(text, calls);
    }
}
