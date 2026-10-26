using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkFlowTests
{
    [TestMethod]
    public async Task SAVEAC015_UnavailableFileReplacementStillInvalidatesConfirmedCandidate()
    {
        await using var f = new WorkFlowFixture(); f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(JsonSerializer.SerializeToElement(new
        {
            expected_revision = 0,
            objective = "讀檔",
            tasks = new[] {
            new { kind = "read", title = "讀", acceptance = "取得內容", requirements = new[] { new { tool = "read_file", path = "a" } } } }
        })).Success);
        f.FileSystem.File("a", [255]);
        var result = await f.Work.SaveAsync("工作", (_, _) => { f.FileSystem.File("a", [254]); return ValueTask.FromResult(WorkApprovalDecision.Approved); }, default);
        Assert.AreEqual("work_revision_conflict", result.ErrorCode); Assert.AreEqual(0, f.Store.Publishes);
    }

    [TestMethod]
    public async Task SAVEAC014_MissingApiUsageIsUnavailableInCompleteSavePreview()
    {
        await using var f = new WorkFlowFixture();
        f.Context.CommitCompletedTurn([LlmMessage.FromUserText("工作"), LlmMessage.FromModel(new object())]);
        f.Client.Respond = (_, _, _) => Task.FromResult(new LlmResult(LlmStatus.Completed, LlmErrorCode.None, ContextTestData.Summary,
            [], LlmMessage.FromModel(new object()), null, true, false));
        string? preview = null;
        var result = await f.Work.SaveAsync("工作", (shown, _) => { preview = shown.DisplayBlock; return ValueTask.FromResult(WorkApprovalDecision.Declined); }, default);
        Assert.AreEqual("approval_declined", result.ErrorCode); Assert.IsNotNull(preview);
        StringAssert.Contains(preview, "Total=不可得"); Assert.IsFalse(result.Usage!.Total.HasKnownValue);
    }

    [TestMethod]
    public async Task SAVEAC022_SensitiveCurrentPinBeforeGenerationRequiresSharedReset()
    {
        await using var f = new WorkFlowFixture();
        Assert.AreEqual("none", f.Contexts.AddPin("Api" + "Key = synthetic-sensitive-value"));
        var result = await f.Work.SaveAsync("工作", f.Approve, default);
        Assert.AreEqual("work_sensitive_content", result.ErrorCode); Assert.IsTrue(result.RequiresContextReset);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.AreEqual(0, f.Store.Publishes);
    }

    [TestMethod]
    public async Task SAVEAC023_StartupBusyEofKeepsNormalExitCode()
    {
        var interaction = new ScriptInteraction(); var client = new ScriptClient(); var factory = new ScriptFactory(client);
        var session = new BootstrapSession(new("/ws"), new("gemini-3.5-flash-lite"), new SessionSecrets(GeminiFixtures.Key.ToCharArray()), WorkContextTests.Id);
        client.Session = session; factory.Sessions.Add(session);
        interaction.OnBusy = signal => signal(ChatControlSignal.EndOfInput);
        var app = new ChatApplication(interaction, factory, new(), new(), "test", new TestWorkspaceFilesFactory(), workSaveStoreFactory: () => new MemoryWorkStore());
        Assert.AreEqual(0, await app.RunAsync(session, default)); Assert.AreEqual(0, client.Requests.Count);
    }

    [TestMethod]
    public async Task SAVEAC022_SensitiveCompletedSourceResetsButFixedMetadataCollisionOnlyRefuses()
    {
        await using (var f = new WorkFlowFixture())
        {
            f.Context.CommitCompletedTurn([LlmMessage.FromUserText("AI" + "za" + new string('A', 35)), LlmMessage.FromModel(new object())]);
            var result = await f.Work.SaveAsync("工作", f.Approve, default);
            Assert.AreEqual("work_sensitive_content", result.ErrorCode); Assert.IsTrue(result.RequiresContextReset);
            Assert.AreEqual(0, f.Client.Requests.Count);
        }
        await using (var f = new WorkFlowFixture("work_id"))
        {
            f.Contexts.AddPin("安全備註");
            var result = await f.Work.SaveAsync("工作", f.Approve, default);
            Assert.AreEqual("work_sensitive_content", result.ErrorCode); Assert.IsFalse(result.RequiresContextReset);
        }
    }

    [TestMethod]
    public async Task SAVEAC012_EmptyAndPinsOnlyNeverGenerateOrChangeCurrentWork()
    {
        await using var f = new WorkFlowFixture();
        Assert.AreEqual("nothing_to_save", (await f.Work.SaveAsync("工作", f.Approve, default)).ErrorCode);
        Assert.AreEqual(0, f.Store.Preflights);
        f.Contexts.AddPin("  保留原文  "); var revision = f.Context.Revision;
        var saved = await f.Work.SaveAsync(" 工作 ", f.Approve, default);
        Assert.AreEqual("none", saved.ErrorCode); Assert.AreEqual(WorkPublicationState.PublishedVerified, saved.PublicationState);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.AreEqual(revision, f.Context.Revision); Assert.AreEqual(1, f.Store.Publishes);
        var document = f.Document(); Assert.AreEqual(" 工作 ", document.Title); Assert.AreEqual("  保留原文  ", document.Pins[0]);
        Assert.IsNull(document.Summary); Assert.AreEqual(0, document.FileRefs.Count);
    }

    [TestMethod]
    public async Task SAVEAC011_SaveCoversLatestTwoTurnsWithoutPublishingCompaction()
    {
        await using var f = new WorkFlowFixture();
        for (var i = 0; i < 2; i++) f.Context.CommitCompletedTurn([LlmMessage.FromUserText("最新" + i), LlmMessage.FromModel(new object())]);
        var result = await f.Work.SaveAsync("工作", f.Approve, default);
        Assert.AreEqual("none", result.ErrorCode); Assert.AreEqual(1, result.GenerationsStarted); Assert.AreEqual(1, f.Client.Requests.Count);
        using var source = JsonDocument.Parse(f.Client.Requests[0].Messages[0].UserText!);
        Assert.AreEqual(2, source.RootElement.GetProperty("turns").GetArrayLength()); Assert.AreEqual(0, f.Client.Requests[0].Tools.Count);
        Assert.IsNull(f.Context.Summary); Assert.AreEqual(2, f.Context.CompletedTurnCount); Assert.AreEqual(0, f.Plans.Facts.Count);
    }

    [TestMethod]
    public async Task SAVEAC014_DeclinedSummaryCountsUsageAndStorePreflightFailureIsZeroGeneration()
    {
        await using var f = new WorkFlowFixture(); f.Context.CommitCompletedTurn([LlmMessage.FromUserText("工作"), LlmMessage.FromModel(new object())]);
        f.Store.PreflightError = "store_limit";
        Assert.AreEqual("store_limit", (await f.Work.SaveAsync("工作", f.Approve, default)).ErrorCode); Assert.AreEqual(0, f.Client.Requests.Count);
        f.Store.PreflightError = "none";
        var result = await f.Work.SaveAsync("工作", (_, _) => ValueTask.FromResult(WorkApprovalDecision.Declined), default);
        Assert.AreEqual("approval_declined", result.ErrorCode); Assert.AreEqual(1, f.Budget.Snapshot().ProgramGenerationsStarted);
        Assert.AreEqual(1L, result.Usage!.DispatchedRequests); Assert.AreEqual(0, f.Store.Publishes);
    }

    [TestMethod]
    public async Task SAVEAC015_ContextAndFileChangesAfterPreviewRefuseOriginalCandidate()
    {
        await using var f = new WorkFlowFixture(); f.Contexts.AddPin("保存");
        var result = await f.Work.SaveAsync("工作", (_, _) => { f.Contexts.AddPin("新備註"); return ValueTask.FromResult(WorkApprovalDecision.Approved); }, default);
        Assert.AreEqual("work_revision_conflict", result.ErrorCode); Assert.AreEqual(0, f.Store.Publishes);
        f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(JsonSerializer.SerializeToElement(new
        {
            expected_revision = 0,
            objective = "讀檔",
            tasks = new[] {
            new { kind = "read", title = "讀", acceptance = "取得內容", requirements = new[] { new { tool = "read_file", path = "a" } } } }
        })).Success);
        f.FileSystem.File("a", Encoding.UTF8.GetBytes("first"));
        result = await f.Work.SaveAsync("工作", (_, _) => { f.FileSystem.File("a", Encoding.UTF8.GetBytes("other")); return ValueTask.FromResult(WorkApprovalDecision.Approved); }, default);
        Assert.AreEqual("work_revision_conflict", result.ErrorCode); Assert.AreEqual(0, f.Store.Publishes); Assert.AreEqual(0, f.Files.Snapshots.Count); Assert.AreEqual(0, f.Plans.Facts.Count);
    }

    [TestMethod]
    public async Task SAVEAC019_ResumeRebuildsOnlyPendingAndImmediatelyResavesWithoutGenerating()
    {
        await using var f = new WorkFlowFixture();
        var history = new WorkSavedPlan("目標", [new(PlanTaskKind.Analysis, "完成", "條件", 1, [], PlanTaskState.Completed, TaskCompletionBasis.ModelAssessment, "歷史", null),
            new(PlanTaskKind.Analysis, "待辦", "新條件", 1, [], PlanTaskState.InProgress, TaskCompletionBasis.None, null, null)]);
        f.Seed(history);
        var result = await f.Work.ResumeAsync(WorkContextTests.Id, f.Approve, default);
        Assert.AreEqual("none", result.ErrorCode); Assert.AreEqual(AgentMode.Plan, f.Plans.Mode); Assert.AreEqual(1, f.Plans.Snapshot!.Tasks.Count);
        Assert.AreEqual(PlanTaskState.Pending, f.Plans.Snapshot.Tasks[0].State); Assert.AreEqual(0, f.Plans.Facts.Count);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.AreEqual(0, f.Context.CompletedTurnCount);
        f.Store.Files.Clear();
        result = await f.Work.SaveAsync("再保存", f.Approve, default);
        Assert.AreEqual("none", result.ErrorCode); Assert.AreEqual("歷史目標", f.Document().Summary!.Goals[0]); Assert.AreEqual(1, f.Document().Plan!.Tasks.Count);
        Assert.AreEqual(0, f.Client.Requests.Count);
    }

    [TestMethod]
    public async Task SAVEAC019_AllCompletedStillRequiresIndependentExecuteConfirmation()
    {
        await using var f = new WorkFlowFixture(); f.Seed(new("完成目標", [new(PlanTaskKind.Analysis, "完成", "條件", 1, [], PlanTaskState.Completed, TaskCompletionBasis.ModelAssessment, null, null)]));
        Assert.AreEqual("none", (await f.Work.ResumeAsync(WorkContextTests.Id, f.Approve, default)).ErrorCode);
        Assert.IsNull(f.Plans.Snapshot); Assert.AreEqual(AgentMode.Plan, f.Plans.Mode); Assert.IsTrue(f.Work.ResumeExecutionConfirmationRequired);
        Assert.AreEqual("approval_declined", (await f.Work.ConfirmExecuteAsync((_, _) => ValueTask.FromResult(WorkApprovalDecision.Declined), default)).ErrorCode);
        Assert.AreEqual(AgentMode.Plan, f.Plans.Mode);
        Assert.AreEqual("none", (await f.Work.ConfirmExecuteAsync(f.Approve, default)).ErrorCode);
        Assert.AreEqual(AgentMode.Execute, f.Plans.Mode); Assert.AreEqual(0, f.Client.Requests.Count);
    }

    [TestMethod]
    public async Task SAVEAC020_ChangedStoreAfterResumePreviewPublishesNoMemory()
    {
        await using var f = new WorkFlowFixture(); f.Seed();
        var result = await f.Work.ResumeAsync(WorkContextTests.Id, (_, _) => { f.Store.Files[WorkContextTests.Id] = f.Store.Entry(WorkContextTests.Id, Encoding.UTF8.GetBytes("{}")); return ValueTask.FromResult(WorkApprovalDecision.Approved); }, default);
        Assert.AreEqual("work_revision_conflict", result.ErrorCode); Assert.IsNull(f.Context.Summary); Assert.AreEqual(0, f.Context.Capture().Pins.Count);
        Assert.IsNull(f.Plans.Snapshot); Assert.AreEqual(AgentMode.Execute, f.Plans.Mode);
    }

    [TestMethod]
    public async Task SAVEAC017_WorkspaceMismatchReadsNoReferencesOrRules()
    {
        await using var f = new WorkFlowFixture(); f.Seed(root: "/different");
        var inspections = f.FileSystem.Inspected.Count;
        Assert.AreEqual("work_workspace_mismatch", (await f.Work.ResumeAsync(WorkContextTests.Id, f.Approve, default)).ErrorCode);
        Assert.AreEqual(1, f.FileSystem.Inspected.Count - inspections); Assert.IsNull(f.Context.Summary);
    }

    [TestMethod]
    public async Task SAVEAC027_QueryAndSingleDeleteDoNotGenerateOrClearMemory()
    {
        await using var f = new WorkFlowFixture(); f.Seed(); f.Contexts.AddPin("目前備註"); var revision = f.Context.Revision;
        Assert.AreEqual("none", (await f.Work.ListAsync(default)).ErrorCode); Assert.AreEqual("none", (await f.Work.ShowAsync(WorkContextTests.Id, default)).ErrorCode);
        Assert.AreEqual("none", (await f.Work.DeleteAsync(WorkContextTests.Id, f.Approve, default)).ErrorCode);
        Assert.AreEqual(1, f.Store.Deletes); Assert.AreEqual(revision, f.Context.Revision); Assert.AreEqual(0, f.Client.Requests.Count);
        f.Seed(); f.Store.Files["11111111111111111111111111111111"] = f.Store.Entry("11111111111111111111111111111111", Encoding.UTF8.GetBytes("{}"));
        var rejected = await f.Work.ListAsync(default); Assert.AreEqual("work_format_invalid", rejected.ErrorCode); Assert.IsNull(rejected.DisplayBlock);
    }

    [TestMethod]
    public async Task SAVEAC022_StoreStopSurvivesClearAndKnownPublishedResultSurvivesCancellation()
    {
        await using var f = new WorkFlowFixture(); f.Contexts.AddPin("保存"); using var c = new CancellationTokenSource();
        f.Store.OnPublish = () => c.Cancel();
        var result = await f.Work.SaveAsync("工作", f.Approve, c.Token);
        Assert.AreEqual(WorkPublicationState.PublishedVerified, result.PublicationState);
        f.Store.Stopped = true; f.Contexts.Clear(); f.Work.Clear();
        Assert.AreEqual("store_unavailable", (await f.Work.ListAsync(default)).ErrorCode);
    }

    [TestMethod]
    public async Task SAVEAC017_ReferencesNeverCreateFactsAndScopeOutsideHasNoTargetIo()
    {
        await using var f = new WorkFlowFixture(); f.FileSystem.Directory("src"); f.FileSystem.File("outside", [65]);
        Assert.AreEqual("none", await f.Rules.TryChangeScopeAsync("src", default)); f.FileSystem.Inspected.Clear();
        var read = await f.Files.ReadWorkReferencesAsync(["outside", "src/missing"], default);
        Assert.AreEqual("outside_scope", read.References[0].Observation); Assert.AreEqual("missing", read.References[1].Observation);
        Assert.IsFalse(f.FileSystem.Inspected.Contains("/ws/outside")); Assert.AreEqual(0, f.Files.Snapshots.Count); Assert.AreEqual(0, f.Plans.Facts.Count);
    }
}

