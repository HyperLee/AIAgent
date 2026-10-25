using System.Globalization;
using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Git;

internal sealed class GitManifestEntry(string? path, string mode, string oid, int stage = 0, char tag = 'H')
{
    internal string? Path { get; } = path;
    internal string Mode { get; } = mode;
    internal string Oid { get; } = oid;
    internal int Stage { get; } = stage;
    internal char Tag { get; } = tag;
    public override string ToString() => "[GitManifestEntry]";
}

internal static class GitManifestParser
{
    internal static IReadOnlyList<GitManifestEntry> Entries(byte[] bytes, string format, bool index)
    {
        var result = new List<GitManifestEntry>();
        foreach (var row in Fields(bytes))
        {
            var tab = Array.IndexOf(row, (byte)'\t');
            if (tab < 0) throw new GitFailure("git_failed");
            var prefix = Encoding.ASCII.GetString(row, 0, tab).Split(' ');
            string mode, oid; var stage = 0; var tag = 'H';
            if (index)
            {
                if (prefix.Length != 4 || prefix[0].Length != 1 || !int.TryParse(prefix[3], NumberStyles.None, CultureInfo.InvariantCulture, out stage) || stage is < 0 or > 3) throw new GitFailure("git_failed");
                tag = prefix[0][0]; mode = prefix[1]; oid = prefix[2];
                if (tag is 'S' or 's' || mode == "040000") throw new GitFailure("unsupported_git_layout");
            }
            else
            {
                if (prefix.Length != 3 || prefix[1] is not ("blob" or "commit" or "tree")) throw new GitFailure("git_failed");
                mode = prefix[0]; oid = prefix[2];
            }
            if (mode.Length != 6 || mode.Any(c => c is < '0' or > '7') || !Oid(oid, format)) throw new GitFailure("git_failed");
            string? path;
            try { path = new UTF8Encoding(false, true).GetString(row, tab + 1, row.Length - tab - 1); }
            catch (DecoderFallbackException) { path = null; }
            if (path is not null && !WorkspacePathRules.IsCanonical(path)) path = null;
            result.Add(new(path, mode, oid, stage, tag));
            if (result.Count > GitLimits.MaxScannedEntries) throw new GitFailure("git_limit");
        }
        return result.AsReadOnly();
    }
    internal static IReadOnlyDictionary<string, string> RawDiff(byte[] bytes, string format)
    {
        var fields = Fields(bytes); var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fields.Count % 2 != 0) throw new GitFailure("git_failed");
        for (var i = 0; i < fields.Count; i += 2)
        {
            var header = Encoding.ASCII.GetString(fields[i]); var parts = header.Split(' ');
            if (parts.Length != 5 || !parts[0].StartsWith(':') || parts[0].Length != 7 || parts[1].Length != 6 ||
                !Oid(parts[2], format) || !Oid(parts[3], format) || parts[4].Length != 1 || !"AMDUTX".Contains(parts[4], StringComparison.Ordinal)) throw new GitFailure("git_failed");
            string path;
            try { path = new UTF8Encoding(false, true).GetString(fields[i + 1]); }
            catch (DecoderFallbackException) { continue; }
            if (WorkspacePathRules.IsCanonical(path) && !result.TryAdd(path, header)) throw new GitFailure("git_failed");
        }
        return result;
    }
    internal static IReadOnlyList<byte[]> Fields(byte[] bytes)
    {
        var result = new List<byte[]>(); var start = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != 0) continue;
            result.Add(bytes.AsSpan(start, i - start).ToArray()); start = i + 1;
            if (result.Count > GitLimits.MaxScannedEntries * 4) throw new GitFailure("git_limit");
        }
        if (start != bytes.Length) throw new GitFailure("git_failed");
        return result;
    }
    internal static bool Oid(string value, string format) => value.Length == (format == "sha1" ? 40 : format == "sha256" ? 64 : -1) && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}