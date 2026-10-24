using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tools;

internal sealed class SubmitPlanTool(PlanStore plans, ISensitiveDataPolicy policy) : ITool
{
    public ToolCapability Capability => ToolCapability.SubmitPlan;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("submit_plan",
        "提交或修訂目前計畫；版本確認由使用者在本機另行處理",
"""
{
  "type": "object",
  "properties": {
    "expected_revision": { "type": "integer", "minimum": 0 },
    "objective": { "type": "string", "minLength": 1, "maxLength": 2048 },
    "tasks": {
      "type": "array", "minItems": 1, "maxItems": 20,
      "items": {
        "type": "object",
        "properties": {
          "task_id": { "type": "string", "minLength": 1, "maxLength": 32 },
          "kind": { "type": "string", "enum": ["analysis", "read", "write", "validation"] },
          "title": { "type": "string", "minLength": 1, "maxLength": 256 },
          "acceptance": { "type": "string", "minLength": 1, "maxLength": 1024 },
          "requirements_version": { "type": "integer", "enum": [1, 2] },
          "requirements": {
            "type": "array", "maxItems": 8,
            "items": {
              "type": "object",
              "properties": {
                "tool": { "type": "string", "enum": ["list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range", "run_dotnet_task"] },
                "path": { "type": "string", "minLength": 1, "maxLength": 4096 },
                "task": { "type": "string", "enum": ["build", "test"] },
                "start_line": { "type": "integer", "minimum": 1 },
                "end_line": { "type": "integer", "minimum": 1 }
              },
              "required": ["tool", "path"],
              "additionalProperties": false
            }
          }
        },
        "required": ["kind", "title", "acceptance", "requirements"],
        "additionalProperties": false
      }
    }
  },
  "required": ["expected_revision", "objective", "tasks"],
  "additionalProperties": false
}
""");
    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(cancellationToken.IsCancellationRequested
            ? ToolResponseWriter.Create(ToolStatus.Cancelled, "none", policy)
            : PlanProjection.Result(plans.Submit(arguments, cancellationToken), true, policy));
    public override string ToString() => "[SubmitPlanTool]";
}

internal sealed class UpdateTaskTool(PlanStore plans, ISensitiveDataPolicy policy) : ITool
{
    public ToolCapability Capability => ToolCapability.UpdateTask;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("update_task",
        "更新已確認版本的任務；完成依據只取本機證據",
"""
{
  "type": "object",
  "properties": {
    "plan_id": { "type": "string", "minLength": 1, "maxLength": 32 },
    "expected_revision": { "type": "integer", "minimum": 1 },
    "task_id": { "type": "string", "minLength": 1, "maxLength": 32 },
    "status": { "type": "string", "enum": ["in_progress", "completed", "blocked"] },
    "summary": { "type": "string", "minLength": 1, "maxLength": 2048 }
  },
  "required": ["plan_id", "expected_revision", "task_id", "status", "summary"],
  "additionalProperties": false
}
""");
    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(cancellationToken.IsCancellationRequested
            ? ToolResponseWriter.Create(ToolStatus.Cancelled, "none", policy)
            : PlanProjection.Result(plans.Update(arguments, cancellationToken), false, policy));
    public override string ToString() => "[UpdateTaskTool]";
}