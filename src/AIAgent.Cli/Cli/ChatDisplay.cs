using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Cli;

internal static class ChatDisplay
{
    internal static void Write(ITerminal terminal, ISensitiveDataPolicy policy, string block)
    {
        var safe = new StringBuilder();
        for (var i = 0; i < block.Length; i++)
        {
            var c = block[i];
            if (c == '\r' && i + 1 < block.Length && block[i + 1] == '\n') { safe.Append('\n'); i++; }
            else if (char.IsControl(c) && c is not ('\n' or '\t')) safe.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            else safe.Append(c);
        }
        terminal.Write(policy.RedactForDisplay(safe.ToString()));
    }

    internal static string Format(ChatEvent message) => message.Kind switch
    {
        ChatEventKind.UserMessage => "使用者：" + message.Text + "\n",
        ChatEventKind.Answer => (message.Status == LlmStatus.Truncated ? "回答未完整產生\n" : "") + "AI：" + message.Text + "\n",
        ChatEventKind.Generating => (message.Text ?? (message.Purpose == GenerationPurpose.Compaction ? "[摘要維護] 正在整理對話；Ctrl+C 取消本回合，Ctrl+D 結束。" : "[模型生成] 處理中；Ctrl+C 取消本回合，Ctrl+D 結束。")) + "\n",
        ChatEventKind.AgentProgress => Operation(message.Agent!) + "\n",
        ChatEventKind.Usage => Usage(message),
        ChatEventKind.TurnEnded => TurnText(message) + "\n",
        _ => (message.Text ?? "") + "\n"
    };

    private static string Usage(ChatEvent message)
    {
        if (message.TurnSummary is { } turn)
            return (message.Purpose == GenerationPurpose.Compaction ? "[摘要維護] " : "") + "本次用量：" + Fields(turn) + "\n程序累計：" + Fields(message.SessionUsage!) +
                "\n[回合] 生成 " + Number(message.GenerationsStarted) + " 次；傳輸嘗試 " + Number(turn.DispatchedRequests) +
                " 次" + (turn.CounterOverflowed ? "／不可完整累計（溢位）" : "") +
                "；程序傳輸嘗試數：" + Number(message.SessionUsage!.DispatchedRequests) +
                (message.SessionUsage.CounterOverflowed ? "／不可完整累計（溢位）" : "") + "\n";
        var current = message.RequestDispatched ? message.TurnUsage : null;
        var sum = message.SessionUsage!;
        return "本次用量：" + Join([Single(current?.PromptTokens), Single(current?.OutputTokens), Single(current?.ThoughtTokens), Single(current?.CachedTokens), Single(current?.TotalTokens)]) + "\n" +
            "程序累計：" + Join([Field(sum.Prompt), Field(sum.Output), Field(sum.Thought), Field(sum.Cached), Field(sum.Total)]) + "\n" +
            (message.RequestDispatched ? "本次已嘗試傳輸" : "本次未嘗試傳輸") + "；傳輸嘗試數：" + Number(sum.DispatchedRequests) +
            (sum.CounterOverflowed ? "／不可完整累計（溢位）" : "") + "\n";
    }

    private static string Fields(UsageSnapshot value) => Join([Field(value.Prompt), Field(value.Output), Field(value.Thought), Field(value.Cached), Field(value.Total)]);

