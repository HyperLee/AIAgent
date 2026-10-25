using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    private async Task CheckRulesOperationAsync(string path, bool directory, WorkspaceOperation operation, CancellationToken token)
    {
        var rules = _executionPolicy.Rules;
        if (rules is null || operation.Lease is null) throw Rejected(WorkspaceErrorCode.RulesUnavailable);
        var error = await rules.ValidateAsync(token, operation.Lease);
        if (error != "none") throw new WorkspaceFailure(error == "workspace_unavailable" ? WorkspaceStatus.Failed : WorkspaceStatus.Rejected, RulesError(error));
        var stamp = rules.Capture();
        operation.RulesStamp ??= stamp;
        if (!rules.IsCurrent(operation.RulesStamp)) throw Rejected(WorkspaceErrorCode.RulesChanged);
        var boundary = await CheckProjectRulesPathAsync(stamp.Scope, path, directory, operation.RuleBoundaries, operation.Lease, token);
        if (boundary != WorkspaceErrorCode.None) throw Rejected(boundary);
    }
    private async Task CheckRulesResultAsync(WorkspaceOperation operation, CancellationToken token)
    {
        if (operation.Lease is null) throw Rejected(WorkspaceErrorCode.RulesUnavailable);
        var error = await _executionPolicy.Rules!.ValidateAsync(token, operation.Lease);
        if (error != "none") throw new WorkspaceFailure(error == "workspace_unavailable" ? WorkspaceStatus.Failed : WorkspaceStatus.Rejected, RulesError(error));
        if (operation.RulesStamp is { } stamp && !_executionPolicy.Rules.IsCurrent(stamp)) throw Rejected(WorkspaceErrorCode.RulesChanged);
        var boundary = RevalidateProjectRulesPaths(operation.RuleBoundaries, operation.Lease);
        if (boundary != WorkspaceErrorCode.None) throw Rejected(boundary);
    }
    private async Task CheckRulesAfterPublicationAsync(string path, WorkspaceNodeState actual, WorkspaceOperation operation)
    {
        var rules = _executionPolicy.Rules!;
        try
        {
            var parent = Path.GetDirectoryName(Path.Combine(_root, path))!;
            var candidate = _fileSystem.Inspect(Path.Combine(parent, "AGENTS.md"));
            if (Path.GetFileName(path) == "AGENTS.md" || candidate.Kind == WorkspaceNodeKind.File &&
                candidate.Device == actual.Device && candidate.Identity == actual.Identity)
                rules.MarkStale();
            else if (operation.Lease?.IsCurrent == true && !_lifetime.IsCancellationRequested)
                _ = await rules.ValidateAsync(CancellationToken.None, operation.Lease);
        }
        catch (OperationCanceledException) when (operation.Lease?.IsCurrent != true || _lifetime.IsCancellationRequested) { }
        catch (Exception) { rules.MarkFatalFailure(); }
    }

    private static WorkspaceErrorCode RulesError(string error) => error switch
    {
        "workspace_unavailable" => WorkspaceErrorCode.WorkspaceUnavailable,
        "rules_changed" => WorkspaceErrorCode.RulesChanged,
        "rules_scope_mismatch" => WorkspaceErrorCode.RulesScopeMismatch,
        "rules_limit" => WorkspaceErrorCode.RulesLimit,
        _ => WorkspaceErrorCode.RulesUnavailable
    };
}