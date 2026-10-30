using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    public Task<WorkspaceWriteResult> CreateFileAsync(string path, string content, CancellationToken cancellationToken) =>
        _delegationScope is not null ? Task.FromResult(WorkspaceWriteResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.ModeRestricted)) :
        RunAsync((operation, token) => WriteCoreAsync(path, content, null, FileChangeKind.Create, operation, token),
            (status, code) => WorkspaceWriteResult.End(status, code), cancellationToken);
    public Task<WorkspaceWriteResult> ReplaceTextAsync(string path, string oldText, string newText, CancellationToken cancellationToken) =>
        _delegationScope is not null ? Task.FromResult(WorkspaceWriteResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.ModeRestricted)) :
        RunAsync((operation, token) => WriteCoreAsync(path, newText, oldText, FileChangeKind.Replace, operation, token),
            (status, code) => WorkspaceWriteResult.End(status, code), cancellationToken);

    private async Task<WorkspaceWriteResult> WriteCoreAsync(string rawPath, string text, string? oldText,
        FileChangeKind kind, WorkspaceOperation operation, CancellationToken token)
    {
        string? path = null;
        string? stagedPath = null;
        WorkspaceNodeState? ownedState = null;
        List<PathObservation>? parents = null;
        var ownsStaged = false;
        var publishStarted = false;
        var readbackStarted = false;
        var candidateReady = false;
        var baselineChecked = false;
        var checkingBaseline = false;
        CheckpointReservation? checkpoint = null;
        CheckpointGitGuard? checkpointGuard = null;
        string? checkpointManualReason = null;
        byte[] checkpointBefore = [];
        byte[] checkpointAfter = [];
        UnixFileMode checkpointMode = 0;
        WorkspaceNodeState? checkpointOriginal = null;
        VerifiedWorkspaceFile? publishedFile = null;
        Guid sourceOperationId = Guid.NewGuid();
        long? predecessorId = null;
        WorkspaceWriteResult result;
        try
        {
            CheckActive(operation.Generation, token);
            if (!_executionPolicy.Allows(ToolCapability.WorkspaceWrite)) throw Rejected(WorkspaceErrorCode.ModeRestricted);
            path = ValidateArgument(rawPath, true);
            if (kind == FileChangeKind.Replace) baselineChecked = _snapshots.TryGet(path, out _);
            await CheckRulesOperationAsync(path, false, operation, token);
            BeginTaskOperation(operation, kind == FileChangeKind.Create ? "create_file" : "replace_text", path, write: true);
            var newBytes = EncodeEdit(text, token);
            if (oldText is not null) _ = EncodeEdit(oldText, token);
            if (_fileSystem is not IWorkspaceWriteFileSystem writeSystem)
                throw new WorkspaceFailure(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable);
            WorkspaceReadSnapshot? snapshot = null;
            if (kind == FileChangeKind.Replace)
            {
                baselineChecked = _snapshots.TryGet(path, out snapshot);
                checkingBaseline = baselineChecked;
            }
            // A retained read baseline includes the path leading to the target.
            // Detect and invalidate parent changes before constructing a candidate.
            parents = InspectWriteParents(path, operation);
            checkingBaseline = false;
            byte[] original = [];
            UnixFileMode finalMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if (kind == FileChangeKind.Create) CheckMissingTarget(path, false);
            else
            {
                if (snapshot is null) throw Rejected(WorkspaceErrorCode.SnapshotRequired);
                original = snapshot.CopyOriginalBytes();
                var source = FileChangePreviewBuilder.Decode(original, out var hasBom);
                CheckSensitive(source, ContentBoundary.WriteCandidate);
                checkingBaseline = true;
                await CheckBaselineAsync(path, snapshot, operation, token);
                checkingBaseline = false;
                finalMode = snapshot.OriginalMode;
                if ((finalMode & (UnixFileMode.SetUser | UnixFileMode.SetGroup | UnixFileMode.StickyBit)) != 0)
                    throw Rejected(WorkspaceErrorCode.UnsupportedMetadata);
                if (oldText!.Length == 0) throw Rejected(WorkspaceErrorCode.EmptyOldText);
                var first = source.IndexOf(oldText, StringComparison.Ordinal);
                CheckActive(operation.Generation, token);
                if (first < 0) throw Rejected(WorkspaceErrorCode.TextNotFound);
                if (source.IndexOf(oldText, first + 1, StringComparison.Ordinal) >= 0) throw Rejected(WorkspaceErrorCode.AmbiguousMatch);
                var prefixBytes = new UTF8Encoding(false, true).GetByteCount(source.AsSpan(0, first));
                var removedBytes = new UTF8Encoding(false, true).GetByteCount(oldText);
                var count = original.Length - removedBytes + newBytes.Length;
                if (count > WorkspaceLimits.MaxFileBytes) throw Rejected(WorkspaceErrorCode.FileTooLarge);
                var offset = prefixBytes + (hasBom ? 3 : 0);
                var combined = new byte[count];
                original.AsSpan(0, offset).CopyTo(combined);
                newBytes.CopyTo(combined, offset);
                original.AsSpan(offset + removedBytes).CopyTo(combined.AsSpan(offset + newBytes.Length));
                newBytes = combined;
                _ = FileChangePreviewBuilder.Decode(newBytes, out var newBom);
                if (newBom != hasBom) throw Rejected(WorkspaceErrorCode.BomChangeNotAllowed);
                if (original.AsSpan().SequenceEqual(newBytes)) throw Rejected(WorkspaceErrorCode.NoChange);
            }
            if (newBytes.Length > WorkspaceLimits.MaxFileBytes) throw Rejected(WorkspaceErrorCode.FileTooLarge);
            var candidateText = FileChangePreviewBuilder.Decode(newBytes, out _);
            CheckSensitive(candidateText, ContentBoundary.WriteCandidate);
            checkpointBefore = original; checkpointAfter = newBytes; checkpointMode = finalMode;
            if (kind == FileChangeKind.Replace) checkpointOriginal = _fileSystem.Inspect(Path.Combine(_root, path));
            lock (_gate)
            {
                CheckActive(operation.Generation, token);
                checkpoint = _checkpoints.Reserve(original, newBytes);
            }
            if (checkpoint is null) throw Rejected(_checkpoints.IdsExhausted ? WorkspaceErrorCode.CheckpointUnavailable : WorkspaceErrorCode.CheckpointLimit);
            try { checkpointGuard = await CaptureCheckpointGitGuardAsync(path, operation, token); }
            catch (GitFailure failure) when (failure.Code == "checkpoint_git_conflict") { throw Rejected(WorkspaceErrorCode.CheckpointGitConflict); }
            catch (GitFailure failure) when (failure.Code is "sensitive_content" or "sensitive_path") { throw Rejected(WorkspaceErrorCode.SensitiveContent); }
            catch (GitFailure failure) when (failure.Code is "process_cleanup_failed" or "process_unconfirmed" or "cleanup_failure") { throw new WorkspaceFailure(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable); }
            catch (GitFailure) { checkpointManualReason = "git_unverified"; }
            catch (OperationCanceledException) { throw new WorkspaceFailure(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None); }
            RequireCheckpointReservation(checkpoint);
            if (checkpointGuard is not null && checkpointOriginal is { } originalState)
            {
                var latest = _checkpoints.Latest(path);
                if (latest is { State: CheckpointState.Available, Anchor: { } anchor } &&
                    latest.Record.AfterEquals(original) && SameIdentity(anchor.Target, originalState) && anchor.Mode == finalMode &&
                    anchor.Guard.SameAs(checkpointGuard) && SameCheckpointParents(anchor.Parents, CheckpointParents(parents))) predecessorId = latest.Record.Id;
            }
            if (!FileChangePreviewBuilder.TryCreate(kind, path, original, newBytes, _policy!, token, out var preview, out var previewError))
                throw Rejected(previewError);
            if (checkpointManualReason is not null)
            {
                var block = preview!.DisplayBlock[..^FileChangePreviewBuilder.Prompt.Length] + "檢查點：git_unverified；此筆只保留程序內歷史，沒有自動復原資格。\n" + FileChangePreviewBuilder.Prompt;
                var bytes = Encoding.UTF8.GetByteCount(block);
                if (bytes > WorkspaceLimits.MaxPreviewBytes) throw Rejected(WorkspaceErrorCode.PreviewTooLarge);
                CheckSensitive(block, ContentBoundary.WriteCandidate);
                preview = new(kind, path, original.LongLength, newBytes.LongLength, preview.OriginalHasUtf8Bom, preview.CandidateHasUtf8Bom, block, bytes);
            }
            var candidate = new WriteCandidate(this, operation.Generation, kind, path, original, newBytes, preview!);
            sourceOperationId = candidate.OperationIdentity;
            candidateReady = true;
            CheckActive(operation.Generation, token);
            FileApprovalDecision decision;
            try { decision = await _approval.ConfirmAsync(candidate.Preview, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { throw new WorkspaceFailure(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable); }
            if (!Enum.IsDefined(decision) || decision == FileApprovalDecision.Unavailable)
                throw new WorkspaceFailure(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable);
            CheckActive(operation.Generation, token);
            if (decision == FileApprovalDecision.Declined) throw Rejected(WorkspaceErrorCode.ApprovalDeclined);
            if (decision is FileApprovalDecision.Cancelled or FileApprovalDecision.EndOfInput) throw new WorkspaceFailure(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None);
            if (decision != FileApprovalDecision.Approved) throw new WorkspaceFailure(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable);
            RequireCheckpointReservation(checkpoint);
            await CheckRulesResultAsync(operation, token);
            if (checkpointGuard is not null) await ValidateWriteCheckpointGuardAsync(path, checkpointGuard, operation, token);
            RevalidateWriteParents(parents);
            if (snapshot is null) CheckMissingTarget(path, true);
            else
            {
                checkingBaseline = true;
                await CheckBaselineAsync(path, snapshot, operation, token);
                checkingBaseline = false;
            }
            candidate.CheckOwner(this, operation.Generation);
            RequireCheckpointReservation(checkpoint);
            CheckSensitive(candidateText, ContentBoundary.WriteCandidate);
            CheckSensitive(candidate.Preview.DisplayBlock, ContentBoundary.WriteCandidate);
            CheckActive(operation.Generation, token);
            var slash = path.LastIndexOf('/');
            var temporaryRelative = (slash < 0 ? "" : path[..(slash + 1)]) + ".aiagent-edit-" + Guid.NewGuid().ToString("N") + ".tmp";
            _ = ValidateArgument(temporaryRelative, true);
            stagedPath = Path.Combine(_root, temporaryRelative);
            CheckSensitive(stagedPath, ContentBoundary.WriteCandidate);
            CheckSensitive(Path.Combine(_root, path), ContentBoundary.WriteCandidate);
            CheckActive(operation.Generation, token);
            await using (var staged = writeSystem.CreateStagedFile(stagedPath))
            {
                ownsStaged = true;
                ownedState = staged.GetState();
                if (ownedState.Value.Kind != WorkspaceNodeKind.File ||
                    writeSystem.GetMode(stagedPath) != (UnixFileMode.UserRead | UnixFileMode.UserWrite)) throw Rejected(WorkspaceErrorCode.PathChanged);
                CheckActive(operation.Generation, token);
                await staged.Stream.WriteAsync(candidate.CopyCandidateBytes(), token);
                CheckActive(operation.Generation, token);
                staged.FlushToDisk();
                CheckActive(operation.Generation, token);
                if (kind == FileChangeKind.Replace) staged.SetMode(finalMode & (UnixFileMode)511);
                staged.FlushToDisk();
                CheckActive(operation.Generation, token);
            }
            var stagedRead = await ReadVerifiedFileAsync(temporaryRelative, operation, token);
            RequireWrittenFile(stagedRead, ownedState.Value, newBytes, finalMode);
            await CheckRulesResultAsync(operation, token);
            if (checkpointGuard is not null) await ValidateWriteCheckpointGuardAsync(path, checkpointGuard, operation, token);
            RevalidateWriteParents(parents);
            if (snapshot is null) CheckMissingTarget(path, true);
            else
            {
                checkingBaseline = true;
                await CheckBaselineAsync(path, snapshot, operation, token);
                checkingBaseline = false;
            }
            var stagedNow = writeSystem.Inspect(stagedPath);
            if (stagedNow != stagedRead.State || writeSystem.GetMode(stagedPath) != finalMode)
                throw Rejected(WorkspaceErrorCode.PathChanged);
            CheckSensitive(candidateText, ContentBoundary.WriteCandidate);
            CheckSensitive(stagedPath, ContentBoundary.WriteCandidate);
            CheckSensitive(Path.Combine(_root, path), ContentBoundary.WriteCandidate);
            lock (_gate)
            {
                CheckActive(operation.Generation, token);
                candidate.CheckOwner(this, operation.Generation);
                RequireCheckpointReservation(checkpoint);
                publishStarted = true;
            }
            try
            {
                if (kind == FileChangeKind.Create) writeSystem.PublishNew(stagedPath, Path.Combine(_root, path));
                else writeSystem.PublishReplacement(stagedPath, Path.Combine(_root, path));
            }
            catch (WorkspacePublishException exception) when (kind == FileChangeKind.Create &&
                exception.Code is WorkspaceErrorCode.Conflict or WorkspaceErrorCode.PublishUnsupported)
            { publishStarted = false; throw Rejected(exception.Code); }
            // Once publication starts, cancellation and generation changes cannot suppress the facts.
            readbackStarted = true;
            var actual = await ReadVerifiedFileAsync(path, operation, CancellationToken.None, settling: true);
            RequireWrittenFile(actual, ownedState.Value, newBytes, finalMode);
            publishedFile = actual;
            result = WorkspaceWriteResult.Success(new(path, newBytes.LongLength, actual.HasBom));
        }
        catch (Exception exception)
        {
            if (publishStarted)
            {
                result = WorkspaceWriteResult.Unconfirmed();
                if (!readbackStarted && path is not null)
                {
                    try { _ = await ReadVerifiedFileAsync(path, operation, CancellationToken.None, settling: true); }
                    catch (Exception) { /* One bounded observation cannot prove this operation succeeded. */ }
                }
            }
            else if (exception is WorkspaceFailure { Code: WorkspaceErrorCode.Unavailable })
                result = WorkspaceWriteResult.End(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, true);
            else if (IsInactive(operation.Generation, token) || exception is OperationCanceledException && token.IsCancellationRequested)
                result = WorkspaceWriteResult.End(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None);
            else
            {
                var failure = ClassifyWriteFailure(exception, operation);
                var code = failure.Code;
                if (baselineChecked && code is WorkspaceErrorCode.NotFound or WorkspaceErrorCode.WrongEntryType or WorkspaceErrorCode.PathChanged)
                    code = WorkspaceErrorCode.Conflict;
                result = WorkspaceWriteResult.End(failure.Status, code,
                    candidateReady || checkingBaseline || code is WorkspaceErrorCode.Conflict or WorkspaceErrorCode.CheckpointLimit or WorkspaceErrorCode.CheckpointGitConflict or WorkspaceErrorCode.CheckpointManualRequired || baselineChecked && code is WorkspaceErrorCode.SymbolicLink or WorkspaceErrorCode.SensitivePath);
            }
        }
        if (path is not null && (publishStarted || checkingBaseline || result.ErrorCode is WorkspaceErrorCode.Conflict or WorkspaceErrorCode.PathChanged or
            WorkspaceErrorCode.SymbolicLink or WorkspaceErrorCode.SensitiveContent or WorkspaceErrorCode.SensitivePath or WorkspaceErrorCode.CheckpointGitConflict or WorkspaceErrorCode.CheckpointManualRequired)) _snapshots.Remove(path);
        if (ownsStaged)
        {
            try
            {
                if (parents is null || stagedPath is null || ownedState is null) throw new IOException("暫存所有權無法確認");
                RevalidateWriteParents(parents);
                var state = _fileSystem.Inspect(stagedPath);
                if (state.Kind != WorkspaceNodeKind.Missing)
                {
                    if (!SameIdentity(state, ownedState.Value)) throw new IOException("暫存身分已變更");
                    ((IWorkspaceWriteFileSystem)_fileSystem).DeleteOwnedStagedFile(stagedPath);
                }
            }
            catch (Exception) { result = result.WithCleanupFailure(); }
        }
        if (checkpoint is not null)
        {
            if (result.WriteState == FileWriteState.NotApplied) _checkpoints.Release(checkpoint);
            else if (path is not null && parents is not null && _checkpoints.IsCurrent(checkpoint))
            {
                try
                {
                    if (result.WriteState == FileWriteState.Unconfirmed) checkpointManualReason = "source_unconfirmed";
                    else if (kind == FileChangeKind.Create) checkpointManualReason = "created_file";
                    if (result.WriteState == FileWriteState.Verified && checkpointGuard is not null)
                    {
                        operation.CheckpointSettling = true;
                        try { await ValidateWriteCheckpointGuardAsync(path, checkpointGuard, operation, token); }
                        catch (WorkspaceFailure failure) when (failure.Code is WorkspaceErrorCode.SensitiveContent or WorkspaceErrorCode.Unavailable)
                        { checkpointManualReason ??= "git_unverified"; result = result.WithCheckpointFailure(); }
                        catch (Exception) { checkpointManualReason ??= "git_unverified"; }
                        finally { operation.CheckpointSettling = false; }
                    }
                    var capturedParents = CheckpointParents(parents);
                    var rules = operation.RulesStamp!;
                    var record = new CheckpointRecord(checkpoint.Id, kind, path, checkpointBefore, checkpointAfter, checkpointMode, checkpointMode,
                        checkpointOriginal, publishedFile?.State, capturedParents, rules.RulesRevision, rules.Scope, sourceOperationId, predecessorId);
                    var anchor = checkpointManualReason is null && publishedFile is not null && checkpointGuard is not null
                        ? new CheckpointRecoveryAnchor(publishedFile.State, checkpointMode, capturedParents, checkpointGuard) : null;
                    lock (_gate)
                    {
                        if (_checkpoints.IsCurrent(checkpoint))
                        {
                            if (predecessorId is null && _checkpoints.Latest(path) is not null) _checkpoints.MarkPathManual(path, "chain_broken");
                            _ = _checkpoints.Complete(checkpoint, record, anchor, checkpointManualReason ?? (anchor is null ? "git_unverified" : null));
                        }
                    }
                }
                catch (Exception) { result = result.WithCheckpointFailure(); }
            }
        }
        if (path is not null && result.ErrorCode is WorkspaceErrorCode.Conflict or WorkspaceErrorCode.CheckpointGitConflict or WorkspaceErrorCode.SymbolicLink or WorkspaceErrorCode.SensitivePath)
            _checkpoints.MarkPathManual(path, "checkpoint_conflict");
        if (publishedFile is not null && path is not null)
        {
            await CheckRulesAfterPublicationAsync(path, publishedFile.State, operation);
            await CheckSkillsAfterPublicationAsync(path, publishedFile.State, operation);
        }
        return result;
    }

    private async Task ValidateWriteCheckpointGuardAsync(string path, CheckpointGitGuard guard, WorkspaceOperation operation, CancellationToken token)
    {
        try { await ValidateCheckpointGuardAsync(path, guard, operation, token); }
        catch (OperationCanceledException) { throw new WorkspaceFailure(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None); }
        catch (GitFailure failure)
        {
            throw new WorkspaceFailure(failure.Code is "process_cleanup_failed" or "process_unconfirmed" or "cleanup_failure" ? WorkspaceStatus.Failed : WorkspaceStatus.Rejected,
                failure.Code switch
                {
                    "checkpoint_git_conflict" => WorkspaceErrorCode.CheckpointGitConflict,
                    "sensitive_content" or "sensitive_path" => WorkspaceErrorCode.SensitiveContent,
                    "process_cleanup_failed" or "process_unconfirmed" or "cleanup_failure" => WorkspaceErrorCode.Unavailable,
                    _ => WorkspaceErrorCode.CheckpointManualRequired
                });
        }
    }

    private static bool SameCheckpointParents(IReadOnlyList<CheckpointParent> a, IReadOnlyList<CheckpointParent> b) =>
        a.Count == b.Count && a.Zip(b).All(x => x.First.Path == x.Second.Path && SameIdentity(x.First.State, x.Second.State));

    private void RequireCheckpointReservation(CheckpointReservation reservation)
    {
        lock (_gate)
            if (!_checkpoints.IsCurrent(reservation)) throw new WorkspaceFailure(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None);
    }

    private byte[] EncodeEdit(string text, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (text is null) throw Rejected(WorkspaceErrorCode.InvalidUtf8);
        if (text.Length > WorkspaceLimits.MaxEditArgumentCodeUnits) throw Rejected(WorkspaceErrorCode.EditTooLarge);
        CheckSensitive(text, ContentBoundary.ToolArguments);
        var encoding = new UTF8Encoding(false, true);
        try
        {
            if (encoding.GetByteCount(text) > WorkspaceLimits.MaxEditArgumentBytes) throw Rejected(WorkspaceErrorCode.EditTooLarge);
            if (text.Any(c => char.IsControl(c) && c is not ('\t' or '\r' or '\n'))) throw Rejected(WorkspaceErrorCode.BinaryContent);
            token.ThrowIfCancellationRequested();
            return encoding.GetBytes(text);
        }
        catch (EncoderFallbackException) { throw Rejected(WorkspaceErrorCode.InvalidUtf8); }
    }

    private List<PathObservation> InspectWriteParents(string path, WorkspaceOperation operation)
    {
        if (path == ".") throw Rejected(WorkspaceErrorCode.WrongEntryType);
        var slash = path.LastIndexOf('/');
        return InspectPath(slash < 0 ? "." : path[..slash], WorkspaceNodeKind.Directory, operation);
    }
    private void RevalidateWriteParents(List<PathObservation> parents)
    {
        var root = InspectRoot();
        if (!SameIdentity(root, parents[0].State)) throw Rejected(WorkspaceErrorCode.PathChanged);
        foreach (var parent in parents.Skip(1))
        {
            var current = _fileSystem.Inspect(parent.Path);
            if (current.Kind == WorkspaceNodeKind.Link) throw Rejected(WorkspaceErrorCode.SymbolicLink);
            if (!SameIdentity(current, parent.State)) throw Rejected(WorkspaceErrorCode.PathChanged);
        }
    }
    private void CheckMissingTarget(string path, bool afterCandidate)
    {
        var state = _fileSystem.Inspect(Path.Combine(_root, path));
        if (state.Kind == WorkspaceNodeKind.Missing) return;
        if (state.Kind == WorkspaceNodeKind.Link) throw Rejected(WorkspaceErrorCode.SymbolicLink);
        throw Rejected(afterCandidate ? WorkspaceErrorCode.Conflict :
            state.Kind == WorkspaceNodeKind.File ? WorkspaceErrorCode.AlreadyExists : WorkspaceErrorCode.WrongEntryType);
    }
    private async Task CheckBaselineAsync(string path, WorkspaceReadSnapshot baseline, WorkspaceOperation operation, CancellationToken token)
    {
        var actual = await ReadVerifiedFileAsync(path, operation, token);
        if (actual.State.Device != baseline.OriginalDevice || actual.State.Identity != baseline.OriginalIdentity ||
            actual.Mode != baseline.OriginalMode || !actual.Bytes.AsSpan().SequenceEqual(baseline.CopyOriginalBytes()))
            throw Rejected(WorkspaceErrorCode.Conflict);
    }
    private static bool SameIdentity(WorkspaceNodeState current, WorkspaceNodeState expected) =>
        current.Kind == expected.Kind && current.Device == expected.Device && current.Identity == expected.Identity;
    private static void RequireWrittenFile(VerifiedWorkspaceFile actual, WorkspaceNodeState expected, byte[] bytes, UnixFileMode mode)
    {
        if (!SameIdentity(actual.State, expected) || actual.Mode != mode || !actual.Bytes.AsSpan().SequenceEqual(bytes))
            throw Rejected(WorkspaceErrorCode.PathChanged);
    }
    private WorkspaceFailure ClassifyWriteFailure(Exception exception, WorkspaceOperation operation)
    {
        if (operation.RootChecked)
        {
            try { InspectRoot(); }
            catch (WorkspaceFailure root) { return root; }
        }
        return exception as WorkspaceFailure ?? exception switch
        {
            UnauthorizedAccessException => new(WorkspaceStatus.Failed, WorkspaceErrorCode.AccessDenied),
            FileNotFoundException or DirectoryNotFoundException => Rejected(WorkspaceErrorCode.PathChanged),
            DecoderFallbackException or EncoderFallbackException => Rejected(WorkspaceErrorCode.InvalidUtf8),
            PathTooLongException or ArgumentException or NotSupportedException => Rejected(WorkspaceErrorCode.InvalidPath),
            _ => new(WorkspaceStatus.Failed, WorkspaceErrorCode.IoFailure)
        };
    }
    private sealed class WriteCandidate(WorkspaceFiles owner, long generation, FileChangeKind kind,
        string path, byte[] original, byte[] candidate, FileChangePreview preview)
    {
        private readonly byte[] _original = original.ToArray();
        private readonly byte[] _candidate = candidate.ToArray();
        internal FileChangeKind Kind { get; } = kind;
        internal string Path { get; } = path;
        internal Guid OperationIdentity { get; } = Guid.NewGuid();
        internal FileChangePreview Preview { get; } = preview;
        internal byte[] CopyCandidateBytes() => _candidate.ToArray();
        internal void CheckOwner(WorkspaceFiles expectedOwner, long expectedGeneration)
        {
            if (!ReferenceEquals(owner, expectedOwner) || generation != expectedGeneration || _original.Length > WorkspaceLimits.MaxFileBytes)
                throw new InvalidOperationException("候選已失效");
        }
        public override string ToString() => "[WriteCandidate]";
    }
}