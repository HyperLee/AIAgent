using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal enum SkillsStatus { Disabled, Valid, Stale }

internal static class SkillsLimits
{
    internal const int MaxSkillCatalogSources = 32;
    internal const int MaxSkillDirectoryEntries = 256;
    internal const int MaxSkillFileBytes = 16384;
    internal const int MaxSkillCatalogProjectionBytes = 32768;
    internal const int MaxActiveSkillsProjectionBytes = 32768;
    internal const int MaxSkillsDisplayBytes = 65536;
}

internal sealed class SkillDescriptor(string id, string description, string path, string scope, long rawByteCount, string sha256)
{
    internal string Kind => "skill";
    internal string Id { get; } = id;
    internal string Description { get; } = description;
    internal string Path { get; } = path;
    internal string Scope { get; } = scope;
    internal long RawByteCount { get; } = rawByteCount;
    internal string Sha256 { get; } = sha256;
    internal bool Same(SkillDescriptor other) => Id == other.Id && Description == other.Description && Path == other.Path && Scope == other.Scope && RawByteCount == other.RawByteCount && Sha256 == other.Sha256;
    public override string ToString() => "[SkillDescriptor]";
}

internal sealed class SkillPathObservation(string path, WorkspaceNodeState state, UnixFileMode? fileMode = null)
{
    internal string Path { get; } = path;
    internal WorkspaceNodeState State { get; } = state;
    internal UnixFileMode? FileMode { get; } = fileMode;
    internal bool Same(SkillPathObservation other) => Path == other.Path && FileMode == other.FileMode && (State.Kind == WorkspaceNodeKind.Directory
        ? ProjectRuleObservation.SameDirectory(State, other.State) : State == other.State);
    public override string ToString() => "[SkillPathObservation]";
}

internal sealed class SkillSource(SkillDescriptor descriptor, string text, string body, UnixFileMode mode,
    IReadOnlyList<SkillPathObservation> observations, IReadOnlyList<ProjectRuleObservation> boundaries)
{
    internal SkillDescriptor Descriptor { get; } = descriptor;
    internal string Text { get; } = text;
    internal string Body { get; } = body;
    internal UnixFileMode Mode { get; } = mode;
    internal IReadOnlyList<SkillPathObservation> Observations { get; } = Array.AsReadOnly(observations.ToArray());
    internal IReadOnlyList<ProjectRuleObservation> Boundaries { get; } = Array.AsReadOnly(boundaries.ToArray());
    internal bool Same(SkillSource other) => Descriptor.Same(other.Descriptor) && Text == other.Text && Body == other.Body && Mode == other.Mode &&
        Observations.Count == other.Observations.Count && Observations.Zip(other.Observations).All(p => p.First.Same(p.Second)) &&
        Boundaries.Count == other.Boundaries.Count && Boundaries.Zip(other.Boundaries).All(p => p.First.Same(p.Second));
    public override string ToString() => "[SkillSource]";
}

internal sealed class SkillCatalog(string scope, IReadOnlyList<SkillDescriptor> skills, int excludedCount,
    IReadOnlyList<SkillPathObservation> observations, IReadOnlyList<string> members)
{
    internal string Scope { get; } = scope;
    internal IReadOnlyList<SkillDescriptor> Skills { get; } = Array.AsReadOnly(skills.ToArray());
    internal int ExcludedCount { get; } = excludedCount;
    internal IReadOnlyList<SkillPathObservation> Observations { get; } = Array.AsReadOnly(observations.ToArray());
    internal IReadOnlyList<string> Members { get; } = Array.AsReadOnly(members.ToArray());
    internal bool Same(SkillCatalog other) => Scope == other.Scope && ExcludedCount == other.ExcludedCount && Members.SequenceEqual(other.Members, StringComparer.Ordinal) &&
        Skills.Count == other.Skills.Count && Skills.Zip(other.Skills).All(p => p.First.Same(p.Second)) &&
        Observations.Count == other.Observations.Count && Observations.Zip(other.Observations).All(p => p.First.Same(p.Second));
    public override string ToString() => "[SkillCatalog]";
}

internal sealed class ActiveSkillSnapshot(long skillsRevision, SkillSource source, string projection)
{
    internal long SkillsRevision { get; } = skillsRevision;
    internal SkillSource Source { get; } = source;
    internal string Projection { get; } = projection;
    public override string ToString() => "[ActiveSkillSnapshot]";
}

internal sealed class SkillsOperationStamp(long sessionGeneration, long skillsGeneration, long skillsRevision, SkillsStatus status,
    ActiveSkillSnapshot? snapshot, ProjectRulesStamp? rules)
{
    internal long SessionGeneration { get; } = sessionGeneration;
    internal long SkillsGeneration { get; } = skillsGeneration;
    internal long SkillsRevision { get; } = skillsRevision;
    internal SkillsStatus Status { get; } = status;
    internal ActiveSkillSnapshot? Snapshot { get; } = snapshot;
    internal long RulesGeneration { get; } = rules?.RulesGeneration ?? 0;
    internal long RulesRevision { get; } = rules?.RulesRevision ?? 0;
    internal string Scope { get; } = rules?.Scope ?? ".";
    public override string ToString() => "[SkillsOperationStamp]";
}

internal sealed class SkillsOperationResult(string errorCode, string? displayBlock = null, bool requiresContextReset = false)
{
    internal string ErrorCode { get; } = errorCode;
    internal string? DisplayBlock { get; } = displayBlock;
    internal bool RequiresContextReset { get; } = requiresContextReset;
    public override string ToString() => "[SkillsOperationResult]";
}

internal sealed class SkillReadResult(string errorCode, SkillSource? source = null, SkillCatalog? catalog = null)
{
    internal string ErrorCode { get; } = errorCode;
    internal SkillSource? Source { get; } = source;
    internal SkillCatalog? Catalog { get; } = catalog;
    public override string ToString() => "[SkillReadResult]";
}

internal sealed class SkillsFlowException(string code) : Exception("技能來源閘門拒絕操作")
{
    internal string Code { get; } = code;
    internal AgentStopReason Reason => Code switch
    {
        "skills_changed" => AgentStopReason.SkillsChanged,
        "skills_limit" => AgentStopReason.SkillsLimit,
        _ => AgentStopReason.SkillsUnavailable
    };
}

internal sealed class SkillsSensitiveException : Exception
{
    internal SkillsSensitiveException() : base("技能未通過敏感檢查") { }
}