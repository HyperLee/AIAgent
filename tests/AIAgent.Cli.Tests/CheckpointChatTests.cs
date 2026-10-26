using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class CheckpointChatTests
{
    [TestMethod]
    public void CKPTAC002_ExactCanonicalCommandsAndPositiveLongIds()
    {
        Assert.AreEqual(CheckpointCommandKind.List, CheckpointCommands.Parse("  /checkpoint list  ").Kind);
        foreach (var (verb, kind) in new[] { ("show", CheckpointCommandKind.Show), ("restore", CheckpointCommandKind.Restore), ("forget", CheckpointCommandKind.Forget) })
            foreach (var id in new[] { 1L, long.MaxValue })
            {
                var parsed = CheckpointCommands.Parse("/checkpoint " + verb + " c" + id);
                Assert.AreEqual(kind, parsed.Kind); Assert.AreEqual(id, parsed.CheckpointId);
            }
        foreach (var raw in new[] { "/Checkpoint list", "/checkpoint  list", "/checkpoint list c1", "/checkpoint show c0", "/checkpoint show c01",
            "/checkpoint show c+1", "/checkpoint show c-1", "/checkpoint show c9223372036854775808", "/checkpoint show C1", "/checkpoint show c١",
            "/checkpoint show latest", "/checkpoint show a.cs", "/checkpoint show c1 c2", "/checkpoint\tlist", "/checkpoint show c1\n", "/checkpoint show c1\r" })
            Assert.AreEqual(CheckpointCommandKind.Invalid, CheckpointCommands.Parse(raw).Kind, raw);
    }

    [TestMethod]
    public void CKPTAC002_InvalidSyntaxIdsAndOverflowHaveDistinctFixedCodes()
    {
        foreach (var raw in new[] { "/Checkpoint list", "/checkpoint  list", "/checkpoint list c1", "/checkpoint show", "/checkpoint show c1 c2", "/checkpoint show  c1", "/checkpoint show\tc1" })
        {
            var parsed = CheckpointCommands.Parse(raw);
            Assert.AreEqual(CheckpointCommandKind.Invalid, parsed.Kind);
            Assert.AreEqual("invalid_checkpoint_command", parsed.ErrorCode, raw);
        }
        foreach (var verb in new[] { "show", "restore", "forget" })
        {
            foreach (var id in new[] { "c0", "c01", "c+1", "c-1", "C1", "c١", "latest", "a.cs", "c", "c9223372036854775808x" })
            {
                var parsed = CheckpointCommands.Parse("/checkpoint " + verb + " " + id);
                Assert.AreEqual(CheckpointCommandKind.Invalid, parsed.Kind);
                Assert.AreEqual("invalid_checkpoint_id", parsed.ErrorCode, id);
            }
            foreach (var id in new[] { "c9223372036854775808", "c" + new string('9', 100) })
            {
                var parsed = CheckpointCommands.Parse("/checkpoint " + verb + " " + id);
                Assert.AreEqual(CheckpointCommandKind.Invalid, parsed.Kind);
                Assert.AreEqual("checkpoint_unavailable", parsed.ErrorCode, id);
            }
        }
        Assert.AreEqual("none", CheckpointCommands.Parse("/checkpoint show c1").ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC002_LocalParserErrorsKeepClassificationWithoutWorkspaceOrModelCalls()
    {
        var fixture = new ChatFixture("/checkpoint show c1 c2", "/checkpoint show c01", "/checkpoint restore c9223372036854775808", "/exit");
        var checkpoints = new CheckpointChatWorkspace();
        Assert.AreEqual(0, await Run(fixture, checkpoints));
        foreach (var code in new[] { "invalid_checkpoint_command", "invalid_checkpoint_id", "checkpoint_unavailable" })
            Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text == "檢查點指令未完成：" + code), code);
        Assert.AreEqual(0, checkpoints.Calls.Count); Assert.AreEqual(0, fixture.Client.Requests.Count);
    }

    [TestMethod]
    public async Task CKPTAC002_LocalQueriesForgetAndPlanGateUseNoModelOrHistory()
    {
        var fixture = new ChatFixture("/checkpoint list", "/checkpoint show c1", "/checkpoint forget c1", "/plan", "/checkpoint restore c1", "/checkpoint list", "/exit");
        var checkpoints = new CheckpointChatWorkspace(); var interaction = new CheckpointInteraction(fixture.Interaction);
        Assert.AreEqual(0, await Run(fixture, checkpoints, interaction));
        CollectionAssert.AreEqual(new[] { "list", "show:1", "forget:1", "list" }, checkpoints.Calls.ToArray());
        Assert.AreEqual(1, interaction.Approvals); Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.AreEqual(0, fixture.Interaction.Remembered.Count); Assert.AreEqual(0L, fixture.Usage.Snapshot().DispatchedRequests);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text == "mode_restricted"));
    }

    [TestMethod]
    public async Task CKPTAC002_MultilineSensitiveAndInvalidCommandsNeverReachWorkspace()
    {
        var fixture = new ChatFixture("/checkpoint list\n/checkpoint restore c1", "/checkpoint show c01", "/checkpoint " + GeminiFixtures.Key, "/Checkpoint list", "/exit");
        var checkpoints = new CheckpointChatWorkspace();
        Assert.AreEqual(0, await Run(fixture, checkpoints));
        Assert.AreEqual(0, checkpoints.Calls.Count); Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task CKPTAC003_OnlyOperationBoundApprovalUsesStoppedBusyReaderAndInvalidEnumFails()
    {
        foreach (var decision in new[] { FileApprovalDecision.Approved, (FileApprovalDecision)123 })
        {
            var fixture = new ChatFixture("/checkpoint restore c1", "/exit"); var checkpoints = new CheckpointChatWorkspace();
            var interaction = new CheckpointInteraction(fixture.Interaction);
            interaction.Confirm = (_, _) =>
            {
                Assert.IsNull(fixture.Interaction.Signal, "busy reader must finish before approval");
                Assert.AreEqual(ChatState.AwaitingCheckpointApproval, interaction.Application!.State);
                return new(decision);
            };
            Assert.AreEqual(decision == FileApprovalDecision.Approved ? 0 : 1, await Run(fixture, checkpoints, interaction));
            Assert.AreEqual(decision == FileApprovalDecision.Approved ? 1 : 0, checkpoints.Publications);
            Assert.AreEqual(0, fixture.Client.Requests.Count);
        }
    }

    [TestMethod]
    public async Task CKPTAC028_PreviewCannotChangeBoundCheckpointOrCommand()
    {
        foreach (var changedId in new[] { true, false })
        {
            var fixture = new ChatFixture("/checkpoint restore c1", "/exit");
            var checkpoints = new CheckpointChatWorkspace { PreviewId = changedId ? 2 : 1, PreviewForget = !changedId };
            var interaction = new CheckpointInteraction(fixture.Interaction);
            Assert.AreEqual(1, await Run(fixture, checkpoints, interaction));
            Assert.AreEqual(0, interaction.Approvals); Assert.AreEqual(0, checkpoints.Publications);
        }
    }

    [TestMethod]
    public async Task CKPTAC016_PublishedFactsSurviveCancellationAndCleanupFailure()
    {
        foreach (var cleanup in new[] { false, true })
        {
            var fixture = new ChatFixture("/checkpoint restore c1", "/exit"); var checkpoints = new CheckpointChatWorkspace { CleanupFailure = cleanup };
            checkpoints.AfterPublication = () => fixture.Interaction.Signal!(ChatControlSignal.CancelTurn);
            Assert.AreEqual(cleanup ? 1 : 0, await Run(fixture, checkpoints));
            Assert.AreEqual(1, checkpoints.Publications);
            Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("[單檔復原結果] Verified", StringComparison.Ordinal) == true));
        }
    }

    [TestMethod]
    public async Task CKPTAC003_CancelEofDeclineAndUnavailableRespectExitSemantics()
    {
        foreach (var decision in new[] { FileApprovalDecision.Cancelled, FileApprovalDecision.EndOfInput, FileApprovalDecision.Declined, FileApprovalDecision.Unavailable })
        {
            var fixture = new ChatFixture("/checkpoint restore c1", "/exit"); var checkpoints = new CheckpointChatWorkspace();
            var interaction = new CheckpointInteraction(fixture.Interaction) { Confirm = (_, _) => new(decision) };
            Assert.AreEqual(decision == FileApprovalDecision.Unavailable ? 1 : 0, await Run(fixture, checkpoints, interaction));
            Assert.AreEqual(0, checkpoints.Publications); Assert.IsNull(fixture.Interaction.Signal);
        }
    }

    [TestMethod]
    public async Task CKPTAC028_LateApprovalCannotReuseOperationAndClearDiscardsCheckpoints()
    {
        var fixture = new ChatFixture("/checkpoint forget c1", "/clear"); var checkpoints = new CheckpointChatWorkspace();
        fixture.Interaction.Inputs.Enqueue(() =>
        {
            Assert.AreEqual(FileApprovalDecision.Unavailable, checkpoints.Approval!.ConfirmAsync(new(1, true, "preview\n" + CheckpointLimits.ForgetPrompt), default).GetAwaiter().GetResult());
            Assert.IsTrue(checkpoints.Clears > 0); return ChatInputResult.Submitted("/exit");
        });
        Assert.AreEqual(0, await Run(fixture, checkpoints));
        Assert.IsTrue(checkpoints.Clears >= 2);
    }

    [TestMethod]
    public async Task CKPTAC021_RecoveryWithoutPriorWorkCommandStillRequiresFreshExecuteConfirmation()
    {
        var fixture = new ChatFixture("/checkpoint restore c1", "/execute", "/exit"); var checkpoints = new CheckpointChatWorkspace();
        var interaction = new CheckpointInteraction(fixture.Interaction);
        Assert.AreEqual(0, await Run(fixture, checkpoints, interaction));
        Assert.AreEqual(1, interaction.WorkApprovals); Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.StartsWith("已切換 Execute", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task CKPTAC025_SensitiveResultUsesCommonResetWithoutDisclosure()
    {
        var fixture = new ChatFixture("/checkpoint list", "/exit"); var checkpoints = new CheckpointChatWorkspace { ListDisplay = GeminiFixtures.Key };
        Assert.AreEqual(0, await Run(fixture, checkpoints));
        Assert.IsTrue(checkpoints.Clears >= 2);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task CKPTAC003_FreshHiddenConfirmationDiscardsPreinputAndHandlesControl()
    {
        foreach (var forget in new[] { false, true })
            foreach (var (answer, expected) in new[] { ("y\r", FileApprovalDecision.Approved), (" YeS \r", FileApprovalDecision.Approved),
                ("n\r", FileApprovalDecision.Declined), (" \r", FileApprovalDecision.Declined), ("\u0003", FileApprovalDecision.Cancelled), ("\u0004", FileApprovalDecision.EndOfInput) })
            {
                var terminal = new ApprovalTerminal(); terminal.Buffered("yes\r\n"); terminal.Bursts.Enqueue(answer);
                using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
                var preview = new CheckpointApprovalPreview(1, forget, "完整新差異\n" + (forget ? CheckpointLimits.ForgetPrompt : CheckpointLimits.RestorePrompt));
                Assert.AreEqual(expected, await interaction.ConfirmCheckpointAsync(preview, default));
                Assert.AreEqual(1, terminal.MaximumReaders);
                Assert.IsFalse(terminal.Output.Contains("yes\r", StringComparison.Ordinal) || terminal.Output.Contains("YeS", StringComparison.Ordinal));
            }
    }

    [TestMethod]
    public async Task CKPTAC003_ConfirmationWholePreviewLimitAndSensitiveAnswerFailClosed()
    {
        foreach (var size in new[] { 65536, 65537 })
        {
            var terminal = new ApprovalTerminal(); terminal.Bursts.Enqueue("y\r");
            using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var prompt = CheckpointLimits.RestorePrompt;
            var block = new string('x', size - System.Text.Encoding.UTF8.GetByteCount(prompt)) + prompt;
            Assert.AreEqual(size == 65536 ? FileApprovalDecision.Approved : FileApprovalDecision.Unavailable,
                await interaction.ConfirmCheckpointAsync(new(1, false, block), default));
            Assert.AreEqual(size == 65536 ? 1 : 0, terminal.MaximumReaders);
        }
        var input = new ApprovalTerminal(); input.Bursts.Enqueue("/clear\r"); input.Bursts.Enqueue(GeminiFixtures.Key + "\r"); input.Bursts.Enqueue("n\r");
        using var shortSession = GeminiFixtures.Session(); using var console = new ConsoleChatInteraction(input); console.SetPolicy(shortSession.Policy);
        Assert.AreEqual(FileApprovalDecision.Declined, await console.ConfirmCheckpointAsync(new(1, true, "preview\n" + CheckpointLimits.ForgetPrompt), default));
        Assert.IsFalse(input.Output.Contains(GeminiFixtures.Key, StringComparison.Ordinal));
    }

    private static Task<int> Run(ChatFixture fixture, CheckpointChatWorkspace checkpoints, CheckpointInteraction? interaction = null)
    {
        var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        var terminal = interaction ?? new CheckpointInteraction(fixture.Interaction);
        var app = new ChatApplication(terminal, fixture.Factory, fixture.Context, fixture.Usage, "test instruction", new CheckpointChatFactory(checkpoints));
        terminal.Application = app;
        return app.RunAsync(session, default);
    }
}

internal sealed class CheckpointInteraction(ScriptInteraction inner) : IChatInteraction
{
    internal ChatApplication? Application { get; set; }
    internal int Approvals { get; private set; }
    internal int WorkApprovals { get; private set; }
    public ValueTask<WorkApprovalDecision> ConfirmWorkAsync(WorkApprovalPreview preview, CancellationToken token) { WorkApprovals++; return new(WorkApprovalDecision.Approved); }
    internal Func<CheckpointApprovalPreview, CancellationToken, ValueTask<FileApprovalDecision>> Confirm { get; set; } = (_, _) => new(FileApprovalDecision.Approved);
    public ValueTask<FileApprovalDecision> ConfirmCheckpointAsync(CheckpointApprovalPreview preview, CancellationToken token) { Approvals++; return Confirm(preview, token); }
    public ValueTask<ChatInputResult> ReadInputAsync(CancellationToken token) => inner.ReadInputAsync(token);
    public ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken token) => inner.ChooseKeyReplacementAsync(token);
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken token) => inner.ReadKeyAsync(token);
    public IDisposable ObserveBusyInput(Action<ChatControlSignal> signal) => inner.ObserveBusyInput(signal);
    public void SetPolicy(Security.ISensitiveDataPolicy policy) => inner.SetPolicy(policy);
    public void RememberAcceptedInput(string text) => inner.RememberAcceptedInput(text);
    public void ClearInputState() => inner.ClearInputState();
    public void Show(ChatEvent message) => inner.Show(message);
}

