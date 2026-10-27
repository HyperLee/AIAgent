namespace AIAgent.Cli.Core;

/// <summary>量測只持版本及弱身分，不延長技能正文生命週期。</summary>
internal sealed class SkillsVersion(SkillsOperationStamp? stamp)
{
    private readonly bool _present = stamp is not null;
    private readonly long _session = stamp?.SessionGeneration ?? 0;
    private readonly long _generation = stamp?.SkillsGeneration ?? 0;
    private readonly long _revision = stamp?.SkillsRevision ?? 0;
    private readonly SkillsStatus _status = stamp?.Status ?? SkillsStatus.Disabled;
    private readonly string _scope = stamp?.Scope ?? ".";
    private readonly WeakReference<ActiveSkillSnapshot>? _snapshot = stamp?.Snapshot is { } active ? new(active) : null;
    internal bool Matches(SkillsOperationStamp? current) => _present == (current is not null) && (current is null ||
        current.SessionGeneration == _session && current.SkillsGeneration == _generation && current.SkillsRevision == _revision && current.Status == _status && current.Scope == _scope &&
        (_snapshot is null ? current.Snapshot is null : _snapshot.TryGetTarget(out var prior) && ReferenceEquals(prior, current.Snapshot)));
    internal static bool Matches(SkillsOperationStamp? previous, SkillsOperationStamp? current) => new SkillsVersion(previous).Matches(current);
    public override string ToString() => "[SkillsVersion]";
}