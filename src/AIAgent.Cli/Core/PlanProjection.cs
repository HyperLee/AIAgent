using System.Globalization;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal static class PlanProjection
{
    internal const string Prompt = "確認此版本並切換 Execute？[y/N] ";
    private const string ReservedId = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    private static readonly string[] BlockReasons = ["capability_unavailable", "turn_paused", "turn_cancelled", "generation_limit",
        "model_outcome", "operation_failed", "approval_declined", "file_conflict", "write_unconfirmed", "mode_changed", "model_reported",
        "resource_limit", "workspace_unavailable", "local_failure", "process_start_failed", "process_failed", "process_timed_out", "process_output_limit",
        "validation_inconclusive", "process_unconfirmed"];
    private static readonly string UpperReason = BlockReasons.OrderByDescending(r => Encoding.UTF8.GetByteCount(r)).First();
    private static readonly string UpperDisplayReason = BlockReasons.OrderByDescending(r => Encoding.UTF8.GetByteCount(r + "；" + Reason(r))).First();
    internal static string Name<T>(T value) where T : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    internal static byte[] State(AgentMode mode, PlanSnapshot? plan, bool upper = false)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject(); writer.WriteString("mode", upper ? "execute" : Name(mode));
            writer.WritePropertyName("plan");
            if (plan is null) writer.WriteNullValue();
            else
            {
                writer.WriteStartObject(); writer.WriteString("plan_id", upper ? ReservedId : plan.PlanId);
                writer.WriteNumber("revision", upper ? long.MaxValue : plan.Revision);
                writer.WriteString("approval", upper ? "approved" : Name(plan.ApprovalState)); writer.WriteString("objective", plan.Objective);
                writer.WriteStartArray("tasks");
                foreach (var task in plan.Tasks)
                {
                    writer.WriteStartObject(); writer.WriteString("task_id", upper ? ReservedId : task.TaskId);
                    writer.WriteString("kind", Name(task.Kind)); writer.WriteString("title", task.Title); writer.WriteString("acceptance", task.Acceptance);
                    writer.WriteNumber("requirements_version", task.RequirementsVersion);
                    writer.WriteStartArray("requirements");
                    foreach (var requirement in task.Requirements)
                    {
                        writer.WriteStartObject(); writer.WriteString("tool", requirement.ToolName); writer.WriteString("path", requirement.Path);
                        if (requirement.Task is { } processTask) writer.WriteString("task", Name(processTask));
                        if (requirement.StartLine is { } start) { writer.WriteNumber("start_line", start); writer.WriteNumber("end_line", requirement.EndLine!.Value); }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray(); writer.WriteString("state", upper ? "in_progress" : Name(task.State));
                    writer.WriteString("summary", task.Summary); writer.WriteString("block_reason", upper ? UpperReason : task.BlockReason);
                    writer.WriteString("completion_basis", upper ? "model_assessment" : Name(task.CompletionBasis));
                    writer.WriteStartArray("completion_fact_ids");
                    if (upper)
                    {
                        var count = task.Kind == PlanTaskKind.Analysis ? 1 : task.Requirements.Count;
                        for (var i = 0; i < count; i++) writer.WriteStringValue(ReservedId);
                    }
                    else foreach (var id in task.CompletionFactIds) writer.WriteStringValue(id);
                    writer.WriteEndArray(); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    internal static string Display(AgentMode mode, PlanSnapshot? plan, int oldFacts, bool approval = false, bool upper = false)
    {
        var output = new StringBuilder(upper ? "[Execute]\n" : mode == AgentMode.Plan ? "[Plan]\n" : "[Execute]\n");
        if (plan is null) output.Append("目前沒有活動計畫。\n");
        else
        {
            output.Append("計畫 ").Append(upper ? ReservedId : plan.PlanId).Append("／版本 ")
                .Append((upper ? long.MaxValue : plan.Revision).ToString(CultureInfo.InvariantCulture)).Append("／")
                .Append(upper ? "approved" : Name(plan.ApprovalState)).Append('\n');
            output.Append("目標：").Append(Escape(plan.Objective)).Append('\n');
            foreach (var task in plan.Tasks)
            {
                output.Append(upper ? ReservedId : task.TaskId).Append(" [").Append(Name(task.Kind)).Append("] ")
                    .Append(Escape(task.Title)).Append('\n').Append("  完成條件：").Append(Escape(task.Acceptance)).Append('\n');
                output.Append("  條件版本：").Append(task.RequirementsVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
                output.Append("  狀態：").Append(upper ? "in_progress" : Name(task.State)).Append("；完成依據：")
                    .Append(upper ? "model_assessment" : Name(task.CompletionBasis)).Append('\n');
                foreach (var requirement in task.Requirements)
                {
                    output.Append("  必要操作：").Append(requirement.ToolName).Append(" \"").Append(Escape(requirement.Path)).Append('"');
                    if (requirement.Task is { } processTask) output.Append(" 任務 ").Append(Name(processTask));
                    if (requirement.StartLine is { } start) output.Append(" 行 ").Append(start.ToString(CultureInfo.InvariantCulture))
                        .Append('～').Append(requirement.EndLine!.Value.ToString(CultureInfo.InvariantCulture));
                    output.Append('\n');
                }
                if (task.Summary is { } summary) output.Append("  摘要：").Append(Escape(summary)).Append('\n');
                if (upper || task.BlockReason is not null) output.Append("  受阻原因：")
                    .Append(upper ? UpperDisplayReason + "；" + Reason(UpperDisplayReason) : task.BlockReason + "；" + Reason(task.BlockReason!)).Append('\n');
                output.Append("  完成事實：");
                if (upper) output.AppendJoin(", ", Enumerable.Repeat(ReservedId, task.Kind == PlanTaskKind.Analysis ? 1 : task.Requirements.Count));
                else output.AppendJoin(", ", task.CompletionFactIds);
                output.Append('\n');
            }
            output.Append("保留的舊版本／退役事實：").Append((upper ? PlanLimits.MaxFacts : oldFacts).ToString(CultureInfo.InvariantCulture)).Append('\n');
            output.Append("完成依據：local_operation＝完成：本機操作已核對；model_assessment＝完成：模型分析判斷；none＝尚未完成。\n");
            output.Append("capability_unavailable＝受阻：驗證能力尚未提供。操作證據只代表當次結果，不代表目前磁碟或功能已驗證。\n");
        }
        if (approval) output.Append("確認計畫只確認方向；寫入與程序操作仍須逐次核准\n").Append(Prompt);
        return output.ToString();
    }

    internal static string Reason(string reason) => reason switch
    {
        "process_start_failed" => "程序未能啟動",
        "process_failed" => "程序未成功",
        "process_timed_out" => "程序已逾時",
        "process_output_limit" => "程序超出資源上限",
        "validation_inconclusive" => "缺少可確認的測試證據",
        "process_unconfirmed" => "程序收尾未確認；需要使用者人工處理",
        "turn_paused" => "等待下一則指示；任務尚未完成",
        "turn_cancelled" => "回合已取消",
        "generation_limit" => "已達生成上限",
        "model_outcome" => "模型回合未完成",
        "operation_failed" => "本機操作未成功",
        "approval_declined" => "使用者拒絕操作",
        "file_conflict" => "檔案或路徑已改變",
        "write_unconfirmed" => "寫入結果未確認",
        "mode_changed" => "已切換 Plan 模式",
        "capability_unavailable" => "驗證能力尚未提供",
        "model_reported" => "模型回報受阻",
        "resource_limit" => "資源或生成額度不足；請檢視 /context、/budget 或 /tasks。任務／證據容量不足時需 /clear 後重新規劃。",
        "workspace_unavailable" => "工作區無法使用",
        _ => "本機流程未完成"
    };

    private static string Escape(string value) => FileChangePreviewBuilder.EscapePath(value);
    internal static bool SafeJson(byte[] bytes, ISensitiveDataPolicy policy, ContentBoundary boundary)
    {
        using var document = JsonDocument.Parse(bytes);
        return policy.CanSendOrUse(Encoding.UTF8.GetString(bytes), boundary) && !SensitiveJson.Contains(document.RootElement, policy, boundary);
    }
    internal static bool SafeDisplay(string block, ISensitiveDataPolicy policy) =>
        new UTF8Encoding(false, true).GetByteCount(block) <= PlanLimits.MaxDisplayBytes && policy.CanSendOrUse(block, ContentBoundary.WriteCandidate);

    internal static byte[] ResultBytes(PlanMutationResult result, bool submit)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject(); writer.WriteString("status", result.Success ? "success" : "rejected"); writer.WriteString("error_code", result.ErrorCode);
            if (result.Success)
            {
                var plan = result.Snapshot!;
                writer.WriteStartObject("data"); writer.WriteString("plan_id", plan.PlanId); writer.WriteNumber("revision", plan.Revision);
                if (submit)
                {
                    writer.WriteString("approval", Name(plan.ApprovalState)); writer.WriteStartArray("tasks");
                    foreach (var task in plan.Tasks)
                    { writer.WriteStartObject(); writer.WriteString("task_id", task.TaskId); writer.WriteString("state", Name(task.State)); writer.WriteEndObject(); }
                    writer.WriteEndArray();
                }
                else
                {
                    var task = result.Task!;
                    writer.WriteString("task_id", task.TaskId); writer.WriteString("state", Name(task.State)); writer.WriteString("completion_basis", Name(task.CompletionBasis));
                    writer.WriteStartArray("completion_fact_ids"); foreach (var id in task.CompletionFactIds) writer.WriteStringValue(id);
                    writer.WriteEndArray(); writer.WriteString("block_reason", task.BlockReason);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    internal static ToolExecutionResult Result(PlanMutationResult result, bool submit, ISensitiveDataPolicy policy)
    {
        if (result.ErrorCode == "turn_cancelled") return ToolResponseWriter.Create(ToolStatus.Cancelled, "none", policy);
        if (result.ErrorCode == "unavailable") return ToolResponseWriter.Create(ToolStatus.Failed, "unavailable", policy);
        var bytes = ResultBytes(result, submit);
        using var document = JsonDocument.Parse(bytes);
        return new(result.Success ? ToolStatus.Success : ToolStatus.Rejected, result.ErrorCode,
            bytes.Length <= PlanLimits.MaxDisplayBytes && SafeJson(bytes, policy, ContentBoundary.ToolResult) ? document.RootElement : null);
    }
}