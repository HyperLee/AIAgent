namespace AIAgent.Cli.Core;

internal sealed class WorkSaveDocument(string workId, string title, string createdUtc, WorkWorkspaceIdentity workspace,
    string scopeHint, IReadOnlyList<WorkRuleReference> rulesRefs, ContextSummary? summary, IReadOnlyList<string> pins,
    WorkSavedPlan? plan, IReadOnlyList<WorkFileReference> fileRefs)
{
    internal string WorkId { get; } = workId;
    internal string Title { get; } = title;
    internal string CreatedUtc { get; } = createdUtc;
    internal WorkWorkspaceIdentity Workspace { get; } = workspace;
    internal string ScopeHint { get; } = scopeHint;
    internal IReadOnlyList<WorkRuleReference> RulesRefs { get; } = Array.AsReadOnly(rulesRefs.ToArray());
    internal ContextSummary? Summary { get; } = summary;
    internal IReadOnlyList<string> Pins { get; } = Array.AsReadOnly(pins.ToArray());
    internal WorkSavedPlan? Plan { get; } = plan;
    internal IReadOnlyList<WorkFileReference> FileRefs { get; } = Array.AsReadOnly(fileRefs.ToArray());
    public override string ToString() => "[WorkSaveDocument]";
}

internal sealed class WorkWorkspaceIdentity(string root, ulong device, ulong identity)
{
    internal string Root { get; } = root;
    internal ulong Device { get; } = device;
    internal ulong Identity { get; } = identity;
    public override string ToString() => "[WorkWorkspaceIdentity]";
}

internal sealed class WorkRuleReference(string path, string sha256)
{
    internal string Path { get; } = path;
    internal string Sha256 { get; } = sha256;
    public override string ToString() => "[WorkRuleReference]";
}

internal sealed class WorkSavedPlan(string objective, IReadOnlyList<WorkSavedTask> tasks)
{
    internal string Objective { get; } = objective;
    internal IReadOnlyList<WorkSavedTask> Tasks { get; } = Array.AsReadOnly(tasks.ToArray());
    public override string ToString() => "[WorkSavedPlan]";
}

internal sealed class WorkSavedTask(PlanTaskKind kind, string title, string acceptance, int requirementsVersion,
    IReadOnlyList<TaskRequirement> requirements, PlanTaskState reportedState, TaskCompletionBasis reportedBasis,
    string? summary, string? blockReason)
{
    internal PlanTaskKind Kind { get; } = kind;
    internal string Title { get; } = title;
    internal string Acceptance { get; } = acceptance;
    internal int RequirementsVersion { get; } = requirementsVersion;
    internal IReadOnlyList<TaskRequirement> Requirements { get; } = Array.AsReadOnly(requirements.ToArray());
    internal PlanTaskState ReportedState { get; } = reportedState;
    internal TaskCompletionBasis ReportedBasis { get; } = reportedBasis;
    internal string? Summary { get; } = summary;
    internal string? BlockReason { get; } = blockReason;
    public override string ToString() => "[WorkSavedTask]";
}

internal sealed class WorkFileReference(string path, string observation, long? byteCount, string? sha256)
{
    internal string Path { get; } = path;
    internal string Observation { get; } = observation;
    internal long? ByteCount { get; } = byteCount;
    internal string? Sha256 { get; } = sha256;
    public override string ToString() => "[WorkFileReference]";
}

internal sealed class WorkDocumentException(string code) : Exception("工作文件未通過檢查。")
{
    internal string Code { get; } = code;
    public override string ToString() => "[WorkDocumentException]";
}