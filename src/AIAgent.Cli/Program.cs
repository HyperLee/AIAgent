using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += onCancel;
try
{
    var interaction = new ConsoleInteraction(new SystemTerminal());
    var bootstrapper = new Bootstrapper(new SettingsLoader(AppContext.BaseDirectory),
        new WorkspaceResolver(new WorkspaceFileSystem()), interaction, interaction);
    using var result = await bootstrapper.StartAsync(args, cancellation.Token);
    interaction.Policy = result.Session?.Policy;
    await using var model = result.Session is { } session ? new GeminiClient(session) : null;
    interaction.ShowStatus(StatusMessages.For(result));
    return result.ExitCode;
}
catch (OperationCanceledException) { return 130; }
catch (Exception) { return 1; } // No raw exceptions may reach console or diagnostics.
finally { Console.CancelKeyPress -= onCancel; }
