using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    private async Task CheckSkillsAsync(WorkspaceOperation operation, CancellationToken token)
    {
        if (operation.Lease is null || _executionPolicy.Skills is not { } skills) throw Rejected(WorkspaceErrorCode.SkillsUnavailable);
        var error = await skills.ValidateAsync(token, operation.Lease);
        if (error != "none") throw Rejected(SkillsError(error));
        operation.SkillsStamp ??= skills.Capture();
        if (!skills.IsCurrent(operation.SkillsStamp)) throw Rejected(WorkspaceErrorCode.SkillsChanged);
    }
    private async Task CheckSkillsAfterPublicationAsync(string path, WorkspaceNodeState actual, WorkspaceOperation operation)
    {
        var skills = _executionPolicy.Skills;
        if (skills?.Capture().Snapshot is not { } snapshot) return;
        try
        {
            var active = snapshot.Source.Descriptor.Path;
            var candidate = _fileSystem.Inspect(Path.Combine(_root, active));
            if (active == path || candidate.Kind == WorkspaceNodeKind.File && candidate.Device == actual.Device && candidate.Identity == actual.Identity)
                skills.MarkStale();
            else if (operation.Lease?.IsCurrent == true && !_lifetime.IsCancellationRequested)
                _ = await skills.ValidateAsync(CancellationToken.None, operation.Lease);
        }
        catch (SkillsSensitiveException) { skills.MarkSensitive(); }
        catch (OperationCanceledException) when (operation.Lease?.IsCurrent != true || _lifetime.IsCancellationRequested) { }
        catch (Exception) { skills.MarkFatalFailure(); }
    }
    private static WorkspaceErrorCode SkillsError(string error) => error switch
    {
        "skills_changed" => WorkspaceErrorCode.SkillsChanged,
        "skills_limit" => WorkspaceErrorCode.SkillsLimit,
        _ => WorkspaceErrorCode.SkillsUnavailable
    };
}