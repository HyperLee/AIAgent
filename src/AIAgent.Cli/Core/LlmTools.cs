using System.Text.Json;

namespace AIAgent.Cli.Core;

public sealed class LlmToolDefinition
{
    public string Name { get; }
    public string Description { get; }
    public JsonElement Parameters { get; }
    public LlmToolDefinition(string name, string description, JsonElement parameters)
    { Name = name; Description = description; Parameters = parameters.ValueKind == JsonValueKind.Undefined ? default : parameters.Clone(); }
    public override string ToString() => "[LlmToolDefinition]";
}

public sealed class LlmToolCall
{
    public int PartIndex { get; }
    public string? CallId { get; }
    public string Name { get; }
    public JsonElement Arguments { get; }
    public LlmToolCall(int partIndex, string? callId, string name, JsonElement arguments)
    { PartIndex = partIndex; CallId = callId; Name = name; Arguments = arguments.ValueKind == JsonValueKind.Undefined ? default : arguments.Clone(); }
    public override string ToString() => "[LlmToolCall]";
}

public sealed class LlmToolResult
{
    public int PartIndex { get; }
    public string? CallId { get; }
    public string Name { get; }
    public JsonElement Response { get; }
    public LlmToolResult(int partIndex, string? callId, string name, JsonElement response)
    { PartIndex = partIndex; CallId = callId; Name = name; Response = response.ValueKind == JsonValueKind.Undefined ? default : response.Clone(); }
    public override string ToString() => "[LlmToolResult]";
}