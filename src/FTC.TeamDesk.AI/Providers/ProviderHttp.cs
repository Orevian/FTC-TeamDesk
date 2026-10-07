using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FTC.TeamDesk.AI.Chat;
using FTC.TeamDesk.Security;

namespace FTC.TeamDesk.AI.Providers;

/// <summary>Shared HTTP plumbing: JSON POST/GET with uniform, secret-free error mapping.</summary>
internal static class ProviderHttp
{
    public static async Task<JsonNode> SendAsync(HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { throw new AiProviderException("Ai.Error.Network"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new AiProviderException("Ai.Error.Timeout"); }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw MapError(response.StatusCode, body);
            try { return JsonNode.Parse(body) ?? throw new AiProviderException("Ai.Error.BadResponse"); }
            catch (JsonException) { throw new AiProviderException("Ai.Error.BadResponse"); }
        }
    }

    public static AiProviderException MapError(HttpStatusCode status, string body)
    {
        var detail = ExtractMessage(body);
        var key = status switch
        {
            HttpStatusCode.Unauthorized => "Ai.Error.Auth",
            HttpStatusCode.Forbidden => "Ai.Error.Auth",
            HttpStatusCode.NotFound => "Ai.Error.NotFound",
            HttpStatusCode.TooManyRequests => "Ai.Error.RateLimited",
            HttpStatusCode.BadRequest => "Ai.Error.BadRequest",
            _ when (int)status >= 500 => "Ai.Error.ProviderDown",
            _ => "Ai.Error.Generic"
        };
        return new AiProviderException(key, $"{(int)status}: {detail}");
    }

    public static string ExtractMessage(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            var msg = node?["error"]?["message"]?.GetValue<string>() ?? node?["error"]?.ToString() ?? node?["message"]?.ToString();
            return SensitiveDataRedactor.Redact(Truncate(msg ?? body, 300));
        }
        catch (JsonException) { return SensitiveDataRedactor.Redact(Truncate(body, 300)); }
    }

    public static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    public static StringContent JsonBody(JsonNode node) => new(node.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
}
