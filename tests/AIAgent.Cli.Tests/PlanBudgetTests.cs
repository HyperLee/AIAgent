using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanBudgetTests
{
    [TestMethod]
    public void PLANAC024_ExactActualAndConservativeProjectionByteLimits()
    {
        foreach (var upper in new[] { false, true })
        foreach (var target in new[] { 32767, 32768, 32769 })
        {
            using var f = new PlanFixture();
            var baseline = TextPlan(0);
            var candidate = TextPlan(target - PlanProjection.State(AgentMode.Plan, baseline, upper).Length);
            Assert.AreEqual(target, PlanProjection.State(AgentMode.Plan, candidate, upper).Length);
            var result = f.Store.Submit(Submission(candidate));
            Assert.AreEqual(upper && target <= 32768 ? "none" : "plan_too_large", result.ErrorCode);
            if (result.Success)
            {
                f.Approve();
                Assert.IsTrue(PlanProjection.State(AgentMode.Execute, f.Store.Snapshot).Length <= 32768);
                Assert.IsTrue(PlanProjection.State(AgentMode.Execute, f.Store.Snapshot, true).Length <= 32768);
            }
            else Assert.IsNull(f.Store.Snapshot);
        }
    }

    [TestMethod]
    public void PLANAC024_ExactActualAndConservativePreviewByteLimitsIncludeEscapingAndPrompt()
    {
        foreach (var upper in new[] { false, true })
        foreach (var target in new[] { 65535, 65536, 65537 })
        {
            using var f = new PlanFixture();
            var baseline = PathPlan(0);
            var baselineBytes = Encoding.UTF8.GetByteCount(PlanProjection.Display(AgentMode.Plan, baseline, 0, true, upper));
            var candidate = PathPlan(target - baselineBytes);
            var block = PlanProjection.Display(AgentMode.Plan, candidate, 0, true, upper);
            Assert.AreEqual(target, Encoding.UTF8.GetByteCount(block));
            Assert.AreEqual(target <= 65536, PlanProjection.SafeDisplay(block, f.Session.Policy));
            Assert.IsTrue(PlanProjection.State(AgentMode.Plan, candidate, true).Length < 32768);
            var result = f.Store.Submit(Submission(candidate));
            Assert.AreEqual(upper && target <= 65536 ? "none" : "plan_too_large", result.ErrorCode);
        }
    }

    [TestMethod]
    public void PLANAC024_NewResultWriterRejectsWholeBlockAboveLimit()
    {
        using var f = new PlanFixture();
        foreach (var target in new[] { 65535, 65536, 65537 })
        {
            var baseline = new PlanSnapshot("p", 1, "objective", PlanApprovalState.Draft, []);
            var size = PlanProjection.ResultBytes(new("none", baseline), true).Length;
            var candidate = new PlanSnapshot("p" + new string('x', target - size), 1, "objective", PlanApprovalState.Draft, []);
            Assert.AreEqual(target, PlanProjection.ResultBytes(new("none", candidate), true).Length);
            Assert.AreEqual(target <= 65536, PlanProjection.Result(new("none", candidate), true, f.Session.Policy).Response is not null);
        }
    }

    [TestMethod]
    public void PLANAC026_NearCapacityTaskCanAlwaysPauseCancelOrBlockWithoutExtraSummary()
    {
        using var f = new PlanFixture();
        var baseline = TextPlan(0);
        var candidate = TextPlan(32768 - PlanProjection.State(AgentMode.Plan, baseline, true).Length - 2050);
        Assert.IsTrue(f.Store.Submit(Submission(candidate)).Success); f.Approve();
        var task = f.Store.Snapshot!.Tasks[0];
        Assert.IsTrue(f.Store.Update(JsonSerializer.SerializeToElement(new { plan_id = "p1", expected_revision = 1, task_id = task.TaskId,
            status = "in_progress", summary = new string('s', 2048) })).Success);
        foreach (var reason in new[] { "turn_cancelled", "model_outcome", "turn_paused", "resource_limit" })
        {
            f.Store.BlockInProgress(reason);
            Assert.AreEqual(PlanTaskState.Blocked, f.Store.Snapshot!.Tasks[0].State);
            Assert.AreEqual(2048, f.Store.Snapshot.Tasks[0].Summary!.Length);
            Assert.IsTrue(PlanProjection.State(f.Store.Mode, f.Store.Snapshot).Length <= 32768);
            Assert.IsNotNull(f.Store.Display());
            Assert.IsTrue(f.Store.Update(JsonSerializer.SerializeToElement(new { plan_id = "p1", expected_revision = 1, task_id = task.TaskId,
                status = "in_progress", summary = new string('s', 2048) })).Success);
        }
    }

    private static PlanSnapshot TextPlan(int padding)
    {
        var tasks = new List<PlanTaskSnapshot>();
        for (var i = 0; i < 20; i++)
        {
            var acceptance = Math.Min(1023, padding); padding -= acceptance;
            var title = Math.Min(255, padding); padding -= title;
            tasks.Add(new("t" + (i + 1), PlanTaskKind.Read, "t" + new string('x', title), "a" + new string('x', acceptance), [new("read_file", "a" + new string('p', 100))]));
        }
        Assert.IsTrue(padding is >= 0 and <= 2047);
        return new("p1", 1, "o" + new string('x', padding), PlanApprovalState.Draft, tasks);
    }

    private static PlanSnapshot PathPlan(int padding)
    {
        var brackets = padding / 8; var tail = padding % 8;
        var first = Math.Min(4095, brackets); var second = brackets - first;
        Assert.IsTrue(second + tail <= 4095);
        return new("p1", 1, "objective", PlanApprovalState.Draft, [new("t1", PlanTaskKind.Read, "task", "checked", [
            new("read_file", "a" + new string('[', first)), new("read_file", "b" + new string('[', second) + new string('x', tail))])]);
    }

    private static JsonElement Submission(PlanSnapshot plan) => JsonSerializer.SerializeToElement(new
    {
        expected_revision = 0, objective = plan.Objective, tasks = plan.Tasks.Select(t => new {
            kind = PlanProjection.Name(t.Kind), title = t.Title, acceptance = t.Acceptance,
            requirements = t.Requirements.Select(r => new { tool = r.ToolName, path = r.Path }).ToArray() }).ToArray()
    });
}