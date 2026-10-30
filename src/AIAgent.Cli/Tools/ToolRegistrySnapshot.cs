using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tools;

internal sealed class ToolRegistrySnapshot(IReadOnlyList<LlmToolDefinition> definitions, McpOperationStamp? mcpStamp, long delegationRevision = 0)
{
    internal IReadOnlyList<LlmToolDefinition> Definitions { get; } = Array.AsReadOnly(definitions.ToArray());
    internal long DelegationRevision { get; } = delegationRevision;
    internal McpOperationStamp? McpStamp { get; } = mcpStamp;
    public override string ToString() => "[ToolRegistrySnapshot]";
}