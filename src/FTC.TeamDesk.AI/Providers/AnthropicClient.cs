using System.Text.Json.Nodes;
using FTC.TeamDesk.AI.Chat;

namespace FTC.TeamDesk.AI.Providers;

/// <summary>Anthropic Messages API with native tool use.</summary>
public sealed class AnthropicClient : IAiChatClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public AnthropicClient(HttpClient http, string baseUrl, string apiKey) { _http = http; _baseUrl = baseUrl; _apiKey = apiKey; }

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        var system = string.Join("\n\n", request.Messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = Math.Clamp(request.Temperature, 0, 1),
            ["messages"] = BuildMessages(request.Messages)
        };
        if (system.Length > 0) body["system"] = system;
        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var t in request.Tools)
                tools.Add(new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["input_schema"] = t.ParametersSchema.DeepClone() });
            body["tools"] = tools;
        }

        var http = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/messages") { Content = ProviderHttp.JsonBody(body) };
        http.Headers.Add("x-api-key", _apiKey);
        http.Headers.Add("anthropic-version", "2023-06-01");
        var root = await ProviderHttp.SendAsync(_http, http, ct).ConfigureAwait(false);

        var text = new System.Text.StringBuilder();
        var calls = new List<ToolCall>();
        if (root["content"] is JsonArray blocks)
        {
            foreach (var b in blocks)
            {
                switch (b?["type"]?.GetValue<string>())
                {
                    case "text": text.Append(b["text"]?.GetValue<string>()); break;
                    case "tool_use":
                        calls.Add(new ToolCall(b["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                            b["name"]?.GetValue<string>() ?? "", b["input"] as JsonObject ?? new JsonObject()));
                        break;
                }
            }
        }
        return new ChatCompletion(text.Length == 0 ? null : text.ToString(), calls);
    }

    private static JsonArray BuildMessages(IReadOnlyList<ChatMessage> messages)
    {
        var arr = new JsonArray();
        JsonArray? pendingToolResults = null;

        void FlushToolResults()
        {
            if (pendingToolResults is null) return;
            arr.Add(new JsonObject { ["role"] = "user", ["content"] = pendingToolResults });
            pendingToolResults = null;
        }

        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case ChatRole.System: break;
                case ChatRole.Tool:
                    pendingToolResults ??= new JsonArray();
                    pendingToolResults.Add(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = m.ToolCallId, ["content"] = m.Content ?? "" });
                    break;
                case ChatRole.User:
                    FlushToolResults();
                    arr.Add(new JsonObject { ["role"] = "user", ["content"] = m.Content ?? "" });
                    break;
                case ChatRole.Assistant:
                    FlushToolResults();
                    var content = new JsonArray();
                    if (!string.IsNullOrEmpty(m.Content)) content.Add(new JsonObject { ["type"] = "text", ["text"] = m.Content });
                    if (m.ToolCalls is not null)
                        foreach (var c in m.ToolCalls)
                            content.Add(new JsonObject { ["type"] = "tool_use", ["id"] = c.Id, ["name"] = c.Name, ["input"] = c.Arguments.DeepClone() });
                    if (content.Count == 0) content.Add(new JsonObject { ["type"] = "text", ["text"] = "..." });
                    arr.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                    break;
            }
        }
        FlushToolResults();
        return arr;
    }
}
