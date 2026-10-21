using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

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
        ChatEventKind.Generating => "處理中；Ctrl+C 取消本回合，Ctrl+D 結束。\n",
        ChatEventKind.AgentProgress => Operation(message.Agent!) + "\n",
        ChatEventKind.Usage => Usage(message),
        ChatEventKind.TurnEnded => TurnText(message) + "\n",
        _ => (message.Text ?? "") + "\n"
    };

    private static string Usage(ChatEvent message)
    {
        if (message.TurnSummary is { } turn)
            return "本次用量：" + Fields(turn) + "\n程序累計：" + Fields(message.SessionUsage!) +
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
        var name = message.ToolName ?? "未知工具";
        if (message.Kind == AgentEventKind.ExecutingTool) return "[工具] " + name + "：開始處理";
        var result = message.Operation!;
        if (result.Status != ToolStatus.Success) return "[本機結果] " + name + "：" + result.Status + "／" + result.ErrorCode;
        if (result.ByteCount is { } bytes) return "[本機結果] 已讀取：" + result.RelativePath + "，" + Number(bytes) + " bytes";
        if (result.ReturnedEntries is null) return "[本機結果] " + name + "：Success；結果無法安全回填";
        return "[本機結果] 已列出安全項目：" + Number(result.ReturnedEntries ?? 0) + " 筆" +
            (result.Truncated ? "；僅回傳部分安全項目" : "") + "；已套用安全過濾";
    }

    private static string TurnText(ChatEvent message) => message.StopReason switch
    {
        AgentStopReason.TurnCancelled => "已取消本回合。",
        AgentStopReason.GenerationLimit => "已達每回合五次生成上限；剩餘工具未執行。",
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