using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitCaptureDeadlineTests
{
    [TestMethod]
    public async Task GITAC009_CaptureDeadlineIsTimeoutRatherThanUserCancellation()
    {
        using var session = WorkspaceFixtures.Session();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, GitSnapshotTests.Files());
        await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        var clock = new ManualCaptureClock(); var adapter = new WaitingGitAdapter();
        await using var git = new GitReadWorkspace(files, () => adapter, clock);
        var capture = git.CaptureAsync(default); await adapter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); clock.Expire();
        Assert.AreEqual("git_timed_out", (await capture.WaitAsync(TimeSpan.FromSeconds(5))).ErrorCode);
    }

    private sealed class WaitingGitAdapter : IGitReadAdapter
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? ExecutableIdentity => "fixture";
        public async ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { }
            return new("turn_cancelled");
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ManualCaptureClock : TimeProvider
    {
        private Action? _expire;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { _expire = () => callback(state); return new ManualTimer(); }
        internal void Expire() => _expire!();
        private sealed class ManualTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}