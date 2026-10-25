using System.Security.Cryptography;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Git;

internal enum GitSelection { All, Staged, Unstaged, Untracked }
internal enum GitLayer { Staged, Unstaged, Untracked }
internal enum GitSide { Old, New }

internal static class GitLimits
{
    internal const int MaxSourceBytes = 131072;
    internal const int MaxSnapshotSourceBytes = 8388608;
    internal const int MaxScannedEntries = 8192;
    internal const int MaxChangedPaths = 200;
    internal const int MaxDepth = 32;
    internal const int MaxResultBytes = 65536;
    internal const int MaxDisplayBytes = 65536;
    internal const int MaxAppendixBytes = 32768;
    internal const int MaxRangeLines = 200;
    internal const int MaxFindings = 30;
    internal const int MaxFindingLines = 10;
    internal static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(60);
    internal static bool Selected(GitSelection selection, GitLayer layer) => selection == GitSelection.All || (int)selection - 1 == (int)layer;
    internal static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
}

internal sealed class GitSource
{
    private readonly byte[]? _bytes;
    internal string Path { get; }
    internal GitLayer Layer { get; }
    internal GitSide Side { get; }
    internal bool Exists { get; }
    internal string? Mode { get; }
    internal long? ByteCount { get; }
    internal bool? HasUtf8Bom { get; }
    internal string? Sha256 { get; }
    internal string? BlobOid { get; }
    internal string? Content { get; }
    internal string? ExclusionReason { get; }
    internal int TotalLines => Content is null ? 0 : WorkspaceTextLines.Split(Content, default).Count;
    internal GitSource(string path, GitLayer layer, GitSide side, byte[] bytes, string mode, string? blobOid = null)
    {
        if (!WorkspacePathRules.IsCanonical(path) || !Enum.IsDefined(layer) || !Enum.IsDefined(side) ||
            bytes.Length > GitLimits.MaxSourceBytes || mode is not ("100644" or "100755")) throw new GitFailure("git_source_unavailable");
        Path = path; Layer = layer; Side = side; Exists = true; Mode = mode; BlobOid = blobOid;
        _bytes = bytes.ToArray(); ByteCount = bytes.Length;
        try { Content = FileChangePreviewBuilder.Decode(_bytes, out var bom); HasUtf8Bom = bom; }
        catch (DecoderFallbackException) { throw new GitFailure("invalid_utf8"); }
        if (Content.Any(c => char.IsControl(c) && c is not ('\t' or '\r' or '\n'))) throw new GitFailure("binary_content");
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(_bytes));
    }
    private GitSource(string path, GitLayer layer, GitSide side, bool exists, string? mode, string? oid, long? bytes, string? reason)
    { Path = path; Layer = layer; Side = side; Exists = exists; Mode = mode; BlobOid = oid; ByteCount = bytes; ExclusionReason = reason; }
    internal static GitSource Absent(string path, GitLayer layer, GitSide side) => new(path, layer, side, false, null, null, null, null);
    internal static GitSource Unsupported(string path, GitLayer layer, GitSide side, string? mode, string? oid, long? bytes, string reason) => new(path, layer, side, true, mode, oid, bytes, reason);
    internal byte[] CopyBytes() => _bytes?.ToArray() ?? [];
    private GitSource(GitSource source, GitLayer layer, GitSide side)
    { Path = source.Path; Layer = layer; Side = side; Exists = source.Exists; Mode = source.Mode; ByteCount = source.ByteCount; HasUtf8Bom = source.HasUtf8Bom; Sha256 = source.Sha256; BlobOid = source.BlobOid; Content = source.Content; ExclusionReason = source.ExclusionReason; _bytes = source._bytes; }
    internal GitSource ForSide(GitLayer layer, GitSide side) => new(this, layer, side);
    private GitSource(GitSource metadata, GitSource storage)
    {
        Path = metadata.Path; Layer = metadata.Layer; Side = metadata.Side; Exists = metadata.Exists; Mode = metadata.Mode;
        ByteCount = metadata.ByteCount; HasUtf8Bom = metadata.HasUtf8Bom; Sha256 = metadata.Sha256; BlobOid = metadata.BlobOid;
        ExclusionReason = metadata.ExclusionReason; Content = storage.Content; _bytes = storage._bytes;
    }
    internal GitSource ShareVersion(GitSource storage)
    {
        if (Path != storage.Path || Sha256 is null || Sha256 != storage.Sha256 || ByteCount != storage.ByteCount) throw new GitFailure("git_failed");
        return new(this, storage);
    }
    internal bool SameRaw(GitSource other) => Exists == other.Exists && Mode == other.Mode && Sha256 == other.Sha256 && ByteCount == other.ByteCount && ExclusionReason == other.ExclusionReason;
    public override string ToString() => "[GitSource]";
}

