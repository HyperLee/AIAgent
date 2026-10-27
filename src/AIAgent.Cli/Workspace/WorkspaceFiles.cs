using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Workspace;

internal sealed class WorkspaceFilesFactory(FileChangeApprovalBridge? approvalBridge = null) : IWorkspaceFilesFactory
{
    public FileChangeApprovalBridge ApprovalBridge { get; } = approvalBridge ?? new();
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null) =>
        new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), ApprovalBridge.ForSession(session), executionPolicy);
    public override string ToString() => "[WorkspaceFilesFactory]";
}

/// <summary>唯一唯讀入口；每次重新驗證，不把模型要求或快照視為磁碟授權。</summary>
internal sealed partial class WorkspaceFiles : IWorkspaceFiles
{
    private readonly object _gate = new();
    private readonly string _root;
    private readonly IWorkspaceReadFileSystem _fileSystem;
    private readonly ReadSnapshotStore _snapshots;
    private readonly IFileChangeApproval _approval;
    private readonly AgentExecutionPolicy _executionPolicy;
    private readonly CheckpointStore _checkpoints;
    private readonly IGitReadAdapter? _checkpointGitAdapter;
    private readonly CancellationTokenSource _lifetime = new();
    private ISensitiveDataPolicy? _policy;
    private TaskCompletionSource? _inFlight;
    private Task? _disposal;
    private CancellationTokenSource? _activeCancellation;
    private long _activePlanGeneration;
    public IReadSnapshotStore Snapshots => _snapshots;
    public ICheckpointWorkspace Checkpoints => this;

    internal WorkspaceFiles(ConfirmedWorkspace workspace, ISensitiveDataPolicy policy, IWorkspaceReadFileSystem fileSystem,
        IFileChangeApproval? approval = null, AgentExecutionPolicy? executionPolicy = null, IGitReadAdapter? checkpointGitAdapter = null)
    {
        _root = workspace.RootPath;
        _policy = policy;
        _fileSystem = fileSystem;
        _approval = approval ?? UnavailableFileChangeApproval.Instance;
        _executionPolicy = executionPolicy ?? new(new PlanStore(policy));
        _checkpoints = new(_gate, _executionPolicy.Plans.CheckpointSequence);
        _checkpointGitAdapter = checkpointGitAdapter;
        _snapshots = new(_gate, CancelActiveOperation);
        _executionPolicy.Rules ??= new(this, policy, _executionPolicy);
        _executionPolicy.Skills ??= new(this, policy, _executionPolicy);
    }

