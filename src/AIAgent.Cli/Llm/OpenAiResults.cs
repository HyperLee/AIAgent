using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

internal static class OpenAiResults
{
    internal static LlmResult Parse(JsonElement root, bool hasTools, object owner, ISensitiveDataPolicy policy, LlmUsage? usage, CancellationToken token)
    {
        // Inspect every field, including duplicate occurrences, before selecting any native item.
        OpenAiJson.Check(root, policy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveOutput);
        OpenAiJson.Validate(root, token);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) throw OpenAiJson.Invalid();
        var text = new StringBuilder(); var calls = new List<LlmToolCall>(); var items = new List<JsonElement>();
        var ids = new HashSet<string>(StringComparer.Ordinal); var refusal = false; var index = 0;
        var status = OpenAiJson.String(root, "status");
        foreach (var item in output.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            if (status == "completed" && item.ValueKind == JsonValueKind.Object && item.TryGetProperty("status", out _) && OpenAiJson.String(item, "status") != "completed")
                throw OpenAiJson.Invalid();
            switch (OpenAiJson.String(item, "type"))
            {
                case "message":
                    if (OpenAiJson.String(item, "role") != "assistant" || !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) throw OpenAiJson.Invalid();
                    foreach (var part in content.EnumerateArray())
                    {
                        switch (OpenAiJson.String(part, "type"))
                        {
                            case "output_text": text.Append(OpenAiJson.String(part, "text") ?? throw OpenAiJson.Invalid()); break;
                            case "refusal": if (OpenAiJson.String(part, "refusal") is null) throw OpenAiJson.Invalid(); refusal = true; break;
                            default: throw OpenAiJson.Invalid();
                        }
                    }
                    break;
                case "function_call":
                    var id = OpenAiJson.String(item, "call_id"); var name = OpenAiJson.String(item, "name");
                    var arguments = OpenAiJson.String(item, "arguments");
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || !OpenAiProtocol.ValidName(name) || arguments is null) throw OpenAiJson.Invalid();
                    using (var document = OpenAiJson.Parse(Encoding.UTF8.GetBytes(arguments)))
                    {
                        OpenAiJson.Check(document.RootElement, policy, ContentBoundary.ToolArguments, LlmErrorCode.SensitiveOutput);
                        OpenAiJson.Validate(document.RootElement, token);
                        if (document.RootElement.ValueKind != JsonValueKind.Object) throw OpenAiJson.Invalid();
                        calls.Add(new(index, id, name!, document.RootElement.Clone()));
                    }
                    break;
                case "reasoning":
                    if (string.IsNullOrWhiteSpace(OpenAiJson.String(item, "encrypted_content")) ||
                        !item.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Array) throw OpenAiJson.Invalid();
                    break;
                default: throw OpenAiJson.Invalid();
            }
            items.Add(item.Clone()); index++;
        }
        var visible = text.ToString();
        OpenAiJson.Check(visible, policy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveOutput);
        if (refusal && calls.Count > 0) throw OpenAiJson.Invalid();
        if (status == "incomplete")
        {
            var reason = root.TryGetProperty("incomplete_details", out var detail) ? OpenAiJson.String(detail, "reason") : null;
            return reason switch
            {
                "max_output_tokens" => new(LlmStatus.Truncated, LlmErrorCode.None, visible, [], null, usage, true, false),
                "content_filter" => new(LlmStatus.Refused, LlmErrorCode.None, "模型拒絕本次請求。", [], null, usage, true, false),
                _ => throw OpenAiJson.Invalid()
            };
        }
        if (status == "failed") throw new OpenAiBoundaryException(LlmErrorCode.ServiceUnavailable);
        if (status != "completed") throw OpenAiJson.Invalid();
        if (refusal) return new(LlmStatus.Refused, LlmErrorCode.None, "模型拒絕本次請求。", [], null, usage, true, false);
        if (!hasTools && calls.Count > 0) throw new OpenAiBoundaryException(LlmErrorCode.UnexpectedToolCall);
        if (calls.Count == 0 && string.IsNullOrWhiteSpace(visible)) return new(LlmStatus.Empty, LlmErrorCode.None, "模型未提供可見回答。", [], null, usage, true, false);
        var contentOwner = new OpenAiModelContent(items, owner, calls);
        var model = LlmMessage.FromModel(contentOwner, calls);
        return new(calls.Count == 0 ? LlmStatus.Completed : LlmStatus.ToolCalls, LlmErrorCode.None, visible, calls, model, usage, true, false);
    }

    internal static string ErrorText(LlmErrorCode code) => code switch
    {
        LlmErrorCode.AuthenticationFailed => "OpenAI 認證失敗；請重新輸入目前模型的 Key 或退出。",
        LlmErrorCode.PermissionDenied => "OpenAI 存取遭拒；請檢查服務與模型權限。",
        LlmErrorCode.ModelUnavailable => "OpenAI 模型不可用；請檢查模型與授權。",
        LlmErrorCode.QuotaOrRateLimited => "OpenAI 配額不足或請求受限；本次未自動重試。",
        LlmErrorCode.Timeout => "OpenAI 請求逾時；本次未自動重試。",
        LlmErrorCode.ServiceUnavailable => "OpenAI 服務暫時無法完成請求；本次未自動重試。",
        LlmErrorCode.NetworkFailure => "無法連線至 OpenAI；本次未自動重試。",
        LlmErrorCode.InvalidRequest => "模型請求資料無法接受；本次流程已停止。",
        LlmErrorCode.InputTooLarge => "模型輸入超出上限；請縮小內容或清除對話。",
        LlmErrorCode.SensitiveInput => "模型輸入含敏感資訊；已阻擋本次傳送。",
        LlmErrorCode.SensitiveOutput => "模型回應含敏感資訊；已阻擋後續使用，需清除對話。",
        LlmErrorCode.UnexpectedToolCall => "模型回傳無法接受的工具呼叫；本次流程已停止。",
        LlmErrorCode.ClientUnavailable => "模型用戶端已結束；未送出請求。",
        _ => "OpenAI 回應無法處理；本次流程已停止。"
    };
}