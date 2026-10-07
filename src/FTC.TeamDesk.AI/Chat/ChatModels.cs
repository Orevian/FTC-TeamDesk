using System.Text.Json.Nodes;

namespace FTC.TeamDesk.AI.Chat;

public enum ChatRole { System, User, Assistant, Tool }

public sealed record ToolCall(string Id, string Name, JsonObject Arguments);

public sealed class ChatMessage
{
    public ChatRole Role { get; init; }
    public string? Content { get; init; }
    /// <summary>Set on assistant messages that request tool execution.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    /// <summary>Set on tool-result messages.</summary>
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }

    public static ChatMessage System(string text) => new() { Role = ChatRole.System, Content = text };
    public static ChatMessage User(string text) => new() { Role = ChatRole.User, Content = text };
    public static ChatMessage Assistant(string? text, IReadOnlyList<ToolCall>? calls = null) => new() { Role = ChatRole.Assistant, Content = text, ToolCalls = calls };
    public static ChatMessage ToolResult(string callId, string toolName, string json) => new() { Role = ChatRole.Tool, ToolCallId = callId, ToolName = toolName, Content = json };
}

public sealed record ToolDefinition(string Name, string Description, JsonObject ParametersSchema);

public sealed record ChatRequest(
    IReadOnlyList<ChatMessage> Messages, IReadOnlyList<ToolDefinition> Tools, string Model, double Temperature, int MaxTokens);

public sealed record ChatCompletion(string? Text, IReadOnlyList<ToolCall> ToolCalls);

public interface IAiChatClient
{
    Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct);
}

/// <summary>Provider failure with a localization key. Detail is sanitized and never contains credentials.</summary>
public sealed class AiProviderException : Exception, FTC.TeamDesk.Core.Common.ILocalizedError
{
    public AiProviderException(string errorKey, string? detail = null) : base(detail ?? errorKey) { ErrorKey = errorKey; }
    public string ErrorKey { get; }
}
