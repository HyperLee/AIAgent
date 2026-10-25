using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tools;

internal sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.Ordinal);
    private readonly ISensitiveDataPolicy _policy;
    private readonly Dictionary<string, ToolCapability> _capabilities = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<LlmToolDefinition> _definitions;
    internal AgentExecutionPolicy ExecutionPolicy { get; }
    internal IReadOnlyList<LlmToolDefinition> Definitions => Array.AsReadOnly(_definitions.Where(d => !IsModeRestricted(d.Name)).ToArray());

    internal ToolRegistry(IReadOnlyList<ITool> tools, ISensitiveDataPolicy policy, AgentExecutionPolicy? executionPolicy = null)
    {
        _policy = policy;
        ExecutionPolicy = executionPolicy ?? tools.Select(t => t.ProjectRulesWorkspace?.ExecutionPolicy).FirstOrDefault(p => p is not null) ?? new(new PlanStore(policy));
        var definitions = new List<LlmToolDefinition>();
        foreach (var tool in tools)
        {
            if (tool?.Definition is not { } definition || string.IsNullOrWhiteSpace(definition.Name) ||
                string.IsNullOrWhiteSpace(definition.Description) || definition.Parameters.ValueKind != JsonValueKind.Object ||
                !_tools.TryAdd(definition.Name, tool)) throw new ArgumentException("工具登錄無效");
            definitions.Add(definition);
            _capabilities.Add(definition.Name, tool.Capability);
        }
        _definitions = Array.AsReadOnly(definitions.ToArray());
    }

    internal bool IsModeRestricted(string name) => _capabilities.TryGetValue(name, out var capability) && !ExecutionPolicy.Allows(capability);
    internal bool IsWorkspaceTool(string name) => _capabilities.TryGetValue(name, out var capability) &&
        capability is ToolCapability.WorkspaceRead or ToolCapability.WorkspaceWrite;

    internal bool IsTaskOperation(string name) => _capabilities.TryGetValue(name, out var capability) &&
        capability is ToolCapability.WorkspaceRead or ToolCapability.WorkspaceWrite or ToolCapability.DotnetTask;
    internal bool IsProcessTool(string name) => _capabilities.TryGetValue(name, out var capability) && capability == ToolCapability.DotnetTask;

    internal string? SafeName(string name) => _tools.ContainsKey(name) && _policy.CanSendOrUse(name, ContentBoundary.ToolArguments) ? name : null;

    internal bool IsSensitive(LlmToolCall call) => !_policy.CanSendOrUse(call.Name, ContentBoundary.ToolArguments) ||
        call.CallId is { } id && !_policy.CanSendOrUse(id, ContentBoundary.ToolArguments) ||
        SensitiveJson.Contains(call.Arguments, _policy, ContentBoundary.ToolArguments);

    internal async Task<ToolExecutionResult> DispatchAsync(LlmToolCall call, CancellationToken cancellationToken)
    {
        using var generation = ExecutionPolicy.Plans.BorrowGeneration();
        if (cancellationToken.IsCancellationRequested) return PreserveWriteKind(call, ToolResponseWriter.Create(ToolStatus.Cancelled, "none", _policy));
        if (IsSensitive(call)) return PreserveWriteKind(call, ToolResponseWriter.Create(ToolStatus.Rejected, "sensitive_arguments", _policy));
        if (!_tools.TryGetValue(call.Name, out var tool)) return PreserveWriteKind(call, ToolResponseWriter.Create(ToolStatus.Rejected, "unknown_tool", _policy));
        if (!ExecutionPolicy.Plans.IsExecutionCurrent) return PreserveWriteKind(call, ToolResponseWriter.Create(ToolStatus.Failed, "unavailable", _policy));
        if (IsModeRestricted(call.Name)) return PreserveWriteKind(call, ToolResponseWriter.Create(ToolStatus.Rejected, "mode_restricted", _policy));
        var rulesError = ExecutionPolicy.Rules is { } rules ? await rules.ValidateAsync(cancellationToken) : "rules_unavailable";
        if (rulesError != "none") return PreserveWriteKind(call, ToolResponseWriter.Create(ToolStatus.Rejected, rulesError, _policy));
        var result = await tool.ExecuteAsync(call.Arguments, cancellationToken);
        if (result is null) throw new InvalidOperationException("工具結果不存在");
        if (result.Response is not { } response || SensitiveJson.Contains(response, _policy, ContentBoundary.ToolResult) ||
            result.RelativePath is { } path && !_policy.CanSendOrUse(path, ContentBoundary.ToolResult))
            return result.WithoutResponse();
        return result;
    }
    internal ToolExecutionResult PreserveWriteKind(LlmToolCall call, ToolExecutionResult result) =>
        result.WriteState is null && _tools.ContainsKey(call.Name) && call.Name is "create_file" or "replace_text"
            ? EditToolProjection.NotApplied(result) : result;
    public override string ToString() => "[ToolRegistry]";
}

internal static class ToolArguments
{
    internal static bool TryStrings(JsonElement arguments, string[] names, out string[] values)
    {
        values = new string[names.Length];
        if (arguments.ValueKind != JsonValueKind.Object) return false;
        var seen = new bool[names.Length]; var count = 0;
        foreach (var property in arguments.EnumerateObject())
        {
            var index = Array.FindIndex(names, name => string.Equals(name, property.Name, StringComparison.Ordinal));
            if (index < 0 || seen[index] || property.Value.ValueKind != JsonValueKind.String) return false;
            seen[index] = true; count++; values[index] = property.Value.GetString()!;
        }
        return count == names.Length;
    }
    internal static bool TryPath(JsonElement arguments, out string path)
    {
        path = "";
        if (arguments.ValueKind != JsonValueKind.Object) return false;
        var properties = arguments.EnumerateObject();
        if (!properties.MoveNext()) return false;
        var property = properties.Current;
        if (property.Name != "path" || property.Value.ValueKind != JsonValueKind.String || properties.MoveNext()) return false;
        path = property.Value.GetString()!;
        return true;
    }
}