using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;

using Microsoft.Win32.SafeHandles;

// Offline PTY assembly only. No product switches, real keys or network clients.
try
{
    var scenario = args[0];
    using var gate = args.Length > 1 ? new FileStream(new SafeFileHandle((IntPtr)int.Parse(args[1]), ownsHandle: true), FileAccess.Read) : null;
    using var cancellation = new CancellationTokenSource();
    using var interrupts = new ConsoleInterruptRouter(cancellation.Cancel);
    var terminal = new SystemTerminal();
    var bootstrap = new ConsoleInteraction(terminal);
    using var result = await new Bootstrapper(new FixedSettings(), new WorkspaceResolver(new WorkspaceFileSystem()), bootstrap, bootstrap).StartAsync([], cancellation.Token);
    if (result.Status != BootstrapStatus.Ready) { bootstrap.ShowStatus(StatusMessages.For(result)); return result.ExitCode; }
    using var interaction = new ConsoleChatInteraction(terminal);
    var chat = new ChatApplication(interaction, new OfflineRuntime(scenario, gate), new(), new(), "test instruction", new WorkspaceFilesFactory());
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
            if (scenario == "failure") throw new IOException("FAKE_LOCAL_FAILURE_ONLY");
            if (scenario == "busy" && ++_calls == 1) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (scenario == "auth" && !replaced)
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