using System.Text.Json;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

using Microsoft.Win32.SafeHandles;

// Offline PTY assembly only. No product switches, real keys or network clients.
try
{
    var scenario = args[0];
    using var gate = args.Length > 1 ? new FileStream(new SafeFileHandle((IntPtr)int.Parse(args[1]), ownsHandle: true), FileAccess.Read) : null;
    using var cancellation = new CancellationTokenSource();
    using var interrupts = new ConsoleInterruptRouter(cancellation.Cancel);
    ITerminal terminal = new OfflineTerminal(new SystemTerminal(), scenario, gate);
    var bootstrap = new ConsoleInteraction(terminal);
    var startupArgs = scenario.StartsWith("work-resume", StringComparison.Ordinal) ? new[] { "--resume", OfflineWorkStore.ResumeId } : [];
    using var result = await new Bootstrapper(new FixedSettings(), new WorkspaceResolver(new WorkspaceFileSystem()), bootstrap, bootstrap).StartAsync(startupArgs, cancellation.Token);
    if (result.Status != BootstrapStatus.Ready) { bootstrap.ShowStatus(StatusMessages.For(result)); return result.ExitCode; }
    using var interaction = new ConsoleChatInteraction(terminal);
    var session = result.TakeSession();
    var chat = new ChatApplication(interaction, new OfflineRuntime(scenario, gate), new(), new(), "test instruction", new OfflineWorkspaceFactory(scenario, gate), scenario.StartsWith("build", StringComparison.Ordinal) ? new OfflineProcessFactory(scenario, gate) : null,
        scenario.StartsWith("review", StringComparison.Ordinal) ? new OfflineGitFactory(scenario, gate) : null,
        scenario.StartsWith("work", StringComparison.Ordinal) ? () => new OfflineWorkStore(scenario, session) : null,
        scenario.StartsWith("mcp", StringComparison.Ordinal) ? guard => new McpReadClient(guard, new OfflineMcpHandler(scenario)) : null);
    interrupts.RouteTo(chat.Interrupt);
    return await chat.RunAsync(session, cancellation.Token);
}
catch (Exception) { return 1; }

internal sealed class FixedSettings : IPublicSettingsLoader
{
    public PublicSettings Load() => new("gemini-3.5-flash-lite");
}

internal sealed class OfflineRuntime(string scenario, FileStream? gate) : IChatRuntimeFactory
{
    public ILlmContextInspector? CreateContextInspector(ILlmClient client) => client as ILlmContextInspector;
    private int _clients;
    public BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput) => new ChatRuntimeFactory().CreateReplacementSession(workspace, settings, acceptedInput);
    public ILlmClient CreateClient(BootstrapSession session) => new OfflineClient(scenario, gate, ++_clients > 1);
}

