using System.Security.Cryptography;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    private readonly Dictionary<string, DelegationScopeObservation> _delegationScopes = new(StringComparer.Ordinal);
    private DelegationScopeObservation? _delegationIdentity;
    private sealed class DelegationScopeObservation(WorkspaceNodeState root, WorkspaceNodeState directory)
    {
        internal WorkspaceNodeState Root { get; } = root;
        internal WorkspaceNodeState Directory { get; } = directory;
        internal bool Same(WorkspaceNodeState actualRoot, WorkspaceNodeState actualDirectory) => ProjectRuleObservation.SameDirectory(Root, actualRoot) && ProjectRuleObservation.SameDirectory(Directory, actualDirectory);
    }
    internal (ulong RootDevice, ulong RootIdentity, ulong ScopeDevice, ulong ScopeIdentity) DelegationIdentity(string scope) =>
        _delegationScopes.TryGetValue(scope, out var value) ? (value.Root.Device, value.Root.Identity, value.Directory.Device, value.Directory.Identity) : default;
    internal void ClearDelegationScopes() => _delegationScopes.Clear();
    internal bool CheckDelegationScopes(WorkspaceOperationLease lease)
    {
        RequireRulesLease(lease);
        var operation = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.ExecutionGeneration) { Lease = lease };
        _activePlanGeneration = operation.PlanGeneration;
        try
        {
            foreach (var (scope, observed) in _delegationScopes)
            {
                var paths = InspectPath(scope, WorkspaceNodeKind.Directory, operation);
                if (!observed.Same(paths[0].State, paths[^1].State)) return false;
            }
            return true;
        }
        catch (WorkspaceFailure) { return false; }
    }
    private void RequireDelegationIdentity()
    {
        if (_delegationScope is null) return;
        var root = _fileSystem.Inspect(_root); var directory = _fileSystem.Inspect(_delegationScope == "." ? _root : Path.Combine(_root, _delegationScope));
        if (_delegationIdentity is null) _delegationIdentity = new(root, directory);
        if (!_delegationIdentity.Same(root, directory)) throw Rejected(WorkspaceErrorCode.PathChanged);
    }
    internal static bool DelegationContains(string scope, string path) => scope == "." || path == scope || path.StartsWith(scope + "/", StringComparison.Ordinal);
    internal DelegationReadSession CreateDelegationReadSession(string scope)
    {
        var ledger = new DelegationSourceLedger();
        var files = new WorkspaceFiles(new(_root), _policy!, _fileSystem, executionPolicy: _executionPolicy, delegationScope: scope, delegationLedger: ledger);
        if (_delegationScopes.TryGetValue(scope, out var identity)) files._delegationIdentity = identity;
        return new(files, ledger);
    }
    internal async Task<string> ValidateDelegationScopeAsync(string scope, CancellationToken token)
    {
        await using var lease = await _executionPolicy.WorkspaceOperations.AcquireAsync(token);
        var state = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.ExecutionGeneration) { Lease = lease };
        _activePlanGeneration = state.PlanGeneration;
        try
        {
            scope = ValidateArgument(scope, false);
            await CheckRulesOperationAsync(scope, true, state, token);
            var observations = InspectPath(scope, WorkspaceNodeKind.Directory, state); Revalidate(observations);
            await CheckRulesResultAsync(state, token);
            if (_delegationScopes.TryGetValue(scope, out var previous) && !previous.Same(observations[0].State, observations[^1].State)) return "delegation_source_changed";
            _delegationScopes[scope] = new(observations[0].State, observations[^1].State); return "none";
        }
        catch (WorkspaceFailure ex) { return Tools.WorkspaceToolProjection.Code(ex.Code); }
    }
    internal async Task<string> ValidateDelegationSourcesAsync(DelegationSourceLedger ledger, CancellationToken token)
    {
        await using var lease = await _executionPolicy.WorkspaceOperations.AcquireAsync(token);
        var state = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.ExecutionGeneration) { Lease = lease };
        _activePlanGeneration = state.PlanGeneration;
        try
        {
            await CheckRulesOperationAsync(_delegationScope!, true, state, token);
            foreach (var source in ledger.Sources)
            {
                token.ThrowIfCancellationRequested();
                var observations = InspectPath(source.Path, source.State.Kind, state);
                if (observations[^1].State != source.State) return "delegation_source_changed";
                if (source.Hash is not null)
                {
                    var file = await ReadVerifiedFileAsync(source.Path, state, token, expected: source.State);
                    if (file.Mode != source.Mode || Convert.ToHexStringLower(SHA256.HashData(file.Bytes)) != source.Hash) return "delegation_source_changed";
                }
                Revalidate(observations);
            }
            await CheckRulesResultAsync(state, token); return "none";
        }
        catch (WorkspaceFailure ex) { return ex.Code == WorkspaceErrorCode.SensitiveContent ? "sensitive_content" : "delegation_source_changed"; }
    }
}