internal sealed class WorkFlowFixture : IAsyncDisposable
{
    internal BootstrapSession Session { get; }
    internal ConversationContext Context { get; } = new();
    internal PlanStore Plans { get; }
    internal ContextCoordinator Contexts { get; }
    internal ControlledWorkspaceFileSystem FileSystem { get; } = new();
    internal WorkspaceFiles Files { get; }
    internal ProjectRulesCoordinator Rules { get; }
    internal ScriptClient Client { get; } = new();
    internal GenerationBudget Budget { get; }
    internal MemoryWorkStore Store { get; } = new();
    internal WorkSessionCoordinator Work { get; }
    internal WorkFlowFixture(string key = GeminiFixtures.Key)
    {
        Session = WorkspaceFixtures.Session(key: key);
        Plans = new(Session.Policy, Context.DecisionGate); var execution = new AgentExecutionPolicy(Plans);
        Files = new(Session.Workspace, Session.Policy, FileSystem, executionPolicy: execution); Rules = execution.Rules!;
        Contexts = new(Context, Plans, Session.Policy); Contexts.BindRules(Rules); Budget = new(Context.DecisionGate);
        Client.Session = Session; Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(ContextTestData.Summary));
        var registry = TestRulesRegistry.Create([new ReadFileTool(Files, Session.Policy)], Session.Policy, execution);
        var runner = new AgentRunner(Client, registry, Session.Policy, "test instruction", new(), _ => { }, Budget, Contexts, Client);
        Work = new(Contexts, Plans, Rules, Files, Store, Session.Policy, runner, Client, Budget, () => new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero), () => WorkContextTests.Id);
    }
    internal ValueTask<WorkApprovalDecision> Approve(WorkApprovalPreview preview, CancellationToken token) => ValueTask.FromResult(WorkApprovalDecision.Approved);
    internal void Seed(WorkSavedPlan? plan = null, string root = "/ws")
    {
        var original = WorkContextTests.Document(plan);
        var document = new WorkSaveDocument(original.WorkId, original.Title, original.CreatedUtc, new(root, 0, 1), ".", [], original.Summary, original.Pins, plan, []);
        Store.Files[original.WorkId] = Store.Entry(original.WorkId, WorkSaveCodec.Write(document, new WorkPersistencePolicy(Session.Policy)));
    }
    internal WorkSaveDocument Document() => WorkSaveCodec.Parse(Store.Files[WorkContextTests.Id].Bytes, WorkContextTests.Id, new(Session.Policy));
    public async ValueTask DisposeAsync() { await Files.DisposeAsync(); await Client.DisposeAsync(); Session.Dispose(); }
}