internal sealed class OfflineClient(string scenario, FileStream? gate, bool replaced) : ILlmClient, ILlmContextInspector
{
    public ContextRequestMeasure Measure(LlmRequest request) => new(ContextMeasureStatus.Measured, 1024);
    public ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages) =>
        new(turnId, messages[0].UserText!, "offline answer", []);
    private int _calls;
    public async Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _calls++;
            if (scenario.StartsWith("mcp", StringComparison.Ordinal))
            {
                if (request.Messages[^1].Kind != LlmMessageKind.ToolResults)
                {
                    if (scenario.StartsWith("mcp-fetch", StringComparison.Ordinal)) return PlanCalls((McpProfile.FetchName,
                        new { url = "https://learn.microsoft.com/zh-tw/aspnet/core/fundamentals?preserve-view=true&view=aspnetcore-10.0" + (scenario == "mcp-fetch-url-rejected" ? "#blocked" : "") }));
                    return PlanCalls((McpProfile.DocsName, new { query = "公開 C# 文件😀" }));
                }
                return new(LlmStatus.Completed, LlmErrorCode.None, "MCP_OFFLINE_RESULT", [], LlmMessage.FromModel(new object()), null, true, false);
            }
            if (scenario.StartsWith("skills", StringComparison.Ordinal))
            {
                if (scenario == "skills-approval" && _calls == 1) return PlanCalls(("create_file", new { path = "new.md", content = "candidate" }));
                if (scenario == "skills-model-gated")
                {
                    Console.WriteLine("OFFLINE_SKILLS_MODEL_WAITING");
                    await gate!.ReadExactlyAsync(new byte[1], cancellationToken);
                }
                var answer = request.SystemInstruction.Contains("\"active\":null", StringComparison.Ordinal) ? "SKILL_DISABLED_RESULT" : "SKILL_ACTIVE_RESULT";
                return new(LlmStatus.Completed, LlmErrorCode.None, answer, [], LlmMessage.FromModel(new object()), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario.StartsWith("work", StringComparison.Ordinal) && request.Tools.Count == 0)
            {
                if (scenario == "work-model-busy")
                {
                    Console.WriteLine("OFFLINE_WORK_MODEL_PAUSED");
                    using var observed = cancellationToken.Register(() => Console.WriteLine("OFFLINE_WORK_CANCEL_OBSERVED"));
                    await gate!.ReadExactlyAsync(new byte[1], CancellationToken.None); cancellationToken.ThrowIfCancellationRequested();
                }
                return new(LlmStatus.Completed, LlmErrorCode.None,
                    "{\"version\":1,\"goals\":[\"離線保存摘要\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}",
                    [], LlmMessage.FromModel(new object()), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario.StartsWith("checkpoint", StringComparison.Ordinal))
            {
                if (_calls == 1) return PlanCalls(("read_file", new { path = "notes.md" }));
                if (_calls == 2) return PlanCalls(("replace_text", new { path = "notes.md", old_text = "before", new_text = "after" }));
                return new(LlmStatus.Completed, LlmErrorCode.None, "離線修改完成", [], LlmMessage.FromModel(new object()), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario.StartsWith("review", StringComparison.Ordinal))
            {
                if (scenario == "review-model-busy")
                {
                    Console.WriteLine("OFFLINE_REVIEW_MODEL_PAUSED");
                    using var observed = cancellationToken.Register(() => Console.WriteLine("OFFLINE_REVIEW_CANCEL_OBSERVED"));
                    await gate!.ReadExactlyAsync(new byte[1], CancellationToken.None); cancellationToken.ThrowIfCancellationRequested();
                }
                if (_calls == 1) return PlanCalls(("git_diff", new { snapshot_id = "g1", layer = "staged", path = "中文 空白.cs" }));
                return new(LlmStatus.Completed, LlmErrorCode.None, "{\"version\":1,\"snapshot_id\":\"g1\",\"selection\":\"staged\",\"summary\":\"離線審查\",\"findings\":[]}", [], LlmMessage.FromModel(new object()), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario.StartsWith("context", StringComparison.Ordinal) && request.Tools.Count == 0)
            {
                if (scenario.EndsWith("busy", StringComparison.Ordinal))
                {
                    Console.WriteLine("OFFLINE_CONTEXT_COMPACTING");
                    using var observed = cancellationToken.Register(() => Console.WriteLine("OFFLINE_CONTEXT_CANCEL_OBSERVED"));
                    await gate!.ReadExactlyAsync(new byte[1], CancellationToken.None);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return new(LlmStatus.Completed, LlmErrorCode.None,
                    "{\"version\":1,\"goals\":[\"離線摘要\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}",
                    [], LlmMessage.FromModel(new object()), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario.StartsWith("build", StringComparison.Ordinal) && _calls == 1)
                return PlanCalls(("run_dotnet_task", new { task = "build", project_path = "中文專案/Example.csproj" }));
            if (scenario.StartsWith("plan", StringComparison.Ordinal))
            {
                if (_calls == 1)
                    return PlanCalls(("submit_plan", new
                    {
                        expected_revision = 0,
                        objective = "中文計畫😀",
                        tasks = new[] {
                        new { kind = "write", title = "建立檔案", acceptance = "本機寫入核對", requirements = new[] { new { tool = "create_file", path = "new.md" } } } }
                    }));
                if (_calls == 3)
                {
                    if (scenario == "plan-busy")
                    {
                        Console.WriteLine("OFFLINE_PLAN_GENERATING");
                        await gate!.ReadExactlyAsync(new byte[1], cancellationToken);
                    }
                    return PlanCalls(("update_task", new { plan_id = "p1", expected_revision = 1, task_id = "t1", status = "in_progress", summary = "開始建立" }),
                        ("create_file", new { path = "new.md", content = "Hello\n" }),
                        ("update_task", new { plan_id = "p1", expected_revision = 1, task_id = "t1", status = "completed", summary = "本機檔案已核對" }));
                }
            }
            if (scenario.StartsWith("search", StringComparison.Ordinal) && _calls == 1)
            {
                LlmToolCall[] calls = [
                    new(0, null, "search_files", JsonSerializer.SerializeToElement(new { path = ".", pattern = "*.md" })),
                    new(1, null, "search_text", JsonSerializer.SerializeToElement(new { path = ".", query = "二" })),
                    new(2, null, "read_file_range", JsonSerializer.SerializeToElement(new { path = "notes.md", start_line = 2, end_line = 20 }))];
                return new(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario.StartsWith("edit", StringComparison.Ordinal) && _calls == 1)
            {
                if (scenario == "edit-busy-gated")
                {
                    Console.WriteLine("OFFLINE_EDIT_GENERATING");
                    await gate!.ReadExactlyAsync(new byte[1], cancellationToken);
                }
                using var editArguments = scenario == "edit-budget"
                    ? JsonSerializer.SerializeToDocument(new { path = "中文.md", content = "\uFEFF中文\r\n😀\n尾\r終" })
                    : JsonDocument.Parse("{\"path\":\"new.md\",\"content\":\"Hello\\n\"}");
                using var secondArguments = JsonSerializer.SerializeToDocument(new { path = "second.md", content = "second" });
                LlmToolCall[] editCalls = scenario == "edit-budget"
                    ? [new(0, null, "create_file", editArguments.RootElement), new(1, null, "create_file", secondArguments.RootElement)]
                    : [new(0, null, "create_file", editArguments.RootElement)];
                return new(LlmStatus.ToolCalls, LlmErrorCode.None, "", editCalls, LlmMessage.FromModel(new object(), editCalls), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario == "failure") throw new IOException("FAKE_LOCAL_FAILURE_ONLY");
            if (scenario == "busy" && _calls == 1) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if ((scenario == "tools" && request.Messages[^1].Kind == LlmMessageKind.User) ||
                (scenario == "tool-busy" || scenario == "tool-auth" && !replaced) && _calls == 1)
            {
                using var arguments = JsonDocument.Parse("{\"path\":\"notes.md\"}");
                LlmToolCall[] calls = [new(0, null, "read_file", arguments.RootElement)];
                return new(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), new(1, 2, null, 0, 3), true, false);
            }
            if (scenario is "auth" or "tool-auth" && !replaced)
            {
                await gate!.ReadExactlyAsync(new byte[1], cancellationToken);
                return new(LlmStatus.Failed, LlmErrorCode.AuthenticationFailed, "", [], null, null, true, false);
            }
            var text = request.Messages[^1].UserText == "control" ? "\u001b]0;blocked\u0007\r\nplain\ttext" : "離線回答";
            return new(LlmStatus.Completed, LlmErrorCode.None, text, [], LlmMessage.FromModel(new object()), new(1, 2, null, 0, 3), true, false);
        }
        catch (OperationCanceledException) { return new(LlmStatus.Cancelled, LlmErrorCode.None, "", [], null, null, true, false); }
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private static LlmResult PlanCalls(params (string Name, object Args)[] operations)
    {
        var calls = operations.Select((op, index) => new LlmToolCall(index, null, op.Name, JsonSerializer.SerializeToElement(op.Args))).ToArray();
        return new(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), new(1, 2, null, 0, 3), true, false);
    }
}

internal sealed class OfflineWorkspaceFactory(string scenario, FileStream? gate) : IWorkspaceFilesFactory
{
    private readonly WorkspaceFilesFactory _factory = new();
    public FileChangeApprovalBridge ApprovalBridge => _factory.ApprovalBridge;
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null)
    {
        if (scenario == "skills-read-gated") return new WorkspaceFiles(session.Workspace, session.Policy, new GatedRulesFileSystem(gate!, true), executionPolicy: executionPolicy);
        if (scenario == "rules-busy") return new WorkspaceFiles(session.Workspace, session.Policy, new GatedRulesFileSystem(gate!), executionPolicy: executionPolicy);
        if (scenario is "edit-stage-gated" or "edit-publish-gated")
            return new ObservedWriteWorkspace(new WorkspaceFiles(session.Workspace, session.Policy,
                new GatedEditFileSystem(scenario, gate!), ApprovalBridge.ForSession(session), executionPolicy));
        var workspace = _factory.Create(session, executionPolicy);
        return scenario is "tool-busy" or "search-busy" or "checkpoint-busy" ? new GatedWorkspace(workspace, gate!, scenario) : workspace;
    }
}

internal sealed class GatedWorkspace(IWorkspaceFiles inner, FileStream gate, string? scenario = null) : IWorkspaceFiles
{
    private bool _waited;
    private ICheckpointWorkspace? _checkpoint;
    public ICheckpointWorkspace? Checkpoints => scenario == "checkpoint-busy" && inner.Checkpoints is { } checkpoints
        ? _checkpoint ??= new GatedCheckpoint(checkpoints, gate) : inner.Checkpoints;
    public IProjectRulesWorkspace? ProjectRulesWorkspace => inner.ProjectRulesWorkspace;
    public ISkillsWorkspace? SkillsWorkspace => inner.SkillsWorkspace;
    public IReadSnapshotStore Snapshots => inner.Snapshots;
    public Task<WorkspaceFileSearchResult> SearchFilesAsync(string path, string pattern, CancellationToken token) => inner.SearchFilesAsync(path, pattern, token);
    public async Task<WorkspaceTextSearchResult> SearchTextAsync(string path, string query, string filePattern, bool caseSensitive, int contextLines, CancellationToken token)
    {
        if (!_waited)
        {
            _waited = true;
            using var cancellation = token.Register(() => Console.WriteLine("OFFLINE_SEARCH_CANCEL_OBSERVED"));
            Console.WriteLine("OFFLINE_SEARCH_WAITING");
            await gate.ReadExactlyAsync(new byte[1], CancellationToken.None);
        }
        return await inner.SearchTextAsync(path, query, filePattern, caseSensitive, contextLines, token);
    }
    public Task<WorkspaceRangeReadResult> ReadFileRangeAsync(string path, int startLine, int endLine, CancellationToken token) => inner.ReadFileRangeAsync(path, startLine, endLine, token);
    public Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken token) => inner.ListFilesAsync(path, token);
    public async Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken token)
    {
        if (!_waited && scenario != "checkpoint-busy")
        {
            _waited = true;
            // Deliberately finish late: the PTY driver observes cancellation, then releases
            // this operation. The application must wait and never publish a late success.
            using var cancellation = token.Register(() => Console.WriteLine("OFFLINE_TOOL_CANCEL_OBSERVED"));
            Console.WriteLine("OFFLINE_TOOL_WAITING");
            await gate.ReadExactlyAsync(new byte[1], CancellationToken.None);
        }
        return await inner.ReadFileAsync(path, token);
    }
    public Task<WorkspaceWriteResult> CreateFileAsync(string path, string content, CancellationToken token) => inner.CreateFileAsync(path, content, token);
    public Task<WorkspaceWriteResult> ReplaceTextAsync(string path, string oldText, string newText, CancellationToken token) => inner.ReplaceTextAsync(path, oldText, newText, token);
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

internal sealed class GatedCheckpoint(ICheckpointWorkspace inner, FileStream gate) : ICheckpointWorkspace
{
    public Task<CheckpointOperationResult> ListAsync(CancellationToken token) => inner.ListAsync(token);
    public Task<CheckpointOperationResult> ShowAsync(long id, CancellationToken token) => inner.ShowAsync(id, token);
    public Task<CheckpointOperationResult> ForgetAsync(long id, ICheckpointApproval approval, CancellationToken token) => inner.ForgetAsync(id, approval, token);
    public async Task<CheckpointOperationResult> RestoreAsync(long id, ICheckpointApproval approval, CancellationToken token)
    {
        using var observed = token.Register(() => Console.WriteLine("OFFLINE_CHECKPOINT_CANCEL_OBSERVED"));
        Console.WriteLine("OFFLINE_CHECKPOINT_WAITING");
        await gate.ReadExactlyAsync(new byte[1], CancellationToken.None);
        return await inner.RestoreAsync(id, approval, token);
    }
    public void Clear() => inner.Clear();
}

internal sealed class OfflineTerminal(ITerminal source, string scenario, FileStream? gate) : ITerminal
{
    private int _keyPrompts;
    private bool _keyPrinted;
    private bool _choicePrinted;
    private bool _inputPrinted;
    private bool _pastePrinted;
    private bool _pasteEnded;
    private bool _collecting;
    private bool _approvalPrinted;
    private bool _planApprovalPrinted;
    private bool _processApprovalPrinted;
    private bool _workApprovalPrinted;
    private bool _checkpointApprovalPrinted;
    private bool _mcpApprovalPrinted;
    private bool _previewWaited;
    public bool IsInteractive => source.IsInteractive;
    public IDisposable BeginInput() => source.BeginInput();
    public ConsoleKeyInfo? ReadKey(CancellationToken token)
    {
        var key = source.ReadKey(token);
        if (scenario.StartsWith("terminal", StringComparison.Ordinal) && _collecting && key?.KeyChar == '\r')
            Console.WriteLine("OFFLINE_PASTE_CR");
        return key;
    }
    public void DiscardBufferedInput() => source.DiscardBufferedInput();
    public ConsoleKeyInfo? TryReadBufferedKey()
    {
        var key = source.TryReadBufferedKey();
        if (key is null && _keyPrinted) { _keyPrinted = false; Console.WriteLine("OFFLINE_KEY_READY"); }
        if (key is null && _choicePrinted) { _choicePrinted = false; Console.WriteLine("OFFLINE_KEY_CHOICE_READY"); }
        if (key is null && _inputPrinted) { _inputPrinted = false; Console.WriteLine("OFFLINE_INPUT_READY"); }
        if (key is null && _pastePrinted) { _pastePrinted = false; Console.WriteLine("OFFLINE_PASTE_READY"); }
        if (key is null && _pasteEnded) { _pasteEnded = false; Console.WriteLine("OFFLINE_PASTE_END_READY"); }
        if (key is null && _approvalPrinted)
        {
            _approvalPrinted = false;
            Console.WriteLine("OFFLINE_APPROVAL_READY");
        }
        if (key is null && _planApprovalPrinted)
        {
            _planApprovalPrinted = false;
            Console.WriteLine("OFFLINE_PLAN_APPROVAL_READY");
        }
        if (key is null && _processApprovalPrinted)
        {
            _processApprovalPrinted = false; Console.WriteLine("OFFLINE_PROCESS_APPROVAL_READY");
        }
        if (key is null && _checkpointApprovalPrinted) { _checkpointApprovalPrinted = false; Console.WriteLine("OFFLINE_CHECKPOINT_APPROVAL_READY"); }
        if (key is null && _mcpApprovalPrinted) { _mcpApprovalPrinted = false; Console.WriteLine("OFFLINE_MCP_APPROVAL_READY"); }
        if (key is null && _workApprovalPrinted) { _workApprovalPrinted = false; Console.WriteLine("OFFLINE_WORK_APPROVAL_READY"); }
        return key;
    }
    public void Write(string text)
    {
        source.Write(text);
        if (text.EndsWith("確認本次外送？[y/N] ", StringComparison.Ordinal))
        {
            _mcpApprovalPrinted = true;
            if (scenario == "mcp-preview-gated" && !_previewWaited)
            { _previewWaited = true; Console.WriteLine("OFFLINE_MCP_PREVIEW_PAUSED"); gate!.ReadExactly(new byte[1]); }
        }
        if (text.EndsWith(CheckpointLimits.RestorePrompt, StringComparison.Ordinal) || text.EndsWith(CheckpointLimits.ForgetPrompt, StringComparison.Ordinal))
        {
            _checkpointApprovalPrinted = true;
            if (scenario == "checkpoint-preview-gated" && !_previewWaited)
            { _previewWaited = true; Console.WriteLine("OFFLINE_CHECKPOINT_PREVIEW_PAUSED"); gate!.ReadExactly(new byte[1]); }
        }
        if (Enum.GetValues<WorkApprovalKind>().Any(kind => text.EndsWith(WorkSessionCoordinator.Prompt(kind), StringComparison.Ordinal)))
        {
            _workApprovalPrinted = true;
            if (scenario == "work-preview-gated" && !_previewWaited)
            { _previewWaited = true; Console.WriteLine("OFFLINE_WORK_PREVIEW_PAUSED"); gate!.ReadExactly(new byte[1]); }
        }
        if (text.StartsWith("請輸入 API", StringComparison.Ordinal)) _keyPrinted = ++_keyPrompts > 1;
        if (text.StartsWith("重新輸入 Key？", StringComparison.Ordinal)) _choicePrinted = true;
        if (text.Contains("你> ", StringComparison.Ordinal)) _inputPrinted = true;
        if (text.StartsWith("貼上收集中", StringComparison.Ordinal)) { _collecting = true; _pastePrinted = true; }
        if (text.StartsWith("貼上收集已結束", StringComparison.Ordinal)) { _collecting = false; _pasteEnded = true; }
        if (text.StartsWith("草稿預覽", StringComparison.Ordinal) && scenario == "terminal-preview-gated" && !_previewWaited)
        {
            _previewWaited = true; Console.WriteLine("OFFLINE_DRAFT_PREVIEW_PAUSED"); gate!.ReadExactly(new byte[1]);
        }
        if (text.EndsWith(ProcessApprovalText.Prompt, StringComparison.Ordinal))
        {
            _processApprovalPrinted = true;
            if (scenario == "build-preview-gated" && !_previewWaited)
            {
                _previewWaited = true; Console.WriteLine("OFFLINE_PROCESS_PREVIEW_PAUSED");
                gate!.ReadExactly(new byte[1]);
            }
        }
        if (text.EndsWith(PlanProjection.Prompt, StringComparison.Ordinal))
        {
            _planApprovalPrinted = true;
            if (scenario == "plan-preview-gated" && !_previewWaited)
            {
                _previewWaited = true;
                Console.WriteLine("OFFLINE_PLAN_PREVIEW_PAUSED");
                gate!.ReadExactly(new byte[1]);
            }
        }
        if (text.EndsWith(FileChangePreviewBuilder.Prompt, StringComparison.Ordinal))
        {
            _approvalPrinted = true;
            if (scenario == "edit-preview-gated" && !_previewWaited)
            {
                _previewWaited = true;
                Console.WriteLine("OFFLINE_PREVIEW_PAUSED");
                gate!.ReadExactly(new byte[1]);
            }
        }
    }
}

internal sealed class ObservedWriteWorkspace(IWorkspaceFiles inner) : IWorkspaceFiles
{
    public IProjectRulesWorkspace? ProjectRulesWorkspace => inner.ProjectRulesWorkspace;
    public ISkillsWorkspace? SkillsWorkspace => inner.SkillsWorkspace;
    public IReadSnapshotStore Snapshots => inner.Snapshots;
    public Task<WorkspaceFileSearchResult> SearchFilesAsync(string path, string pattern, CancellationToken token) => inner.SearchFilesAsync(path, pattern, token);
    public Task<WorkspaceTextSearchResult> SearchTextAsync(string path, string query, string filePattern, bool caseSensitive, int contextLines, CancellationToken token) => inner.SearchTextAsync(path, query, filePattern, caseSensitive, contextLines, token);
    public Task<WorkspaceRangeReadResult> ReadFileRangeAsync(string path, int startLine, int endLine, CancellationToken token) => inner.ReadFileRangeAsync(path, startLine, endLine, token);
    public Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken token) => inner.ListFilesAsync(path, token);
    public Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken token) => inner.ReadFileAsync(path, token);
    public async Task<WorkspaceWriteResult> CreateFileAsync(string path, string content, CancellationToken token)
    {
        using var observed = token.Register(() => Console.WriteLine("OFFLINE_EDIT_CANCEL_OBSERVED"));
        return await inner.CreateFileAsync(path, content, token);
    }
    public Task<WorkspaceWriteResult> ReplaceTextAsync(string path, string oldText, string newText, CancellationToken token) => inner.ReplaceTextAsync(path, oldText, newText, token);
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

internal sealed class GatedEditFileSystem(string scenario, FileStream gate) : IWorkspaceWriteFileSystem
{
    private readonly MacWorkspaceFileSystem _inner = new();
    private bool _waited;
    private bool StageGate => scenario == "edit-stage-gated";
    private void Wait()
    {
        if (_waited) return;
        _waited = true;
        Console.WriteLine("OFFLINE_EDIT_IO_GATE");
        gate.ReadExactly(new byte[1]);
    }
    public bool HasExactName(string path) => _inner.HasExactName(path);
    public WorkspaceNodeState Inspect(string path) => _inner.Inspect(path);
    public IEnumerable<string> EnumerateNames(string path) => _inner.EnumerateNames(path);
    public IWorkspaceReadFile OpenRead(string path) => _inner.OpenRead(path);
    public UnixFileMode GetMode(string path) => _inner.GetMode(path);
    public IWorkspaceStagedFile CreateStagedFile(string path) => new StagedFile(this, _inner.CreateStagedFile(path));
    public void PublishNew(string staged, string target) { if (scenario == "edit-publish-gated") Wait(); _inner.PublishNew(staged, target); }
    public void PublishReplacement(string staged, string target) { if (scenario == "edit-publish-gated") Wait(); _inner.PublishReplacement(staged, target); }
    public void DeleteOwnedStagedFile(string path) => _inner.DeleteOwnedStagedFile(path);
    private sealed class StagedFile(GatedEditFileSystem owner, IWorkspaceStagedFile inner) : IWorkspaceStagedFile
    {
        public Stream Stream => inner.Stream;
        public WorkspaceNodeState GetState() => inner.GetState();
        public void SetMode(UnixFileMode mode) => inner.SetMode(mode);
        public void FlushToDisk() { inner.FlushToDisk(); if (owner.StageGate) owner.Wait(); }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}

// Tests inject this simulator only into the offline terminal host; it is not native isolation evidence.
internal sealed class OfflineProcessFactory(string scenario, FileStream? gate) : IControlledDotnetTasksFactory
{
    public bool IsAvailable => true;
    public IControlledDotnetTasks Create(ConfirmedWorkspace workspace, ISensitiveDataPolicy policy, AgentExecutionPolicy execution,
        IWorkspaceFiles files, Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve,
        Action<string> progress) => new OfflineProcessTasks(scenario, gate, policy, execution, approve, progress);
}

internal sealed class OfflineProcessTasks(string scenario, FileStream? gate, ISensitiveDataPolicy policy, AgentExecutionPolicy execution,
    Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve, Action<string> progress) : IControlledDotnetTasks, IProcessTurnLifecycle
{
    public void BeginTurn() { }
    public async Task<DotnetTaskResult> RunAsync(DotnetTaskKind task, string projectPath, CancellationToken token)
    {
        const string preview = "離線程序模擬器：可信專案；Debug；無外部網路；只允許標準 bin／obj 與私人暫存；產物保留。\n" +
            "程序可能執行專案程式；不保證追蹤所有刻意逃離後代。\n" + ProcessApprovalText.Prompt;
        var decision = await approve(new(preview, this, execution.Plans.Generation, null, "offline-once"), token);
        if (decision != ProcessApprovalDecision.Approved || token.IsCancellationRequested)
            return new(ToolResponseWriter.Create(decision == ProcessApprovalDecision.Unavailable ? ToolStatus.Failed :
                decision == ProcessApprovalDecision.Declined ? ToolStatus.Rejected : ToolStatus.Cancelled,
                decision == ProcessApprovalDecision.Unavailable ? "unavailable" : decision == ProcessApprovalDecision.Declined ? "approval_declined" : "turn_cancelled", policy).WithProcessStop());
        var cancelled = false;
        if (scenario == "build-running")
        {
            using var observed = token.Register(() => Console.WriteLine("OFFLINE_PROCESS_CANCEL_OBSERVED"));
            Console.WriteLine("OFFLINE_PROCESS_RUNNING");
            await gate!.ReadExactlyAsync(new byte[1], CancellationToken.None);
            cancelled = token.IsCancellationRequested;
        }
        if (scenario == "build-progress")
        {
            try { await Task.Delay(5000, token); progress("[程序] build；執行中，已經過 5 秒。"); }
            catch (OperationCanceledException) { cancelled = true; }
        }
        var unconfirmed = scenario == "build-unconfirmed";
        var summary = new ProcessExecutionSummary(task, projectPath, ProcessStartState.Started,
            unconfirmed ? ProcessEndState.Unconfirmed : cancelled ? ProcessEndState.Cancelled : ProcessEndState.Exited,
            unconfirmed || cancelled ? null : 0, 1, !unconfirmed, false,
            unconfirmed || cancelled ? ValidationOutcome.NotVerified : ValidationOutcome.BuildSucceeded);
        return new(ProcessResultWriter.Create(unconfirmed ? ToolStatus.Failed : cancelled ? ToolStatus.Cancelled : ToolStatus.Success,
            unconfirmed ? "process_unconfirmed" : cancelled ? "turn_cancelled" : "none", summary, null, policy));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class GatedRulesFileSystem(FileStream gate, bool skills = false) : IWorkspaceReadFileSystem
{
    private readonly MacWorkspaceFileSystem _inner = new();
    private int _reads;
    public bool HasExactName(string path) => _inner.HasExactName(path);
    public WorkspaceNodeState Inspect(string path) => _inner.Inspect(path);
    public IEnumerable<string> EnumerateNames(string path) => _inner.EnumerateNames(path);
    public IWorkspaceReadFile OpenRead(string path)
    {
        var file = _inner.OpenRead(path);
        return ++_reads == (skills ? 1 : 3) ? new GatedFile(file, gate, skills) : file;
    }
    private sealed class GatedFile(IWorkspaceReadFile inner, FileStream gate, bool skills) : IWorkspaceReadFile
    {
        private readonly Stream _stream = new GatedStream(inner.Stream, gate, skills);
        public Stream Stream => _stream;
        public WorkspaceNodeState GetState() => inner.GetState();
        public UnixFileMode GetMode() => inner.GetMode();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
    private sealed class GatedStream(Stream inner, FileStream gate, bool skills) : Stream
    {
        private bool _waited;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (!_waited)
            {
                _waited = true; Console.WriteLine(skills ? "OFFLINE_SKILLS_READING" : "OFFLINE_RULES_READING");
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException)
                {
                    Console.WriteLine(skills ? "OFFLINE_SKILLS_CANCEL_OBSERVED" : "OFFLINE_RULES_CANCEL_OBSERVED");
                    _ = await gate.ReadAsync(new byte[1], CancellationToken.None);
                    throw;
                }
            }
            return await inner.ReadAsync(buffer, token);
        }
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}