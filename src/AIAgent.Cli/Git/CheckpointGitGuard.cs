namespace AIAgent.Cli.Git;

/// <summary>用途限定、不可變的單一目標 Git metadata；不含 blob 或全域 index 指紋。</summary>
internal sealed class CheckpointGitGuard
{
    private readonly KeyValuePair<string, string>[] _observations;
    internal CheckpointGitGuard(bool isRootRepository, IEnumerable<KeyValuePair<string, string>> observations)
    {
        IsRootRepository = isRootRepository;
        _observations = observations.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
    }
    internal bool IsRootRepository { get; }
    internal bool SameAs(CheckpointGitGuard other) => other is not null && IsRootRepository == other.IsRootRepository &&
        _observations.SequenceEqual(other._observations);
    internal bool MatchesRootObservation(string rootState, string gitdirState) =>
        _observations.Any(pair => pair.Key == "root" && pair.Value == rootState) &&
        _observations.Any(pair => pair.Key == "gitdir" && pair.Value == gitdirState);
    public override string ToString() => "[CheckpointGitGuard]";
}