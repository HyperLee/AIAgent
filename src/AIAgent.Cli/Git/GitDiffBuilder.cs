using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Git;

internal sealed class GitHunk(int oldStart, int oldCount, int newStart, int newCount)
{
    internal int OldStart { get; } = oldStart;
    internal int OldCount { get; } = oldCount;
    internal int NewStart { get; } = newStart;
    internal int NewCount { get; } = newCount;
    public override string ToString() => "[GitHunk]";
}

internal sealed class GitDiffData(string text, IEnumerable<GitHunk> hunks, IEnumerable<int> oldChanged, IEnumerable<int> newChanged)
{
    internal string Text { get; } = text;
    internal IReadOnlyList<GitHunk> Hunks { get; } = Array.AsReadOnly(hunks.ToArray());
    internal IReadOnlyList<int> OldChangedLines { get; } = Array.AsReadOnly(oldChanged.ToArray());
    internal IReadOnlyList<int> NewChangedLines { get; } = Array.AsReadOnly(newChanged.ToArray());
    public override string ToString() => "[GitDiffData]";
}

internal static class GitDiffBuilder
{
    internal static GitDiffData Create(string snapshotId, GitChange change, CancellationToken token)
    {
        var oldText = change.Old.Content ?? ""; var newText = change.New.Content ?? "";
        var old = Tokens(oldText, token); var next = Tokens(newText, token);
        var prefix = 0;
        while (prefix < old.Count && prefix < next.Count && old[prefix] == next[prefix]) { token.ThrowIfCancellationRequested(); prefix++; }
        var suffix = 0;
        while (suffix < old.Count - prefix && suffix < next.Count - prefix && old[^(suffix + 1)] == next[^(suffix + 1)]) { token.ThrowIfCancellationRequested(); suffix++; }
        var path = FileChangePreviewBuilder.EscapePath(change.Path); var layer = GitLimits.Name(change.Layer);
        var text = new StringBuilder().Append("原始 bytes 視角；線性完整 hunk，非最短 diff，不能直接套用。\n")
            .Append("--- ").Append(snapshotId).Append('/').Append(layer).Append("/old ").Append(path).Append('\n')
            .Append("+++ ").Append(snapshotId).Append('/').Append(layer).Append("/new ").Append(path).Append('\n');
        foreach (var side in new[] { change.Old, change.New })
            text.Append(GitLimits.Name(side.Side)).Append(": exists=").Append(side.Exists ? "true" : "false").Append(" mode=").Append(side.Mode ?? "null")
                .Append(" BOM=").Append(side.HasUtf8Bom?.ToString() ?? "null").Append('\n');
        if (!change.ContentAvailable) return new(text.Append("metadata；未做內容審查。\n").ToString(), [], [], []);
        var before = Math.Min(prefix, 3); var after = Math.Min(suffix, 3); var start = prefix - before;
        var oldCount = old.Count - prefix - suffix + before + after; var newCount = next.Count - prefix - suffix + before + after;
        var hunk = new GitHunk(oldCount == 0 ? start : start + 1, oldCount, newCount == 0 ? start : start + 1, newCount);
        text.Append(FormattableString.Invariant($"@@ -{hunk.OldStart},{oldCount} +{hunk.NewStart},{newCount} @@\n"));
        var byteCount = Encoding.UTF8.GetByteCount(text.ToString());
        void Add(char mark, string line)
        {
            token.ThrowIfCancellationRequested();
            var ending = line.EndsWith("\r\n", StringComparison.Ordinal) ? "CRLF" : line.EndsWith('\n') ? "LF" : line.EndsWith('\r') ? "CR" : "EOF";
            var body = ending == "CRLF" ? line[..^2] : ending == "EOF" ? line : line[..^1];
            var rendered = mark + FileChangePreviewBuilder.EscapePath(body) + " [" + ending + "]\n";
            byteCount = checked(byteCount + Encoding.UTF8.GetByteCount(rendered));
            if (byteCount > GitLimits.MaxResultBytes) throw new GitFailure("result_too_large");
            text.Append(rendered);
        }
        for (var i = start; i < prefix; i++) Add(' ', old[i]);
        for (var i = prefix; i < old.Count - suffix; i++) Add('-', old[i]);
        for (var i = prefix; i < next.Count - suffix; i++) Add('+', next[i]);
        for (var i = 0; i < after; i++) Add(' ', old[old.Count - suffix + i]);
        return new(text.ToString(), [hunk], Enumerable.Range(prefix + 1, old.Count - prefix - suffix), Enumerable.Range(prefix + 1, next.Count - prefix - suffix));
    }
    private static IReadOnlyList<string> Tokens(string text, CancellationToken token) => WorkspaceTextLines.Split(text, token).Select(x => text[x.Start..x.End]).ToArray();
}