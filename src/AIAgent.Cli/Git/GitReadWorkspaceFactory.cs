using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Git;

internal interface IGitReadWorkspaceFactory
{
    IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace);
}

internal sealed class GitReadWorkspaceFactory : IGitReadWorkspaceFactory
{
    public IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => workspace is WorkspaceFiles files
        ? new GitReadWorkspace(files, session.Workspace.RootPath) : new UnavailableGitReadWorkspace();
    public override string ToString() => "[GitReadWorkspaceFactory]";
}
internal sealed class UnavailableGitReadWorkspace : IGitReadWorkspace
{
    public Task<GitCaptureResult> CaptureAsync(CancellationToken cancellationToken) => Task.FromResult(new GitCaptureResult("capability_unavailable"));
    public Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken cancellationToken) => Task.FromResult("capability_unavailable");
    public Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken cancellationToken) => Task.FromResult("capability_unavailable");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public override string ToString() => "[UnavailableGitReadWorkspace]";
}