internal sealed class CheckpointChatWorkspace : ICheckpointWorkspace
{
    internal CheckpointRecoveryState? Recovery { get; set; }
    internal List<string> Calls { get; } = [];
    internal int Publications { get; private set; }
    internal int Clears { get; private set; }
    internal ICheckpointApproval? Approval { get; private set; }
    internal string ListDisplay { get; set; } = "程序內歷史：c1";
    internal Action? AfterPublication { get; set; }
    internal bool CleanupFailure { get; set; }
    internal long? PreviewId { get; set; }
    internal bool? PreviewForget { get; set; }
    public Task<CheckpointOperationResult> ListAsync(CancellationToken token) { Calls.Add("list"); return Task.FromResult(new CheckpointOperationResult("none", ListDisplay)); }
    public Task<CheckpointOperationResult> ShowAsync(long id, CancellationToken token) { Calls.Add("show:" + id); return Task.FromResult(new CheckpointOperationResult("none", "原始差異")); }
    public Task<CheckpointOperationResult> RestoreAsync(long id, ICheckpointApproval approval, CancellationToken token) => Apply(id, approval, false, token);
    public Task<CheckpointOperationResult> ForgetAsync(long id, ICheckpointApproval approval, CancellationToken token) => Apply(id, approval, true, token);
    private async Task<CheckpointOperationResult> Apply(long id, ICheckpointApproval approval, bool forget, CancellationToken token)
    {
        Calls.Add((forget ? "forget:" : "restore:") + id); Approval = approval;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        var previewForget = PreviewForget ?? forget;
        var decision = await approval.ConfirmAsync(new(PreviewId ?? id, previewForget, "完整新差異\n" + (previewForget ? CheckpointLimits.ForgetPrompt : CheckpointLimits.RestorePrompt)), linked.Token);
        if (decision != FileApprovalDecision.Approved) return new("approval_declined", decision: decision);
        if (forget) return new("none", "已忘記單筆記憶體。", decision: decision);
        Publications++;
        Recovery?.Record(id, "a.cs", WorkspaceWriteResult.Success(new("a.cs", 1, false)), true);
        AfterPublication?.Invoke();
        return new(CleanupFailure ? "cleanup_failure" : "none", checkpointId: id, relativePath: "a.cs", writeState: FileWriteState.Verified,
            verifiedByteCount: 1, verifiedHasUtf8Bom: false, cleanupFailed: CleanupFailure, fatal: CleanupFailure, decision: decision);
    }
    public void Clear() => Clears++;
}

