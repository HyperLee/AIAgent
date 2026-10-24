using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanStoreTests
{
    [TestMethod]
    public void PLANAC008_RevisionsAreAtomicAndIdentifiersAreNeverReused()
    {
        using var f = new PlanFixture();
        Assert.IsTrue(f.Submit().Success);
        Assert.AreEqual("p1", f.Store.Snapshot!.PlanId);
        Assert.AreEqual(1L, f.Store.Snapshot.Revision);
        var preview = f.Store.ApprovalPreview()!;
        Assert.AreEqual("plan_revision_conflict", f.Submit(expected: 0).ErrorCode);
        Assert.AreEqual("no_change", f.Submit(expected: 1, id: "t1").ErrorCode);
        Assert.IsTrue(f.Store.Approve(preview)); f.Store.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Submit(expected: 1, id: "t1", title: "revised").Success);
        Assert.AreEqual("t2", f.Store.Snapshot.Tasks.Single().TaskId);
        Assert.AreEqual(PlanApprovalState.Draft, f.Store.Snapshot.ApprovalState);
        Assert.IsFalse(f.Store.Approve(preview));
        f.Store.Clear(); Assert.IsTrue(f.Submit().Success);
        Assert.AreEqual("p2", f.Store.Snapshot.PlanId); Assert.AreEqual("t3", f.Store.Snapshot.Tasks.Single().TaskId);
    }

    [TestMethod]
    public void PLANAC009_OnlyExactExistingTaskDefinitionsRetainIdentityAndEvidence()
    {
        using var f = new PlanFixture(); f.Submit(kind: "analysis"); f.Approve(); f.Update("in_progress"); f.Update("completed");
        var fact = f.Store.Facts.Single(); f.Store.SetMode(AgentMode.Plan);
        Assert.AreEqual("no_change", f.Submit(expected: 1, id: "t1", kind: "analysis", title: " task ").ErrorCode);
        Assert.AreEqual(PlanTaskState.Completed, f.Task.State);
        Assert.IsTrue(f.Submit(expected: 1, id: "t1", kind: "analysis", title: "changed").Success);
        Assert.AreEqual("t2", f.Task.TaskId); Assert.AreEqual(PlanTaskState.Pending, f.Task.State);
        Assert.AreEqual("t1", fact.Context.TaskId); Assert.AreEqual(1, f.Store.Facts.Count);
        Assert.AreEqual("task_not_found", f.Submit(expected: 2, id: "t1", kind: "analysis").ErrorCode);
        Assert.AreEqual("task_not_found", f.Submit(expected: 2, id: "t2", objective: "new objective").ErrorCode);
        Assert.IsTrue(f.Submit(expected: 2, objective: "new objective").Success);
        Assert.AreEqual("p2", f.Store.Snapshot!.PlanId); Assert.AreEqual(1L, f.Store.Snapshot.Revision);
    }

    [TestMethod]
    public void PLANAC011_TaskTransitionsAndValidationUseLocalRules()
    {
        using var f = new PlanFixture();
        f.Store.SetMode(AgentMode.Execute);
        Assert.AreEqual("plan_required", f.Update("in_progress").ErrorCode);
        f.Store.SetMode(AgentMode.Plan);
        f.Submit(kind: "validation"); f.Store.SetMode(AgentMode.Execute);
        Assert.AreEqual("plan_not_approved", f.Update("in_progress").ErrorCode);
        f.Store.SetMode(AgentMode.Plan); f.Approve();
        Assert.IsTrue(f.Update("in_progress").Success);
        Assert.AreEqual(PlanTaskState.Blocked, f.Task.State); Assert.AreEqual("capability_unavailable", f.Task.BlockReason);
        Assert.AreEqual("capability_unavailable", f.Update("completed").ErrorCode);
        Assert.AreEqual(0, f.Store.Facts.Count);
        f.Store.Clear(); f.Store.SetMode(AgentMode.Plan); f.Submit(kind: "analysis"); f.Approve();
        Assert.AreEqual("invalid_task_transition", f.Update("completed").ErrorCode);
        Assert.IsTrue(f.Update("in_progress").Success);
        Assert.AreEqual("invalid_task_transition", f.Update("in_progress").ErrorCode);
        Assert.IsTrue(f.Update("completed", "analysis summary").Success);
        Assert.AreEqual(TaskCompletionBasis.ModelAssessment, f.Task.CompletionBasis);
        Assert.AreEqual("analysis summary", f.Store.Facts.Single().Summary);
        Assert.AreEqual("invalid_task_transition", f.Update("in_progress").ErrorCode);
        Assert.AreEqual("invalid_task_transition", f.Update("completed").ErrorCode);
    }

    [TestMethod]
    public void PLANAC014_LatestMatchingFailureCannotBeHiddenByEarlierSuccess()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        var request = new TaskRequirement("read_file", "a");
        Assert.AreEqual("none", f.Store.Reserve(request, false, out var first));
        f.Store.Record(first, ToolStatus.Success, "none", bytes: 1);
        Assert.AreEqual("none", f.Store.Reserve(request, false, out var second));
        f.Store.Record(second, ToolStatus.Failed, "io_failure");
        Assert.AreEqual(PlanTaskState.Blocked, f.Task.State);
        f.Update("in_progress");
        Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
        f.Store.Reserve(request, false, out var third); f.Store.Record(third, ToolStatus.Success, "none");
        Assert.IsTrue(f.Update("completed").Success);
        CollectionAssert.AreEqual(new[] { "op3" }, f.Task.CompletionFactIds.ToArray());
    }

    [TestMethod]
    public void PLANAC025_ReservationsEnforceGlobalFactLimitAndClearInvalidatesLateResults()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        var request = new TaskRequirement("read_file", "a");
        for (var i = 0; i < 255; i++)
        { f.Store.Reserve(request, false, out var item); f.Store.Record(item, ToolStatus.Success, "none"); }
        f.Store.Reserve(request, false, out var last);
        Assert.AreEqual("evidence_limit", f.Store.Reserve(request, false, out _));
        f.Store.Record(last, ToolStatus.Success, "none"); f.Store.Record(last, ToolStatus.Success, "none");
        Assert.AreEqual(256, f.Store.Facts.Count);
        Assert.AreEqual("resource_limit", f.Task.BlockReason);
        f.Store.Clear(); f.Store.Record(last, ToolStatus.Success, "none");
        Assert.AreEqual(0, f.Store.Facts.Count); Assert.IsNull(f.Store.Snapshot);
    }

    [TestMethod]
    public void PLANAC006_StrictArgumentsRejectMalformedInputsWithoutPartialState()
    {
        using var f = new PlanFixture();
        var valid = PlanFixture.Submission().GetRawText();
        foreach (var json in new[] { "null", "[]", "{}", valid.Replace("\"expected_revision\":0", "\"expected_revision\":-0", StringComparison.Ordinal),
            valid.Replace("\"expected_revision\":0", "\"expected_revision\":0.0", StringComparison.Ordinal),
            valid.Replace("\"expected_revision\":0", "\"expected_revision\":0e0", StringComparison.Ordinal),
            valid.Replace("\"expected_revision\":0", "\"expected_revision\":0,\"expected_revision\":0", StringComparison.Ordinal),
            valid.Replace("\"title\":\"task\"", "\"title\":null", StringComparison.Ordinal),
            valid.Replace("\"title\":\"task\"", "\"title\":\"task\",\"state\":\"completed\"", StringComparison.Ordinal),
            valid.Replace("\"path\":\"a\"", "\"path\":\"a\",\"path\":\"b\"", StringComparison.Ordinal) })
        {
            Assert.AreEqual("invalid_arguments", f.Store.Submit(GeminiFixtures.Element(json)).ErrorCode, json);
            Assert.IsNull(f.Store.Snapshot);
        }
        Assert.IsTrue(f.Submit().Success); Assert.AreEqual("p1", f.Store.Snapshot!.PlanId);
    }

    [TestMethod]
    public void PLANAC007_RawLengthsUnicodeAndRequirementLimitsAreEnforced()
    {
        using var f = new PlanFixture();
        foreach (var title in new[] { " ", "bad\nline", "bad\u2028line", new string('x', 257) })
            Assert.AreEqual("invalid_arguments", f.Submit(title: title).ErrorCode);
        Assert.IsTrue(f.Submit(title: "中文😀" + new string('x', 252)).Success);
        Assert.AreEqual("invalid_path", f.Store.Submit(PlanFixture.Submission(path: "../a", expected: 1)).ErrorCode);
        Assert.AreEqual("sensitive_path", f.Store.Submit(PlanFixture.Submission(path: ".env", expected: 1)).ErrorCode);
        Assert.AreEqual("invalid_arguments", f.Store.Submit(PlanFixture.Submission(path: "a", tool: "read_file_range", start: 1, end: 201, expected: 1)).ErrorCode);
        Assert.IsTrue(f.Store.Submit(PlanFixture.Submission(path: "a", tool: "read_file_range", start: 1, end: 200, expected: 1)).Success);
    }

    [TestMethod]
    public void PLANAC024_ConservativeCapacityRejectsBeforePublishingAndReservesAutomaticClose()
    {
        using var f = new PlanFixture();
        var tasks = Enumerable.Range(0, 20).Select(_ => new { kind = "analysis", title = new string('x', 256), acceptance = new string('y', 1024), requirements = Array.Empty<object>() }).ToArray();
        Assert.AreEqual("plan_too_large", f.Store.Submit(JsonSerializer.SerializeToElement(new { expected_revision = 0, objective = new string('z', 2048), tasks })).ErrorCode);
        Assert.IsNull(f.Store.Snapshot);
        f.Submit(); f.Approve(); f.Update("in_progress", new string('x', 2048));
        f.Store.BlockInProgress("turn_paused");
        Assert.AreEqual("turn_paused", f.Task.BlockReason);
        Assert.IsTrue(PlanProjection.State(f.Store.Mode, f.Store.Snapshot, true).Length <= 32768);
        Assert.IsNotNull(f.Store.Display());
    }

    [TestMethod]
    public void PLANAC007_InvalidSurrogateIsRejectedAsArgumentsWithoutThrowing()
    {
        using var f = new PlanFixture();
        var json = PlanFixture.Submission().GetRawText().Replace("\"title\":\"task\"", "\"title\":\"\\uD800\"", StringComparison.Ordinal);
        Assert.AreEqual("invalid_arguments", f.Store.Submit(GeminiFixtures.Element(json)).ErrorCode);
        Assert.IsNull(f.Store.Snapshot);
    }

    [TestMethod]
    public void PLANAC030_BorrowedGenerationRejectsLateInitialSubmissionAfterClear()
    {
        using var f = new PlanFixture();
        using (f.Store.BorrowGeneration())
        {
            f.Store.Clear();
            Assert.AreEqual("unavailable", f.Submit().ErrorCode);
        }
        Assert.IsTrue(f.Submit().Success); Assert.AreEqual("p1", f.Store.Snapshot!.PlanId);
    }
}

