namespace AIAgent.Cli.Core;

public interface ILlmClient : IAsyncDisposable
{
    Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken cancellationToken);
}

public enum LlmStatus { Completed, ToolCalls, Empty, Refused, Truncated, Cancelled, Failed }
public enum LlmErrorCode
{
    None, InvalidRequest, InputTooLarge, SensitiveInput, SensitiveOutput,
    AuthenticationFailed, ModelUnavailable, PermissionDenied, QuotaOrRateLimited,
    NetworkFailure, Timeout, ServiceUnavailable, InvalidResponse, UnexpectedToolCall, ClientUnavailable
}

public sealed record LlmUsage(long? PromptTokens, long? OutputTokens, long? ThoughtTokens, long? CachedTokens, long? TotalTokens);

public sealed class LlmRequest
{
    public string SystemInstruction { get; }
    public IReadOnlyList<LlmMessage> Messages { get; }
    public IReadOnlyList<LlmToolDefinition> Tools { get; }

    public LlmRequest(string systemInstruction, IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools)
    {
        SystemInstruction = systemInstruction;
        Messages = Array.AsReadOnly(messages?.ToArray() ?? []);
        Tools = Array.AsReadOnly(tools?.ToArray() ?? []);
    }
    public override string ToString() => "[LlmRequest]";
}

public sealed class LlmResult
{
    public LlmStatus Status { get; }
    public LlmErrorCode ErrorCode { get; }
    public string SafeText { get; }
    public IReadOnlyList<LlmToolCall> ToolCalls { get; }
    [System.Text.Json.Serialization.JsonIgnore] public LlmMessage? ModelMessage { get; }
    public LlmUsage? Usage { get; }
    public bool RequestDispatched { get; }
    public bool RequiresContextReset { get; }

    public LlmResult(LlmStatus status, LlmErrorCode errorCode, string safeText,
        IReadOnlyList<LlmToolCall> toolCalls, LlmMessage? modelMessage, LlmUsage? usage,
        bool requestDispatched, bool requiresContextReset)
    {
        Status = status; ErrorCode = errorCode; SafeText = safeText;
        ToolCalls = Array.AsReadOnly(toolCalls.ToArray()); ModelMessage = modelMessage;
        Usage = usage; RequestDispatched = requestDispatched; RequiresContextReset = requiresContextReset;
    }
    public override string ToString() => "[LlmResult]";
}