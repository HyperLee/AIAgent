using System.Text.Json;

namespace AIAgent.Cli.Core;

internal enum GenerationPurpose { Conversation, Compaction, WorkSaveSummary, Review }
internal enum CompactionTrigger { Automatic, Manual }
internal enum ContextMeasureStatus { Measured, Unavailable }

internal interface ILlmContextInspector
{
    ContextRequestMeasure Measure(LlmRequest request);
    ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages);
}

internal interface ILlmRequestCapacityInspector
{
    Task<ContextRequestMeasure> MeasureActualRequestAsync(LlmRequest request, CancellationToken cancellationToken);
}

internal interface ILlmContextTelemetry
{
    IDisposable ObserveRequest(Action<long, bool> report);
}

internal interface ILlmToolCapabilityInspector
{
    bool CanRepresentTools(IReadOnlyList<LlmToolDefinition> tools);
}

internal interface ILlmRuntimePolicyValidator
{
    bool ValidatePolicy(string instruction, IReadOnlyList<LlmToolDefinition> tools);
}

internal sealed class ContextRequestMeasure(ContextMeasureStatus status, long? estimatedJsonBytes = null,
    bool requiresContextReset = false)
{
    internal const int MaxRequestBytes = 262144;
    internal ContextMeasureStatus Status { get; } = status;
    internal long? EstimatedJsonBytes { get; } = estimatedJsonBytes;
    internal bool RequiresContextReset { get; } = requiresContextReset;
    public override string ToString() => "[ContextRequestMeasure]";
}

internal sealed class ContextTurnProjection(long turnId, string userText, string assistantText, IReadOnlyList<JsonElement> tools)
{
    internal long TurnId { get; } = turnId;
    internal string UserText { get; } = userText;
    internal string AssistantText { get; } = assistantText;
    internal IReadOnlyList<JsonElement> Tools { get; } = Array.AsReadOnly(tools.Select(t => t.Clone()).ToArray());
    public override string ToString() => "[ContextTurnProjection]";
}

internal sealed class ContextCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages)
{
    internal long TurnId { get; } = turnId;
    internal IReadOnlyList<LlmMessage> Messages { get; } = Array.AsReadOnly(messages.ToArray());
    public override string ToString() => "[ContextCompletedTurn]";
}

internal sealed class ContextPin(string pinId, string text, string? sourceWorkId = null)
{
    internal string? SourceWorkId { get; } = sourceWorkId;
    internal string PinId { get; } = pinId;
    internal string Text { get; } = text;
    public override string ToString() => "[ContextPin]";
}

internal sealed class ContextSummary(long throughTurnId, long coveredTurnCount, IReadOnlyList<string> goals,
    IReadOnlyList<string> decisions, IReadOnlyList<string> openQuestions, IReadOnlyList<string> background, string? sourceWorkId = null)
{
    internal string? SourceWorkId { get; } = sourceWorkId;
    internal int Version => 1;
    internal long ThroughTurnId { get; } = throughTurnId;
    internal long CoveredTurnCount { get; } = coveredTurnCount;
    internal IReadOnlyList<string> Goals { get; } = Array.AsReadOnly(goals.ToArray());
    internal IReadOnlyList<string> Decisions { get; } = Array.AsReadOnly(decisions.ToArray());
    internal IReadOnlyList<string> OpenQuestions { get; } = Array.AsReadOnly(openQuestions.ToArray());
    internal IReadOnlyList<string> Background { get; } = Array.AsReadOnly(background.ToArray());
    public override string ToString() => "[ContextSummary]";
}

internal sealed class ContextSnapshot(long revision, long pinsRevision, ContextSummary? summary,
    IReadOnlyList<ContextCompletedTurn> completedTurns, IReadOnlyList<ContextPin> pins)
{
    internal long Revision { get; } = revision;
    internal long PinsRevision { get; } = pinsRevision;
    internal ContextSummary? Summary { get; } = summary;
    internal IReadOnlyList<ContextCompletedTurn> CompletedTurns { get; } = Array.AsReadOnly(completedTurns.ToArray());
    internal IReadOnlyList<ContextPin> Pins { get; } = Array.AsReadOnly(pins.ToArray());
    public override string ToString() => "[ContextSnapshot]";
}

