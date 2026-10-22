using System.Text;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    /// <summary>完整關閉後才回傳；內部查核不發布快照或模型資料。</summary>
    private async Task<VerifiedWorkspaceFile> ReadVerifiedFileAsync(string path, WorkspaceOperation operation,
        CancellationToken token, bool settling = false)
    {
        void Active() { if (!settling) CheckActive(operation.Generation, token); }
        Active();
        var observations = InspectPath(path, WorkspaceNodeKind.File, operation);
        var initial = observations[^1].State;
        if (initial.Length > WorkspaceLimits.MaxFileBytes) throw Rejected(WorkspaceErrorCode.FileTooLarge);
        Active(); Revalidate(observations);
        byte[] original;
        UnixFileMode mode;
        string content;
        bool hasBom;
        await using (var file = _fileSystem.OpenRead(observations[^1].Path))
        {
            mode = file.GetMode();
            if (file.GetState() != initial) throw Rejected(WorkspaceErrorCode.PathChanged);
            var buffer = new byte[WorkspaceLimits.MaxFileBytes + 1]; var count = 0;
            while (count < buffer.Length)
            {
                Active();
                var received = await file.Stream.ReadAsync(buffer.AsMemory(count, buffer.Length - count), token);
                Active();
                if (received == 0) break;
                count += received;
                if (count > WorkspaceLimits.MaxFileBytes) throw Rejected(WorkspaceErrorCode.FileTooLarge);
            }
            original = buffer.AsSpan(0, count).ToArray();
            try { content = FileChangePreviewBuilder.Decode(original, out hasBom); }
            catch (DecoderFallbackException) { throw Rejected(WorkspaceErrorCode.InvalidUtf8); }
            Active();
            if (content.Any(c => char.IsControl(c) && c is not ('\t' or '\n' or '\r'))) throw Rejected(WorkspaceErrorCode.BinaryContent);
            CheckSensitive(content, ContentBoundary.ToolResult);
            Revalidate(observations);
            if (file.GetState() != initial || count != initial.Length || file.GetMode() != mode) throw Rejected(WorkspaceErrorCode.PathChanged);
        }
        Revalidate(observations);
        if (_fileSystem is IWorkspaceWriteFileSystem modes && modes.GetMode(observations[^1].Path) != mode) throw Rejected(WorkspaceErrorCode.PathChanged);
        Active();
        return new(original, content, hasBom, initial, mode, observations);
    }
    private sealed class VerifiedWorkspaceFile(byte[] bytes, string content, bool hasBom, WorkspaceNodeState state, UnixFileMode mode, List<PathObservation> observations)
    {
        internal byte[] Bytes { get; } = bytes;
        internal string Content { get; } = content;
        internal bool HasBom { get; } = hasBom;
        internal WorkspaceNodeState State { get; } = state;
        internal UnixFileMode Mode { get; } = mode;
        internal List<PathObservation> Observations { get; } = observations;
        public override string ToString() => "[VerifiedWorkspaceFile]";
    }
}