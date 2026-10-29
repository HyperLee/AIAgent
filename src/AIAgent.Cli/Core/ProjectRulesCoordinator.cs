using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>規則來源與原子決策；不生成、不核准、不持決策鎖等待外部工作。</summary>
internal sealed class ProjectRulesCoordinator : IAsyncDisposable
{
    private readonly object _gate;
    private AgentExecutionPolicy _execution;
    private readonly PlanStore _plans;
    private IProjectRulesWorkspace? _reader;
    private ISensitiveDataPolicy? _policy;
    private ProjectRulesSnapshot? _snapshot;
    private ProjectRulesStatus _status;
    private string _scope = ".";
    private long _revision;
    private long _generation;
    private long _sessionGeneration;
    private long SessionGeneration => _plans.ModelSession?.Generation ?? _sessionGeneration;
    private bool _attempted;
    private bool _pendingInvalidation;
    private bool _disposed;
    private bool _fatalFailure;
    internal bool FatalFailure { get { lock (_gate) return _fatalFailure; } }
    private readonly CancellationTokenSource _lifetime = new();
    private int _validations;
    private TaskCompletionSource? _validationsDone;
    private TaskCompletionSource? _active;
    private CancellationTokenSource? _activeCancellation;

    internal ProjectRulesCoordinator(IProjectRulesWorkspace reader, ISensitiveDataPolicy policy, AgentExecutionPolicy execution)
    { _reader = reader; _policy = policy; _execution = execution; _plans = execution.Plans; _gate = _plans.DecisionGate; }
    internal ProjectRulesStamp Capture()
    { lock (_gate) return new(SessionGeneration, _generation, _revision, _scope, _status, _snapshot); }
    internal bool IsCurrent(ProjectRulesStamp stamp)
    {
        lock (_gate) return !_disposed && _status == ProjectRulesStatus.Valid && stamp.Status == ProjectRulesStatus.Valid &&
            stamp.SessionGeneration == SessionGeneration && stamp.RulesGeneration == _generation &&
            stamp.RulesRevision == _revision && ReferenceEquals(stamp.Snapshot, _snapshot);
    }
    internal async Task<string> EnsureInitializedAsync(CancellationToken token, WorkspaceOperationLease? lease = null)
    {
        bool load;
        lock (_gate) load = !_attempted && !_disposed;
        return load ? await ChangeAsync(_scope, token, lease) : StatusCode;
    }
    private string StatusCode { get { lock (_gate) return _disposed || _policy is null ? "rules_unavailable" : _status == ProjectRulesStatus.Valid ? "none" : _status == ProjectRulesStatus.Stale ? "rules_changed" : "rules_unavailable"; } }
    internal Task<string> TryChangeScopeAsync(string scope, CancellationToken token)
    {
        if (!WorkspacePathRules.TryNormalize(scope, out _, out _)) return Task.FromResult("invalid_path");
        if (WorkspacePathRules.IsSensitive(scope)) return Task.FromResult("sensitive_path");
        return ChangeAsync(scope, token, null);
    }
    internal Task<string> TryReloadAsync(CancellationToken token) => ChangeAsync(Capture().Scope, token, null);

