using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;
using System.Text.Json;

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
    using var result = await new Bootstrapper(new FixedSettings(), new WorkspaceResolver(new WorkspaceFileSystem()), bootstrap, bootstrap).StartAsync([], cancellation.Token);
    if (result.Status != BootstrapStatus.Ready) { bootstrap.ShowStatus(StatusMessages.For(result)); return result.ExitCode; }
    using var interaction = new ConsoleChatInteraction(terminal);
    var chat = new ChatApplication(interaction, new OfflineRuntime(scenario, gate), new(), new(), "test instruction", new OfflineWorkspaceFactory(scenario, gate));
    interrupts.RouteTo(chat.Interrupt);
    return await chat.RunAsync(result.TakeSession(), cancellation.Token);
}
catch (Exception) { return 1; }

internal sealed class FixedSettings : IPublicSettingsLoader
{
    public PublicSettings Load() => new("gemini-3.5-flash-lite");
}

internal sealed class OfflineRuntime(string scenario, FileStream? gate) : IChatRuntimeFactory
{
    private int _clients;
    public BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput) => new ChatRuntimeFactory().CreateReplacementSession(workspace, settings, acceptedInput);
    public ILlmClient CreateClient(BootstrapSession session) => new OfflineClient(scenario, gate, ++_clients > 1);
}

internal sealed class OfflineClient(string scenario, FileStream? gate, bool replaced) : ILlmClient
{
    private int _calls;
    public async Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _calls++;
            if (scenario.StartsWith("edit", StringComparison.Ordinal) && _calls == 1)
            {
                if (scenario == "edit-busy-gated")
                {
                    Console.WriteLine("OFFLINE_EDIT_GENERATING");
                    await gate!.ReadExactlyAsync(new byte[1], cancellationToken);
                }
                using var editArguments = JsonDocument.Parse("{\"path\":\"new.md\",\"content\":\"Hello\\n\"}");
                LlmToolCall[] editCalls = [new(0, null, "create_file", editArguments.RootElement)];
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
}

internal sealed class OfflineWorkspaceFactory(string scenario, FileStream? gate) : IWorkspaceFilesFactory
{
    private readonly WorkspaceFilesFactory _factory = new();
    public FileChangeApprovalBridge ApprovalBridge => _factory.ApprovalBridge;
    public IWorkspaceFiles Create(BootstrapSession session)
    {
        if (scenario is "edit-stage-gated" or "edit-publish-gated")
            return new ObservedWriteWorkspace(new WorkspaceFiles(session.Workspace, session.Policy,
                new GatedEditFileSystem(scenario, gate!), ApprovalBridge.ForSession(session)));
        var workspace = _factory.Create(session);
        return scenario == "tool-busy" ? new GatedWorkspace(workspace, gate!) : workspace;
    }
}

internal sealed class GatedWorkspace(IWorkspaceFiles inner, FileStream gate) : IWorkspaceFiles
{
    private bool _waited;
    public IReadSnapshotStore Snapshots => inner.Snapshots;
    public Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken token) => inner.ListFilesAsync(path, token);
    public async Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken token)
    {
        if (!_waited)
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

internal sealed class OfflineTerminal(ITerminal source, string scenario, FileStream? gate) : ITerminal
{
    private bool _approvalPrinted;
    private bool _previewWaited;
    public bool IsInteractive => source.IsInteractive;
    public IDisposable BeginInput() => source.BeginInput();
    public ConsoleKeyInfo? ReadKey(CancellationToken token) => source.ReadKey(token);
    public void DiscardBufferedInput() => source.DiscardBufferedInput();
    public ConsoleKeyInfo? TryReadBufferedKey()
    {
        var key = source.TryReadBufferedKey();
        if (key is null && _approvalPrinted)
        {
            _approvalPrinted = false;
            Console.WriteLine("OFFLINE_APPROVAL_READY");
        }
        return key;
    }
    public void Write(string text)
    {
        source.Write(text);
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
    public IReadSnapshotStore Snapshots => inner.Snapshots;
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