internal sealed class PlanFixture : IDisposable
{
    internal BootstrapSession Session { get; } = WorkspaceFixtures.Session();
    internal PlanStore Store { get; }
    internal PlanTaskSnapshot Task => Store.Snapshot!.Tasks.Single();
    internal PlanFixture() { Store = new(Session.Policy); Store.SetMode(AgentMode.Plan); }
    internal PlanMutationResult Submit(long expected = 0, string? id = null, string kind = "read", string title = "task", string objective = "objective") =>
        Store.Submit(Submission(expected, id, kind, title, objective));
    internal static JsonElement Submission(long expected = 0, string? id = null, string kind = "read", string title = "task", string objective = "objective",
        string tool = "read_file", string path = "a", int? start = null, int? end = null)
    {
        var requirement = new Dictionary<string, object?> { ["tool"] = tool, ["path"] = path };
        if (start is not null) requirement["start_line"] = start;
        if (end is not null) requirement["end_line"] = end;
        var task = new Dictionary<string, object?> { ["kind"] = kind, ["title"] = title, ["acceptance"] = "checked",
            ["requirements"] = kind is "analysis" or "validation" ? Array.Empty<object>() : new object[] { requirement } };
        if (id is not null) task["task_id"] = id;
        return JsonSerializer.SerializeToElement(new { expected_revision = expected, objective, tasks = new[] { task } });
    }
    internal void Approve() { Assert.IsTrue(Store.Approve(Store.ApprovalPreview()!)); }
    internal PlanMutationResult Update(string status, string summary = "summary") => Store.Update(JsonSerializer.SerializeToElement(new
    {
        plan_id = Store.Snapshot?.PlanId ?? "p1", expected_revision = Store.Snapshot?.Revision ?? 1,
        task_id = Store.Snapshot?.Tasks.Single().TaskId ?? "t1", status, summary
    }));
    public void Dispose() { Store.Stop(); Session.Dispose(); }
}