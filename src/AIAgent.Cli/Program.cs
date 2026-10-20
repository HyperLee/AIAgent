using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;

try
{
    using var cancellation = new CancellationTokenSource();
    using var interrupts = new ConsoleInterruptRouter(cancellation.Cancel);
    var terminal = new SystemTerminal();
    var interaction = new ConsoleInteraction(terminal);
    var bootstrapper = new Bootstrapper(new SettingsLoader(AppContext.BaseDirectory),
        new WorkspaceResolver(new WorkspaceFileSystem()), interaction, interaction);
    using var result = await bootstrapper.StartAsync(args, cancellation.Token);
    if (result.Status != BootstrapStatus.Ready)
    {
        interaction.ShowStatus(StatusMessages.For(result));
        return result.ExitCode;
    }
    using var chatInteraction = new ConsoleChatInteraction(terminal);
    var chat = new ChatApplication(chatInteraction, new ChatRuntimeFactory(), new ConversationContext(),
        new UsageAccumulator(), ApplicationInstructions.Value, new WorkspaceFilesFactory());
    interrupts.RouteTo(chat.Interrupt);
    return await chat.RunAsync(result.TakeSession(), cancellation.Token);
}
catch (OperationCanceledException) { return 130; }
catch (Exception) { return 1; } // No raw exceptions may reach console or diagnostics.