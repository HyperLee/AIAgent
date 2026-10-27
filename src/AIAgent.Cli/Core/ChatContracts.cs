using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal interface IChatInteraction
{
    ValueTask<ChatInputResult> ReadInputAsync(CancellationToken cancellationToken);
    ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken cancellationToken);
    ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken);
    IDisposable ObserveBusyInput(Action<ChatControlSignal> onSignal);
    void SetPolicy(ISensitiveDataPolicy policy);
    void RememberAcceptedInput(string text);
    void ClearInputState();
    void Show(ChatEvent message);
    ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken cancellationToken) => new(FileApprovalDecision.Unavailable);
    ValueTask<ProcessApprovalDecision> ConfirmProcessAsync(ProcessApprovalPreview preview, CancellationToken cancellationToken) => new(ProcessApprovalDecision.Unavailable);
    ValueTask<PlanApprovalDecision> ConfirmPlanAsync(PlanApprovalPreview preview, CancellationToken cancellationToken) => new(PlanApprovalDecision.Unavailable);
    ValueTask<FileApprovalDecision> ConfirmCheckpointAsync(CheckpointApprovalPreview preview, CancellationToken cancellationToken) => new(FileApprovalDecision.Unavailable);
    ValueTask<WorkApprovalDecision> ConfirmWorkAsync(WorkApprovalPreview preview, CancellationToken cancellationToken) => new(WorkApprovalDecision.Unavailable);
}

internal interface IChatRuntimeFactory
{
    BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput);
    ILlmClient CreateClient(BootstrapSession session);
    ILlmContextInspector? CreateContextInspector(ILlmClient client) => null;
}

internal enum ChatState { AwaitingInput, LoadingRules, LoadingSkills, CapturingReview, Generating, ExecutingTools, AwaitingApproval, AwaitingPlanApproval, AwaitingProcessApproval, Working, AwaitingWorkApproval, CheckpointOperation, AwaitingCheckpointApproval, Cancelling, AwaitingKeyChoice, ReadingReplacementKey, Stopping }
internal enum ChatInputStatus { Submitted, Invalid, EndOfInput, Cancelled, Unavailable }
internal enum KeyReplacementChoice { Reenter, Exit, EndOfInput, Cancelled, Unavailable }
internal enum ChatControlSignal { CancelTurn, EndOfInput, Unavailable }
internal enum ChatEventKind { Ready, InputRejected, UserMessage, Generating, AgentProgress, Answer, TurnEnded, ContextCleared, Usage, Stopping, TaskSummary }

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
    LlmErrorCode? errorCode = null, LlmUsage? turnUsage = null, UsageSnapshot? sessionUsage = null, bool requestDispatched = false,
    AgentEvent? agentEvent = null, AgentTurnStatus? turnStatus = null, AgentStopReason? stopReason = null,
    int generationsStarted = 0, UsageSnapshot? turnSummary = null, PlanSnapshot? planSummary = null,
    GenerationPurpose purpose = GenerationPurpose.Conversation)
{
    internal ChatEventKind Kind { get; } = kind;
    internal string? Text { get; } = text;
    internal LlmStatus? Status { get; } = status;
    internal LlmErrorCode? ErrorCode { get; } = errorCode;
    internal LlmUsage? TurnUsage { get; } = turnUsage;
    internal UsageSnapshot? SessionUsage { get; } = sessionUsage;
    internal bool RequestDispatched { get; } = requestDispatched;
    internal AgentEvent? Agent { get; } = agentEvent;
    internal AgentTurnStatus? TurnStatus { get; } = turnStatus;
    internal AgentStopReason? StopReason { get; } = stopReason;
    internal int GenerationsStarted { get; } = generationsStarted;
    internal UsageSnapshot? TurnSummary { get; } = turnSummary;
    internal PlanSnapshot? PlanSummary { get; } = planSummary;
    internal GenerationPurpose Purpose { get; } = purpose;
    public override string ToString() => "[ChatEvent]";
}

internal sealed class LastTurnOutcome(AgentTurnResult result, AgentTurnStatus status, AgentStopReason stopReason)
{
    internal AgentTurnStatus Status { get; } = status;
    internal AgentStopReason StopReason { get; } = stopReason;
    internal LlmStatus? ModelStatus { get; } = result.ModelStatus;
    internal LlmErrorCode? ModelErrorCode { get; } = result.ModelErrorCode;
    internal int GenerationsStarted { get; } = result.GenerationsStarted;
    internal UsageSnapshot TurnUsage { get; } = result.TurnUsage;
    internal bool RequiresContextReset { get; } = result.RequiresContextReset;
    internal IReadOnlyList<LocalToolOutcome> Operations { get; } = Array.AsReadOnly(result.Operations.ToArray());
    public override string ToString() => "[LastTurnOutcome]";
}