internal sealed class MemoryWorkStore : IWorkSaveStore
{
    internal Dictionary<string, WorkStoreFile> Files { get; } = new(StringComparer.Ordinal);
    public bool Stopped { get; set; }
    internal int Preflights { get; private set; }
    internal int Publishes { get; private set; }
    internal int Deletes { get; private set; }
    internal string PreflightError { get; set; } = "none";
    internal Action? OnPublish { get; set; }
    private ulong _identity;
    internal WorkStoreFile Entry(string id, byte[] bytes) => new(id, bytes, new(WorkspaceNodeKind.File, bytes.Length, Identity: ++_identity), 1, 2);
    public Task<string> PreflightSaveAsync(CancellationToken token) { Preflights++; return Task.FromResult(PreflightError); }
    public Task<IReadOnlyList<WorkStoreFile>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<WorkStoreFile>>(Files.Values.ToArray());
    public Task<WorkStoreFile> ReadAsync(string workId, CancellationToken token) => Files.TryGetValue(workId, out var file) ? Task.FromResult(file) : throw new WorkStoreException("work_not_found");
    public Task<WorkStoreMutation> PublishAsync(string workId, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        Publishes++; OnPublish?.Invoke();
        if (Files.ContainsKey(workId)) return Task.FromResult(new WorkStoreMutation("work_conflict"));
        Files[workId] = Entry(workId, bytes.ToArray()); return Task.FromResult(new WorkStoreMutation("none", WorkPublicationState.PublishedVerified));
    }
    public Task<WorkStoreMutation> DeleteAsync(WorkStoreFile expected, CancellationToken token)
    {
        if (!Files.TryGetValue(expected.WorkId, out var current) || !expected.Matches(current)) return Task.FromResult(new WorkStoreMutation("work_conflict"));
        Files.Remove(expected.WorkId); Deletes++; return Task.FromResult(new WorkStoreMutation("none", deleteState: WorkDeleteState.DeletedVerified));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}