using System.Text.Json;

using AIAgent.Cli.Core;

using Google.GenAI.Types;

namespace AIAgent.Cli.Llm;

internal static class GeminiResults
{
    internal static (LlmStatus Status, string Text, IReadOnlyList<LlmToolCall> Calls, Content? Content) Parse(GenerateContentResponse response, bool hasTools, bool invalidParts)
    {
        if (response.PromptFeedback?.BlockReason is { } reason && reason.Value is not ("" or "BLOCK_REASON_UNSPECIFIED" or "BLOCKED_REASON_UNSPECIFIED"))
        {
            if (!BlockedReason.AllValues.Contains(reason)) throw Invalid();
            return (LlmStatus.Refused, "模型拒絕本次請求。", [], null);
        }
        if (response.Candidates is not { Count: > 0 })
        {
            if (invalidParts) throw Invalid();
            return (LlmStatus.Empty, "模型未提供可見回答。", [], null);
        }
        if (response.Candidates.Count != 1) throw Invalid();
        var candidate = response.Candidates[0];
        var finish = candidate.FinishReason?.Value;
        var content = candidate.Content;
        var parts = content?.Parts ?? [];
        var visible = string.Concat(parts.Where(p => p.Thought != true).Select(p => p.Text));
        if (finish is "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII") return (LlmStatus.Refused, "模型拒絕本次請求。", [], null);
        if (finish is "MAX_TOKENS" or "CONTINUATION") return (LlmStatus.Truncated, visible, [], null);
        if (finish is "MALFORMED_FUNCTION_CALL" or "UNEXPECTED_TOOL_CALL" or "TOO_MANY_TOOL_CALLS" || !hasTools && parts.Any(p => p.FunctionCall is not null)) throw new GeminiBoundaryException(LlmErrorCode.UnexpectedToolCall);
        if (finish != "STOP" || invalidParts) throw Invalid();
        var calls = new List<LlmToolCall>();
        for (var index = 0; index < parts.Count; index++)
        {
            if (parts[index].FunctionCall is not { } call) continue;
            if (string.IsNullOrWhiteSpace(call.Name) || call.Args is null) throw Invalid();
            calls.Add(new(index, call.Id, call.Name, JsonSerializer.SerializeToElement(call.Args)));
        }
        if (calls.Count > 0) return (LlmStatus.ToolCalls, visible, calls.AsReadOnly(), content);
        return string.IsNullOrWhiteSpace(visible) ? (LlmStatus.Empty, "模型未提供可見回答。", [], null) : (LlmStatus.Completed, visible, [], content);
    }

    private static GeminiBoundaryException Invalid() => new(LlmErrorCode.InvalidResponse);

    internal static string ErrorText(LlmErrorCode code, bool remote = false) => code switch
    {
        LlmErrorCode.AuthenticationFailed => "Gemini 認證失敗；請重新輸入 Key 或退出。",
        LlmErrorCode.ModelUnavailable => "模型或 API 路徑不可用；請檢查模型設定與授權。",
        LlmErrorCode.PermissionDenied => "Gemini 存取遭拒；請檢查服務與模型權限。",
        LlmErrorCode.QuotaOrRateLimited => "Gemini 配額不足或請求受限；本次未自動重試。",
        LlmErrorCode.InvalidRequest when remote => "Gemini 請求不被接受；請檢查模型設定或縮小輸入。",
        LlmErrorCode.InvalidRequest => "模型請求資料不完整或不合法；未送出請求。",
        LlmErrorCode.Timeout => "Gemini 請求逾時；本次未自動重試。",
        LlmErrorCode.ServiceUnavailable => "Gemini 服務暫時無法完成請求；本次未自動重試。",
        LlmErrorCode.NetworkFailure => "無法連線至 Gemini；請檢查網路。",
        LlmErrorCode.InputTooLarge => "模型輸入超出本機上限；請縮小內容或清除對話。",
        LlmErrorCode.SensitiveInput => "模型輸入含當前 Key；已阻擋本次傳送。",
        LlmErrorCode.SensitiveOutput => "模型回應含敏感資訊；已阻擋後續使用，需清除對話。",
        LlmErrorCode.UnexpectedToolCall => "模型回傳無法接受的工具呼叫；本次流程已停止。",
        LlmErrorCode.ClientUnavailable => "模型用戶端已結束；未送出請求。",
        _ => "Gemini 回應無法處理；本次流程已停止。"
    };
}