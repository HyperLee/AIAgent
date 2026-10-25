using System.Security.Cryptography;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles : IProjectRulesWorkspace
{
    public IProjectRulesWorkspace ProjectRulesWorkspace => this;
    public AgentExecutionPolicy ExecutionPolicy => _executionPolicy;

    public async Task<ProjectRulesReadResult> ReadProjectRulesAsync(string scope, WorkspaceOperationLease lease, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lease.CancellationToken, _lifetime.Token);
        var state = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.Generation);
        try
        {
            RequireRulesLease(lease); _activePlanGeneration = state.PlanGeneration;
            var normalized = ValidateArgument(scope, false);
            if (normalized != "." && normalized.Split('/').Length > ProjectRulesLimits.MaxProjectRuleDepth) return new(WorkspaceErrorCode.RulesLimit);
            var parents = InspectPath(normalized, WorkspaceNodeKind.Directory, state);
            if (parents.Count - 1 > ProjectRulesLimits.MaxProjectRuleDepth) return new(WorkspaceErrorCode.RulesLimit);
            var sources = new List<ProjectRuleSource>(); var observed = new List<ProjectRuleObservation>();
            foreach (var parent in parents)
            {
                linked.Token.ThrowIfCancellationRequested(); RequireRulesLease(lease);
                var directory = Path.GetRelativePath(_root, parent.Path);
                var path = directory == "." ? "AGENTS.md" : directory + "/AGENTS.md";
                _ = ValidateArgument(path, true);
                var candidate = _fileSystem.Inspect(Path.Combine(_root, path));
                if (candidate.Kind != WorkspaceNodeKind.Missing)
                {
                    if (sources.Count == ProjectRulesLimits.MaxProjectRuleSources) return new(WorkspaceErrorCode.RulesLimit);
                    var file = await ReadVerifiedFileAsync(path, state, linked.Token, expected: candidate,
                        maxBytes: ProjectRulesLimits.MaxProjectRuleFileBytes);
                    sources.Add(new(path, directory, file.Bytes.LongLength,
                        Convert.ToHexStringLower(SHA256.HashData(file.Bytes)), file.Content));
                }
                observed.Add(new(directory, parent.State, candidate));
            }
            Revalidate(parents);
            var error = RevalidateProjectRulesPaths(observed, lease);
            return error == WorkspaceErrorCode.None ? new(error, new(normalized, sources, observed)) : new(error);
        }
        catch (OperationCanceledException) { throw; }
        catch (WorkspaceFailure ex) { return new(ex.Code); }
        catch (Exception ex)
        {
            return new(ex switch
            {
                UnauthorizedAccessException => WorkspaceErrorCode.AccessDenied,
                FileNotFoundException or DirectoryNotFoundException => WorkspaceErrorCode.PathChanged,
                ArgumentException or NotSupportedException => WorkspaceErrorCode.InvalidPath,
                _ => WorkspaceErrorCode.IoFailure
            });
        }
    }

    public Task<WorkspaceErrorCode> CheckProjectRulesPathAsync(string scope, string path, bool directory,
        ICollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease, CancellationToken token)
    {
        try
        {
            RequireRulesLease(lease); token.ThrowIfCancellationRequested();
            path = ValidateArgument(path, !directory);
            var target = directory ? path : path.Contains('/') ? path[..path.LastIndexOf('/')] : ".";
            if (scope != "." && target != scope && !target.StartsWith(scope + "/", StringComparison.Ordinal))
                return Task.FromResult(WorkspaceErrorCode.RulesScopeMismatch);
            var depth = target == "." ? 0 : target.Split('/').Length;
            if (depth > ProjectRulesLimits.MaxProjectRuleDepth) return Task.FromResult(WorkspaceErrorCode.RulesLimit);
            var state = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.Generation);
            var parents = InspectPath(target, WorkspaceNodeKind.Directory, state);
            foreach (var parent in parents)
            {
                token.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(_root, parent.Path);
                if (relative == "." || relative == scope || scope.StartsWith(relative + "/", StringComparison.Ordinal)) continue;
                var candidatePath = Path.Combine(parent.Path, "AGENTS.md");
                CheckSensitive(candidatePath, ContentBoundary.ToolArguments);
                var candidate = _fileSystem.Inspect(candidatePath);
                if (candidate.Kind != WorkspaceNodeKind.Missing) return Task.FromResult(WorkspaceErrorCode.RulesScopeMismatch);
                observations.Add(new(relative, parent.State, candidate));
            }
            Revalidate(parents);
            return Task.FromResult(WorkspaceErrorCode.None);
        }
        catch (WorkspaceFailure ex) { return Task.FromResult(ex.Code); }
    }

    public WorkspaceErrorCode RevalidateProjectRulesPaths(IReadOnlyCollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease)
    {
        RequireRulesLease(lease);
        foreach (var item in observations)
        {
            var parent = item.Directory == "." ? _root : Path.Combine(_root, item.Directory);
            var actualParent = _fileSystem.Inspect(parent);
            if (!ProjectRuleObservation.SameDirectory(actualParent, item.Parent)) return WorkspaceErrorCode.PathChanged;
            if (_fileSystem.Inspect(Path.Combine(parent, "AGENTS.md")) != item.Candidate) return WorkspaceErrorCode.RulesScopeMismatch;
        }
        return WorkspaceErrorCode.None;
    }

    public void ClearProjectRuleSnapshots() { lock (_gate) _snapshots.InvalidateAllForRootFailure(); }
    private void RequireRulesLease(WorkspaceOperationLease lease)
    {
        if (!lease.BelongsTo(_executionPolicy.WorkspaceOperations) || !lease.IsCurrent || _policy is null)
            throw new OperationCanceledException();
    }
}