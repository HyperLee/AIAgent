using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tools;

internal sealed class DelegateReadonlyTool(DelegationCoordinator coordinator, ISensitiveDataPolicy policy) : ITool
{
    internal const string Name = "delegate_readonly";
    public ToolCapability Capability => ToolCapability.DelegationRead;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition(Name, "分派一批一至兩個同模型唯讀探索或審查子任務；共用額度，整批收尾後彙整",
        """{"type":"object","additionalProperties":false,"required":["tasks"],"properties":{"tasks":{"type":"array","minItems":1,"maxItems":2,"items":{"type":"object","additionalProperties":false,"required":["role","objective","scope_path","acceptance"],"properties":{"role":{"type":"string","enum":["explore","review"]},"objective":{"type":"string"},"scope_path":{"type":"string"},"acceptance":{"type":"string"}}}}}}""");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken token)
    {
        if (SensitiveJson.Contains(arguments, policy, ContentBoundary.ToolArguments)) return DelegationJson.Response(new(ToolStatus.Rejected, "sensitive_arguments", null, reset: true), policy);
        var code = DelegationJson.ParseTasks(arguments, out var tasks);
        if (code != "none") return DelegationJson.Response(new(ToolStatus.Rejected, code, null), policy);
        return DelegationJson.Response(await coordinator.RunBatchAsync(new(coordinator.ParentTurnId, tasks!), token), policy);
    }
    public override string ToString() => "[DelegateReadonlyTool]";
}