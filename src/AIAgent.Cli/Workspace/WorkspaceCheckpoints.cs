using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles : ICheckpointWorkspace
{
    Task<CheckpointOperationResult> ICheckpointWorkspace.ListAsync(CancellationToken token) =>
        RunAsync((operation, cancellation) => CheckpointQueryAsync(null, operation, cancellation), CheckpointEnd, token);
    Task<CheckpointOperationResult> ICheckpointWorkspace.ShowAsync(long id, CancellationToken token) =>
        RunAsync((operation, cancellation) => CheckpointQueryAsync(id, operation, cancellation), CheckpointEnd, token);
    Task<CheckpointOperationResult> ICheckpointWorkspace.RestoreAsync(long id, ICheckpointApproval approval, CancellationToken token) =>
        RunAsync((operation, cancellation) => RestoreCheckpointAsync(id, approval, operation, cancellation), CheckpointEnd, token);
    Task<CheckpointOperationResult> ICheckpointWorkspace.ForgetAsync(long id, ICheckpointApproval approval, CancellationToken token) =>
        RunAsync((operation, cancellation) => ForgetCheckpointAsync(id, approval, operation, cancellation), CheckpointEnd, token);
    void ICheckpointWorkspace.Clear() { lock (_gate) _checkpoints.Clear(); }

    private static CheckpointOperationResult CheckpointEnd(WorkspaceStatus status, WorkspaceErrorCode code) =>
        new(status == WorkspaceStatus.Cancelled ? "turn_cancelled" : code == WorkspaceErrorCode.Unavailable ? "checkpoint_unavailable" : WorkspaceToolProjection.Code(code));

    private Task<CheckpointOperationResult> CheckpointQueryAsync(long? id, WorkspaceOperation operation, CancellationToken token)
    {
        CheckActive(operation.Generation, token);
        lock (_gate)
        {
            if (id is <= 0) return Task.FromResult(new CheckpointOperationResult("invalid_checkpoint_id"));
            string display;
            if (id is { } number)
            {
                var entry = _checkpoints.Find(number);
                if (entry is null) return Task.FromResult(new CheckpointOperationResult("checkpoint_not_found"));
                if (!FileChangePreviewBuilder.TryCreate(entry.Record.Kind, entry.Record.Path, entry.Record.CopyBefore(), entry.Record.CopyAfter(),
                    _policy!, token, out var preview, out var error)) return Task.FromResult(CheckpointEnd(WorkspaceStatus.Rejected, error));
                display = CheckpointMetadata(entry) + "這是原始修改的程序內歷史，不是目前檔案或復原核准。\n" +
                    preview!.DisplayBlock[..^FileChangePreviewBuilder.Prompt.Length];
            }
            else
            {
                display = "[程序內檢查點歷史] Available 不保證目前可復原；clear／換 Key／退出會丟失。\n" +
                    FormattableString.Invariant($"容量：{_checkpoints.Count}/{CheckpointLimits.MaxCheckpointCount} 筆；{_checkpoints.TotalBytes}/{CheckpointLimits.MaxCheckpointBytes} bytes（含預留）。\n");
                foreach (var entry in _checkpoints.Entries) display += CheckpointMetadata(entry);
            }
            return Task.FromResult(CheckpointDisplay(display, id));
        }
    }

    private static string CheckpointMetadata(CheckpointEntry entry)
    {
        var record = entry.Record;
        return FormattableString.Invariant($"{CheckpointIds.Display(record.Id)} {record.Kind} \"{FileChangePreviewBuilder.EscapePath(record.Path)}\"；CaptureVersion={record.CaptureVersion}；Scope=\"{FileChangePreviewBuilder.EscapePath(record.Scope)}\"；RulesRevision={record.RulesRevision}；before={(record.BeforeAbsent ? "Absent" : record.BeforeBytes.ToString(CultureInfo.InvariantCulture))}；after={record.AfterBytes} bytes；{entry.State}；{entry.ManualReason ?? "none"}\n");
    }

    private CheckpointOperationResult CheckpointDisplay(string display, long? id = null, string? path = null)
    {
        if (new UTF8Encoding(false, true).GetByteCount(display) > CheckpointLimits.MaxCheckpointDisplayBytes) return new("preview_too_large");
        CheckSensitive(display, ContentBoundary.WriteCandidate);
        return new("none", display, id, path);
    }

    private async Task<CheckpointOperationResult> ForgetCheckpointAsync(long id, ICheckpointApproval approval, WorkspaceOperation operation, CancellationToken token)
    {
        CheckActive(operation.Generation, token);
        if (id <= 0) return new("invalid_checkpoint_id");
        CheckpointEntry? entry; long revision; long generation;
        lock (_gate) { entry = _checkpoints.Find(id); revision = _checkpoints.StoreRevision; generation = _checkpoints.LifetimeGeneration; }
        if (entry is null) return new("checkpoint_not_found");
        var block = CheckpointMetadata(entry) + "忘記只丟棄此筆記憶體資料，不讀寫或刪除工作檔；尚未消費的鏈節可能使較早紀錄無法自動復原。\n" + CheckpointLimits.ForgetPrompt;
        var display = CheckpointDisplay(block, id, entry.Record.Path);
        if (display.ErrorCode != "none") return display;
        var decision = await ConfirmCheckpointAsync(approval, new(id, true, block), token);
        if (CheckpointDecision(decision) is { } denied) return denied;
        lock (_gate)
        {
            CheckActive(operation.Generation, token);
            return _checkpoints.Forget(id, revision, generation) ? new("none", checkpointId: id, relativePath: entry.Record.Path) : new("checkpoint_revision_conflict");
        }
    }

    private static async ValueTask<FileApprovalDecision> ConfirmCheckpointAsync(ICheckpointApproval approval, CheckpointApprovalPreview preview, CancellationToken token)
    {
        try
        {
            if (approval is null) return FileApprovalDecision.Unavailable;
            var decision = await approval.ConfirmAsync(preview, token);
            return Enum.IsDefined(decision) ? decision : FileApprovalDecision.Unavailable;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return FileApprovalDecision.Cancelled; }
        catch (Exception) { return FileApprovalDecision.Unavailable; }
    }
    private static CheckpointOperationResult? CheckpointDecision(FileApprovalDecision decision) => decision switch
    {
        FileApprovalDecision.Approved => null,
        FileApprovalDecision.Declined => new("approval_declined", decision: decision),
        FileApprovalDecision.Cancelled => new("turn_cancelled", decision: decision),
        FileApprovalDecision.EndOfInput => new("end_of_input", decision: decision),
        _ => new("checkpoint_unavailable", fatal: true, decision: FileApprovalDecision.Unavailable)
    };

    private async Task<CheckpointOperationResult> RestoreCheckpointAsync(long id, ICheckpointApproval approval, WorkspaceOperation operation, CancellationToken token)
    {
        if (id <= 0) return new("invalid_checkpoint_id");
        var entry = _checkpoints.Find(id);
        if (entry is null) return new("checkpoint_not_found");
        var record = entry.Record;
        if (_executionPolicy.Mode != AgentMode.Execute) return new("mode_restricted");
        if (entry.State == CheckpointState.Restored) return new("checkpoint_already_restored");
        if (entry.State != CheckpointState.Available || record.Kind != FileChangeKind.Replace || entry.Anchor is null)
            return CheckpointManual(entry);
        if (_checkpoints.HasLater(entry)) return new("checkpoint_order");
        if (!_executionPolicy.Recovery.CanPublish || _executionPolicy.ProcessSessionStopped) return new("checkpoint_unavailable");
        var anchor = entry.Anchor;
        var revision = _checkpoints.StoreRevision;
        var lifetime = _checkpoints.LifetimeGeneration;
        string? stagedPath = null;
        WorkspaceNodeState? stagedIdentity = null;
        List<PathObservation>? parents = null;
        var publishStarted = false;
        var readbackStarted = false;
        var owned = false;
        VerifiedWorkspaceFile? actual = null;
        WorkspaceWriteResult write = WorkspaceWriteResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.Unavailable);
        string? rejection = null;
        FileApprovalDecision? decision = null;
        try
        {
            var path = ValidateArgument(record.Path, true);
            await CheckRulesOperationAsync(path, false, operation, token);
            await ValidateCheckpointTargetAsync(entry, anchor, operation, token);
            await ValidateCheckpointGuardAsync(path, anchor.Guard, operation, token);
            var before = record.CopyBefore();
            if (!FileChangePreviewBuilder.TryCreate(FileChangeKind.Replace, path, record.CopyAfter(), before, _policy!, token, out var diff, out var previewError))
                throw Rejected(previewError);
            var block = CheckpointMetadata(entry) + FormattableString.Invariant($"目前規則：{operation.RulesStamp!.RulesRevision}；Scope=\"{FileChangePreviewBuilder.EscapePath(operation.RulesStamp.Scope)}\"；mode={(int)anchor.Mode}→{(int)record.BeforeMode}。\n") +
                "只恢復此檔完整 bytes／BOM 及普通 mode；不恢復 Git index、owner、ACL、xattr、時間戳、硬連結或程序副作用。\n" +
                diff!.DisplayBlock[..^FileChangePreviewBuilder.Prompt.Length] + CheckpointLimits.RestorePrompt;
            var projected = CheckpointDisplay(block, id, path);
            if (projected.ErrorCode != "none") return projected;
            decision = await ConfirmCheckpointAsync(approval, new(id, false, block), token);
            if (CheckpointDecision(decision.Value) is { } denied) return denied;
            RequireCheckpointCandidate(entry, revision, lifetime, operation, token);
            await CheckRulesResultAsync(operation, token);
            await ValidateCheckpointTargetAsync(entry, anchor, operation, token);
            await ValidateCheckpointGuardAsync(path, anchor.Guard, operation, token);
            if (_fileSystem is not IWorkspaceWriteFileSystem writeSystem) throw Rejected(WorkspaceErrorCode.Unavailable);
            parents = InspectWriteParents(path, operation);
            var slash = path.LastIndexOf('/');
            var temporaryRelative = (slash < 0 ? "" : path[..(slash + 1)]) + ".aiagent-edit-" + Guid.NewGuid().ToString("N") + ".tmp";
            _ = ValidateArgument(temporaryRelative, true);
            stagedPath = Path.Combine(_root, temporaryRelative);
            CheckSensitive(stagedPath, ContentBoundary.WriteCandidate);
            CheckSensitive(Path.Combine(_root, path), ContentBoundary.WriteCandidate);
            await using (var staged = writeSystem.CreateStagedFile(stagedPath))
            {
                owned = true; stagedIdentity = staged.GetState();
                if (stagedIdentity.Value.Kind != WorkspaceNodeKind.File || writeSystem.GetMode(stagedPath) != (UnixFileMode.UserRead | UnixFileMode.UserWrite)) throw Rejected(WorkspaceErrorCode.PathChanged);
                CheckActive(operation.Generation, token);
                await staged.Stream.WriteAsync(before, token); staged.FlushToDisk();
                CheckActive(operation.Generation, token);
                staged.SetMode(record.BeforeMode & (UnixFileMode)511); staged.FlushToDisk();
                CheckActive(operation.Generation, token);
            }
            var stagedRead = await ReadVerifiedFileAsync(temporaryRelative, operation, token);
            RequireWrittenFile(stagedRead, stagedIdentity.Value, before, record.BeforeMode);
            await CheckRulesResultAsync(operation, token);
            await ValidateCheckpointTargetAsync(entry, anchor, operation, token);
            await ValidateCheckpointGuardAsync(path, anchor.Guard, operation, token);
            RevalidateWriteParents(parents);
            if (writeSystem.Inspect(stagedPath) != stagedRead.State || writeSystem.GetMode(stagedPath) != record.BeforeMode) throw Rejected(WorkspaceErrorCode.PathChanged);
            CheckSensitive(block, ContentBoundary.WriteCandidate);
            CheckSensitive(FileChangePreviewBuilder.Decode(before, out _), ContentBoundary.WriteCandidate);
            lock (_gate)
            {
                RequireCheckpointCandidate(entry, revision, lifetime, operation, token);
                if (!_executionPolicy.Recovery.CanPublish) throw Rejected(WorkspaceErrorCode.CheckpointUnavailable);
                publishStarted = true;
            }
            writeSystem.PublishReplacement(stagedPath, Path.Combine(_root, path));
            readbackStarted = true;
            actual = await ReadVerifiedFileAsync(path, operation, CancellationToken.None, settling: true);
            RequireWrittenFile(actual, stagedIdentity.Value, before, record.BeforeMode);
            write = WorkspaceWriteResult.Success(new(path, before.LongLength, actual.HasBom));
        }
        catch (Exception exception)
        {
            if (publishStarted)
            {
                write = WorkspaceWriteResult.Unconfirmed();
                if (!readbackStarted)
                {
                    try { _ = await ReadVerifiedFileAsync(record.Path, operation, CancellationToken.None, settling: true); }
                    catch (Exception) { }
                }
            }
            else
            {
                if (exception is GitFailure git) rejection = git.Code;
                else if (exception is CheckpointCandidateFailure) rejection = "checkpoint_revision_conflict";
                else if (IsInactive(operation.Generation, token) || exception is OperationCanceledException) rejection = "turn_cancelled";
                else
                {
                    var failure = ClassifyWriteFailure(exception, operation);
                    rejection = failure.Code is WorkspaceErrorCode.Conflict or WorkspaceErrorCode.PathChanged or WorkspaceErrorCode.NotFound or WorkspaceErrorCode.WrongEntryType
                        ? "checkpoint_conflict" : WorkspaceToolProjection.Code(failure.Code);
                }
                if (rejection is "checkpoint_conflict" or "checkpoint_git_conflict" or "symbolic_link" or "sensitive_path" or "sensitive_content" or "workspace_unavailable" or "invalid_utf8" or "binary_content" or "file_too_large")
                {
                    lock (_gate) _checkpoints.MarkPathManual(record.Path, rejection);
                    _snapshots.Remove(record.Path);
                    if (rejection == "workspace_unavailable") _checkpoints.MarkAllManual(rejection);
                }
            }
        }
        if (owned)
        {
            try
            {
                if (parents is null || stagedPath is null || stagedIdentity is null) throw new IOException("暫存所有權無法確認");
                RevalidateWriteParents(parents);
                var state = _fileSystem.Inspect(stagedPath);
                if (state.Kind != WorkspaceNodeKind.Missing)
                {
                    if (!SameIdentity(state, stagedIdentity.Value)) throw new IOException("暫存身分已變更");
                    ((IWorkspaceWriteFileSystem)_fileSystem).DeleteOwnedStagedFile(stagedPath);
                }
            }
            catch (Exception) { write = write.WithCleanupFailure(); rejection = "cleanup_failure"; }
        }
        if (publishStarted)
        {
            _snapshots.Remove(record.Path);
            lock (_gate)
            {
                var current = lifetime == _checkpoints.LifetimeGeneration;
                if (current)
                {
                    if (write.WriteState == FileWriteState.Verified && actual is not null)
                        _checkpoints.Restored(entry, new(actual.State, actual.Mode, CheckpointParents(actual.Observations.Take(actual.Observations.Count - 1)), anchor.Guard));
                    else _checkpoints.MarkPathManual(record.Path, "source_unconfirmed");
                }
                _executionPolicy.Recovery.Record(id, record.Path, write, current);
            }
            if (actual is not null) await CheckRulesAfterPublicationAsync(record.Path, actual.State, operation);
            rejection = write.CleanupFailed ? "cleanup_failure" : write.WriteState == FileWriteState.Verified ? "none" : "write_unconfirmed";
        }
        var fatal = write.CleanupFailed || write.WriteState == FileWriteState.Unconfirmed || rejection is "checkpoint_unavailable" or "cleanup_failure" or "process_cleanup_failed" or "process_unconfirmed" or "io_failure";
        return new(rejection ?? "checkpoint_unavailable", checkpointId: id, relativePath: record.Path, writeState: write.WriteState,
            verifiedByteCount: write.VerifiedByteCount, verifiedHasUtf8Bom: write.VerifiedHasUtf8Bom, cleanupFailed: write.CleanupFailed,
            fatal: fatal, decision: decision);
    }

    private CheckpointOperationResult CheckpointManual(CheckpointEntry entry)
    {
        var display = CheckpointDisplay(CheckpointMetadata(entry) + "需人工核對此單一檔案；新建檔由使用者自行確認是否刪除。沒有工作區全回復或 index 復原。\n", entry.Record.Id, entry.Record.Path);
        return display.ErrorCode != "none" ? display : new("checkpoint_manual_required", display.DisplayBlock, entry.Record.Id, entry.Record.Path);
    }
    private void RequireCheckpointCandidate(CheckpointEntry entry, long revision, long lifetime, WorkspaceOperation operation, CancellationToken token)
    {
        CheckActive(operation.Generation, token);
        if (_checkpoints.StoreRevision != revision || _checkpoints.LifetimeGeneration != lifetime || !ReferenceEquals(_checkpoints.Find(entry.Record.Id), entry))
            throw new CheckpointCandidateFailure();
    }
    private async Task ValidateCheckpointTargetAsync(CheckpointEntry entry, CheckpointRecoveryAnchor anchor, WorkspaceOperation operation, CancellationToken token)
    {
        foreach (var parent in anchor.Parents)
        {
            var current = _fileSystem.Inspect(parent.Path);
            if (current.Kind == WorkspaceNodeKind.Link) throw Rejected(WorkspaceErrorCode.SymbolicLink);
            if (parent.Path == _root && !SameIdentity(current, parent.State))
            { _snapshots.InvalidateAllForRootFailure(); throw Rejected(WorkspaceErrorCode.WorkspaceUnavailable); }
            if (!SameIdentity(current, parent.State)) throw Rejected(WorkspaceErrorCode.Conflict);
        }
        var actual = await ReadVerifiedFileAsync(entry.Record.Path, operation, token);
        if (!SameIdentity(actual.State, anchor.Target) || actual.Mode != anchor.Mode || !entry.Record.AfterEquals(actual.Bytes)) throw Rejected(WorkspaceErrorCode.Conflict);
    }
    private async Task ValidateCheckpointGuardAsync(string path, CheckpointGitGuard expected, WorkspaceOperation operation, CancellationToken token)
    {
        var root = InspectRoot();
        var gitdir = _fileSystem.Inspect(Path.Combine(_root, ".git"));
        if (expected.IsRootRepository != (gitdir.Kind != WorkspaceNodeKind.Missing) ||
            !expected.MatchesRootObservation(GitState(root), gitdir.Kind == WorkspaceNodeKind.Missing ? "Absent" : GitState(gitdir)))
            throw new GitFailure("checkpoint_git_conflict");
        var observed = await CaptureCheckpointGitGuardAsync(path, operation, token);
        if (!expected.SameAs(observed)) throw new GitFailure("checkpoint_git_conflict");
    }
    private static IReadOnlyList<CheckpointParent> CheckpointParents(IEnumerable<PathObservation> observations) =>
        Array.AsReadOnly(observations.Select(x => new CheckpointParent(x.Path, x.State)).ToArray());
    private sealed class CheckpointCandidateFailure : Exception;
}