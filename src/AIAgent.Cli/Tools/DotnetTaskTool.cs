using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tools;

internal sealed class RunDotnetTaskTool(IControlledDotnetTasks tasks, ISensitiveDataPolicy policy) : ITool
{
    public ToolCapability Capability => ToolCapability.DotnetTask;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("run_dotnet_task",
        "逐次核准的隔離 Debug 建置；SDK 10.0.100 的 MTP test 因私人 IPC 限制不支援；只限明確單一 csproj",
"""
{"type":"object","properties":{"task":{"type":"string","enum":["build","test"]},"project_path":{"type":"string","minLength":1,"maxLength":4096}},"required":["task","project_path"],"additionalProperties":false}
""");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!DotnetTaskArguments.TryParse(arguments, policy, out var task, out var path, out var error))
            return ToolResponseWriter.Create(ToolStatus.Rejected, error, policy).WithProcessStop();
        return (await tasks.RunAsync(task, path, cancellationToken)).ToolResult;
    }
    public override string ToString() => "[RunDotnetTaskTool]";
}

internal static class DotnetTaskArguments
{
    internal static bool TryParse(JsonElement arguments, ISensitiveDataPolicy policy, out DotnetTaskKind task, out string path, out string error)
    {
        task = default; path = ""; error = "invalid_arguments";
        try
        {
            if (SensitiveJson.Contains(arguments, policy, ContentBoundary.ToolArguments) ||
                !policy.CanSendOrUse(arguments.GetRawText(), ContentBoundary.ToolArguments))
            { error = "sensitive_arguments"; return false; }
            if (!ToolArguments.TryStrings(arguments, ["task", "project_path"], out var values)) return false;
            if (values[0] is not ("build" or "test")) return false;
            task = values[0] == "build" ? DotnetTaskKind.Build : DotnetTaskKind.Test;
            return TryPath(values[1], policy, out path, out error);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or JsonException) { return false; }
    }
    internal static bool TryPath(string raw, ISensitiveDataPolicy policy, out string path, out string error)
    {
        path = ""; error = "invalid_path";
        if (!policy.CanSendOrUse(raw, ContentBoundary.ToolArguments)) { error = "sensitive_arguments"; return false; }
        if (!WorkspacePathRules.TryNormalize(raw, out path, out var directory) || directory) return false;
        if (!policy.CanSendOrUse(path, ContentBoundary.ToolArguments)) { error = "sensitive_arguments"; return false; }
        if (WorkspacePathRules.IsSensitive(path)) { error = "sensitive_path"; return false; }
        if (!path.EndsWith(".csproj", StringComparison.Ordinal)) return false;
        error = "none"; return true;
    }
}