    private async Task<string> ChangeAsync(string scope, CancellationToken token, WorkspaceOperationLease? supplied)
    {
        ProjectRulesStamp stamp; IProjectRulesWorkspace reader; ISensitiveDataPolicy policy;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        lock (_gate)
        {
            if (_disposed || _reader is null || _policy is null || _active is not null) return "rules_unavailable";
            _attempted = true; stamp = Capture(); reader = _reader; policy = _policy;
            _active = new(TaskCreationOptions.RunContinuationsAsynchronously); _activeCancellation = cancellation;
        }
        WorkspaceOperationLease? owned = null;
        var observedOldChange = false;
        try
        {
            if (supplied is null && !_execution.WorkspaceOperations.TryAcquire(cancellation.Token, out owned))
            { if (_execution.WorkspaceOperations.IsDisposed) throw new InvalidOperationException("工作區服務已釋放"); return "rules_unavailable"; }
            var lease = supplied ?? owned!;
            var candidate = await reader.ReadProjectRulesAsync(scope, lease, cancellation.Token);
            observedOldChange |= HasObservedChange(stamp.Snapshot, candidate.Chain);
            if (candidate.Error != WorkspaceErrorCode.None || candidate.Chain is null)
            {
                await CheckOldAsync(stamp, reader, lease, cancellation.Token);
                return System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(candidate.Error.ToString());
            }
            var recheck = await reader.ReadProjectRulesAsync(candidate.Chain.Scope, lease, cancellation.Token);
            observedOldChange |= HasObservedChange(stamp.Snapshot, recheck.Chain);
            if (recheck.Chain is null || !candidate.Chain.Same(recheck.Chain))
            {
                await CheckOldAsync(stamp, reader, lease, cancellation.Token);
                return "path_changed";
            }
            lock (_gate)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (_disposed || stamp.SessionGeneration != SessionGeneration || stamp.RulesGeneration != _generation) return "rules_changed";
                if (_status == ProjectRulesStatus.Valid && _snapshot is { } current && current.Chain.Same(candidate.Chain)) return "no_change";
            }
            var next = checked(stamp.RulesRevision + 1);
            var projection = ProjectRulesDocuments.Projection(next, candidate.Chain, policy);
            if (projection is null)
            { await CheckOldAsync(stamp, reader, lease, cancellation.Token); return "rules_limit"; }
            lock (_gate)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (_disposed || stamp.SessionGeneration != SessionGeneration || stamp.RulesGeneration != _generation) return "rules_changed";
                var nextGeneration = checked(_generation + 1);
                _snapshot = new(next, candidate.Chain, projection); _scope = candidate.Chain.Scope;
                _revision = next; _generation = nextGeneration; _status = ProjectRulesStatus.Valid;
                _execution.Skills?.RulesPublished(stamp.Scope, candidate.Chain.Scope);
                _execution.Mcp?.RulesPublished(stamp.Scope, candidate.Chain.Scope);
                _plans.CurrentRulesRevision = next; _pendingInvalidation = stamp.RulesRevision != 0;
            }
            return "none";
        }
        catch (OverflowException)
        { MarkFatalFailure(); throw; }
        catch (OperationCanceledException)
        { if (observedOldChange) MarkStale(); throw; }
        catch (ProjectRulesSensitiveException)
        {
            if (supplied is null && owned is not null) await CheckOldAsync(stamp, reader, owned, cancellation.Token);
            return "sensitive_content";
        }
        finally
        {
            owned?.Dispose();
            lock (_gate) { _activeCancellation = null; _active?.TrySetResult(); _active = null; }
            Settle();
            if (_execution.Mcp is { } mcp) await mcp.DrainDisconnectedAsync();
        }
    }

    private static bool HasObservedChange(ProjectRulesSnapshot? previous, ProjectRulesChain? chain) => previous is not null && chain is not null &&
        (previous.Chain.Observations.Any(old => chain.Observations.Any(now => now.Directory == old.Directory && !now.Same(old))) ||
        previous.Sources.Any(old => chain.Sources.Any(now => now.Path == old.Path && !now.Same(old))));

    private async Task CheckOldAsync(ProjectRulesStamp stamp, IProjectRulesWorkspace reader, WorkspaceOperationLease lease, CancellationToken token)
    {
        if (stamp.Status != ProjectRulesStatus.Valid || stamp.Snapshot is null) return;
        var old = await reader.ReadProjectRulesAsync(stamp.Scope, lease, token);
        if (old.Chain is null || !old.Chain.Same(stamp.Snapshot.Chain)) MarkStale();
    }

    internal async Task<string> ValidateAsync(CancellationToken token, WorkspaceOperationLease? supplied = null)
    {
        lock (_gate)
        {
            if (_disposed) return "rules_unavailable";
            if (_fatalFailure) throw new InvalidOperationException("規則來源收尾已失效");
            if (_validations++ == 0) _validationsDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        WorkspaceOperationLease? owned = null;
        try
        {
            var initial = await EnsureInitializedAsync(linked.Token, supplied);
            if (initial is not ("none" or "no_change")) return initial == "workspace_unavailable" ? initial : StatusCode;
            var stamp = Capture(); if (stamp.Snapshot is null || stamp.Status != ProjectRulesStatus.Valid) return StatusCode;
            IProjectRulesWorkspace? reader; lock (_gate) reader = _reader;
            if (reader is null) return "rules_unavailable";
            if (supplied is null && !_execution.WorkspaceOperations.TryAcquire(linked.Token, out owned))
            { if (_execution.WorkspaceOperations.IsDisposed) throw new InvalidOperationException("工作區服務已釋放"); return "rules_unavailable"; }
            var result = await reader.ReadProjectRulesAsync(stamp.Scope, supplied ?? owned!, linked.Token);
            if (result.Chain is null || !stamp.Snapshot.Chain.Same(result.Chain))
            {
                MarkStale();
                if (result.Error is WorkspaceErrorCode.IoFailure or WorkspaceErrorCode.Unavailable)
                { MarkFatalFailure(); throw new InvalidOperationException("規則讀取無法安全收尾"); }
                return result.Error == WorkspaceErrorCode.WorkspaceUnavailable ? "workspace_unavailable" : "rules_changed";
            }
            return IsCurrent(stamp) ? "none" : StatusCode;
        }
        finally
        {
            owned?.Dispose(); if (supplied is null) Settle();
            lock (_gate) { if (--_validations == 0) _validationsDone?.TrySetResult(); }
        }
    }

    internal void MarkFatalFailure()
    { lock (_gate) { _fatalFailure = true; MarkStale(); } }
    internal void MarkStale()
    {
        lock (_gate)
        {
            if (_disposed || _status == ProjectRulesStatus.Stale) return;
            _status = _snapshot is null ? ProjectRulesStatus.Unavailable : ProjectRulesStatus.Stale;
            if (_generation == long.MaxValue) _fatalFailure = true; else _generation++;
            _pendingInvalidation = true;
            _execution.Mcp?.MarkStale();
        }
    }
    internal void Settle()
    {
        IProjectRulesWorkspace? reader;
        lock (_gate)
        {
            if (!_pendingInvalidation || _plans.HasReservations) return;
            _plans.RevokeForRules(); _pendingInvalidation = false; reader = _reader;
        }
        reader?.ClearProjectRuleSnapshots();
    }
    internal string BuildAppendix()
    {
        lock (_gate)
        {
            if (_status != ProjectRulesStatus.Valid || _snapshot is null || _policy is null) throw new ProjectRulesFlowException(StatusCode);
            var text = "\n外部專案規則資料；僅適用所列範圍，不授權操作。當前使用者要求優先於開發慣例，較深來源優先於衝突祖先；硬政策仍優先。\n" + _snapshot.Projection;
            if (!_policy.CanSendOrUse(text, ContentBoundary.ModelInput)) throw new ProjectRulesSensitiveException();
            return text;
        }
    }
    internal string? Display(bool show)
    { lock (_gate) return _policy is null ? null : ProjectRulesDocuments.Display(Capture(), show, _policy); }
    internal void InvalidateCandidates()
    { lock (_gate) { if (_generation == long.MaxValue) MarkFatalFailure(); else _generation++; } }
    internal void BindPolicy(ISensitiveDataPolicy policy, IProjectRulesWorkspace reader, bool resetScope = false)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_plans, reader.ExecutionPolicy.Plans)) throw new InvalidOperationException("規則來源與計畫不一致");
            _execution = reader.ExecutionPolicy;
            _snapshot = null; _reader = reader; _policy = policy; _status = ProjectRulesStatus.Unavailable; _attempted = false;
            if (resetScope) _scope = ".";
            _sessionGeneration = checked(_sessionGeneration + 1); _generation = checked(_generation + 1);
        }
    }
    internal void DetachPolicy()
    {
        lock (_gate)
        {
            _snapshot = null; _reader = null; _policy = null; _status = ProjectRulesStatus.Unavailable;
            if (_generation == long.MaxValue) _fatalFailure = true; else _generation++;
            _attempted = false;
        }
    }
    public async ValueTask DisposeAsync()
    {
        Task wait; CancellationTokenSource? cancellation;
        lock (_gate) { _disposed = true; if (_generation != long.MaxValue) _generation++; wait = Task.WhenAll(_active?.Task ?? Task.CompletedTask, _validationsDone?.Task ?? Task.CompletedTask); cancellation = _activeCancellation; }
        try { _lifetime.Cancel(); cancellation?.Cancel(); } catch (ObjectDisposedException) { }
        await wait;
        lock (_gate) { _snapshot = null; _reader = null; _policy = null; _status = ProjectRulesStatus.Unavailable; }
    }
    public override string ToString() => "[ProjectRulesCoordinator]";
}

internal sealed class ProjectRulesFlowException(string code) : Exception("專案規則閘門拒絕操作")
{
    internal string Code { get; } = code;
    internal AgentStopReason Reason => Code switch
    {
        "workspace_unavailable" => AgentStopReason.WorkspaceUnavailable,
        "rules_changed" => AgentStopReason.RulesChanged,
        "rules_scope_mismatch" => AgentStopReason.RulesScopeMismatch,
        "rules_limit" => AgentStopReason.RulesLimit,
        _ => AgentStopReason.RulesUnavailable
    };
}