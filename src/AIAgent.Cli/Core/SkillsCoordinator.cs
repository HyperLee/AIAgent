using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>單一明確選用來源；所有發布與計畫共用決策鎖，I/O 在鎖外完成。</summary>
internal sealed class SkillsCoordinator : IAsyncDisposable
{
    private readonly PlanStore _plans;
    private readonly object _gate;
    private AgentExecutionPolicy _execution;
    private ISkillsWorkspace? _reader;
    private ISensitiveDataPolicy? _policy;
    private ActiveSkillSnapshot? _snapshot;
    private SkillsStatus _status;
    private long _revision;
    private long _generation;
    private long _sessionGeneration;
    private long SessionGeneration => _plans.ModelSession?.Generation ?? _sessionGeneration;
    private bool _pendingInvalidation;
    private bool _disposed;
    private bool _fatalFailure;
    private bool _unsafeSource;
    internal bool RequiresContextReset { get { lock (_gate) return _unsafeSource; } }
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<TaskCompletionSource> _operations = [];
    private Task? _disposal;
    internal bool FatalFailure { get { lock (_gate) return _fatalFailure; } }

    internal SkillsCoordinator(ISkillsWorkspace? reader, ISensitiveDataPolicy policy, AgentExecutionPolicy execution)
    { _reader = reader; _policy = policy; _execution = execution; _plans = execution.Plans; _gate = _plans.DecisionGate; _plans.Skills = this; }
    internal SkillsOperationStamp Capture()
    { lock (_gate) return new(SessionGeneration, _generation, _revision, _status, _snapshot, _execution.Rules?.Capture()); }
    internal bool IsCurrent(SkillsOperationStamp stamp)
    {
        lock (_gate)
        {
            var rules = _execution.Rules?.Capture();
            return !_disposed && !_fatalFailure && _policy is not null && _status != SkillsStatus.Stale && stamp.Status == _status &&
                stamp.SessionGeneration == SessionGeneration && stamp.SkillsGeneration == _generation && stamp.SkillsRevision == _revision && ReferenceEquals(stamp.Snapshot, _snapshot) &&
                stamp.RulesGeneration == (rules?.RulesGeneration ?? 0) && stamp.RulesRevision == (rules?.RulesRevision ?? 0) && stamp.Scope == (rules?.Scope ?? ".");
        }
    }
    internal string StatusCode { get { lock (_gate) return _disposed || _fatalFailure || _policy is null ? "skills_unavailable" : _status == SkillsStatus.Stale ? "skills_changed" : "none"; } }
    internal Task<SkillsOperationResult> ListAsync(CancellationToken token) => QueryAsync(null, false, token);
    internal Task<SkillsOperationResult> ShowAsync(string id, CancellationToken token) => QueryAsync(id, false, token);
    internal Task<SkillsOperationResult> TryUseAsync(string id, CancellationToken token) => QueryAsync(id, true, token);
    internal Task<SkillsOperationResult> TryReloadAsync(CancellationToken token)
    {
        string? id;
        lock (_gate) id = _snapshot?.Source.Descriptor.Id;
        return id is null ? Task.FromResult(new SkillsOperationResult(StatusCode == "none" ? "skill_not_active" : StatusCode)) : QueryAsync(id, true, token);
    }
    private async Task<SkillsOperationResult> QueryAsync(string? id, bool activate, CancellationToken token)
    {
        if (id is not null && !SkillFormat.ValidId(id)) return new("invalid_skills_command");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SkillsOperationStamp stamp; ISkillsWorkspace reader; ISensitiveDataPolicy policy;
        lock (_gate)
        {
            if (_disposed || _fatalFailure || _policy is null || _reader is null || _operations.Count != 0) return new("skills_unavailable");
            _operations.Add(completion); stamp = Capture(); reader = _reader; policy = _policy;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        WorkspaceOperationLease? lease = null;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (!_execution.WorkspaceOperations.TryAcquire(linked.Token, out lease)) return new("skills_unavailable");
            var ruleError = await _execution.Rules!.ValidateAsync(linked.Token, lease);
            if (ruleError != "none") return new(ruleError);
            // Initial lazy rule loading is independent of skill selection.
            lock (_gate)
            {
                if (_disposed || stamp.SessionGeneration != SessionGeneration || stamp.SkillsGeneration != _generation) return new("skills_changed");
                stamp = Capture();
            }
            var first = id is null ? await reader.ReadSkillCatalogAsync(stamp.Scope, lease!, linked.Token) : await reader.ReadSkillAsync(stamp.Scope, id, lease!, linked.Token);
            Observe(first, stamp);
            if (first.ErrorCode != "none") return await FailedAsync(first.ErrorCode);
            var second = id is null ? await reader.ReadSkillCatalogAsync(stamp.Scope, lease!, linked.Token) : await reader.ReadSkillAsync(stamp.Scope, id, lease!, linked.Token);
            Observe(second, stamp);
            if (second.ErrorCode != "none") return await FailedAsync(second.ErrorCode);
            if (id is null ? !first.Catalog!.Same(second.Catalog!) : !first.Source!.Same(second.Source!)) return await FailedAsync("path_changed");
            ruleError = await _execution.Rules.ValidateAsync(linked.Token, lease);
            if (ruleError != "none") return await FailedAsync(ruleError);
            if (id is null)
            {
                var json = SkillsDocuments.Catalog(second.Catalog!, policy);
                var display = json is null ? null : SkillsDocuments.DisplayBlock("目前 Scope 的完整技能目錄；不會自動選用。\n" + json + "\n", policy);
                if (display is null) return await FailedAsync("skills_limit");
                lock (_gate) { linked.Token.ThrowIfCancellationRequested(); if (!MatchesCandidate(stamp)) return new("skills_changed"); }
                return new("none", display);
            }
            var source = second.Source!;
            if (!activate)
            {
                var equal = stamp.Snapshot?.Source.Same(source) == true;
                var display = SkillsDocuments.DisplayBlock("目前磁碟檢視，不會啟用或更新啟用快照。\n" + SkillsDocuments.Metadata(source.Descriptor) +
                    (stamp.Snapshot?.Source.Descriptor.Id == id ? "與啟用快照" + (equal ? "相同。\n" : "不同；目前技能已過時。\n") : "") + FileChangePreviewBuilder.EscapePath(source.Text) + "\n", policy);
                if (display is null) return await FailedAsync("skills_limit");
                lock (_gate) { linked.Token.ThrowIfCancellationRequested(); if (!MatchesCandidate(stamp)) return new("skills_changed"); }
                return new("none", display);
            }
            long next;
            lock (_gate)
            {
                linked.Token.ThrowIfCancellationRequested(); if (!MatchesCandidate(stamp)) return new("skills_changed");
                if (_status == SkillsStatus.Valid && _snapshot!.Source.Same(source)) return new("no_change", SkillsDocuments.Display(Capture(), policy));
                next = checked(_revision + 1);
            }
            var projection = SkillsDocuments.Projection(next, stamp.Scope, source, policy);
            var candidate = projection is null ? null : new ActiveSkillSnapshot(next, source, projection);
            var resultDisplay = candidate is null ? null : SkillsDocuments.Display(new(SessionGeneration, _generation, next, SkillsStatus.Valid, candidate, _execution.Rules.Capture()), policy);
            resultDisplay = resultDisplay is null ? null : SkillsDocuments.DisplayBlock(resultDisplay + "已選用；需要重新確認計畫並等待新指示，每筆操作仍須逐次核准。\n", policy);
            if (candidate is null || resultDisplay is null) return await FailedAsync("skills_limit");
            lock (_gate)
            {
                linked.Token.ThrowIfCancellationRequested(); if (!MatchesCandidate(stamp)) return new("skills_changed");
                var generation = checked(_generation + 1);
                _snapshot = candidate; _revision = next; _generation = generation; _status = SkillsStatus.Valid; _pendingInvalidation = true;
                _plans.CurrentSkillsRevision = next;
            }
            return new("none", resultDisplay);

            async Task<SkillsOperationResult> FailedAsync(string error)
            {
                var reset = error == "sensitive_content" && stamp.Snapshot?.Source.Descriptor.Id == id;
                if (stamp.Status == SkillsStatus.Valid && stamp.Snapshot is not null)
                {
                    var old = await reader.ValidateSkillSourceAsync(stamp.Snapshot.Source, lease!, linked.Token);
                    if (old.Source is null || !old.Source.Same(stamp.Snapshot.Source)) MarkStale();
                    reset |= old.ErrorCode == "sensitive_content";
                    if (old.ErrorCode == "io_failure") MarkFatalFailure();
                }
                return new(error, requiresContextReset: reset);
            }
        }
        catch (SkillsSensitiveException)
        {
            var reset = stamp.Snapshot?.Source.Descriptor.Id == id;
            if (lease is not null && stamp.Status == SkillsStatus.Valid && stamp.Snapshot is not null)
            {
                var old = await reader.ValidateSkillSourceAsync(stamp.Snapshot.Source, lease, linked.Token);
                if (old.Source is null || !old.Source.Same(stamp.Snapshot.Source)) MarkStale();
                reset |= old.ErrorCode == "sensitive_content";
                if (old.ErrorCode == "io_failure") MarkFatalFailure();
            }
            return new("sensitive_content", requiresContextReset: reset);
        }
        catch (OverflowException) { MarkFatalFailure(); throw; }
        finally
        {
            try { lease?.Dispose(); Settle(); }
            catch (Exception) { MarkFatalFailure(); throw; }
            finally { lock (_gate) { _operations.Remove(completion); completion.TrySetResult(); } }
        }
    }
    private bool MatchesCandidate(SkillsOperationStamp stamp)
    {
        var current = Capture();
        return !_disposed && !_fatalFailure && current.SessionGeneration == stamp.SessionGeneration && current.SkillsRevision == stamp.SkillsRevision &&
            current.RulesGeneration == stamp.RulesGeneration && current.RulesRevision == stamp.RulesRevision && current.Scope == stamp.Scope &&
            (current.SkillsGeneration == stamp.SkillsGeneration || current.Status == SkillsStatus.Stale && ReferenceEquals(current.Snapshot, stamp.Snapshot));
    }
    private void Observe(SkillReadResult result, SkillsOperationStamp stamp)
    {
        if (stamp.Snapshot is not { } active) return;
        var previous = active.Source.Descriptor;
        if (result.Source is { } source && source.Descriptor.Scope == previous.Scope && source.Descriptor.Id == previous.Id && !source.Same(active.Source)) MarkStale();
        if (result.Catalog is { } catalog && catalog.Scope == previous.Scope &&
            catalog.Skills.SingleOrDefault(s => s.Id == previous.Id) is not { } descriptor) MarkStale();
        else if (result.Catalog is { } found && found.Skills.SingleOrDefault(s => s.Id == previous.Id) is { } entry &&
            (!previous.Same(entry) || active.Source.Observations.Any(old => found.Observations.LastOrDefault(now => now.Path == old.Path) is { } now && !old.Same(now)))) MarkStale();
    }