    private static string Operation(AgentEvent message)
    {
        var name = message.ToolName is "list_files" or "read_file" or "create_file" or "replace_text" or
            "search_files" or "search_text" or "read_file_range" or "submit_plan" or "update_task" or
            "run_dotnet_task" or "git_status" or "git_diff" or "read_git_file_range" ? message.ToolName : "未知工具";
        if (message.Kind == AgentEventKind.ExecutingTool) return "[工具開始] " + name + "：開始處理";
        var result = message.Operation!;
        if (result.ProcessSummary is { } process)
            return "[本機程序] " + PlanProjection.Name(process.Task) + " " + FileChangePreviewBuilder.EscapePath(process.ProjectPath) +
                "；" + PlanProjection.Name(process.StartState) + "／" + PlanProjection.Name(process.EndState) +
                "；退出碼 " + (process.ExitCode is { } exit ? Number(exit) : "不可得") +
                "；驗證 " + PlanProjection.Name(process.ValidationOutcome) +
                (process.Tests is { } tests ? "；執行 " + Number(tests.Executed) + "，通過 " + Number(tests.Passed) + "，失敗 " + Number(tests.Failed) + "，略過 " + Number(tests.Skipped) : "") +
                (process.StdoutTruncated || process.StderrTruncated ? "；輸出節錄已截斷" : "") +
                (process.CleanupConfirmed ? "；支援群組、已知後代與管線收尾已確認（不保證所有刻意逃離程序）" : "；收尾未確認；需人工處理，不能確認沒有殘留程序");
        if (result.GitSummary is { } git)
            return "[Git 觀察] " + name + "；選取 " + Git.GitLimits.Name(git.Selection) + "；" + result.Status + "／" + result.ErrorCode +
                "；" + git.Observation + "；安全變更 " + Number(git.EligibleChangeCount) + "；此觀察不建立完成證據";
        if (result.WriteState == FileWriteState.Verified)
            return "[本機結果] 已寫入並核對：" + FileChangePreviewBuilder.EscapePath(result.VerifiedPath!) + "，" + Number(result.VerifiedByteCount!.Value) + " bytes" +
                (result.CleanupFailed ? "；內部暫存清理未完成；需人工處理" : "");
        if (result.WriteState == FileWriteState.Unconfirmed)
            return "[本機結果] 寫入結果未確認；停止後續操作" + (result.CleanupFailed ? "；內部暫存清理未完成；需人工處理" : "");
        if (result.CleanupFailed) return "[本機結果] 內部暫存清理未完成；需人工處理";
        if (result.ErrorCode == "approval_declined") return "[本機結果] 未套用此變更";
        if (result.Status != ToolStatus.Success) return "[本機結果] " + name + "：" + result.Status + "／" + result.ErrorCode;
        if (result.DiscoverySummary is { } discovery)
        {
            if (discovery.Kind == DiscoveryKind.LineRange)
                return "[本機結果] 已取得行範圍：" + FileChangePreviewBuilder.EscapePath(result.RelativePath!) + "，" +
                    (discovery.StartLine is { } start ? "第 " + Number(start) + "～" + Number(discovery.EndLine!.Value) + " 行" : "空內容") +
                    "；回傳 " + Number(discovery.ContentByteCount!.Value) + " bytes";
            return "[本機結果] " + (discovery.Kind == DiscoveryKind.FileSearch ? "找到安全檔案：" : "找到文字命中：") +
                Number(discovery.ReturnedCount) + (discovery.Kind == DiscoveryKind.FileSearch ? " 筆" : " 行") +
                (result.Truncated ? "；僅回傳部分結果" : "") + "；已套用搜尋範圍限制";
        }
        if (result.ByteCount is { } bytes) return "[本機結果] 已讀取：" + FileChangePreviewBuilder.EscapePath(result.RelativePath!) + "，" + Number(bytes) + " bytes";
        if (result.ReturnedEntries is null) return "[本機結果] " + name + "：Success；結果無法安全回填";
        return "[本機結果] 已列出安全項目：" + Number(result.ReturnedEntries ?? 0) + " 筆" +
            (result.Truncated ? "；僅回傳部分安全項目" : "") + "；已套用安全過濾";
    }

