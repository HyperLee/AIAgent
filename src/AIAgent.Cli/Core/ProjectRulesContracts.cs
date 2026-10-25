using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal enum ProjectRulesStatus { Unavailable, Valid, Stale }

internal static class ProjectRulesLimits
{
    internal const int MaxProjectRuleFileBytes = 16384;
    internal const int MaxProjectRuleSources = 8;
    internal const int MaxProjectRuleDepth = 32;
    internal const int MaxProjectRulesProjectionBytes = 32768;
    internal const int MaxProjectRulesDisplayBytes = 65536;
}

internal sealed class ProjectRuleSource(string path, string scope, long rawByteCount, string sha256, string text)
{
    internal string Kind => "project_rule";
    internal string Path { get; } = path;
    internal string Scope { get; } = scope;
    internal long RawByteCount { get; } = rawByteCount;
    internal string Sha256 { get; } = sha256;
    internal string Text { get; } = text;
    internal bool Same(ProjectRuleSource other) => Path == other.Path && Scope == other.Scope &&
        RawByteCount == other.RawByteCount && Sha256 == other.Sha256 && Text == other.Text;
    public override string ToString() => "[ProjectRuleSource]";
}

internal sealed class ProjectRuleObservation(string directory, WorkspaceNodeState parent, WorkspaceNodeState candidate)
{
    internal string Directory { get; } = directory;
    internal WorkspaceNodeState Parent { get; } = parent;
    internal WorkspaceNodeState Candidate { get; } = candidate;
    internal static bool SameDirectory(WorkspaceNodeState a, WorkspaceNodeState b) =>
        a.Kind == b.Kind && a.Device == b.Device && a.Identity == b.Identity && a.Mode == b.Mode && a.User == b.User;
    internal bool Same(ProjectRuleObservation other) => Directory == other.Directory &&
        SameDirectory(Parent, other.Parent) && Candidate == other.Candidate;
    public override string ToString() => "[ProjectRuleObservation]";
}

internal sealed class ProjectRulesChain(string scope, IReadOnlyList<ProjectRuleSource> sources, IReadOnlyList<ProjectRuleObservation> observations)
{
    internal string Scope { get; } = scope;
    internal IReadOnlyList<ProjectRuleSource> Sources { get; } = Array.AsReadOnly(sources.ToArray());
    internal IReadOnlyList<ProjectRuleObservation> Observations { get; } = Array.AsReadOnly(observations.ToArray());
    internal bool Same(ProjectRulesChain other) => Scope == other.Scope && Sources.Count == other.Sources.Count &&
        Observations.Count == other.Observations.Count && Sources.Zip(other.Sources).All(p => p.First.Same(p.Second)) &&
        Observations.Zip(other.Observations).All(p => p.First.Same(p.Second));
    public override string ToString() => "[ProjectRulesChain]";
}

internal sealed class ProjectRulesSnapshot(long rulesRevision, ProjectRulesChain chain, string projection)
{
    internal long RulesRevision { get; } = rulesRevision;
    internal string Scope => Chain.Scope;
    internal IReadOnlyList<ProjectRuleSource> Sources => Chain.Sources;
    internal ProjectRulesChain Chain { get; } = chain;
    internal string Projection { get; } = projection;
    public override string ToString() => "[ProjectRulesSnapshot]";
}

internal sealed class ProjectRulesStamp(long sessionGeneration, long rulesGeneration, long rulesRevision, string scope,
    ProjectRulesStatus status, ProjectRulesSnapshot? snapshot)
{
    internal long SessionGeneration { get; } = sessionGeneration;
    internal long RulesGeneration { get; } = rulesGeneration;
    internal long RulesRevision { get; } = rulesRevision;
    internal string Scope { get; } = scope;
    internal ProjectRulesStatus Status { get; } = status;
    internal ProjectRulesSnapshot? Snapshot { get; } = snapshot;
    public override string ToString() => "[ProjectRulesStamp]";
}

internal sealed class ProjectRulesReadResult(WorkspaceErrorCode error, ProjectRulesChain? chain = null)
{
    internal WorkspaceErrorCode Error { get; } = error;
    internal ProjectRulesChain? Chain { get; } = chain;
    public override string ToString() => "[ProjectRulesReadResult]";
}