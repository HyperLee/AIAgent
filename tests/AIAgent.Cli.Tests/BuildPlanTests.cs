using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildPlanTests
{
    [TestMethod]
    public void BUILDAC011_UnsupportedTestRecipeBlocksValidationWithoutDisablingBuild()
    {
        foreach (var task in new[] { "build", "test" })
        {
            using var f = new PlanFixture(); f.Store.ProcessCapabilityAvailable = true; f.Store.ProcessTestCapabilityAvailable = false;
            Assert.IsTrue(Submit(f, "[{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"" + task + "\"}]").Success);
            f.Approve(); Assert.IsTrue(f.Update("in_progress").Success);
            Assert.AreEqual(task == "build" ? PlanTaskState.InProgress : PlanTaskState.Blocked, f.Task.State);
            if (task == "test") Assert.AreEqual("capability_unavailable", f.Task.BlockReason);
        }
    }

    [TestMethod]
    public void BUILDAC010_V2ValidationAcceptsExplicitBuildAndTestRequirements()
    {
        using var session = WorkspaceFixtures.Session();
        var store = new PlanStore(session.Policy);
        store.SetMode(AgentMode.Plan);
        using var json = JsonDocument.Parse("""
            {"expected_revision":0,"objective":"verify","tasks":[{"kind":"validation","title":"verify","acceptance":"passed","requirements_version":2,"requirements":[{"tool":"run_dotnet_task","path":"app.csproj","task":"build"},{"tool":"run_dotnet_task","path":"app.csproj","task":"test"}]}]}
            """);
        Assert.AreEqual("none", store.Submit(json.RootElement).ErrorCode);
    }

    [TestMethod]
    public void BUILDAC010_VersionsAndProcessVariantsAreStrictAndAtomic()
    {
        using var f = new PlanFixture();
        string[] invalidTasks = [
            "{\"kind\":\"validation\",\"requirements_version\":2,\"requirements\":[]}",
            "{\"kind\":\"validation\",\"requirements_version\":2.0,\"requirements\":[]}",
            "{\"kind\":\"validation\",\"requirements_version\":2e0,\"requirements\":[]}",
            "{\"kind\":\"validation\",\"requirements_version\":3,\"requirements\":[]}",
            "{\"kind\":\"read\",\"requirements_version\":2,\"requirements\":[]}",
            "{\"kind\":\"validation\",\"requirements_version\":1,\"requirements\":[{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"build\"}]}"
        ];
        foreach (var item in invalidTasks)
        {
            using var doc = JsonDocument.Parse(item);
            var task = JsonSerializer.Deserialize<Dictionary<string, object>>(item)!;
            task["title"] = "task"; task["acceptance"] = "checked";
            Assert.AreEqual("invalid_arguments", f.Store.Submit(JsonSerializer.SerializeToElement(new
            { expected_revision = 0, objective = "verify", tasks = new[] { task } })).ErrorCode, item);
        }
        string[] invalidRequirements = [
            "{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\"}",
            "{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"Build\"}",
            "{\"tool\":\"run_dotnet_task\",\"path\":\"a.sln\",\"task\":\"build\"}",
            "{\"tool\":\"run_dotnet_task\",\"path\":\"*.csproj\",\"task\":\"build\"}",
            "{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"build\",\"start_line\":1}",
            "{\"tool\":\"read_file\",\"path\":\"a.csproj\",\"task\":\"build\"}",
            "{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"build\",\"task\":\"test\"}"
        ];
        foreach (var item in invalidRequirements)
            Assert.AreEqual("invalid_arguments", Submit(f, "[" + item + "]").ErrorCode, item);
        var requirement = "{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"build\"}";
        Assert.AreEqual("invalid_arguments", Submit(f, "[" + requirement + "," + requirement + "]").ErrorCode);
        Assert.AreEqual("invalid_arguments", Submit(f, "[" + string.Join(',', Enumerable.Repeat(requirement, 9)) + "]").ErrorCode);
        Assert.IsNull(f.Store.Snapshot);
    }

    [TestMethod]
    public void BUILDAC010_V1MigrationReplacesIdentityAndRequiresNewApproval()
    {
        using var f = new PlanFixture();
        Assert.IsTrue(f.Submit(kind: "validation").Success);
        var old = f.Store.ApprovalPreview()!;
        Assert.AreEqual(1, f.Task.RequirementsVersion);
        Assert.IsTrue(Submit(f, expected: 1, id: "t1").Success);
        Assert.AreEqual(2L, f.Store.Snapshot!.Revision);
        Assert.AreEqual(2, f.Task.RequirementsVersion);
        Assert.AreEqual("t2", f.Task.TaskId);
        Assert.AreEqual(PlanApprovalState.Draft, f.Store.Snapshot.ApprovalState);
        Assert.IsFalse(f.Store.Approve(old));
        f.Approve();
        Assert.IsTrue(f.Update("in_progress").Success);
        Assert.AreEqual(PlanTaskState.Blocked, f.Task.State);
        f.Store.ProcessCapabilityAvailable = true;
        Assert.IsTrue(f.Update("in_progress").Success);
        Assert.AreEqual(PlanTaskState.InProgress, f.Task.State);
        Assert.AreEqual(2, f.Task.WithState(PlanTaskState.Blocked, "x").RequirementsVersion);
        using var projection = JsonDocument.Parse(PlanProjection.State(AgentMode.Execute, f.Store.Snapshot));
        var projectedTask = projection.RootElement.GetProperty("plan").GetProperty("tasks")[0];
        Assert.AreEqual(2, projectedTask.GetProperty("requirements_version").GetInt32());
        Assert.AreEqual("build", projectedTask.GetProperty("requirements")[0].GetProperty("task").GetString());
    }

    [TestMethod]
    public void BUILDAC011_ProcessAdmissionNeedsExactApprovedInProgressValidation()
    {
        using var f = new PlanFixture();
        Assert.AreEqual("mode_restricted", f.Store.CheckProcess(DotnetTaskKind.Build, "a.csproj"));
        f.Store.SetMode(AgentMode.Execute);
        Assert.AreEqual("none", f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var standalone));
        Assert.IsNull(standalone);
        f.Store.SetMode(AgentMode.Plan); Assert.IsTrue(Submit(f).Success); f.Store.SetMode(AgentMode.Execute);
        Assert.AreEqual("plan_not_approved", f.Store.CheckProcess(DotnetTaskKind.Build, "a.csproj"));
        f.Approve(); f.Store.ProcessCapabilityAvailable = true;
        Assert.AreEqual("task_required", f.Store.CheckProcess(DotnetTaskKind.Build, "a.csproj"));
        f.Update("in_progress");
        Assert.AreEqual("task_scope_mismatch", f.Store.CheckProcess(DotnetTaskKind.Build, "b.csproj"));
        Assert.AreEqual("task_scope_mismatch", f.Store.CheckProcess(DotnetTaskKind.Test, "a.csproj"));
        Assert.AreEqual("none", f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var reservation));
        Assert.IsNotNull(reservation);
        f.Store.Clear(); f.Store.RecordProcess(reservation, ToolStatus.Success, "none", Success(DotnetTaskKind.Build));
        Assert.AreEqual(0, f.Store.Facts.Count);
    }

    [TestMethod]
    public void BUILDAC012_AllLatestProcessFactsAreRequiredAndTruncationKeepsValidation()
    {
        using var f = new PlanFixture();
        Assert.IsTrue(Submit(f, BuildAndTest).Success); f.Store.ProcessCapabilityAvailable = true; f.Approve(); f.Update("in_progress");
        Record(f, DotnetTaskKind.Build, Success(DotnetTaskKind.Build));
        Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
        Record(f, DotnetTaskKind.Test, Success(DotnetTaskKind.Test));
        Assert.IsTrue(f.Update("completed").Success);
        CollectionAssert.AreEqual(new[] { "op1", "op2" }, f.Task.CompletionFactIds.ToArray());
        Assert.IsTrue(f.Store.Facts.All(fact => fact.ProcessSummary!.StdoutTruncated));
    }

    [TestMethod]
    public void BUILDAC013_LatestFailureAndUnverifiedCleanupCannotUseEarlierSuccess()
    {
        using var f = new PlanFixture();
        Submit(f); f.Store.ProcessCapabilityAvailable = true; f.Approve(); f.Update("in_progress");
        Record(f, DotnetTaskKind.Build, Success(DotnetTaskKind.Build));
        var failed = new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj", ProcessStartState.Started,
            ProcessEndState.Exited, 1, 1, true, false, ValidationOutcome.NotVerified);
        Record(f, DotnetTaskKind.Build, failed, ToolStatus.Failed, "process_failed");
        Assert.AreEqual("process_failed", f.Task.BlockReason); f.Update("in_progress");
        Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
        var unverified = new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj", ProcessStartState.Started,
            ProcessEndState.Exited, 0, 1, false, false, ValidationOutcome.NotVerified);
        Record(f, DotnetTaskKind.Build, unverified);
        Assert.AreEqual(PlanTaskState.Blocked, f.Task.State); f.Update("in_progress");
        Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
        Record(f, DotnetTaskKind.Build, Success(DotnetTaskKind.Build));
        Assert.IsTrue(f.Update("completed").Success);
        CollectionAssert.AreEqual(new[] { "op4" }, f.Task.CompletionFactIds.ToArray());
    }

    [TestMethod]
    public void BUILDAC026_NotStartedReleasesCapacityAndDuplicateReceiptsAreIgnored()
    {
        using var f = new PlanFixture();
        Submit(f); f.Store.ProcessCapabilityAvailable = true; f.Approve(); f.Update("in_progress");
        f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var reservation);
        f.Store.RecordProcess(reservation, ToolStatus.Rejected, "approval_declined", new(DotnetTaskKind.Build,
            "a.csproj", ProcessStartState.NotStarted, ProcessEndState.NotStarted, null, 0, true, false, ValidationOutcome.NotVerified));
        Assert.AreEqual(0, f.Store.Facts.Count); Assert.AreEqual("approval_declined", f.Task.BlockReason);
        f.Update("in_progress");
        f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out reservation);
        f.Store.RecordProcess(reservation, ToolStatus.Success, "none", Success(DotnetTaskKind.Build));
        f.Store.RecordProcess(reservation, ToolStatus.Failed, "process_failed", Success(DotnetTaskKind.Build));
        Assert.AreEqual(1, f.Store.Facts.Count);
        Assert.IsTrue(f.Update("completed").Success);
    }

    [TestMethod]
    public void BUILDAC014_SummaryRejectsImpossibleSuccessAndOverflow()
    {
        Assert.Throws<ArgumentException>(() => new TestCounts(2, 1, 0, 0));
        Assert.Throws<OverflowException>(() => new TestCounts(long.MaxValue, long.MaxValue, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestCounts(-1, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj",
            ProcessStartState.NotStarted, ProcessEndState.Exited, 0, 0, true, false, ValidationOutcome.BuildSucceeded));
        Assert.Throws<ArgumentException>(() => new ProcessExecutionSummary(DotnetTaskKind.Test, "a.csproj",
            ProcessStartState.Started, ProcessEndState.Exited, 0, 0, true, false, ValidationOutcome.TestsPassed, new(1, 0, 0, 1)));
        Assert.AreEqual("[ProcessExecutionSummary]", Success(DotnetTaskKind.Build).ToString());
    }

    [TestMethod]
    public void BUILDAC013_LaterStartFailureCannotBeMaskedByAnEarlierSuccess()
    {
        using var f = new PlanFixture(); Submit(f); f.Store.ProcessCapabilityAvailable = true; f.Approve(); f.Update("in_progress");
        Record(f, DotnetTaskKind.Build, Success(DotnetTaskKind.Build));
        Record(f, DotnetTaskKind.Build, new(DotnetTaskKind.Build, "a.csproj", ProcessStartState.StartFailed,
            ProcessEndState.NotStarted, null, 1, true, false, ValidationOutcome.NotVerified), ToolStatus.Failed, "process_start_failed");
        Assert.AreEqual(2, f.Store.Facts.Count); Assert.AreEqual("process_start_failed", f.Task.BlockReason);
        f.Update("in_progress"); Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
    }

    [TestMethod]
    public void BUILDAC032_ProcessFactsKeepTheShared256LimitAndReleaseUnstartedReservations()
    {
        using var f = new PlanFixture(); Submit(f); f.Store.ProcessCapabilityAvailable = true; f.Approve(); f.Update("in_progress");
        for (var index = 0; index < 255; index++) Record(f, DotnetTaskKind.Build, Success(DotnetTaskKind.Build));
        Assert.AreEqual("none", f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var unused));
        f.Store.Release(unused);
        Assert.AreEqual("none", f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var last));
        Assert.AreEqual("evidence_limit", f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out _));
        f.Store.RecordProcess(last, ToolStatus.Success, "none", Success(DotnetTaskKind.Build));
        Assert.AreEqual(256, f.Store.Facts.Count);
        f.Update("in_progress"); Assert.IsTrue(f.Update("completed").Success);
        CollectionAssert.AreEqual(new[] { "op256" }, f.Task.CompletionFactIds.ToArray());
    }

    [TestMethod]
    public void BUILDAC061_ProjectionReservesEveryV2CompletionIdAndAllBlockReasons()
    {
        var requirements = Enumerable.Range(0, 8).Select(i => new TaskRequirement("run_dotnet_task", "p" + i + ".csproj", task: DotnetTaskKind.Test)).ToArray();
        var task = new PlanTaskSnapshot("t1", PlanTaskKind.Validation, "verify", "passed", requirements, requirementsVersion: 2);
        var plan = new PlanSnapshot("p1", 1, "objective", PlanApprovalState.Approved, [task]);
        var upper = PlanProjection.State(AgentMode.Execute, plan, true);
        using var document = JsonDocument.Parse(upper);
        Assert.AreEqual(8, document.RootElement.GetProperty("plan").GetProperty("tasks")[0].GetProperty("completion_fact_ids").GetArrayLength());
        foreach (var reason in new[] { "capability_unavailable", "process_start_failed", "process_failed", "process_timed_out",
            "process_output_limit", "validation_inconclusive", "process_unconfirmed", "resource_limit" })
        {
            var blocked = plan.WithTasks([task.WithState(PlanTaskState.Blocked, null, reason)]);
            Assert.IsTrue(PlanProjection.State(AgentMode.Execute, blocked).Length <= upper.Length, reason);
            Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(PlanProjection.Display(AgentMode.Execute, blocked, 0, true)) <=
                System.Text.Encoding.UTF8.GetByteCount(PlanProjection.Display(AgentMode.Execute, plan, 0, true, true)), reason);
        }
    }

    private const string BuildAndTest = "[{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"build\"},{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"test\"}]";
    private static PlanMutationResult Submit(PlanFixture f,
        string requirements = "[{\"tool\":\"run_dotnet_task\",\"path\":\"a.csproj\",\"task\":\"build\"}]", long expected = 0, string? id = null)
    {
        using var doc = JsonDocument.Parse($$"""
            {"expected_revision":{{expected}},"objective":"objective","tasks":[{"kind":"validation","title":"task","acceptance":"checked","requirements_version":2,"requirements":{{requirements}}{{(id is null ? "" : ",\"task_id\":\"" + id + "\"")}}}]}
            """);
        return f.Store.Submit(doc.RootElement);
    }
    private static ProcessExecutionSummary Success(DotnetTaskKind kind) => new(kind, "a.csproj", ProcessStartState.Started,
        ProcessEndState.Exited, 0, 1, true, false, kind == DotnetTaskKind.Build ? ValidationOutcome.BuildSucceeded : ValidationOutcome.TestsPassed,
        kind == DotnetTaskKind.Test ? new(2, 1, 0, 1) : null, stdoutTruncated: true);
    private static void Record(PlanFixture f, DotnetTaskKind kind, ProcessExecutionSummary summary,
        ToolStatus status = ToolStatus.Success, string error = "none")
    {
        Assert.AreEqual("none", f.Store.ReserveProcess(kind, "a.csproj", out var reservation));
        f.Store.RecordProcess(reservation, status, error, summary);
    }
}