internal sealed class ContextOperationStamp(long sessionGeneration, ContextSnapshot context, long planGeneration,
    AgentMode mode, PlanSnapshot? plan, long rulesRevision = 0, long rulesGeneration = 0, long recoveryRevision = 0, SkillsOperationStamp? skillsStamp = null, McpOperationStamp? mcpStamp = null)
{
    internal SkillsOperationStamp? SkillsStamp { get; } = skillsStamp;
    internal McpOperationStamp? McpStamp { get; } = mcpStamp;
    internal bool McpMatches(McpOperationStamp? other) => McpStamp is null ? other is null : other is not null && McpStamp.Same(other);
    internal bool SkillsMatches(SkillsOperationStamp? current) => SkillsVersion.Matches(SkillsStamp, current);
    internal long RecoveryRevision { get; } = recoveryRevision;
    internal long RulesRevision { get; } = rulesRevision;
    internal long RulesGeneration { get; } = rulesGeneration;
    internal long SessionGeneration { get; } = sessionGeneration;
    internal long ContextRevision { get; } = context.Revision;
    internal long PinsRevision { get; } = context.PinsRevision;
    internal long PlanGeneration { get; } = planGeneration;
    internal AgentMode Mode { get; } = mode;
    internal string? PlanId { get; } = plan?.PlanId;
    internal long? PlanRevision { get; } = plan?.Revision;
    internal PlanSnapshot? Plan { get; } = plan;
    internal ContextSnapshot Context { get; } = context;
    public override string ToString() => "[ContextOperationStamp]";
}

internal sealed class CompactionCandidate(ContextOperationStamp stamp, int prefixCount, ContextSummary summary, string appendix)
{
    internal ContextOperationStamp Stamp { get; } = stamp;
    internal int PrefixCount { get; } = prefixCount;
    internal ContextSummary Summary { get; } = summary;
    internal string Appendix { get; } = appendix;
    public override string ToString() => "[CompactionCandidate]";
}

internal sealed class ContextMetricStamp(ContextOperationStamp stamp)
{
    private readonly WeakReference<PlanSnapshot>? _plan = stamp.Plan is { } plan ? new(plan) : null;
    private readonly SkillsVersion _skills = new(stamp.SkillsStamp);
    private readonly McpOperationStamp? _mcp = stamp.McpStamp;
    internal long RecoveryRevision { get; } = stamp.RecoveryRevision;
    internal long RulesRevision { get; } = stamp.RulesRevision;
    internal long RulesGeneration { get; } = stamp.RulesGeneration;
    internal long SessionGeneration { get; } = stamp.SessionGeneration;
    internal long ContextRevision { get; } = stamp.ContextRevision;
    internal long PinsRevision { get; } = stamp.PinsRevision;
    internal long PlanGeneration { get; } = stamp.PlanGeneration;
    internal AgentMode Mode { get; } = stamp.Mode;
    internal bool Matches(ContextOperationStamp current) => (_mcp is null ? current.McpStamp is null : current.McpStamp is not null && _mcp.Same(current.McpStamp)) && _skills.Matches(current.SkillsStamp) && RecoveryRevision == current.RecoveryRevision && RulesRevision == current.RulesRevision && RulesGeneration == current.RulesGeneration &&
        SessionGeneration == current.SessionGeneration &&
        ContextRevision == current.ContextRevision && PinsRevision == current.PinsRevision && PlanGeneration == current.PlanGeneration &&
        Mode == current.Mode && (_plan is null ? current.Plan is null : _plan.TryGetTarget(out var prior) && ReferenceEquals(prior, current.Plan));
    public override string ToString() => "[ContextMetricStamp]";
}

internal sealed class ContextCapacityException : Exception
{
    internal ContextCapacityException() : base("Context 容量不足") { }
}