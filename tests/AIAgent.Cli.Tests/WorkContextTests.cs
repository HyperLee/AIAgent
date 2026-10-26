using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkContextTests
{
    internal const string Id = "0123456789abcdef0123456789abcdef";
    internal static WorkSaveDocument Document(WorkSavedPlan? plan = null) => new(Id, "工作", "2026-10-26T08:00:00Z",
        new("/example/workspace", 1, 2), ".", [], new(0, 0, ["歷史目標"], [], [], []), ["  原文備註  "], plan, []);

    [TestMethod]
    public void SAVEAC020_AtomicImportUsesNewPendingDefinitionsAndNoFacts()
    {
        using var session = GeminiFixtures.Session(); var context = new ConversationContext();
        var plans = new PlanStore(session.Policy, context.DecisionGate); var coordinator = new ContextCoordinator(context, plans, session.Policy);
        var historical = new WorkSavedPlan("目標", [new(PlanTaskKind.Analysis, "完成", "條件", 1, [], PlanTaskState.Completed, TaskCompletionBasis.ModelAssessment, "舊判斷", null),
            new(PlanTaskKind.Analysis, "受阻", "新條件", 1, [], PlanTaskState.Blocked, TaskCompletionBasis.None, "舊摘要", "model_reported")]);
        var candidate = coordinator.PrepareWorkImport(Document(historical));
        Assert.IsNull(context.Summary); Assert.IsNull(plans.Snapshot);
        Assert.IsTrue(coordinator.TryPublishWorkImport(candidate, default));
        Assert.AreEqual(AgentMode.Plan, plans.Mode); Assert.AreEqual(PlanApprovalState.Draft, plans.Snapshot!.ApprovalState);
        Assert.AreEqual(1L, plans.Snapshot.Revision); Assert.AreEqual(1, plans.Snapshot.Tasks.Count);
        var task = plans.Snapshot.Tasks[0]; Assert.AreEqual("受阻", task.Title); Assert.AreEqual(PlanTaskState.Pending, task.State);
        Assert.AreEqual(TaskCompletionBasis.None, task.CompletionBasis); Assert.IsNull(task.Summary); Assert.IsNull(task.BlockReason);
        Assert.AreEqual(0, plans.Facts.Count); Assert.AreEqual(0, context.CompletedTurnCount);
        Assert.AreEqual("  原文備註  ", context.Capture().Pins[0].Text);
        using var memory = JsonDocument.Parse(ContextDocuments.Memory(context.Summary, context.Capture().Pins));
        var summary = memory.RootElement.GetProperty("summary"); Assert.AreEqual("saved_work_summary", summary.GetProperty("source").GetString());
        Assert.AreEqual(Id, summary.GetProperty("work_id").GetString()); Assert.IsFalse(summary.TryGetProperty("through_turn_id", out _));
        Assert.AreEqual(historical, coordinator.LoadedWorkPlan);
        StringAssert.Contains(coordinator.BuildMemoryAppendix(), "saved_work_plan");
        StringAssert.Contains(coordinator.BuildMemoryAppendix(), "reported_state");
    }

    [TestMethod]
    public void SAVEAC020_StaleOrCancelledImportNeverPublishesPartialPinsOrPlan()
    {
        using var session = GeminiFixtures.Session(); var context = new ConversationContext(); var plans = new PlanStore(session.Policy, context.DecisionGate);
        var coordinator = new ContextCoordinator(context, plans, session.Policy); var candidate = coordinator.PrepareWorkImport(Document());
        coordinator.AddPin("目前");
        Assert.IsFalse(coordinator.TryPublishWorkImport(candidate, default)); Assert.IsNull(context.Summary); Assert.IsNull(coordinator.LoadedWorkPlan);
        Assert.AreEqual(1, context.Capture().Pins.Count); Assert.AreEqual(AgentMode.Execute, plans.Mode);
        candidate = coordinator.PrepareWorkImport(Document()); using var c = new CancellationTokenSource(); c.Cancel();
        Assert.IsFalse(coordinator.TryPublishWorkImport(candidate, c.Token)); Assert.IsNull(context.Summary);
    }

    [TestMethod]
    public void SAVEAC021_FirstCompactionRetainsSavedBackgroundAndOnlyCountsNewTurns()
    {
        using var session = GeminiFixtures.Session(); var context = new ConversationContext(); var plans = new PlanStore(session.Policy, context.DecisionGate);
        var coordinator = new ContextCoordinator(context, plans, session.Policy);
        Assert.IsTrue(coordinator.TryPublishWorkImport(coordinator.PrepareWorkImport(Document()), default));
        for (var i = 0; i < 3; i++) context.CommitCompletedTurn([LlmMessage.FromUserText("新回合"), LlmMessage.FromModel(new object())]);
        var stamp = coordinator.Capture(); var source = coordinator.PrepareCompaction(stamp, new WorkProjectionInspector());
        Assert.IsTrue(source.Contains("歷史目標", StringComparison.Ordinal) || source.Contains("\\u6B77", StringComparison.Ordinal));
        var compact = coordinator.PrepareCandidate(stamp, ContextTestData.Summary); Assert.IsNotNull(compact);
        Assert.IsTrue(coordinator.TryPublishCompaction(compact, default));
        Assert.AreEqual(1L, context.Summary!.CoveredTurnCount); Assert.AreEqual(Id, context.Summary.SourceWorkId);
        Assert.AreEqual(2, context.CompletedTurnCount);
        Assert.IsTrue(Encoding.UTF8.GetByteCount(coordinator.BuildMemoryAppendix()) <= 32768 + 300);
        coordinator.Clear(); Assert.IsNull(coordinator.LoadedWorkPlan); Assert.IsNull(context.Summary);
    }
}

internal sealed class WorkProjectionInspector : ILlmContextInspector
{
    public ContextRequestMeasure Measure(LlmRequest request) => new(ContextMeasureStatus.Measured, 1024);
    public ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages) =>
        new(turnId, messages[0].UserText!, "回答", []);
}