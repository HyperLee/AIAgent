using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

internal interface IChatInteraction
{
    ValueTask<ChatInputResult> ReadInputAsync(CancellationToken cancellationToken);
    ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken cancellationToken);
    ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken);
    IDisposable ObserveBusyInput(Action<ChatControlSignal> onSignal);
    void SetPolicy(ISensitiveDataPolicy policy);
    void Show(ChatEvent message);
}

internal interface IChatRuntimeFactory
{
    BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput);
    ILlmClient CreateClient(BootstrapSession session);
}

internal enum ChatState { AwaitingInput, Generating, Cancelling, AwaitingKeyChoice, ReadingReplacementKey, Stopping }
internal enum ChatInputStatus { Submitted, Invalid, EndOfInput, Cancelled, Unavailable }
internal enum KeyReplacementChoice { Reenter, Exit, EndOfInput, Cancelled, Unavailable }
internal enum ChatControlSignal { CancelTurn, EndOfInput, Unavailable }
internal enum ChatEventKind { Ready, InputRejected, UserMessage, Generating, Answer, TurnEnded, ContextCleared, Usage, Stopping }

internal sealed class ChatInputResult
{
    internal ChatInputStatus Status { get; }
    internal string? Text { get; }
    private ChatInputResult(ChatInputStatus status, string? text = null) { Status = status; Text = text; }
    internal static ChatInputResult Submitted(string text) => new(ChatInputStatus.Submitted, text ?? throw new ArgumentNullException(nameof(text)));
    internal static ChatInputResult End(ChatInputStatus status) => status == ChatInputStatus.Submitted || !Enum.IsDefined(status)
        ? throw new ArgumentException("輸入狀態無效") : new(status);
    public override string ToString() => "[ChatInputResult]";
}

internal sealed class ChatEvent(ChatEventKind kind, string? text = null, LlmStatus? status = null,
    LlmErrorCode? errorCode = null, LlmUsage? turnUsage = null, UsageSnapshot? sessionUsage = null, bool requestDispatched = false)
{
    internal ChatEventKind Kind { get; } = kind;
    internal string? Text { get; } = text;
    internal LlmStatus? Status { get; } = status;
    internal LlmErrorCode? ErrorCode { get; } = errorCode;
    internal LlmUsage? TurnUsage { get; } = turnUsage;
    internal UsageSnapshot? SessionUsage { get; } = sessionUsage;
    internal bool RequestDispatched { get; } = requestDispatched;
    public override string ToString() => "[ChatEvent]";
}

internal sealed class LastTurnOutcome(LlmStatus status, LlmErrorCode errorCode, bool requestDispatched, bool contextWasReset)
{
    internal LlmStatus Status { get; } = status;
    internal LlmErrorCode ErrorCode { get; } = errorCode;
    internal bool RequestDispatched { get; } = requestDispatched;
    internal bool ContextWasReset { get; } = contextWasReset;
    public override string ToString() => "[LastTurnOutcome]";
}