    private static string TurnText(ChatEvent message) => message.StopReason switch
    {
        AgentStopReason.ReviewInvalid => "review_invalid：審查報告格式或來源位置無法驗證；本次未發布新報告。",
        AgentStopReason.GitStopped => "Git 來源或審查閘門已停止本回合；請檢視受控原因，等待新指示。",
        AgentStopReason.SkillsChanged => "技能來源已變動；請 /skills reload 或 off，重新確認計畫後給予新指示。",
        AgentStopReason.SkillsUnavailable => "技能來源服務不可用；本回合停止。",
        AgentStopReason.SkillsLimit => "技能容量超出上限；本回合停止。",
        AgentStopReason.RulesChanged => "專案規則已變動；停止本回合，請 /rules reload、重新確認計畫後給予新指示。",
        AgentStopReason.RulesUnavailable => "專案規則不可用；請 /rules 檢視或明確重載。",
        AgentStopReason.RulesScopeMismatch => "目標超出目前規則 Scope 或遇到新巢狀規則；請明確選擇 Scope。",
        AgentStopReason.RulesLimit => "專案規則鏈或容量超出上限；本回合停止。",
        AgentStopReason.ProcessFailed => "受控 .NET 任務未成功；本回合停止，下一則明確指示須重新核准。",
        AgentStopReason.ProcessResourceLimit => "已達本回合程序次數或時間上限；本回合停止。",
        AgentStopReason.ProcessUnconfirmed => "程序狀態或收尾未確認；已停止此 session，需使用者人工處理殘留程序。",
        AgentStopReason.TurnCancelled => "已取消本回合。",
        AgentStopReason.ApprovalDeclined => "已拒絕此變更；本回合停止。",
        AgentStopReason.FileConflict => "檔案或路徑已變動；停止本回合，請重新讀檔。",
        AgentStopReason.FileWriteFailed => "檔案寫入未完成；本回合已停止。",
        AgentStopReason.WriteUnconfirmed => "寫入結果未確認；停止後續操作。",
        AgentStopReason.GenerationLimit => "已達本回合生成上限；剩餘工具未執行，請檢視 /budget。",
        AgentStopReason.ProgramGenerationLimit => "程序生成額度不足；請檢視 /budget，剩餘工具未執行。",
        AgentStopReason.ContextCapacity => "Context 容量或檢視不可用；本次需求尚未執行完成，請檢視 /context。",
        AgentStopReason.ContextCompactionFailed => "對話整理未完成或來源已變更；本次需求尚未執行，等待下一則指示。",
        AgentStopReason.ModeRestricted => "目前模式禁止此工具；本回合停止。",
        AgentStopReason.PlanRequired => "此操作需要活動計畫；本回合停止。",
        AgentStopReason.PlanNotApproved => "目前計畫版本尚未確認；本回合停止。",
        AgentStopReason.TaskBlocked => "任務操作不符合條件或未成功；本回合停止。",
        AgentStopReason.TaskResourceLimit => "計畫或證據容量不足；請 /clear 後重新規劃。",
        AgentStopReason.SensitiveData => "工具流程含敏感資訊或無法安全回填；本回合已停止。",
        AgentStopReason.InvalidProtocol => "模型或工具配對無法驗證；本回合已停止。",
        AgentStopReason.WorkspaceUnavailable => "工作區已無法使用；本回合已停止。",
        AgentStopReason.RunnerUnavailable or AgentStopReason.LocalFailure => "本機工具流程無法完成；已安全停止。",
        _ => ResultText(message.Status, message.ErrorCode)
    };

    private static string Join(string[] values) => string.Join("；", new[] { "輸入", "輸出", "思考", "快取", "總計" }.Zip(values, (label, value) => label + " " + value));
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Single(long? value) => value is >= 0 ? Number(value.Value) : "不可得";
    private static string Field(UsageFieldSnapshot value)
    {
        var text = value.HasKnownValue ? (value.MissingRequestCount > 0 || value.Overflowed ? "已知小計 " : "") + Number(value.KnownTotal) : "不可得";
        if (value.MissingRequestCount > 0) text += value.HasKnownValue ? "／不完整（" + Number(value.MissingRequestCount) + " 次缺值）" : "（" + Number(value.MissingRequestCount) + " 次缺值）";
        if (value.Overflowed) text += "／不可完整累計（溢位）";
        return text;
    }

    private static string ResultText(LlmStatus? status, LlmErrorCode? error) => error switch
    {
        LlmErrorCode.AuthenticationFailed => "Gemini 認證失敗；請重新輸入 Key 或退出。",
        LlmErrorCode.ModelUnavailable => "模型或 API 路徑不可用；請檢查模型設定與授權。",
        LlmErrorCode.PermissionDenied => "Gemini 存取遭拒；請檢查服務與模型權限。",
        LlmErrorCode.QuotaOrRateLimited => "Gemini 配額不足或請求受限；本次未自動重試。",
        LlmErrorCode.NetworkFailure => "無法連線至 Gemini；請檢查網路。",
        LlmErrorCode.Timeout => "Gemini 請求逾時；本次未自動重試。",
        LlmErrorCode.ServiceUnavailable => "Gemini 服務暫時無法完成請求；本次未自動重試。",
        LlmErrorCode.InvalidRequest => "Gemini 請求不被接受；請檢查模型設定或縮小輸入。",
        LlmErrorCode.InputTooLarge => "模型輸入超出本機上限；請縮小內容或輸入 /clear。",
        LlmErrorCode.SensitiveInput => "模型輸入含當前 Key；已阻擋本次傳送。",
        LlmErrorCode.SensitiveOutput => "模型回應含敏感資訊；已阻擋後續使用。",
        LlmErrorCode.UnexpectedToolCall => "模型回傳未授權的工具資料；本次未執行操作。",
        LlmErrorCode.ClientUnavailable => "模型用戶端已結束；未送出請求。",
        LlmErrorCode.InvalidResponse => "Gemini 回應無法處理；本次流程已停止。",
        _ => status switch
        {
            LlmStatus.Cancelled => "已取消本次請求。",
            LlmStatus.Empty => "模型未提供可見回答。",
            LlmStatus.Refused => "模型拒絕本次請求。",
            _ => "本次流程已停止。"
        }
    };
}