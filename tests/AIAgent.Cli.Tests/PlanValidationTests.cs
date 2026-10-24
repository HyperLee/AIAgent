using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanValidationTests
{
    [TestMethod]
    public void PLANAC006_AllSubmitLevelsRejectMissingExtraDuplicateCaseAndWrongTypes()
    {
        var valid = PlanFixture.Submission().GetRawText();
        var invalid = new[] {
            valid.Replace("\"objective\":\"objective\",", "", StringComparison.Ordinal),
            valid.Replace("\"objective\"", "\"Objective\"", StringComparison.Ordinal),
            valid.Replace("\"objective\":\"objective\"", "\"objective\":false", StringComparison.Ordinal),
            valid.Replace("\"tasks\":[", "\"approved\":true,\"tasks\":[", StringComparison.Ordinal),
            valid.Replace("\"kind\":\"read\",", "", StringComparison.Ordinal),
            valid.Replace("\"kind\":\"read\"", "\"kind\":\"Read\"", StringComparison.Ordinal),
            valid.Replace("\"kind\":\"read\"", "\"kind\":null", StringComparison.Ordinal),
            valid.Replace("\"acceptance\":\"checked\"", "\"acceptance\":[]", StringComparison.Ordinal),
            valid.Replace("\"requirements\":[", "\"Requirements\":[", StringComparison.Ordinal),
            valid.Replace("\"tool\":\"read_file\",", "", StringComparison.Ordinal),
            valid.Replace("\"tool\":\"read_file\"", "\"tool\":\"read_file\",\"approved\":true", StringComparison.Ordinal),
            valid.Replace("\"path\":\"a\"", "\"path\":null", StringComparison.Ordinal),
            valid.Replace("\"path\":\"a\"", "\"Path\":\"a\"", StringComparison.Ordinal),
            valid.Replace("\"path\":\"a\"", "\"path\":\"a\",\"start_line\":1", StringComparison.Ordinal)
        };
        using var f = new PlanFixture();
        foreach (var json in invalid)
        { Assert.AreEqual("invalid_arguments", f.Store.Submit(GeminiFixtures.Element(json)).ErrorCode); Assert.IsNull(f.Store.Snapshot); }
        Assert.AreEqual("none", f.Submit().ErrorCode);
    }

    [TestMethod]
    public void PLANAC006_IntegerWireFormsAndMalformedUtf16NeverPublish()
    {
        using var f = new PlanFixture(); var valid = PlanFixture.Submission().GetRawText();
        foreach (var number in new[] { "-0", "-1", "0.0", "0e0", "\"0\"", "null", "false", "9223372036854775808" })
            Assert.AreEqual("invalid_arguments", f.Store.Submit(GeminiFixtures.Element(valid.Replace("\"expected_revision\":0", "\"expected_revision\":" + number, StringComparison.Ordinal))).ErrorCode);
        var range = PlanFixture.Submission(tool: "read_file_range", start: 1, end: 2).GetRawText();
        foreach (var number in new[] { "-0", "0", "1.0", "1e0", "\"1\"", "null", "2147483648" })
            Assert.AreEqual("invalid_arguments", f.Store.Submit(GeminiFixtures.Element(range.Replace("\"start_line\":1", "\"start_line\":" + number, StringComparison.Ordinal))).ErrorCode);
        foreach (var text in new[] { "\\uD800", "\\uDC00", "\\u2028", "\\u2029", "\\u0000" })
            Assert.AreEqual("invalid_arguments", f.Store.Submit(GeminiFixtures.Element(valid.Replace("\"title\":\"task\"", "\"title\":\"" + text + "\"", StringComparison.Ordinal))).ErrorCode);
        Assert.IsNull(f.Store.Snapshot);
    }

    [TestMethod]
    public void PLANAC014_ModelEvidenceApprovalAndProcessReportsCannotUpdateTasks()
    {
        using var f = new PlanFixture(); f.Submit(kind: "analysis"); f.Approve(); f.Update("in_progress");
        var valid = JsonSerializer.Serialize(PlanSdkTests.Update("completed"));
        foreach (var field in new[] { "\"evidence\":[\"op1\"]", "\"approved\":true", "\"exit_code\":0", "\"mode\":\"execute\"", "\"status\":\"completed\"" })
        {
            Assert.AreEqual("invalid_arguments", f.Store.Update(GeminiFixtures.Element(valid[..^1] + "," + field + "}")).ErrorCode);
            Assert.AreEqual(PlanTaskState.InProgress, f.Task.State); Assert.AreEqual(0, f.Store.Facts.Count);
        }
        foreach (var summary in new[] { " ", new string('x', 2049), "bad\nsummary" })
            Assert.AreEqual("invalid_arguments", f.Update("completed", summary).ErrorCode);
        Assert.AreEqual("none", f.Update("completed", new string('x', 2048)).ErrorCode);
    }

    [TestMethod]
    public void PLANAC009_ReorderingTasksReconfirmsWithoutRebindingFacts()
    {
        using var f = new PlanFixture();
        object Task(string? id, string title) => id is null ? new { kind = "analysis", title, acceptance = "checked", requirements = Array.Empty<object>() } :
            new { task_id = id, kind = "analysis", title, acceptance = "checked", requirements = Array.Empty<object>() };
        Assert.IsTrue(f.Store.Submit(JsonSerializer.SerializeToElement(new { expected_revision = 0, objective = "o", tasks = new[] { Task(null, "a"), Task(null, "b") } })).Success);
        f.Approve();
        Assert.IsTrue(f.Store.Update(JsonSerializer.SerializeToElement(new { plan_id = "p1", expected_revision = 1, task_id = "t1", status = "in_progress", summary = "a" })).Success);
        Assert.AreEqual("task_in_progress", f.Store.Update(JsonSerializer.SerializeToElement(new { plan_id = "p1", expected_revision = 1, task_id = "t2", status = "in_progress", summary = "b" })).ErrorCode);
        Assert.IsTrue(f.Store.Update(JsonSerializer.SerializeToElement(new { plan_id = "p1", expected_revision = 1, task_id = "t1", status = "completed", summary = "assessed" })).Success);
        f.Store.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Store.Submit(JsonSerializer.SerializeToElement(new { expected_revision = 1, objective = "o", tasks = new[] { Task("t2", "b"), Task("t1", "a") } })).Success);
        Assert.AreEqual(2L, f.Store.Snapshot!.Revision); Assert.AreEqual(PlanApprovalState.Draft, f.Store.Snapshot.ApprovalState);
        Assert.AreEqual(PlanTaskState.Completed, f.Store.Snapshot.Tasks[1].State); Assert.AreEqual("op1", f.Store.Snapshot.Tasks[1].CompletionFactIds.Single());
        Assert.AreEqual(1L, f.Store.Facts.Single().Context.PlanRevision);
        Assert.AreEqual("task_not_found", f.Store.Submit(JsonSerializer.SerializeToElement(new { expected_revision = 2, objective = "o", tasks = new[] { Task("t1", "a"), Task("t1", "a") } })).ErrorCode);
    }
}