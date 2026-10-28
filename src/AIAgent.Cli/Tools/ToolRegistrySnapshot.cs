using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tools;

internal sealed class ToolRegistrySnapshot(IReadOnlyList<LlmToolDefinition> definitions, McpOperationStamp? mcpStamp)
{
    internal IReadOnlyList<LlmToolDefinition> Definitions { get; } = Array.AsReadOnly(definitions.ToArray());
    internal McpOperationStamp? McpStamp { get; } = mcpStamp;
    public override string ToString() => "[ToolRegistrySnapshot]";
}