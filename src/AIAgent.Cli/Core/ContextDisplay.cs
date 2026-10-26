using System.Globalization;
using System.Text;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal static class ContextDisplay
{
    internal static string Context(ContextCoordinator coordinator, bool show)
    {
        var snapshot = coordinator.Capture().Context;
        var output = new StringBuilder("[Context]\n原始完整回合：" + N(snapshot.CompletedTurns.Count) + "；");
        output.Append(snapshot.Summary is { } summary ? (summary.SourceWorkId is null ? "model_summary" : "saved_work_summary（未驗證歷史）") + " 版本 1；本程序涵蓋 " + N(summary.CoveredTurnCount) + " 回合；至 " + N(summary.ThroughTurnId) : "摘要：無");
        output.Append("\n備註：").Append(N(snapshot.Pins.Count)).Append("；pins JSON bytes：").Append(N(ContextDocuments.Pins(snapshot.Pins).Length));
        if (coordinator.LastMeasurement is { } measure)
            output.Append("\n最近本機容量估算（").Append(Purpose(measure.Purpose)).Append("）：")
                .Append(measure.Measure.EstimatedJsonBytes is { } bytes ? N(bytes) + " JSON bytes" : "不可得")
                .Append("；Context版本 ").Append(N(measure.Stamp.ContextRevision))
                .Append(coordinator.IsCurrent(measure.Stamp) ? "；目前版本" : "；過時");
        else output.Append("\n最近本機容量估算：不可得");
        if (coordinator.LastBodyMeasurement is { } body)
            output.Append("\n最近實際 SDK body（").Append(Purpose(body.Purpose)).Append("）：").Append(N(body.Bytes)).Append(" bytes；")
                .Append(body.RequestDispatched ? "已嘗試傳輸" : "未傳輸").Append(coordinator.IsCurrent(body.Stamp) ? "；目前版本" : "；過時");
        else output.Append("\n最近實際 SDK body：不可得");
        output.Append("\n估算不是實際 wire bytes、token 或帳單；模型 token 上限／剩餘容量：不可得。\n");
        output.Append("模型摘要可能遺失未釘選資訊；必要自由文字請 pin。備註原文保留；本機任務與完成依據請用 /tasks。\n");
        if (show)
        {
            if (snapshot.Summary is { } value)
            {
                void Items(string label, IReadOnlyList<string> items)
                { output.Append(label).Append("：\n"); foreach (var item in items) output.Append("  ").Append(FileChangePreviewBuilder.EscapePath(item)).Append('\n'); }
                Items("goals", value.Goals); Items("decisions", value.Decisions); Items("open_questions", value.OpenQuestions); Items("background", value.Background);
            }
            foreach (var pin in snapshot.Pins) output.Append(pin.PinId).Append("：").Append(FileChangePreviewBuilder.EscapePath(pin.Text)).Append('\n');
        }
        return output.ToString();
    }
    internal static string Budget(GenerationBudget budget, LastTurnOutcome? turn, LastMaintenanceOutcome? maintenance)
    {
        var s = budget.Snapshot();
        return "[Budget]\n回合上限：" + N(s.TurnLimit) + "；程序上限：" + N(s.ProgramLimit) + "；程序已消耗：" + N(s.ProgramGenerationsStarted) +
            "；程序剩餘：" + N(s.ProgramRemaining) + "\n最近一般回合生成：" + (turn is null ? "不可得" : N(turn.GenerationsStarted)) +
            "；傳輸嘗試：" + (turn is null ? "不可得" : N(turn.TurnUsage.DispatchedRequests)) +
            "\n最近維護生成：" + (maintenance is null ? "不可得" : N(maintenance.GenerationsStarted)) +
            "；傳輸嘗試：" + (maintenance is null ? "不可得" : N(maintenance.Usage.DispatchedRequests)) +
            "\n生成次數不是 token 或正式費用；reset 恢復上限，clear／換 Key 均不清除程序消耗。\n";
    }
    internal static bool Safe(string text, ISensitiveDataPolicy policy) =>
        new UTF8Encoding(false, true).GetByteCount(text) <= ContextDocuments.MaxDisplayBytes && policy.CanSendOrUse(text, ContentBoundary.WriteCandidate);
    internal static string Purpose(GenerationPurpose purpose) => purpose switch { GenerationPurpose.Compaction => "摘要", GenerationPurpose.WorkSaveSummary => "保存摘要", _ => "一般" };
    private static string N(long number) => number.ToString(CultureInfo.InvariantCulture);
}
internal sealed class LastMaintenanceOutcome(AgentTurnResult result)
{
    internal AgentTurnStatus Status { get; } = result.Status;
    internal AgentStopReason StopReason { get; } = result.StopReason;
    internal LlmStatus? ModelStatus { get; } = result.ModelStatus;
    internal LlmErrorCode? ModelErrorCode { get; } = result.ModelErrorCode;
    internal int GenerationsStarted { get; } = result.GenerationsStarted;
    internal UsageSnapshot Usage { get; } = result.TurnUsage;
    public override string ToString() => "[LastMaintenanceOutcome]";
}