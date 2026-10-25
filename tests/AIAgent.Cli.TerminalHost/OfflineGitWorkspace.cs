using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

internal sealed class OfflineGitFactory(string scenario, FileStream? gate) : IGitReadWorkspaceFactory
{
    public IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => new OfflineGitWorkspace(scenario, gate, workspace.ProjectRulesWorkspace!.ExecutionPolicy);
}

internal sealed class OfflineGitWorkspace(string scenario, FileStream? gate, AgentExecutionPolicy execution) : IGitReadWorkspace
{
    private int _validations;
    public async Task<GitCaptureResult> CaptureAsync(CancellationToken cancellationToken)
    {
        if (scenario == "review-capture-busy")
        { await Pause("OFFLINE_REVIEW_CAPTURE_PAUSED", cancellationToken); return new("turn_cancelled"); }
        var old = new GitSource("中文 空白.cs", GitLayer.Staged, GitSide.Old, Encoding.UTF8.GetBytes("context\r\nold\n"), "100644");
        var next = new GitSource("中文 空白.cs", GitLayer.Staged, GitSide.New, Encoding.UTF8.GetBytes("context\r\nnew\n"), "100644");
        return new("none", new("", ".", new string('1', 40), "sha1", scenario == "review-empty" ? [] : [new("中文 空白.cs", GitLayer.Staged, old, next)], [], new(".", []), execution.Rules!.Capture()));
    }
    public async Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (scenario == "review-tool-busy" && ++_validations == 4) { await Pause("OFFLINE_REVIEW_TOOL_PAUSED", cancellationToken); return "turn_cancelled"; }
        return cancellationToken.IsCancellationRequested ? "turn_cancelled" : "none";
    }
    public Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken cancellationToken) => Task.FromResult("none");
    private async Task Pause(string label, CancellationToken token)
    {
        Console.WriteLine(label); using var observed = token.Register(() => Console.WriteLine("OFFLINE_REVIEW_CANCEL_OBSERVED"));
        await gate!.ReadExactlyAsync(new byte[1], CancellationToken.None);
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}