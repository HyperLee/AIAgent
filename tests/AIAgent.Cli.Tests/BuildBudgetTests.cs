using AIAgent.Cli.Processes;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildBudgetTests
{
    [TestMethod]
    public void BUILDAC023_TwoLaunchPermissionsAndRemainingExecutionTimeAreAtomic()
    {
        var time = new ControlledTime(); var budget = new ProcessTurnBudget(time);
        Assert.IsTrue(budget.TryStart(DotnetTaskKind.Build, out var first));
        Assert.AreEqual(TimeSpan.FromSeconds(120), first!.Timeout);
        time.Advance(120); first.Dispose();
        Assert.IsTrue(budget.TryStart(DotnetTaskKind.Test, out var second));
        Assert.AreEqual(TimeSpan.FromSeconds(180), second!.Timeout);
        time.Advance(180); second.Dispose();
        Assert.IsFalse(budget.TryStart(DotnetTaskKind.Build, out _));
        budget.BeginTurn();
        Assert.IsTrue(budget.TryStart(DotnetTaskKind.Test, out var next)); next!.Dispose();
    }
    [TestMethod]
    public void BUILDAC023_ApprovalWaitingDoesNotConsumeExecutionAndCannotResetActiveLease()
    {
        var time = new ControlledTime(); var budget = new ProcessTurnBudget(time);
        time.Advance(1000);
        Assert.IsTrue(budget.TryStart(DotnetTaskKind.Test, out var first));
        time.Advance(180); first!.Dispose();
        Assert.IsTrue(budget.TryStart(DotnetTaskKind.Test, out var second));
        Assert.AreEqual(TimeSpan.FromSeconds(120), second!.Timeout);
        Assert.Throws<InvalidOperationException>(budget.BeginTurn);
        second.Dispose();
    }
    private sealed class ControlledTime : TimeProvider
    {
        private long _now;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _now;
        internal void Advance(long seconds) => _now += seconds * 1000;
    }
}