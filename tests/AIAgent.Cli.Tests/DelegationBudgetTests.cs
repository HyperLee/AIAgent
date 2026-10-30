using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationBudgetTests
{
    [TestMethod]
    public void MULTIAC018_ReservationsProtectParentAndShareActualTurnAndProgramLimits()
    {
        var budget = new GenerationBudget(); budget.BeginConversation();
        Assert.IsTrue(budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true));
        Assert.IsFalse(budget.TryReserveDelegation(1, 2, default, out _));
        Assert.IsTrue(budget.TryReserveDelegation(1, 1, default, out var lease));
        Assert.AreEqual(3, lease!.Remaining(0));
        for (var i = 0; i < 3; i++) Assert.IsTrue(lease.TryBeginChild(0, default, () => true));
        Assert.IsFalse(lease.TryBeginChild(0, default, () => true));
        Assert.IsFalse(budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true));
        Assert.IsTrue(lease.TryBeginParent(default, () => true));
        Assert.AreEqual(5, budget.Snapshot().ProgramGenerationsStarted);
        lease.Dispose();
    }

    [TestMethod]
    public void MULTIAC019_ReleasingUnusedReservationsDoesNotRefundStartedRequests()
    {
        var budget = new GenerationBudget(); budget.SetTurnLimit(6); budget.BeginConversation();
        Assert.IsTrue(budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true));
        Assert.IsTrue(budget.TryReserveDelegation(7, 2, default, out var lease));
        Assert.AreEqual(2, lease!.Remaining(0)); Assert.AreEqual(2, lease.Remaining(1));
        Assert.IsTrue(lease.TryBeginChild(0, default, () => true)); lease.ReleaseChild(0);
        Assert.AreEqual(2, budget.Snapshot().TurnGenerationsStarted);
        Assert.AreEqual(2, lease.Remaining(1));
        lease.Dispose(); Assert.AreEqual(2, budget.Snapshot().ProgramGenerationsStarted);
    }

    [TestMethod]
    public async Task MULTIAC017_WorkspaceWaitersAreFifoAndCancelledWaitersNeverAcquire()
    {
        await using var operations = new WorkspaceOperationCoordinator();
        Assert.IsTrue(operations.TryAcquire(default, out var first));
        using var cancelled = new CancellationTokenSource();
        var a = operations.AcquireAsync(default); var b = operations.AcquireAsync(cancelled.Token); var c = operations.AcquireAsync(default);
        cancelled.Cancel(); first!.Dispose();
        await using var second = await a;
        Assert.IsFalse(c.IsCompleted); Assert.IsFalse(operations.TryAcquire(default, out _));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await b);
        second.Dispose(); await using var third = await c;
        Assert.IsTrue(third.IsCurrent);
    }
}