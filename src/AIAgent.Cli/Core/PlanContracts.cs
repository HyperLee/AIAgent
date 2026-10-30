using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal enum AgentMode { Execute, Plan }
internal enum PlanApprovalState { Draft, Approved }
internal enum PlanTaskKind { Analysis, Read, Write, Validation }
internal enum PlanTaskState { Pending, InProgress, Completed, Blocked }
internal enum TaskCompletionBasis { None, LocalOperation, ModelAssessment }
internal enum PlanApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }
internal enum ToolCapability { Unspecified, WorkspaceRead, WorkspaceWrite, SubmitPlan, UpdateTask, DotnetTask, GitRead, ExternalRead, DelegationRead }

internal static class PlanLimits
{
    internal const int MaxTasks = 20;
    internal const int MaxRequirements = 8;
    internal const int MaxFacts = 256;
    internal const int MaxProjectionBytes = 32768;
    internal const int MaxDisplayBytes = 65536;
}

internal sealed class TaskRequirement(string toolName, string path, int? startLine = null, int? endLine = null, DotnetTaskKind? task = null)
{
    internal DotnetTaskKind? Task { get; } = task;
    internal string ToolName { get; } = toolName;
    internal string Path { get; } = path;
    internal int? StartLine { get; } = startLine;
    internal int? EndLine { get; } = endLine;
    internal bool Matches(TaskRequirement other) => ToolName == other.ToolName && Path == other.Path &&
        StartLine == other.StartLine && EndLine == other.EndLine && Task == other.Task;
    public override string ToString() => "[TaskRequirement]";
}

internal sealed class PlanTaskSnapshot(string taskId, PlanTaskKind kind, string title, string acceptance,
    IReadOnlyList<TaskRequirement> requirements, PlanTaskState state = PlanTaskState.Pending,
    string? summary = null, string? blockReason = null, TaskCompletionBasis completionBasis = TaskCompletionBasis.None,
    IReadOnlyList<string>? completionFactIds = null, int requirementsVersion = 1, long? completionRulesRevision = null, long? completionEvidenceEpoch = null)
{
    internal long? CompletionRulesRevision { get; } = completionRulesRevision;
    internal long? CompletionEvidenceEpoch { get; } = state == PlanTaskState.Completed ? completionEvidenceEpoch ?? 0 : null;
    internal int RequirementsVersion { get; } = requirementsVersion;
    internal string TaskId { get; } = taskId;
    internal PlanTaskKind Kind { get; } = kind;
    internal string Title { get; } = title;
    internal string Acceptance { get; } = acceptance;
    internal IReadOnlyList<TaskRequirement> Requirements { get; } = Array.AsReadOnly(requirements.ToArray());
    internal PlanTaskState State { get; } = state;
    internal string? Summary { get; } = summary;
    internal string? BlockReason { get; } = blockReason;
    internal TaskCompletionBasis CompletionBasis { get; } = completionBasis;
    internal IReadOnlyList<string> CompletionFactIds { get; } = Array.AsReadOnly(completionFactIds?.ToArray() ?? []);
    internal PlanTaskSnapshot WithState(PlanTaskState value, string? text, string? reason = null,
        TaskCompletionBasis basis = TaskCompletionBasis.None, IReadOnlyList<string>? facts = null, long? rulesRevision = null, long? evidenceEpoch = null) =>
        new(TaskId, Kind, Title, Acceptance, Requirements, value, text, reason, basis, facts, RequirementsVersion, value == PlanTaskState.Completed ? rulesRevision ?? CompletionRulesRevision : null,
            value == PlanTaskState.Completed ? evidenceEpoch ?? CompletionEvidenceEpoch ?? 0 : null);
    internal bool SameDefinition(PlanTaskSnapshot other) => RequirementsVersion == other.RequirementsVersion && Kind == other.Kind && Title == other.Title && Acceptance == other.Acceptance &&
        Requirements.Count == other.Requirements.Count && Requirements.All(r => other.Requirements.Any(r.Matches));
    public override string ToString() => "[PlanTaskSnapshot]";
}

