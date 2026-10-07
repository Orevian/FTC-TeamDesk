using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.AI.Tools;

public sealed record ActionField(string LabelKey, string Value);

/// <summary>
/// A change the AI proposed. It is NOT applied until the user confirms in the ACTION REQUEST dialog.
/// </summary>
public sealed class PendingAction
{
    public PendingAction(string toolName, AiPermissionKey permission, string titleKey, IReadOnlyList<ActionField> fields,
        Func<CancellationToken, Task> apply, string logDescription, decimal? budgetImpact = null, string? currency = null, bool isDestructive = false)
    {
        ToolName = toolName; Permission = permission; TitleKey = titleKey; Fields = fields; _apply = apply;
        LogDescription = logDescription; BudgetImpact = budgetImpact; Currency = currency; IsDestructive = isDestructive;
    }

    private readonly Func<CancellationToken, Task> _apply;
    public Guid Id { get; } = Guid.NewGuid();
    public string ToolName { get; }
    public AiPermissionKey Permission { get; }
    public string TitleKey { get; }
    public IReadOnlyList<ActionField> Fields { get; }
    public decimal? BudgetImpact { get; }
    public string? Currency { get; }
    public bool IsDestructive { get; }
    public string LogDescription { get; }

    public Task ApplyAsync(CancellationToken ct) => _apply(ct);
}

public sealed record AiToolOutcome(string ResultJson, PendingAction? Pending = null);

public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    JsonObject Schema { get; }
    AiPermissionKey Permission { get; }
    bool IsWrite { get; }
    Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct);
}

internal static class AiJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
    public static AiToolOutcome Ok(object value) => new(Serialize(value));
    public static AiToolOutcome Error(string code, string message, object? extra = null) => new(Serialize(new { error = code, message, extra }));
    public static string Trunc(string? s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "...";
    public static string Money(decimal amount, string currency) => amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;
}

internal static class Schema
{
    public static JsonObject Object(IEnumerable<KeyValuePair<string, JsonObject>>? props = null, params string[] required)
    {
        var p = new JsonObject();
        if (props is not null) foreach (var kv in props) p[kv.Key] = kv.Value;
        var obj = new JsonObject { ["type"] = "object", ["properties"] = p };
        if (required.Length > 0) obj["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray());
        return obj;
    }

    public static KeyValuePair<string, JsonObject> Str(string name, string description, params string[] enumValues)
    {
        var o = new JsonObject { ["type"] = "string", ["description"] = description };
        if (enumValues.Length > 0) o["enum"] = new JsonArray(enumValues.Select(e => (JsonNode)e).ToArray());
        return new(name, o);
    }

    public static KeyValuePair<string, JsonObject> Num(string name, string description) => new(name, new JsonObject { ["type"] = "number", ["description"] = description });
    public static KeyValuePair<string, JsonObject> Int(string name, string description) => new(name, new JsonObject { ["type"] = "integer", ["description"] = description });

    public static KeyValuePair<string, JsonObject> StrArray(string name, string description) =>
        new(name, new JsonObject { ["type"] = "array", ["description"] = description, ["items"] = new JsonObject { ["type"] = "string" } });
}

internal static class ArgExtensions
{
    public static string? Str(this JsonObject o, string name)
    {
        if (!o.TryGetPropertyValue(name, out var n) || n is null) return null;
        var s = n is JsonValue v && v.TryGetValue<string>(out var str) ? str : n.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public static decimal? Dec(this JsonObject o, string name)
    {
        var s = o.Str(name);
        return s is not null && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static int? Int(this JsonObject o, string name)
    {
        var s = o.Str(name);
        return s is not null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }

    public static Guid? Guid(this JsonObject o, string name) => System.Guid.TryParse(o.Str(name), out var g) ? g : null;

    public static DateTime? Date(this JsonObject o, string name)
        => DateTime.TryParse(o.Str(name), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static List<string> StrList(this JsonObject o, string name)
    {
        if (!o.TryGetPropertyValue(name, out var n) || n is not JsonArray arr) return new();
        return arr.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
    }

    public static T? Enum<T>(this JsonObject o, string name) where T : struct, Enum
        => System.Enum.TryParse<T>(o.Str(name), ignoreCase: true, out var e) ? e : null;
}
