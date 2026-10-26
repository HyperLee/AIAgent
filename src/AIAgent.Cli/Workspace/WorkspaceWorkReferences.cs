using System.Security.Cryptography;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal interface IWorkReferenceWorkspace
{
    Task<WorkWorkspaceIdentity> CaptureWorkRootAsync(CancellationToken token);
    Task<WorkReferenceSet> ReadWorkReferencesAsync(IReadOnlyList<string> paths, CancellationToken token);
}

internal sealed class WorkReferenceObservation(WorkFileReference reference, IReadOnlyList<WorkspaceNodeState> nodes)
{
    internal WorkFileReference Reference { get; } = reference;
    internal IReadOnlyList<WorkspaceNodeState> Nodes { get; } = Array.AsReadOnly(nodes.ToArray());
    internal bool Same(WorkReferenceObservation other) => Reference.Path == other.Reference.Path && Reference.Observation == other.Reference.Observation &&
        Reference.ByteCount == other.Reference.ByteCount && Reference.Sha256 == other.Reference.Sha256 && Nodes.SequenceEqual(other.Nodes);
    public override string ToString() => "[WorkReferenceObservation]";
}

internal sealed class WorkReferenceSet(WorkWorkspaceIdentity root, IReadOnlyList<WorkReferenceObservation> observations)
{
    internal WorkWorkspaceIdentity Root { get; } = root;
    internal IReadOnlyList<WorkReferenceObservation> Observations { get; } = Array.AsReadOnly(observations.ToArray());
    internal IReadOnlyList<WorkFileReference> References => Array.AsReadOnly(Observations.Select(o => o.Reference).ToArray());
    internal bool Same(WorkReferenceSet other) => Root.Root == other.Root.Root && Root.Device == other.Root.Device && Root.Identity == other.Root.Identity &&
        Observations.Count == other.Observations.Count && Observations.Zip(other.Observations).All(p => p.First.Same(p.Second));
    public override string ToString() => "[WorkReferenceSet]";
}

internal sealed partial class WorkspaceFiles : IWorkReferenceWorkspace
{
    public IWorkReferenceWorkspace WorkReferences => this;
    public Task<WorkWorkspaceIdentity> CaptureWorkRootAsync(CancellationToken token)
    {
        if (!_executionPolicy.WorkspaceOperations.TryAcquire(token, out var acquired)) throw new WorkDocumentException("store_busy");
        using var lease = acquired!;
        try { return CaptureWorkRootCore(token); }
        catch (WorkspaceFailure ex) { throw WorkReferenceFailure(ex); }
    }
    private Task<WorkWorkspaceIdentity> CaptureWorkRootCore(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _ = ValidateArgument(".", false);
        var root = InspectRoot();
        if (root.Identity == 0) throw new WorkDocumentException("store_unavailable");
        return Task.FromResult(new WorkWorkspaceIdentity(_root, root.Device, root.Identity));
    }