    public Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken cancellationToken) =>
        RunAsync((operation, token) => ListCoreAsync(path, operation, token), WorkspaceListResult.End, cancellationToken);
    public Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken cancellationToken) =>
        RunAsync((operation, token) => ReadCoreAsync(path, operation, token), WorkspaceReadResult.End, cancellationToken);

    private Task<T> RunAsync<T>(Func<WorkspaceOperation, CancellationToken, Task<T>> operation,
        Func<WorkspaceStatus, WorkspaceErrorCode, T> end, CancellationToken external)
    {
        long generation;
        WorkspaceOperationLease? lease;
        lock (_gate)
        {
            if (_executionPolicy.Plans.ReviewTurnActive) return Task.FromResult(end(WorkspaceStatus.Rejected, WorkspaceErrorCode.ModeRestricted));
            if (_disposal is not null || _inFlight is not null) return Task.FromResult(end(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable));
            if (external.IsCancellationRequested) return Task.FromResult(end(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None));
            if (!_executionPolicy.WorkspaceOperations.TryAcquire(external, out lease))
                return Task.FromResult(end(WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable));
            _inFlight = new(TaskCreationOptions.RunContinuationsAsynchronously);
            generation = _snapshots.Generation;
            _activePlanGeneration = _executionPolicy.Plans.ExecutionGeneration;
        }
        return RunCoreAsync(operation, end, new WorkspaceOperation(generation, _activePlanGeneration) { Lease = lease! }, lease!);
    }

    private async Task<T> RunCoreAsync<T>(Func<WorkspaceOperation, CancellationToken, Task<T>> operation,
        Func<WorkspaceStatus, WorkspaceErrorCode, T> end, WorkspaceOperation state, WorkspaceOperationLease lease)
    {
        using var admitted = lease;
        using var borrowing = _executionPolicy.Plans.BorrowGeneration(state.PlanGeneration);
        T Complete(T result) { RecordTaskOperation(state, result); return result; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lease.CancellationToken, _lifetime.Token);
        lock (_gate) _activeCancellation = linked;
        try { return Complete(await operation(state, linked.Token)); }
        catch (Exception exception)
        {
            if (IsInactive(state.Generation, linked.Token)) return Complete(end(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None));
            Exception classified = exception;
            if (state.RootChecked && (exception is not (WorkspaceFailure or OperationCanceledException) ||
                exception is WorkspaceFailure { Code: WorkspaceErrorCode.NotFound or WorkspaceErrorCode.PathChanged }))
            {
                try { InspectRoot(); }
                catch (WorkspaceFailure rootFailure) { classified = rootFailure; }
            }
            if (IsInactive(state.Generation, linked.Token)) return Complete(end(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None));
            if (state.Discovery is { } scan && (classified is not (WorkspaceFailure or OperationCanceledException) ||
                classified is WorkspaceFailure
                {
                    Code: WorkspaceErrorCode.PathChanged or WorkspaceErrorCode.NotFound or
                    WorkspaceErrorCode.SymbolicLink or WorkspaceErrorCode.WrongEntryType
                }))
            {
                try { RevalidateDiscovery(scan, state, linked.Token); }
                catch (WorkspaceFailure observationFailure) { classified = observationFailure; }
                catch (OperationCanceledException) { return Complete(end(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None)); }
                catch (Exception) { _snapshots.InvalidateAllForRootFailure(); }
            }
            if (IsInactive(state.Generation, linked.Token)) return Complete(end(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None));
            if (classified is WorkspaceFailure failure) return Complete(end(failure.Status, failure.Code));
            if (classified is OperationCanceledException) return Complete(end(WorkspaceStatus.Cancelled, WorkspaceErrorCode.None));
            return Complete(end(classified is UnauthorizedAccessException ? WorkspaceStatus.Failed :
                classified is FileNotFoundException or DirectoryNotFoundException or PathTooLongException or ArgumentException or NotSupportedException ? WorkspaceStatus.Rejected : WorkspaceStatus.Failed,
                classified switch
                {
                    UnauthorizedAccessException => WorkspaceErrorCode.AccessDenied,
                    FileNotFoundException or DirectoryNotFoundException => WorkspaceErrorCode.PathChanged,
                    PathTooLongException or ArgumentException or NotSupportedException => WorkspaceErrorCode.InvalidPath,
                    _ => WorkspaceErrorCode.IoFailure
                }));
        }
        finally
        {
            _executionPolicy.Plans.Release(state.TaskReservation);
            TaskCompletionSource? finished;
            lock (_gate) { finished = _inFlight; _inFlight = null; _activeCancellation = null; }
            _executionPolicy.Rules?.Settle();
            _executionPolicy.Skills?.Settle();
            finished?.TrySetResult();
        }
    }

    private async Task<WorkspaceListResult> ListCoreAsync(string rawPath, WorkspaceOperation operation, CancellationToken token)
    {
        var path = ValidateArgument(rawPath, false);
        await CheckRulesOperationAsync(path, true, operation, token);
        BeginTaskOperation(operation, "list_files", path);
        CheckActive(operation.Generation, token);
        var observations = InspectPath(path, WorkspaceNodeKind.Directory, operation);
        Revalidate(observations);
        var retained = new SortedDictionary<string, WorkspaceEntry>(StringComparer.Ordinal);
        var truncated = false;
        var scanned = 0;
        using (var entries = _fileSystem.EnumerateNames(observations[^1].Path).GetEnumerator())
        {
            while (scanned < WorkspaceLimits.MaxScannedEntries)
            {
                CheckActive(operation.Generation, token);
                if (!entries.MoveNext()) break;
                scanned++;
                CheckActive(operation.Generation, token);
                var name = entries.Current;
                CheckSensitive(name, ContentBoundary.ToolResult);
                var relative = path == "." ? name : path + "/" + name;
                CheckSensitive(relative, ContentBoundary.ToolResult);
                if (name.Contains('/') || name is "." or ".." || !WorkspacePathRules.IsCanonical(relative) || WorkspacePathRules.IsSensitive(relative)) continue;
                var state = _fileSystem.Inspect(Path.Combine(_root, relative));
                if (state.Kind == WorkspaceNodeKind.Missing) throw Rejected(WorkspaceErrorCode.PathChanged);
                if (state.Kind is not (WorkspaceNodeKind.File or WorkspaceNodeKind.Directory)) continue;
                retained[relative] = new(relative, state.Kind == WorkspaceNodeKind.File ? WorkspaceEntryKind.File : WorkspaceEntryKind.Directory);
                if (retained.Count > WorkspaceLimits.MaxListEntries)
                { retained.Remove(retained.Last().Key); truncated = true; }
            }
        }
        if (scanned == WorkspaceLimits.MaxScannedEntries) truncated = true;
        WorkspaceListResult result;
        while (true)
        {
            CheckActive(operation.Generation, token);
            result = WorkspaceListResult.Success(new(path, retained.Values.ToArray(), truncated));
            if (WorkspaceResponseWriter.TryCreateResponse(result, _policy!, out _, out var error)) break;
            if (error != WorkspaceErrorCode.ResultTooLarge || retained.Count == 0) throw Rejected(error);
            retained.Remove(retained.Last().Key); truncated = true;
        }
        Revalidate(observations);
        await CheckRulesResultAsync(operation, token);
        lock (_gate) { CheckActive(operation.Generation, token); return result; }
    }

    private async Task<WorkspaceReadResult> ReadCoreAsync(string rawPath, WorkspaceOperation operation, CancellationToken token)
    {
        var path = ValidateArgument(rawPath, true);
        await CheckRulesOperationAsync(path, false, operation, token);
        BeginTaskOperation(operation, "read_file", path);
        lock (_gate) { CheckActive(operation.Generation, token); _snapshots.Remove(path); }
        var file = await ReadVerifiedFileAsync(path, operation, token);
        var result = WorkspaceReadResult.Success(new(path, file.Content, file.Bytes.LongLength, file.HasBom));
        if (!WorkspaceResponseWriter.TryCreateResponse(result, _policy!, out _, out var error)) throw Rejected(error);
        Revalidate(file.Observations);
        if (_fileSystem is IWorkspaceWriteFileSystem modes && modes.GetMode(Path.Combine(_root, path)) != file.Mode)
            throw Rejected(WorkspaceErrorCode.PathChanged);
        await CheckRulesResultAsync(operation, token);
        var snapshot = new WorkspaceReadSnapshot(path, file.Bytes, file.HasBom, file.State.Device, file.State.Identity, file.Mode);
        lock (_gate)
        {
            CheckActive(operation.Generation, token);
            _snapshots.Publish(snapshot);
            return result;
        }
    }

    internal long InvalidateForProcess(WorkspaceOperationLease lease)
    {
        lock (_gate)
        {
            if (_disposal is not null || _policy is null || !lease.BelongsTo(_executionPolicy.WorkspaceOperations) || !lease.IsCurrent)
                throw new OperationCanceledException();
            return _snapshots.InvalidateForProcess();
        }
    }

    // Cleanup may clear baselines after cancellation, but can never admit or revive work.
    internal void InvalidateAfterProcess(WorkspaceOperationLease lease)
    {
        lock (_gate)
        {
            if (_policy is null || !lease.BelongsTo(_executionPolicy.WorkspaceOperations) || !_executionPolicy.WorkspaceOperations.IsActive(lease))
                throw new OperationCanceledException();
            _snapshots.InvalidateForProcess();
        }
    }

    private string ValidateArgument(string rawPath, bool read)
    {
        if (!WorkspacePathRules.TryNormalize(rawPath, out var relative, out var directory)) throw Rejected(WorkspaceErrorCode.InvalidPath);
        CheckSensitive(rawPath, ContentBoundary.ToolArguments);
        CheckSensitive(relative, ContentBoundary.ToolArguments);
        if (WorkspacePathRules.IsSensitive(_root) || WorkspacePathRules.IsSensitive(relative)) throw Rejected(WorkspaceErrorCode.SensitivePath);
        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        if (!WorkspacePathRules.IsWithinRoot(_root, candidate)) throw Rejected(WorkspaceErrorCode.OutsideWorkspace);
        if (read && directory) throw Rejected(WorkspaceErrorCode.WrongEntryType);
        return relative;
    }

    private List<PathObservation> InspectPath(string relative, WorkspaceNodeKind required, WorkspaceOperation operation)
    {
        var rootState = InspectRoot();
        operation.RootChecked = true;
        var observations = new List<PathObservation> { new(_root, rootState) };
        var current = _root;
        if (relative != ".")
        {
            var segments = relative.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                var state = _fileSystem.Inspect(current);
                if (state.Kind == WorkspaceNodeKind.Missing) throw Rejected(WorkspaceErrorCode.NotFound);
                if (state.Kind == WorkspaceNodeKind.Link) throw Rejected(WorkspaceErrorCode.SymbolicLink);
                var expected = index == segments.Length - 1 ? required : WorkspaceNodeKind.Directory;
                if (state.Kind != expected) throw Rejected(WorkspaceErrorCode.WrongEntryType);
                observations.Add(new(current, state));
            }
        }
        else if (required != WorkspaceNodeKind.Directory) throw Rejected(WorkspaceErrorCode.WrongEntryType);
        return observations;
    }

    private WorkspaceNodeState InspectRoot()
    {
        try
        {
            var state = _fileSystem.Inspect(_root);
            if (state.Kind != WorkspaceNodeKind.Directory) throw new IOException("工作區無法使用");
            return state;
        }
        catch (Exception)
        {
            _snapshots.InvalidateAllForRootFailure();
            _checkpoints.MarkAllManual("workspace_unavailable");
            throw new WorkspaceFailure(WorkspaceStatus.Failed, WorkspaceErrorCode.WorkspaceUnavailable);
        }
    }

    private void Revalidate(List<PathObservation> observations)
    {
        if (InspectRoot() != observations[0].State)
        { _snapshots.InvalidateAllForRootFailure(); throw Rejected(WorkspaceErrorCode.PathChanged); }
        foreach (var observation in observations.Skip(1))
        {
            WorkspaceNodeState actual;
            try { actual = _fileSystem.Inspect(observation.Path); }
            catch { InvalidateObservation(observation.Path, observation.State); throw; }
            if (actual != observation.State)
            { InvalidateObservation(observation.Path, observation.State); throw Rejected(WorkspaceErrorCode.PathChanged); }
        }
    }
    private void CheckSensitive(string text, ContentBoundary boundary)
    { if (!_policy!.CanSendOrUse(text, boundary)) throw Rejected(WorkspaceErrorCode.SensitiveContent); }
    private bool IsInactive(long generation, CancellationToken token)
    {
        lock (_gate) return token.IsCancellationRequested || _disposal is not null || generation != _snapshots.Generation ||
            _activePlanGeneration != _executionPolicy.Plans.Generation;
    }
    private void CheckActive(long generation, CancellationToken token)
    {
        if (IsInactive(generation, token)) throw new OperationCanceledException();
    }
    private static WorkspaceFailure Rejected(WorkspaceErrorCode code) => new(WorkspaceStatus.Rejected, code);

    private void CancelActiveOperation()
    {
        CancellationTokenSource? active;
        lock (_gate) active = _activeCancellation;
        _executionPolicy.WorkspaceOperations.Clear();
        try { active?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource finished;
        Task wait;
        lock (_gate)
        {
            if (_disposal is not null) return new(_disposal);
            finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = finished.Task;
            wait = _inFlight?.Task ?? Task.CompletedTask;
        }
        _ = DisposeCoreAsync(wait, finished);
        return new(finished.Task);
    }
    private async Task DisposeCoreAsync(Task wait, TaskCompletionSource finished)
    {
        var failed = false;
        try
        {
            try { _lifetime.Cancel(); }
            catch (Exception) { failed = true; }
            await wait;
            await _executionPolicy.WorkspaceOperations.DisposeAsync();
        }
        catch (Exception) { failed = true; }
        finally
        {
            try
            {
                lock (_gate) { _checkpoints.Clear(); _executionPolicy.Recovery.Clear(); _snapshots.Clear(); _policy = null; }
                _lifetime.Dispose();
            }
            catch (Exception) { failed = true; }
        }
        if (failed) finished.TrySetException(new InvalidOperationException("工作區服務無法完成釋放"));
        else finished.TrySetResult();
    }
    public override string ToString() => "[WorkspaceFiles]";

    private sealed class WorkspaceOperation(long generation, long planGeneration)
    {
        internal ProjectRulesStamp? RulesStamp { get; set; }
        internal SkillsOperationStamp? SkillsStamp { get; set; }
        internal WorkspaceOperationLease? Lease { get; init; }
        internal List<ProjectRuleObservation> RuleBoundaries { get; } = [];
        internal long Generation { get; } = generation;
        internal long PlanGeneration { get; } = planGeneration;
        internal bool RootChecked { get; set; }
        internal bool CheckpointSettling { get; set; }
        internal DiscoveryScan? Discovery { get; set; }
        internal TaskEvidenceReservation? TaskReservation { get; set; }
    }

    private sealed class PathObservation(string path, WorkspaceNodeState state)
    {
        internal string Path { get; } = path;
        internal WorkspaceNodeState State { get; } = state;
        public override string ToString() => "[PathObservation]";
    }
    private sealed class WorkspaceFailure(WorkspaceStatus status, WorkspaceErrorCode code) : Exception("工作區操作無法完成")
    {
        internal WorkspaceStatus Status { get; } = status;
        internal WorkspaceErrorCode Code { get; } = code;
    }
}