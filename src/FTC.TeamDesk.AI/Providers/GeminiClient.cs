using System.Text.Json.Nodes;
using FTC.TeamDesk.AI.Chat;

namespace FTC.TeamDesk.AI.Providers;

/// <summary>Gemini generateContent with native function calling. The API key is sent in a header, never in the URL.</summary>
public sealed class GeminiClient : IAiChatClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public GeminiClient(HttpClient http, string baseUrl, string apiKey) { _http = http; _baseUrl = baseUrl; _apiKey = apiKey; }

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["contents"] = BuildContents(request.Messages),
            ["generationConfig"] = new JsonObject { ["temperature"] = request.Temperature, ["maxOutputTokens"] = request.MaxTokens }
        };
        var system = string.Join("\n\n", request.Messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
        if (system.Length > 0)
            body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) };
        if (request.Tools.Count > 0)
        {
            var decls = new JsonArray();
            foreach (var t in request.Tools)
                decls.Add(new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = ConvertSchema(t.ParametersSchema) });
            body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = decls });
        }

        var model = request.Model.StartsWith("models/", StringComparison.Ordinal) ? request.Model : "models/" + request.Model;
        var http = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/{model}:generateContent") { Content = ProviderHttp.JsonBody(body) };
        http.Headers.Add("x-goog-api-key", _apiKey);
        var root = await ProviderHttp.SendAsync(_http, http, ct).ConfigureAwait(false);

        var parts = root["candidates"]?[0]?["content"]?["parts"] as JsonArray;
        var text = new System.Text.StringBuilder();
        var calls = new List<ToolCall>();
        if (parts is not null)
        {
            var i = 0;
            foreach (var p in parts)
            {
                if (p?["text"] is JsonNode t) text.Append(t.GetValue<string>());
                if (p?["functionCall"] is JsonObject fc)
                    calls.Add(new ToolCall($"call_{i++}", fc["name"]?.GetValue<string>() ?? "", fc["args"] as JsonObject ?? new JsonObject()));
            }
        }
        if (text.Length == 0 && calls.Count == 0 && root["promptFeedback"]?["blockReason"] is not null)
            throw new AiProviderException("Ai.Error.Blocked");
        return new ChatCompletion(text.Length == 0 ? null : text.ToString(), calls);
    }

    private static JsonArray BuildContents(IReadOnlyList<ChatMessage> messages)
    {
        var arr = new JsonArray();
        JsonArray? pendingResponses = null;

        void Flush()
        {
            if (pendingResponses is null) return;
            arr.Add(new JsonObject { ["role"] = "user", ["parts"] = pendingResponses });
            pendingResponses = null;
        }

        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case ChatRole.System: break;
                case ChatRole.User:
                    Flush();
                    arr.Add(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = m.Content ?? "" }) });
                    break;
                case ChatRole.Assistant:
                    Flush();
                    var parts = new JsonArray();
                    if (!string.IsNullOrEmpty(m.Content)) parts.Add(new JsonObject { ["text"] = m.Content });
                    if (m.ToolCalls is not null)
                        foreach (var c in m.ToolCalls)
                            parts.Add(new JsonObject { ["functionCall"] = new JsonObject { ["name"] = c.Name, ["args"] = c.Arguments.DeepClone() } });
                    if (parts.Count == 0) parts.Add(new JsonObject { ["text"] = "..." });
                    arr.Add(new JsonObject { ["role"] = "model", ["parts"] = parts });
                    break;
                case ChatRole.Tool:
                    pendingResponses ??= new JsonArray();
                    JsonNode? parsed;
                    try { parsed = JsonNode.Parse(m.Content ?? "{}"); } catch (System.Text.Json.JsonException) { parsed = null; }
                    var responseObj = parsed as JsonObject ?? new JsonObject { ["result"] = m.Content };
                    pendingResponses.Add(new JsonObject { ["functionResponse"] = new JsonObject { ["name"] = m.ToolName, ["response"] = responseObj } });
                    break;
            }
        }
        Flush();
        return arr;
    }

    /// <summary>Gemini accepts an OpenAPI subset: upper-case type names and no additionalProperties.</summary>
    internal static JsonNode ConvertSchema(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var kv in obj)
                {
                    if (kv.Key == "additionalProperties") continue;
                    if (kv.Key == "type" && kv.Value is JsonValue v && v.TryGetValue<string>(out var s)) result["type"] = s.ToUpperInvariant();
                    else result[kv.Key] = kv.Value is null ? null : ConvertSchema(kv.Value);
                }
                return result;
            case JsonArray arr:
                var copy = new JsonArray();
                foreach (var item in arr) copy.Add(item is null ? null : ConvertSchema(item));
                return copy;
            default:
                return node.DeepClone();
        }
    }
}