internal sealed class PlanSnapshot(string planId, long revision, string objective, PlanApprovalState approvalState,
    IReadOnlyList<PlanTaskSnapshot> tasks)
{
    internal string PlanId { get; } = planId;
    internal long Revision { get; } = revision;
    internal string Objective { get; } = objective;
    internal PlanApprovalState ApprovalState { get; } = approvalState;
    internal IReadOnlyList<PlanTaskSnapshot> Tasks { get; } = Array.AsReadOnly(tasks.ToArray());
    internal PlanSnapshot WithTasks(IReadOnlyList<PlanTaskSnapshot> values) => new(PlanId, Revision, Objective, ApprovalState, values);
    public override string ToString() => "[PlanSnapshot]";
}

internal sealed class TaskExecutionContext(string planId, long planRevision, string taskId, long rulesRevision = 0)
{
    internal string PlanId { get; } = planId;
    internal long PlanRevision { get; } = planRevision;
    internal string TaskId { get; } = taskId;
    internal long RulesRevision { get; } = rulesRevision;
    public override string ToString() => "[TaskExecutionContext]";
}

internal sealed class TaskOperationFact(string operationId, TaskExecutionContext context, TaskCompletionBasis kind,
    TaskRequirement? request, ToolStatus status, string errorCode, bool truncated = false,
    FileWriteState? writeState = null, bool cleanupFailed = false, long? byteCount = null,
    int? returnedCount = null, WorkspaceDiscoverySummary? discoverySummary = null, string? summary = null, ProcessExecutionSummary? processSummary = null, long evidenceEpoch = 0)
{
    internal long EvidenceEpoch { get; } = evidenceEpoch;
    internal ProcessExecutionSummary? ProcessSummary { get; } = processSummary;
    internal string OperationId { get; } = operationId;
    internal TaskExecutionContext Context { get; } = context;
    internal TaskCompletionBasis Kind { get; } = kind;
    internal string? ToolName => Request?.ToolName;
    internal TaskRequirement? Request { get; } = request;
    internal string? RelativePath => Request?.Path;
    internal ToolStatus Status { get; } = status;
    internal string ErrorCode { get; } = errorCode;
    internal bool Truncated { get; } = truncated;
    internal FileWriteState? WriteState { get; } = writeState;
    internal bool CleanupFailed { get; } = cleanupFailed;
    internal long? ByteCount { get; } = byteCount;
    internal int? ReturnedCount { get; } = returnedCount;
    internal WorkspaceDiscoverySummary? DiscoverySummary { get; } = discoverySummary;
    internal string? Summary { get; } = summary;
    public override string ToString() => "[TaskOperationFact]";
}

internal sealed class PlanApprovalPreview(string planId, long revision, long generation, string displayBlock, long rulesRevision = 0, SkillsOperationStamp? skillsStamp = null, McpOperationStamp? mcpStamp = null)
{
    internal string PlanId { get; } = planId;
    internal long Revision { get; } = revision;
    internal long Generation { get; } = generation;
    internal long RulesRevision { get; } = rulesRevision;
    internal string DisplayBlock { get; } = displayBlock;
    internal SkillsOperationStamp? SkillsStamp { get; } = skillsStamp;
    internal McpOperationStamp? McpStamp { get; } = mcpStamp;
    public override string ToString() => "[PlanApprovalPreview]";
}

internal sealed class PlanMutationResult(string errorCode, PlanSnapshot? snapshot = null, PlanTaskSnapshot? task = null)
{
    internal string ErrorCode { get; } = errorCode;
    internal bool Success => ErrorCode == "none";
    internal PlanSnapshot? Snapshot { get; } = snapshot;
    internal PlanTaskSnapshot? Task { get; } = task;
    public override string ToString() => "[PlanMutationResult]";
}

internal sealed class TaskEvidenceReservation(long generation, TaskExecutionContext context, TaskRequirement request, long evidenceEpoch = 0)
{
    internal long Generation { get; } = generation;
    internal long EvidenceEpoch { get; } = evidenceEpoch;
    internal TaskExecutionContext Context { get; } = context;
    internal TaskRequirement Request { get; } = request;
    public override string ToString() => "[TaskEvidenceReservation]";
}