    public async Task<WorkReferenceSet> ReadWorkReferencesAsync(IReadOnlyList<string> paths, CancellationToken token)
    {
        try { return await ReadWorkReferencesCoreAsync(paths, token); }
        catch (WorkspaceFailure ex) { throw WorkReferenceFailure(ex); }
    }
    private async Task<WorkReferenceSet> ReadWorkReferencesCoreAsync(IReadOnlyList<string> paths, CancellationToken token)
    {
        if (paths.Count > 160) throw new WorkDocumentException("work_capacity");
        if (!_executionPolicy.WorkspaceOperations.TryAcquire(token, out var acquired)) throw new WorkDocumentException("store_busy");
        using var lease = acquired!;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lease.CancellationToken, _lifetime.Token);
        var state = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.Generation) { Lease = lease };
        var rules = _executionPolicy.Rules ?? throw new WorkDocumentException("rules_unavailable");
        var error = await rules.ValidateAsync(linked.Token, lease);
        if (error != "none") throw new WorkDocumentException(error);
        var stamp = rules.Capture(); var root = await CaptureWorkRootCore(linked.Token);
        var observations = new List<WorkReferenceObservation>(paths.Count);
        foreach (var raw in paths)
        {
            linked.Token.ThrowIfCancellationRequested();
            var path = ValidateArgument(raw, false);
            if (stamp.Scope != "." && path != stamp.Scope && !path.StartsWith(stamp.Scope + "/", StringComparison.Ordinal))
            { observations.Add(new(new(path, "outside_scope", null, null), [])); continue; }
            WorkspaceNodeState? observedNode = null;
            try
            {
                await CheckRulesOperationAsync(path, false, state, linked.Token);
                var full = path == "." ? _root : Path.Combine(_root, path);
                var node = _fileSystem.Inspect(full); observedNode = node;
                if (node.Kind == WorkspaceNodeKind.Missing)
                {
                    var parent = path.Contains('/') ? path[..path.LastIndexOf('/')] : ".";
                    var parents = InspectPath(parent, WorkspaceNodeKind.Directory, state);
                    Revalidate(parents);
                    observations.Add(new(new(path, "missing", null, null), parents.Select(p => StableWorkNode(p.State)).ToArray()));
                }
                else if (node.Kind == WorkspaceNodeKind.Directory)
                {
                    await CheckRulesOperationAsync(path, true, state, linked.Token);
                    var parents = InspectPath(path, WorkspaceNodeKind.Directory, state); Revalidate(parents);
                    observations.Add(new(new(path, "directory", null, null), parents.Select(p => StableWorkNode(p.State)).ToArray()));
                }
                else if (node.Kind == WorkspaceNodeKind.File)
                {
                    var file = await ReadVerifiedFileAsync(path, state, linked.Token, expected: node);
                    observations.Add(new(new(path, "file", file.Bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(file.Bytes))),
                        file.Observations.Select(p => p.Path == full ? p.State : StableWorkNode(p.State)).ToArray()));
                }
                else observations.Add(new(new(path, "unavailable", null, null), [node]));
            }
            catch (WorkspaceFailure failure) when (failure.Code is not (WorkspaceErrorCode.WorkspaceUnavailable or WorkspaceErrorCode.RulesChanged or WorkspaceErrorCode.RulesUnavailable or WorkspaceErrorCode.SensitiveContent))
            { observations.Add(new(new(path, "unavailable", null, null), observedNode is { } observed ? [observed] : [])); }
            catch (UnauthorizedAccessException)
            { observations.Add(new(new(path, "unavailable", null, null), observedNode is { } observed ? [observed] : [])); }
            await CheckRulesResultAsync(state, linked.Token);
        }
        error = await rules.ValidateAsync(linked.Token, lease);
        if (error != "none" || !rules.IsCurrent(stamp)) throw new WorkDocumentException(error == "none" ? "rules_changed" : error);
        var after = await CaptureWorkRootCore(linked.Token);
        if (root.Root != after.Root || root.Device != after.Device || root.Identity != after.Identity) throw new WorkDocumentException("work_revision_conflict");
        return new(root, observations);
    }
    private static WorkDocumentException WorkReferenceFailure(WorkspaceFailure error) => new(error.Code switch
    {
        WorkspaceErrorCode.SensitiveContent => "work_sensitive_content",
        WorkspaceErrorCode.RulesChanged => "rules_changed",
        WorkspaceErrorCode.RulesUnavailable => "rules_unavailable",
        WorkspaceErrorCode.WorkspaceUnavailable => "workspace_unavailable",
        WorkspaceErrorCode.RulesScopeMismatch => "rules_scope_mismatch",
        WorkspaceErrorCode.RulesLimit => "rules_limit",
        _ => "store_unavailable"
    });
    private static WorkspaceNodeState StableWorkNode(WorkspaceNodeState state) => new(state.Kind, Device: state.Device,
        Identity: state.Identity, Links: state.Links, User: state.User, Mode: state.Mode);
}