internal sealed class GitChange(string path, GitLayer layer, GitSource old, GitSource @new, string? exclusionReason = null)
{
    internal string Path { get; } = path;
    internal GitLayer Layer { get; } = layer;
    internal GitSource Old { get; } = old;
    internal GitSource New { get; } = @new;
    internal string? ExclusionReason { get; } = exclusionReason ?? old.ExclusionReason ?? @new.ExclusionReason;
    internal bool HasChange => !Old.SameRaw(New);
    internal bool ContentAvailable => ExclusionReason is null && (Old.Content is not null || !Old.Exists) &&
        (New.Content is not null || !New.Exists) && !string.Equals(Old.Content ?? "", New.Content ?? "", StringComparison.Ordinal);
    internal string ChangeKind => ExclusionReason is not null ? "unsupported" : !Old.Exists ? "added" : !New.Exists ? "deleted" : ContentAvailable ? "modified" : "metadata";
    public override string ToString() => "[GitChange]";
}

internal sealed class GitExclusion(string? path, GitLayer? layer, string reason)
{
    internal string? Path { get; } = path;
    internal GitLayer? Layer { get; } = layer;
    internal string Reason { get; } = reason;
    public override string ToString() => "[GitExclusion]";
}

internal sealed class GitObservation(string name, string value)
{
    internal string Name { get; } = name;
    internal string Value { get; } = value;
    public override string ToString() => "[GitObservation]";
}

internal sealed class GitValidationManifest(string scope, IEnumerable<GitObservation> observations)
{
    internal string Scope { get; } = scope;
    internal IReadOnlyList<GitObservation> Observations { get; } = Array.AsReadOnly(observations.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray());
    internal string Fingerprint => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(Observations.Select(x => x.Name.Length + ":" + x.Name + x.Value.Length + ":" + x.Value)))));
    public override string ToString() => "[GitValidationManifest]";
}

internal sealed class GitSnapshot(string snapshotId, string scope, string? headOid, string objectFormat,
    IEnumerable<GitChange> changes, IEnumerable<GitExclusion> exclusions, GitValidationManifest manifest,
    ProjectRulesStamp rulesStamp, bool enumerationComplete = true)
{
    internal string SnapshotId { get; } = snapshotId;
    internal string Scope { get; } = scope;
    internal string? HeadOid { get; } = headOid;
    internal string ObjectFormat { get; } = objectFormat;
    internal IReadOnlyList<GitChange> Changes { get; } = Array.AsReadOnly(changes.OrderBy(c => c.Path, StringComparer.Ordinal).ThenBy(c => c.Layer).ToArray());
    internal IReadOnlyList<GitExclusion> Exclusions { get; } = Array.AsReadOnly(exclusions.ToArray());
    internal GitValidationManifest Manifest { get; } = manifest;
    internal ProjectRulesStamp RulesStamp { get; } = rulesStamp;
    internal bool EnumerationComplete { get; } = enumerationComplete;
    internal DateTimeOffset CapturedAt { get; } = DateTimeOffset.UtcNow;
    internal GitSnapshot WithId(string id) => new(id, Scope, HeadOid, ObjectFormat, Changes, Exclusions, Manifest, RulesStamp, EnumerationComplete);
    public override string ToString() => "[GitSnapshot]";
}

internal sealed class GitCaptureResult(string errorCode, GitSnapshot? snapshot = null)
{
    internal string ErrorCode { get; } = errorCode;
    internal GitSnapshot? Snapshot { get; } = errorCode == "none" ? snapshot : null;
    public override string ToString() => "[GitCaptureResult]";
}

internal interface IGitReadWorkspace : IAsyncDisposable
{
    Task<GitCaptureResult> CaptureAsync(CancellationToken cancellationToken);
    Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken cancellationToken);
    Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken cancellationToken);
}

internal sealed class GitFailure(string code) : Exception("Git 操作無法完成")
{
    internal string Code { get; } = code;
}