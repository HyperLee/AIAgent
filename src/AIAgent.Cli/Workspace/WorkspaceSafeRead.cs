using System.Text;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    /// <summary>完整關閉後才回傳；內部查核不發布快照或模型資料。</summary>
    private async Task<VerifiedWorkspaceFile> ReadVerifiedFileAsync(string path, WorkspaceOperation operation,
        CancellationToken token, bool settling = false, DiscoveryReadBudget? budget = null,
        WorkspaceNodeState? expected = null, Action<UnixFileMode>? observeMode = null, int maxBytes = WorkspaceLimits.MaxFileBytes)
    {
        void Active() { if (!settling) CheckActive(operation.Generation, token); }
        Active();
        var observations = InspectPath(path, WorkspaceNodeKind.File, operation);
        var initial = observations[^1].State;
        if (expected is { } observed && initial != observed) throw Rejected(WorkspaceErrorCode.PathChanged);
        if (initial.Length > maxBytes) throw Rejected(WorkspaceErrorCode.FileTooLarge);
        Active(); Revalidate(observations);
        byte[] original;
        UnixFileMode mode;
        string content;
        bool hasBom;
        await using (var file = _fileSystem.OpenRead(observations[^1].Path))
        {
            mode = file.GetMode(); observeMode?.Invoke(mode);
            if (file.GetState() != initial) throw Rejected(WorkspaceErrorCode.PathChanged);
            var buffer = new byte[maxBytes + 1]; var count = 0;
            while (count < buffer.Length)
            {
                Active();
                var length = Math.Min(buffer.Length - count, budget?.Remaining ?? int.MaxValue);
                if (length == 0) throw new DiscoveryBudgetExhausted();
                var received = await file.Stream.ReadAsync(buffer.AsMemory(count, length), token);
                budget?.Consume(received);
                Active();
                if (received == 0) break;
                count += received;
                if (count > maxBytes) throw Rejected(WorkspaceErrorCode.FileTooLarge);
            }
            Revalidate(observations);
            if (file.GetState() != initial || count != initial.Length || file.GetMode() != mode) throw Rejected(WorkspaceErrorCode.PathChanged);
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