using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkSummaryTests
{
    [TestMethod]
    public async Task SAVEAC012_SaveSummaryUsesSameRunnerExactlyOnceWithoutChangingContext()
    {
        await using var f = new ContextFlowFixture(2);
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(ContextTestData.Summary));
        var stamp = f.Coordinator.Capture(); var revision = f.Context.Revision;
        var source = f.Coordinator.PrepareWorkSource(stamp, new WorkProjectionInspector());
        var result = await f.Runner.RunWorkSaveSummaryAsync(stamp, source, default);
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status); Assert.AreEqual(1, result.GenerationsStarted);
        Assert.AreEqual(1, f.Client.Requests.Count); Assert.AreEqual(0, f.Client.Requests[0].Tools.Count);
        Assert.AreEqual(1, f.Client.Requests[0].Messages.Count); Assert.AreEqual(2, f.Context.CompletedTurnCount);
        Assert.AreEqual(revision, f.Context.Revision); Assert.IsNull(f.Context.Summary);
        Assert.AreEqual(1, f.Budget.Snapshot().ProgramGenerationsStarted); Assert.AreEqual(0, f.Budget.Snapshot().TurnGenerationsStarted);
        Assert.AreEqual(0, f.Plans.Facts.Count);
    }

    [TestMethod]
    public async Task SAVEAC014_SaveBudgetExhaustionNeverStartsClient()
    {
        await using var f = new ContextFlowFixture(2); f.Budget.SetProgramLimit(1);
        Assert.IsTrue(f.Budget.TryBeginGeneration(GenerationPurpose.WorkSaveSummary, true, default, () => true));
        var stamp = f.Coordinator.Capture(); var result = await f.Runner.RunWorkSaveSummaryAsync(stamp,
            f.Coordinator.PrepareWorkSource(stamp, new WorkProjectionInspector()), default);
        Assert.AreEqual(AgentStopReason.ProgramGenerationLimit, result.StopReason);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.AreEqual(0, result.GenerationsStarted);
    }
}