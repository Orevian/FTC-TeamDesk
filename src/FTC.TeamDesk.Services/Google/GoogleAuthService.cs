using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;

namespace FTC.TeamDesk.Services.Google;

public sealed class GoogleAuthException : Exception, ILocalizedError
{
    public GoogleAuthException(string errorKey, string? detail = null) : base(detail ?? errorKey) { ErrorKey = errorKey; }
    public string ErrorKey { get; }
}

/// <summary>
/// OAuth 2.0 for installed apps: system browser + loopback redirect + PKCE (RFC 7636/8252).
/// Tokens are stored ONLY in the DPAPI-protected secret store, never in the database or config file.
/// </summary>
public sealed class GoogleAuthService
{
    public const string Scopes =
        "openid email https://www.googleapis.com/auth/spreadsheets.readonly https://www.googleapis.com/auth/drive.metadata.readonly";
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public GoogleAuthService(HttpClient http, ISecretStore secrets, ISettingsService settings, IAppLogger logger)
    {
        _http = http; _secrets = secrets; _settings = settings; _logger = logger;
    }

    private sealed class StoredTokens
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string? Email { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    public async Task<bool> HasTokensAsync(CancellationToken ct) => await _secrets.ExistsAsync(SecretNames.GoogleTokens, ct).ConfigureAwait(false);

    public async Task<string?> GetAccountEmailAsync(CancellationToken ct) => (await LoadAsync(ct).ConfigureAwait(false))?.Email;

    public async Task ConnectAsync(string clientId, string? clientSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId)) throw new GoogleAuthException("Google.Error.ClientIdMissing");

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        var port = FreePort();
        var redirect = $"http://127.0.0.1:{port}/";

        var url = $"{AuthEndpoint}?client_id={Uri.EscapeDataString(clientId.Trim())}&redirect_uri={Uri.EscapeDataString(redirect)}" +
                  $"&response_type=code&scope={Uri.EscapeDataString(Scopes)}&code_challenge={challenge}&code_challenge_method=S256" +
                  $"&state={state}&access_type=offline&prompt=consent";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect);
        listener.Start();
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        string code;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var contextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, timeout.Token)).ConfigureAwait(false);
            if (completed != contextTask)
            {
                ct.ThrowIfCancellationRequested();
                throw new GoogleAuthException("Google.Error.Timeout");
            }
            var context = await contextTask.ConfigureAwait(false);
            var query = context.Request.QueryString;
            var error = query["error"];
            var returnedState = query["state"];
            code = query["code"] ?? string.Empty;
            await RespondAsync(context, success: error is null && !string.IsNullOrEmpty(code)).ConfigureAwait(false);

            if (error is not null) throw new GoogleAuthException("Google.Error.Denied", error);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(returnedState ?? ""), Encoding.UTF8.GetBytes(state)))
                throw new GoogleAuthException("Google.Error.StateMismatch");
            if (string.IsNullOrEmpty(code)) throw new GoogleAuthException("Google.Error.Denied");
        }
        finally { listener.Stop(); }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(), ["code"] = code, ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code"
        };
        if (!string.IsNullOrWhiteSpace(clientSecret)) form["client_secret"] = clientSecret.Trim();

        var token = await PostTokenAsync(form, ct).ConfigureAwait(false);
        var stored = new StoredTokens
        {
            AccessToken = token.AccessToken!, RefreshToken = token.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn)), Email = EmailFromIdToken(token.IdToken)
        };
        await SaveAsync(stored, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(clientSecret)) await _secrets.SetAsync(SecretNames.GoogleClientSecret, clientSecret.Trim(), ct).ConfigureAwait(false);
    }

    /// <summary>Returns a valid access token, refreshing it if needed.</summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var tokens = await LoadAsync(ct).ConfigureAwait(false) ?? throw new GoogleAuthException("Google.Error.NotConnected");
        if (tokens.ExpiresAtUtc > DateTime.UtcNow.AddSeconds(60)) return tokens.AccessToken;

        await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            tokens = await LoadAsync(ct).ConfigureAwait(false) ?? throw new GoogleAuthException("Google.Error.NotConnected");
            if (tokens.ExpiresAtUtc > DateTime.UtcNow.AddSeconds(60)) return tokens.AccessToken;
            if (string.IsNullOrEmpty(tokens.RefreshToken)) throw new GoogleAuthException("Google.Error.SessionExpired");

            var clientId = _settings.Current.GoogleClientId;
            if (string.IsNullOrWhiteSpace(clientId)) throw new GoogleAuthException("Google.Error.ClientIdMissing");
            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId, ["refresh_token"] = tokens.RefreshToken, ["grant_type"] = "refresh_token"
            };
            var secret = await _secrets.GetAsync(SecretNames.GoogleClientSecret, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(secret)) form["client_secret"] = secret;

            TokenResponse refreshed;
            try { refreshed = await PostTokenAsync(form, ct).ConfigureAwait(false); }
            catch (GoogleAuthException) { throw new GoogleAuthException("Google.Error.SessionExpired"); }

            tokens.AccessToken = refreshed.AccessToken!;
            tokens.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, refreshed.ExpiresIn));
            if (!string.IsNullOrEmpty(refreshed.RefreshToken)) tokens.RefreshToken = refreshed.RefreshToken;
            await SaveAsync(tokens, ct).ConfigureAwait(false);
            return tokens.AccessToken;
        }
        finally { _refreshGate.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        var tokens = await LoadAsync(ct).ConfigureAwait(false);
        var toRevoke = tokens?.RefreshToken ?? tokens?.AccessToken;
        if (!string.IsNullOrEmpty(toRevoke))
        {
            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = toRevoke });
                await _http.PostAsync(RevokeEndpoint, content, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) { _logger.Warning("Google token revoke failed: " + ex.Message); }
        }
        await _secrets.DeleteAsync(SecretNames.GoogleTokens, ct).ConfigureAwait(false);
        await _secrets.DeleteAsync(SecretNames.GoogleClientSecret, ct).ConfigureAwait(false);
    }

    // ---- internals ----

    private async Task<TokenResponse> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _http.PostAsync(TokenEndpoint, content, ct).ConfigureAwait(false);
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || body?.AccessToken is null)
                throw new GoogleAuthException("Google.Error.TokenExchange", body?.Error ?? ((int)response.StatusCode).ToString());
            return body;
        }
        catch (HttpRequestException) { throw new GoogleAuthException("Google.Error.Network"); }
    }

    private async Task<StoredTokens?> LoadAsync(CancellationToken ct)
    {
        var json = await _secrets.GetAsync(SecretNames.GoogleTokens, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<StoredTokens>(json); }
        catch (JsonException) { return null; }
    }

    private Task SaveAsync(StoredTokens tokens, CancellationToken ct)
        => _secrets.SetAsync(SecretNames.GoogleTokens, JsonSerializer.Serialize(tokens), ct);

    private static string? EmailFromIdToken(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken)) return null;
        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { return null; }
    }

    private static async Task RespondAsync(HttpListenerContext context, bool success)
    {
        var title = success ? "FTC TeamDesk is connected to Google." : "Google connection was not completed.";
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>FTC TeamDesk</title></head>" +
                   $"<body style=\"font-family:Segoe UI,sans-serif;text-align:center;margin-top:20vh;color:#111827\"><h2>{title}</h2><p>You can close this tab and return to the app.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
