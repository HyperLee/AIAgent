using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ProjectRulesLifecycleTests
{
    [TestMethod]
    public async Task RULEAC024_DisposeWaitsForAnInFlightValidation()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); await f.Rules.TryReloadAsync(default);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Fs.OnRead = async _ => { started.TrySetResult(); await release.Task; };
        var validation = f.Rules.ValidateAsync(default); await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var disposal = f.Rules.DisposeAsync().AsTask();
        var waited = !disposal.IsCompleted;
        release.TrySetResult();
        try { await validation; } catch (OperationCanceledException) { }
        await disposal;
        Assert.IsTrue(waited); Assert.IsTrue(f.Fs.Handles.All(h => h.Disposed));
    }

    [TestMethod]
    public async Task RULEAC015_ScopeChangePreservesPlanFactsButRequiresNewEvidenceForNewCompletion()
    {
        await using var f = new RulesFixture(); f.Fs.File("a", [65]); f.Fs.Directory("sub");
        await f.Rules.TryReloadAsync(default); f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission()).Success); Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!));
        Assert.IsTrue(Update(f.Plans, "in_progress").Success); await f.Files.ReadFileAsync("a", default);
        var old = f.Plans.Snapshot!; var preview = f.Plans.ApprovalPreview()!;
        Assert.AreEqual("none", await f.Rules.TryChangeScopeAsync("sub", default));
        Assert.AreEqual(old.PlanId, f.Plans.Snapshot!.PlanId); Assert.AreEqual(old.Revision, f.Plans.Snapshot.Revision);
        Assert.AreEqual(PlanApprovalState.Draft, f.Plans.Snapshot.ApprovalState);
        Assert.AreEqual(PlanTaskState.Blocked, f.Plans.Snapshot.Tasks.Single().State);
        Assert.AreEqual(1, f.Plans.Facts.Count); Assert.AreEqual(1L, f.Plans.Facts.Single().Context.RulesRevision);
        Assert.IsFalse(f.Plans.Approve(preview)); Assert.AreEqual(0, f.Files.Snapshots.Count);
        Assert.AreEqual("none", await f.Rules.TryChangeScopeAsync(".", default));
        Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!)); Assert.IsTrue(Update(f.Plans, "in_progress").Success);
        Assert.AreEqual("evidence_required", Update(f.Plans, "completed").ErrorCode);
        await f.Files.ReadFileAsync("a", default); Assert.IsTrue(Update(f.Plans, "completed").Success);
        Assert.AreEqual(3L, f.Plans.Snapshot.Tasks.Single().CompletionRulesRevision);
    }

    [TestMethod]
    public async Task RULEAC023_ApprovedOwnRulesWriteRecordsVerifiedFactBeforeRevocation()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", "old rule");
        using var session = WorkspaceFixtures.Session(root); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        var approval = new EditApproval(); await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval, execution);
        await execution.Rules!.TryReloadAsync(default); plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(PlanFixture.Submission(kind: "write", tool: "replace_text", path: "AGENTS.md")).Success);
        Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); Assert.IsTrue(Update(plans, "in_progress").Success);
        await files.ReadFileAsync("AGENTS.md", default);
        var result = await files.ReplaceTextAsync("AGENTS.md", "old rule", "new rule", default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual("new rule", await File.ReadAllTextAsync(root + "/AGENTS.md"));
        Assert.AreEqual(ProjectRulesStatus.Stale, execution.Rules.Capture().Status);
        Assert.AreEqual(PlanApprovalState.Draft, plans.Snapshot!.ApprovalState);
        Assert.AreEqual(FileWriteState.Verified, plans.Facts.Last().WriteState); Assert.AreEqual(1L, plans.Facts.Last().Context.RulesRevision);
        Assert.AreEqual(0, files.Snapshots.Count); Assert.AreEqual(1, approval.Calls);
    }

    [TestMethod]
    public async Task RULEAC022_SourceChangeDuringYesPreventsPublication()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", "old");
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        var approval = new EditApproval { OnConfirm = _ => File.WriteAllText(root + "/AGENTS.md", "new") };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval, execution);
        var result = await files.CreateFileAsync("new", "candidate", default);
        Assert.AreEqual(WorkspaceErrorCode.RulesChanged, result.ErrorCode); Assert.AreEqual(FileWriteState.NotApplied, result.WriteState);
        Assert.IsFalse(File.Exists(root + "/new")); Assert.AreEqual(ProjectRulesStatus.Stale, execution.Rules!.Capture().Status);
    }

    [TestMethod]
    public async Task RULEAC025_ClearDoesNotApplyChangedRulesAndExplicitReloadDoesNotReplay()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", "old");
        using var session = WorkspaceFixtures.Session(root); var f = new ChatFixture();
        foreach (var input in new[] { "first", "/clear", "blocked", "/rules reload", "next" })
            f.Interaction.Inputs.Enqueue(() => { if (input == "/clear") File.WriteAllText(root + "/AGENTS.md", "new"); return ChatInputResult.Submitted(input); });
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(2, f.Client.Requests.Count);
        Assert.IsTrue(f.Client.Requests[0].SystemInstruction.Contains("old", StringComparison.Ordinal));
        Assert.IsTrue(f.Client.Requests[1].SystemInstruction.Contains("new", StringComparison.Ordinal));
        Assert.AreEqual("next", f.Client.Requests[1].Messages.Last().UserText);
    }

    [TestMethod]
    public async Task RULEAC020_SummaryRequestOmitsRulesAndChangedSourceRejectsCandidate()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", "RULE_ORIGINAL_UNIQUE");
        using var session = WorkspaceFixtures.Session(root); var f = new ChatFixture("one", "two", "three", "/compact");
        f.Client.Respond = async (_, request, _) =>
        {
            if (request.Tools.Count != 0) return ChatFixture.Completed();
            Assert.IsFalse(request.SystemInstruction.Contains("RULE_ORIGINAL_UNIQUE", StringComparison.Ordinal));
            Assert.IsFalse(request.Messages.Single().UserText!.Contains("RULE_ORIGINAL_UNIQUE", StringComparison.Ordinal));
            await File.WriteAllTextAsync(root + "/AGENTS.md", "changed"); return ChatFixture.Completed(ContextTestData.Summary);
        };
        f.Interaction.Inputs.Enqueue(() => { Assert.AreEqual(3, f.Context.CompletedTurnCount); Assert.IsNull(f.Context.Capture().Summary); return ChatInputResult.Submitted("/rules"); });
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(4, f.Client.Requests.Count);
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("Stale", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task RULEAC025_NewKeyReloadUsesNewPolicyWithoutReusingOldRules()
    {
        const string replacement = "REPLACEMENT_SYNTHETIC_ONLY";
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", replacement);
        using var session = WorkspaceFixtures.Session(root); var f = new ChatFixture("authenticate", "blocked", "/rules");
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
        f.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter); f.Interaction.Keys.Enqueue(() => KeyInputResult.Accept(replacement));
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(0, f.Factory.ReplacementClient.Requests.Count);
        Assert.IsFalse(f.Interaction.Events.Any(e => e.Text?.Contains(replacement, StringComparison.Ordinal) == true));
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("Unavailable", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task RULEAC004_LiteralDirectoryAndInvalidCommandsDoNotGenerate()
    {
        var root = WorkspaceFixtures.NewDirectory(); const string directory = " 中文 空白 "; Directory.CreateDirectory(root + "/" + directory);
        await File.WriteAllTextAsync(root + "/" + directory + "/AGENTS.md", "leaf");
        using var session = WorkspaceFixtures.Session(root);
        var f = new ChatFixture("/Rules", "/rules reload extra", "/rules scope", "/rules scope " + directory, "/rules", "question", "/rules scope .");
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(1, f.Client.Requests.Count);
        Assert.IsTrue(f.Client.Requests.Single().SystemInstruction.Contains("leaf", StringComparison.Ordinal));
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains(directory, StringComparison.Ordinal) == true));
        Assert.IsFalse(f.Client.Requests.Any(r => r.Messages.Any(m => m.UserText?.StartsWith("/rules", StringComparison.Ordinal) == true)));
    }

    [TestMethod]
    public async Task RULEAC014_RulesGenerationMakesStoredContextMeasurementStale()
    {
        await using var f = new RulesFixture(); await f.Rules.TryReloadAsync(default);
        var contexts = new ContextCoordinator(new(), f.Plans, f.Session.Policy); contexts.BindRules(f.Rules);
        var stamp = contexts.Capture(); contexts.RecordMeasure(stamp, GenerationPurpose.Conversation, new(ContextMeasureStatus.Measured, 100));
        Assert.IsTrue(contexts.IsCurrent(contexts.LastMeasurement!.Stamp)); f.Rules.InvalidateCandidates();
        Assert.IsFalse(contexts.IsCurrent(contexts.LastMeasurement.Stamp));
    }

    [TestMethod]
    public async Task RULEAC027_LastReservedFactSurvivesOwnRulesCreationAndRevocation()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/a", "A");
        using var session = WorkspaceFixtures.Session(root); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
        await execution.Rules!.TryReloadAsync(default); plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(PlanFixture.Submission()).Success); Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!));
        Assert.IsTrue(Update(plans, "in_progress").Success);
        for (var i = 0; i < 255; i++) Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a", default)).Status);
        plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(PlanFixture.Submission(expected: 1, kind: "write", tool: "create_file", path: "AGENTS.md")).Success);
        Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); Assert.IsTrue(Update(plans, "in_progress").Success);
        var result = await files.CreateFileAsync("AGENTS.md", "Use tests.", default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual(256, plans.Facts.Count);
        Assert.AreEqual(FileWriteState.Verified, plans.Facts.Last().WriteState); Assert.AreEqual(1L, plans.Facts.Last().Context.RulesRevision);
        Assert.AreEqual(PlanApprovalState.Draft, plans.Snapshot!.ApprovalState); Assert.AreEqual(PlanTaskState.Blocked, plans.Snapshot.Tasks.Single().State);
        Assert.IsTrue(PlanProjection.State(plans.Mode, plans.Snapshot, true).Length <= PlanLimits.MaxProjectionBytes);
        Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(plans.Display()!) <= PlanLimits.MaxDisplayBytes);
    }

    [TestMethod]
    public async Task RULEAC024_CancelAfterObservingChangedSourceDoesNotRestoreValid()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]);
        using var cancellation = new CancellationTokenSource(); var reader = new CancelAfterRead(f.Files, cancellation);
        f.Execution.Rules = new(reader, f.Session.Policy, f.Execution); await f.Rules.TryReloadAsync(default);
        f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66]; reader.Cancel = true;
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await f.Rules.TryReloadAsync(cancellation.Token));
        Assert.AreEqual(ProjectRulesStatus.Stale, f.Rules.Capture().Status);
        Assert.AreEqual(1L, f.Rules.Capture().RulesRevision);
    }

    [TestMethod]
    public async Task RULEAC023_FilesystemResolvedRulesNameAlsoInvalidatesNewBoundary()
    {
        var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(root + "/sub");
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
        var result = await files.CreateFileAsync("sub/agents.md", "new rule", default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState);
        Assert.AreEqual(File.Exists(root + "/sub/AGENTS.md") ? ProjectRulesStatus.Stale : ProjectRulesStatus.Valid, execution.Rules!.Capture().Status);
    }

    [TestMethod]
    public async Task RULEAC021_PostPublicationRulesIoFailureKeepsVerifiedResultAndFailsClosed()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var execution = new AgentExecutionPolicy(new(session.Policy)); var fs = new EditFileSystem(); var readback = false;
        fs.OnOpen = path => { if (path == root + "/a" && fs.PublishCalls > 0) readback = true; };
        fs.OnInspect = path => { if (readback && path == root + "/AGENTS.md") throw new IOException("SYNTHETIC_RULES_IO"); };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval(), execution);
        var client = new ScriptClient
        {
            Session = session,
            Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            PlanWorkflowTests.Call("create_file", new { path = "a", content = "candidate" })))
        };
        var registry = new AIAgent.Cli.Tools.ToolRegistry([new AIAgent.Cli.Tools.CreateFileTool(files, session.Policy)], session.Policy, execution);
        var result = await new AgentRunner(client, registry, session.Policy, "fixture", new(), _ => { }).RunAsync([LlmMessage.FromUserText("write")], default);
        Assert.AreEqual(AgentStopReason.LocalFailure, result.StopReason); Assert.AreEqual(FileWriteState.Verified, result.Operations.Single().WriteState);
        Assert.AreEqual("candidate", await File.ReadAllTextAsync(root + "/a")); Assert.AreEqual(1, client.Requests.Count);
    }

    [TestMethod]
    public async Task RULEAC021_ConfirmedProcessResultSurvivesPostExecutionRulesIoFailure()
    {
        if (!AIAgent.Cli.Processes.DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native macOS fixture required.");
        var root = await NativeBuildFixture.CreateAsync(false); using var session = WorkspaceFixtures.Session(root);
        var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans) { ProcessCapabilityAvailable = true };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        using var cancellation = new CancellationTokenSource(); var reader = new CancelAfterRead(files, cancellation) { Fail = () => plans.Facts.Count > 0 };
        execution.Rules = new(reader, session.Policy, execution); await execution.Rules.TryReloadAsync(default); plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(GeminiFixtures.Element("{\"expected_revision\":0,\"objective\":\"verify\",\"tasks\":[{\"kind\":\"validation\",\"title\":\"build\",\"acceptance\":\"passed\",\"requirements_version\":2,\"requirements\":[{\"tool\":\"run_dotnet_task\",\"path\":\"Fixture.csproj\",\"task\":\"build\"}]}]}")).Success);
        Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); Assert.IsTrue(Update(plans, "in_progress").Success);
        await using var tasks = new AIAgent.Cli.Processes.ControlledDotnetTasks(session.Workspace, session.Policy, execution, files,
            (_, _) => ValueTask.FromResult(AIAgent.Cli.Processes.ProcessApprovalDecision.Approved), _ => { });
        var result = await tasks.RunAsync(AIAgent.Cli.Processes.DotnetTaskKind.Build, "Fixture.csproj", default);
        Assert.AreEqual(AIAgent.Cli.Processes.ValidationOutcome.BuildSucceeded, result.Summary?.ValidationOutcome);
        Assert.AreEqual(AIAgent.Cli.Processes.ValidationOutcome.BuildSucceeded, plans.Facts.Single().ProcessSummary?.ValidationOutcome);
        Assert.AreEqual(ProjectRulesStatus.Stale, execution.Rules.Capture().Status);
    }

    [TestMethod]
    public async Task RULEAC022_RulesGenerationChangeInvalidatesWaitingFileApproval()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var execution = new AgentExecutionPolicy(new(session.Policy)); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval, execution);
        approval.OnConfirm = _ => execution.Rules!.InvalidateCandidates();
        var result = await files.CreateFileAsync("a", "candidate", default);
        Assert.AreEqual(WorkspaceErrorCode.RulesChanged, result.ErrorCode); Assert.IsFalse(File.Exists(root + "/a"));
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task RULEAC015_RetainedCompletedTaskKeepsRulesVersionAcrossPlanRevision(bool reload)
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); f.Fs.File("a", [65]);
        await f.Rules.TryReloadAsync(default); f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission()).Success); Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!));
        Assert.IsTrue(Update(f.Plans, "in_progress").Success); await f.Files.ReadFileAsync("a", default);
        Assert.IsTrue(Update(f.Plans, "completed").Success); var completed = f.Plans.Snapshot!.Tasks.Single();
        Assert.AreEqual(1L, completed.CompletionRulesRevision);
        if (reload) { f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66]; Assert.AreEqual("none", await f.Rules.TryReloadAsync(default)); }
        f.Plans.SetMode(AgentMode.Plan);
        var retained = PlanFixture.Submission(expected: 1, id: completed.TaskId).GetProperty("tasks")[0];
        var added = JsonSerializer.SerializeToElement(new { kind = "analysis", title = "next", acceptance = "checked", requirements = Array.Empty<object>() });
        Assert.IsTrue(f.Plans.Submit(JsonSerializer.SerializeToElement(new { expected_revision = 1, objective = "objective", tasks = new[] { retained, added } })).Success);
        var kept = f.Plans.Snapshot!.Tasks.Single(t => t.TaskId == completed.TaskId);
        Assert.AreEqual(2L, f.Plans.Snapshot.Revision); Assert.AreEqual(PlanTaskState.Completed, kept.State);
        Assert.AreEqual(1L, kept.CompletionRulesRevision);
        CollectionAssert.AreEqual(completed.CompletionFactIds.ToArray(), kept.CompletionFactIds.ToArray());
    }

    [TestMethod]
    public async Task RULEAC024_CancelAfterSecondReadPreservesKnownChangeAndRevokesApproval()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); f.Fs.File("a", [65]);
        using var cancellation = new CancellationTokenSource(); var reader = new CancelAfterRead(f.Files, cancellation);
        f.Execution.Rules = new(reader, f.Session.Policy, f.Execution); await f.Rules.TryReloadAsync(default);
        f.Plans.SetMode(AgentMode.Plan); Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission()).Success);
        Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!)); Assert.IsTrue(Update(f.Plans, "in_progress").Success);
        await f.Files.ReadFileAsync("a", default); Assert.AreEqual(1, f.Files.Snapshots.Count);
        var reads = 0;
        reader.AfterRead = () => { if (++reads == 1) f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66]; else if (reads == 2) cancellation.Cancel(); };
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await f.Rules.TryReloadAsync(cancellation.Token));
        Assert.AreEqual(ProjectRulesStatus.Stale, f.Rules.Capture().Status); Assert.AreEqual(1L, f.Rules.Capture().RulesRevision);
        Assert.AreEqual(PlanApprovalState.Draft, f.Plans.Snapshot!.ApprovalState);
        Assert.AreEqual(PlanTaskState.Blocked, f.Plans.Snapshot.Tasks.Single().State);
        Assert.AreEqual(0, f.Files.Snapshots.Count); Assert.AreEqual(1, f.Plans.Facts.Count);
    }

    private sealed class CancelAfterRead(IProjectRulesWorkspace inner, CancellationTokenSource cancellation) : IProjectRulesWorkspace
    {
        internal bool Cancel { get; set; }
        internal Func<bool>? Fail { get; set; }
        internal Action? AfterRead { get; set; }
        public AgentExecutionPolicy ExecutionPolicy => inner.ExecutionPolicy;
        public async Task<ProjectRulesReadResult> ReadProjectRulesAsync(string scope, WorkspaceOperationLease lease, CancellationToken token)
        {
            if (Fail?.Invoke() == true) return new(WorkspaceErrorCode.IoFailure);
            var result = await inner.ReadProjectRulesAsync(scope, lease, token);
            AfterRead?.Invoke();
            if (Cancel) { Cancel = false; cancellation.Cancel(); }
            return result;
        }
        public Task<WorkspaceErrorCode> CheckProjectRulesPathAsync(string scope, string path, bool directory,
            ICollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease, CancellationToken token) =>
            inner.CheckProjectRulesPathAsync(scope, path, directory, observations, lease, token);
        public WorkspaceErrorCode RevalidateProjectRulesPaths(IReadOnlyCollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease) => inner.RevalidateProjectRulesPaths(observations, lease);
        public void ClearProjectRuleSnapshots() => inner.ClearProjectRuleSnapshots();
    }

    internal static PlanMutationResult Update(PlanStore plans, string state) => plans.Update(JsonSerializer.SerializeToElement(new
    {
        plan_id = plans.Snapshot!.PlanId,
        expected_revision = plans.Snapshot.Revision,
        task_id = plans.Snapshot.Tasks.Single().TaskId,
        status = state,
        summary = "checked"
    }));
}