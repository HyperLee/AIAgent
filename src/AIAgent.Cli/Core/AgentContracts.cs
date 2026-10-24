using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal enum AgentTurnStatus { Completed, Cancelled, Stopped, Failed }
internal enum AgentStopReason
{
    None, ModelOutcome, TurnCancelled, GenerationLimit, SensitiveData, InvalidProtocol,
    WorkspaceUnavailable, RunnerUnavailable, LocalFailure, ApprovalDeclined, FileConflict, FileWriteFailed, WriteUnconfirmed,
    ModeRestricted, PlanRequired, PlanNotApproved, TaskBlocked, TaskResourceLimit, ProcessFailed, ProcessUnconfirmed, ProcessResourceLimit
}
internal enum AgentEventKind { Generating, ExecutingTool, ToolCompleted }

internal sealed class LocalToolOutcome(int generationNumber, int partIndex, string? toolName, ToolExecutionResult result)
{
    internal int GenerationNumber { get; } = generationNumber;
    internal int PartIndex { get; } = partIndex;
    internal string? ToolName { get; } = toolName;
    internal ToolStatus Status { get; } = result.Status;
    internal string ErrorCode { get; } = result.ErrorCode;
    internal string? RelativePath { get; } = result.RelativePath;
    internal long? ByteCount { get; } = result.ByteCount;
    internal int? ReturnedEntries { get; } = result.ReturnedEntries;
    internal bool Truncated { get; } = result.Truncated;
    internal WorkspaceDiscoverySummary? DiscoverySummary { get; } = result.DiscoverySummary;
    internal FileWriteState? WriteState { get; } = result.WriteState;
    internal string? VerifiedPath { get; } = result.VerifiedPath;
    internal long? VerifiedByteCount { get; } = result.VerifiedByteCount;
    internal bool? VerifiedHasUtf8Bom { get; } = result.VerifiedHasUtf8Bom;
    internal bool CleanupFailed { get; } = result.CleanupFailed;
    internal bool ProcessCleanupFailed { get; } = result.ProcessCleanupFailed;
    internal ProcessExecutionSummary? ProcessSummary { get; } = result.ProcessSummary;
    public override string ToString() => "[LocalToolOutcome]";
}

internal sealed class AgentEvent(AgentEventKind kind, int generationNumber, int? partIndex = null,
    string? toolName = null, LocalToolOutcome? operation = null)
{
    internal AgentEventKind Kind { get; } = kind;
    internal int GenerationNumber { get; } = generationNumber;
    internal int? PartIndex { get; } = partIndex;
    internal string? ToolName { get; } = toolName;
    internal LocalToolOutcome? Operation { get; } = operation;
    public override string ToString() => "[AgentEvent]";
}

internal sealed class AgentTurnResult(AgentTurnStatus status, AgentStopReason stopReason,
    LlmStatus? modelStatus, LlmErrorCode? modelErrorCode, string safeText,
    IReadOnlyList<LlmMessage> completedTurn, IReadOnlyList<LocalToolOutcome> operations,
    int generationsStarted, UsageSnapshot turnUsage, bool requiresContextReset)
{
    internal AgentTurnStatus Status { get; } = status;
    internal AgentStopReason StopReason { get; } = stopReason;
    internal LlmStatus? ModelStatus { get; } = modelStatus;
    internal LlmErrorCode? ModelErrorCode { get; } = modelErrorCode;
    internal string SafeText { get; } = safeText;
    internal IReadOnlyList<LlmMessage> CompletedTurn { get; } = Array.AsReadOnly(completedTurn.ToArray());
    internal IReadOnlyList<LocalToolOutcome> Operations { get; } = Array.AsReadOnly(operations.ToArray());
    internal int GenerationsStarted { get; } = generationsStarted;
    internal UsageSnapshot TurnUsage { get; } = turnUsage;
    internal bool RequiresContextReset { get; } = requiresContextReset;
    public override string ToString() => "[AgentTurnResult]";
}