internal sealed class CheckpointChatFactory(CheckpointChatWorkspace checkpoints) : IWorkspaceFilesFactory
{
    private readonly TestWorkspaceFilesFactory _inner = new();
    public FileChangeApprovalBridge ApprovalBridge => _inner.ApprovalBridge;
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null)
    { checkpoints.Recovery = executionPolicy?.Recovery; return new Files(_inner.Create(session, executionPolicy), checkpoints); }
    private sealed class Files(IWorkspaceFiles inner, ICheckpointWorkspace checkpoints) : IWorkspaceFiles
    {
        public ICheckpointWorkspace Checkpoints => checkpoints;
        public IReadSnapshotStore Snapshots => inner.Snapshots;
        public IProjectRulesWorkspace? ProjectRulesWorkspace => inner.ProjectRulesWorkspace;
        public Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken token) => inner.ListFilesAsync(path, token);
        public Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken token) => inner.ReadFileAsync(path, token);
        public Task<WorkspaceFileSearchResult> SearchFilesAsync(string path, string pattern, CancellationToken token) => inner.SearchFilesAsync(path, pattern, token);
        public Task<WorkspaceTextSearchResult> SearchTextAsync(string path, string query, string pattern, bool sensitive, int lines, CancellationToken token) => inner.SearchTextAsync(path, query, pattern, sensitive, lines, token);
        public Task<WorkspaceRangeReadResult> ReadFileRangeAsync(string path, int start, int end, CancellationToken token) => inner.ReadFileRangeAsync(path, start, end, token);
        public Task<WorkspaceWriteResult> CreateFileAsync(string path, string text, CancellationToken token) => inner.CreateFileAsync(path, text, token);
        public Task<WorkspaceWriteResult> ReplaceTextAsync(string path, string before, string after, CancellationToken token) => inner.ReplaceTextAsync(path, before, after, token);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}