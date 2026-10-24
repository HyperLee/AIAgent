using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildWorkspaceTests
{
    [TestMethod]
    public async Task BUILDAC028_ProcessLeaseInvalidatesSnapshotsWithoutCancellingItselfOrPlan()
    {
        using var session = WorkspaceFixtures.Session();
        var plans = new PlanStore(session.Policy); var policy = new AgentExecutionPolicy(plans);
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a.txt", [65]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: policy);
        Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a.txt", default)).Status);
        Assert.AreEqual(1, files.Snapshots.Count);
        Assert.IsTrue(policy.WorkspaceOperations.TryAcquire(default, out var lease));
        using (lease)
        {
            var generation = plans.Generation;
            var first = files.InvalidateForProcess(lease!);
            Assert.AreEqual(0, files.Snapshots.Count); Assert.IsTrue(lease!.IsCurrent);
            Assert.AreEqual(first + 1, files.InvalidateForProcess(lease));
            Assert.AreEqual(generation, plans.Generation);
            var reads = fs.Opened;
            Assert.AreEqual(WorkspaceErrorCode.Unavailable, (await files.ReadFileAsync("a.txt", default)).ErrorCode);
            Assert.AreEqual(reads, fs.Opened);
        }
        Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a.txt", default)).Status);
    }

    [TestMethod]
    public async Task BUILDAC029_FileAndProcessUseTheSameExclusiveAdmission()
    {
        using var session = WorkspaceFixtures.Session(); var policy = new AgentExecutionPolicy(new(session.Policy));
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a.txt", [65]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fs.OnRead = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: policy);
        var reading = files.ReadFileAsync("a.txt", default); await entered.Task;
        Assert.IsFalse(policy.WorkspaceOperations.TryAcquire(default, out _));
        release.SetResult(); Assert.AreEqual(WorkspaceStatus.Success, (await reading).Status);
        Assert.IsTrue(policy.WorkspaceOperations.TryAcquire(default, out var lease)); lease!.Dispose();
    }

    [TestMethod]
    public async Task BUILDAC029_ClearCancelsProcessAndPreventsLateSnapshotInvalidation()
    {
        using var session = WorkspaceFixtures.Session(); var policy = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: policy);
        Assert.IsTrue(policy.WorkspaceOperations.TryAcquire(default, out var lease));
        using (lease)
        {
            files.Snapshots.Clear();
            Assert.IsTrue(lease!.CancellationToken.IsCancellationRequested); Assert.IsFalse(lease.IsCurrent);
            Assert.Throws<OperationCanceledException>(() => files.InvalidateForProcess(lease));
            Assert.IsFalse(policy.WorkspaceOperations.TryAcquire(default, out _));
        }
        Assert.IsTrue(policy.WorkspaceOperations.TryAcquire(default, out lease)); lease!.Dispose();
    }

    [TestMethod]
    public async Task BUILDAC029_DisposeWaitsUntilProcessReleasesLease()
    {
        using var session = WorkspaceFixtures.Session(); var policy = new AgentExecutionPolicy(new(session.Policy));
        var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: policy);
        Assert.IsTrue(policy.WorkspaceOperations.TryAcquire(default, out var lease));
        var disposal = files.DisposeAsync().AsTask();
        Assert.IsTrue(lease!.CancellationToken.IsCancellationRequested); Assert.IsFalse(disposal.IsCompleted);
        lease.Dispose(); await disposal;
        Assert.IsFalse(policy.WorkspaceOperations.TryAcquire(default, out _));
    }

    [TestMethod]
    public async Task BUILDAC028_CancelledActiveLeaseCanOnlyPerformFinalInvalidation()
    {
        using var session = WorkspaceFixtures.Session(); var policy = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: policy);
        Assert.IsTrue(policy.WorkspaceOperations.TryAcquire(default, out var lease));
        files.Snapshots.Clear();
        Assert.Throws<OperationCanceledException>(() => files.InvalidateForProcess(lease!));
        files.InvalidateAfterProcess(lease!);
        Assert.IsTrue(lease!.CancellationToken.IsCancellationRequested);
        lease.Dispose();
        Assert.Throws<OperationCanceledException>(() => files.InvalidateAfterProcess(lease));
    }

    [TestMethod]
    public async Task BUILDAC029_ForeignLeaseAndCancelledAdmissionAreRejected()
    {
        using var session = WorkspaceFixtures.Session(); var policy = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: policy);
        await using var other = new WorkspaceOperationCoordinator();
        Assert.IsTrue(other.TryAcquire(default, out var lease));
        using (lease) Assert.Throws<OperationCanceledException>(() => files.InvalidateForProcess(lease!));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.IsFalse(policy.WorkspaceOperations.TryAcquire(cancelled.Token, out _));
    }
}