    internal async Task<string> ValidateAsync(CancellationToken token, WorkspaceOperationLease? supplied = null)
    {
        SkillsOperationStamp stamp; ISkillsWorkspace? reader;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_unsafeSource) throw new SkillsSensitiveException();
            if (StatusCode != "none") return StatusCode;
            if (_status == SkillsStatus.Disabled) return "none";
            stamp = Capture(); reader = _reader;
            if (reader is null) return "skills_unavailable";
            _operations.Add(completion);
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        WorkspaceOperationLease? owned = null;
        try
        {
            if (supplied is null && !_execution.WorkspaceOperations.TryAcquire(linked.Token, out owned)) return "skills_unavailable";
            var result = await reader.ValidateSkillSourceAsync(stamp.Snapshot!.Source, supplied ?? owned!, linked.Token);
            if (result.Source is null || !stamp.Snapshot.Source.Same(result.Source))
            {
                MarkStale();
                if (result.ErrorCode == "sensitive_content") { MarkSensitive(); throw new SkillsSensitiveException(); }
                if (result.ErrorCode == "io_failure") { MarkFatalFailure(); throw new InvalidOperationException("技能來源無法安全收尾"); }
                return "skills_changed";
            }
            return IsCurrent(stamp) ? "none" : "skills_changed";
        }
        finally
        {
            try { owned?.Dispose(); if (supplied is null) Settle(); }
            catch (Exception) { MarkFatalFailure(); throw; }
            finally { lock (_gate) { _operations.Remove(completion); completion.TrySetResult(); } }
        }
    }
    internal SkillsOperationResult Disable()
    {
        lock (_gate)
        {
            if (_disposed || _policy is null || _fatalFailure) return new("skills_unavailable");
            if (_status == SkillsStatus.Disabled) return new("no_change");
            var next = checked(_revision + 1); var generation = checked(_generation + 1);
            var projection = SkillsDocuments.Projection(next, _execution.Rules?.Capture().Scope ?? ".", null, _policy);
            if (projection is null) return new("skills_limit");
            var display = SkillsDocuments.DisplayBlock("技能已停用；需要重新確認計畫並等待新指示。\n", _policy);
            if (display is null) return new("skills_limit");
            _revision = next; _generation = generation; _snapshot = null; _status = SkillsStatus.Disabled;
            _plans.CurrentSkillsRevision = next; _pendingInvalidation = true;
        }
        Settle(); return new("none");
    }
    internal string BuildAppendix()
    {
        lock (_gate)
        {
            if (StatusCode != "none") throw new SkillsFlowException(StatusCode);
            var projection = _status == SkillsStatus.Valid ? _snapshot!.Projection : SkillsDocuments.Projection(_revision, Capture().Scope, null, _policy!);
            if (projection is null) throw new SkillsFlowException("skills_limit");
            var appendix = SkillsDocuments.AppendixLabel + projection;
            if (!_policy!.CanSendOrUse(appendix, ContentBoundary.ModelInput)) throw new SkillsSensitiveException();
            return appendix;
        }
    }
    internal string? Display()
    { lock (_gate) return _policy is null ? null : SkillsDocuments.Display(Capture(), _policy); }
    internal void MarkStale()
    {
        lock (_gate)
        {
            if (_disposed || _snapshot is null || _status == SkillsStatus.Stale) return;
            _status = SkillsStatus.Stale; _pendingInvalidation = true;
            if (_generation == long.MaxValue) _fatalFailure = true; else _generation++;
        }
    }
    internal void MarkSensitive() { lock (_gate) { _unsafeSource = true; MarkStale(); } }
    internal void MarkFatalFailure() { lock (_gate) { _fatalFailure = true; MarkStale(); } }
    internal void Settle()
    {
        ISkillsWorkspace? reader;
        lock (_gate)
        {
            if (!_pendingInvalidation || _plans.HasReservations) return;
            _plans.RevokeForSkills(); _pendingInvalidation = false; reader = _reader;
        }
        reader?.ClearSkillSnapshots();
    }
    internal void RulesPublished(string oldScope, string newScope)
    {
        lock (_gate)
        {
            if (oldScope != newScope) Clear(); else MarkStale();
        }
    }
    internal void Clear()
    {
        lock (_gate)
        {
            var generation = checked(_generation + 1); var session = checked(_sessionGeneration + 1);
            var revision = _snapshot is null ? _revision : checked(_revision + 1);
            _generation = generation; _sessionGeneration = session; _revision = revision; _snapshot = null; _status = SkillsStatus.Disabled;
            _plans.CurrentSkillsRevision = revision; _pendingInvalidation = false; _unsafeSource = false;
        }
    }
    internal void BindPolicy(ISensitiveDataPolicy policy, ISkillsWorkspace? reader, AgentExecutionPolicy execution)
    { lock (_gate) { Clear(); _policy = policy; _reader = reader; _execution = execution; } }
    internal void DetachPolicy() { lock (_gate) { _policy = null; _reader = null; } }
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion; Task[] wait;
        lock (_gate)
        {
            if (_disposal is not null) return new(_disposal);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _disposal = completion.Task;
            _disposed = true; if (_generation != long.MaxValue) _generation++;
            wait = _operations.Select(o => o.Task).ToArray();
        }
        _ = DisposeCoreAsync(wait, completion); return new(completion.Task);
    }
    private async Task DisposeCoreAsync(Task[] wait, TaskCompletionSource completion)
    {
        try
        {
            _lifetime.Cancel(); await Task.WhenAll(wait);
            lock (_gate) { _snapshot = null; _reader = null; _policy = null; _status = SkillsStatus.Disabled; }
            _lifetime.Dispose(); completion.TrySetResult();
        }
        catch (Exception) { completion.TrySetException(new InvalidOperationException("技能來源無法完成釋放")); }
    }
    public override string ToString() => "[SkillsCoordinator]";
}