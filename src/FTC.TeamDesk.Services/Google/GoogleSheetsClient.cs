using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services.Google;

public sealed class GoogleApiException : Exception, ILocalizedError
{
    public GoogleApiException(string errorKey, string? detail = null) : base(detail ?? errorKey) { ErrorKey = errorKey; }
    public string ErrorKey { get; }
}

/// <summary>Thin REST client for the Sheets v4 and Drive v3 endpoints the app needs (read-only scopes).</summary>
public sealed partial class GoogleSheetsClient
{
    private readonly HttpClient _http;
    private readonly GoogleAuthService _auth;

    public GoogleSheetsClient(HttpClient http, GoogleAuthService auth) { _http = http; _auth = auth; }

    [GeneratedRegex(@"/spreadsheets/d/([a-zA-Z0-9\-_]+)")]
    private static partial Regex UrlIdPattern();

    /// <summary>Accepts a full Google Sheets URL or a bare spreadsheet id.</summary>
    public static string ExtractSpreadsheetId(string input)
    {
        var trimmed = input.Trim();
        var m = UrlIdPattern().Match(trimmed);
        if (m.Success) return m.Groups[1].Value;
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9\-_]{20,}$")) return trimmed;
        throw new GoogleApiException("Google.Error.InvalidSpreadsheet");
    }

    public async Task<IReadOnlyList<SpreadsheetInfo>> ListSpreadsheetsAsync(CancellationToken ct)
    {
        var q = Uri.EscapeDataString("mimeType='application/vnd.google-apps.spreadsheet' and trashed=false");
        var url = $"https://www.googleapis.com/drive/v3/files?q={q}&orderBy=modifiedTime%20desc&pageSize=50&fields=files(id,name)";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var list = new List<SpreadsheetInfo>();
        if (doc.RootElement.TryGetProperty("files", out var files))
            foreach (var f in files.EnumerateArray())
                list.Add(new SpreadsheetInfo(f.GetProperty("id").GetString()!, f.GetProperty("name").GetString() ?? ""));
        return list;
    }

    public async Task<(string Title, IReadOnlyList<string> Tabs)> GetSpreadsheetAsync(string spreadsheetId, CancellationToken ct)
    {
        var url = $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}?fields=properties.title,sheets.properties.title";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var title = doc.RootElement.GetProperty("properties").GetProperty("title").GetString() ?? "";
        var tabs = new List<string>();
        if (doc.RootElement.TryGetProperty("sheets", out var sheets))
            foreach (var s in sheets.EnumerateArray())
                tabs.Add(s.GetProperty("properties").GetProperty("title").GetString() ?? "");
        return (title, tabs);
    }

    /// <summary>Returns all rows of a tab as strings (first row = headers, i.e. the form questions).</summary>
    public async Task<IReadOnlyList<IReadOnlyList<string>>> GetRowsAsync(string spreadsheetId, string tab, CancellationToken ct)
    {
        var range = Uri.EscapeDataString($"'{tab.Replace("'", "''")}'");
        var url = $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/{range}?majorDimension=ROWS&valueRenderOption=FORMATTED_VALUE";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var rows = new List<IReadOnlyList<string>>();
        if (doc.RootElement.TryGetProperty("values", out var values))
            foreach (var row in values.EnumerateArray())
                rows.Add(row.EnumerateArray().Select(c => c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : c.ToString()).ToList());
        return rows;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { throw new GoogleApiException("Google.Error.Network"); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var key = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Google.Error.SessionExpired",
                    HttpStatusCode.Forbidden => "Google.Error.Permission",
                    HttpStatusCode.NotFound => "Google.Error.SpreadsheetNotFound",
                    HttpStatusCode.BadRequest => "Google.Error.BadRequest",
                    HttpStatusCode.TooManyRequests => "Google.Error.RateLimited",
                    _ => "Google.Error.Generic"
                };
                throw new GoogleApiException(key, ((int)response.StatusCode).ToString());
            }
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        }
    }
}
