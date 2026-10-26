using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;

namespace AIAgent.Cli.Core;

public interface IBootstrapInteraction
{
    ValueTask<bool> ConfirmWorkspaceAsync(ConfirmedWorkspace workspace, PublicSettings settings, CancellationToken cancellationToken);
    ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken);
    void ShowStatus(string safeMessage);
}

public interface ITerminalCapability
{
    void EnsureAvailable();
}

public sealed class Bootstrapper
{
    private readonly IPublicSettingsLoader settings;
    private readonly IWorkspaceResolver workspace;
    private readonly ITerminalCapability capability;
    private readonly IBootstrapInteraction interaction;
    private readonly Func<string> startupDirectory;
    public Bootstrapper(IPublicSettingsLoader settings, IWorkspaceResolver workspace, ITerminalCapability capability,
        IBootstrapInteraction interaction, Func<string>? startupDirectory = null)
    { this.settings = settings; this.workspace = workspace; this.capability = capability; this.interaction = interaction; this.startupDirectory = startupDirectory ?? Directory.GetCurrentDirectory; }

    public async ValueTask<BootstrapResult> StartAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = ArgumentParser.Parse(args, startupDirectory());
            if (parsed.Help) return BootstrapResult.End(BootstrapStatus.Help);
            var publicSettings = settings.Load();
            capability.EnsureAvailable();
            var root = workspace.Resolve(parsed.Options);
            if (!await interaction.ConfirmWorkspaceAsync(root, publicSettings, cancellationToken))
                return BootstrapResult.End(BootstrapStatus.Declined);
            cancellationToken.ThrowIfCancellationRequested();
            workspace.Revalidate(root);
            using var key = await interaction.ReadKeyAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (key.Status != KeyInputStatus.Accepted)
                return key.Status switch
                {
                    KeyInputStatus.EndOfInput => BootstrapResult.End(BootstrapStatus.EndOfInput),
                    KeyInputStatus.Cancelled => BootstrapResult.End(BootstrapStatus.Cancelled),
                    _ => BootstrapResult.Failed(BootstrapErrorCode.TerminalUnavailable)
                };
            var secrets = key.TakeSecrets();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return BootstrapResult.Ready(new BootstrapSession(root, publicSettings, secrets, parsed.ResumeWorkId));
            }
            catch { secrets.Dispose(); throw; }
        }
        catch (BootstrapEndOfInputException) { return BootstrapResult.End(BootstrapStatus.EndOfInput); }
        catch (OperationCanceledException) { return BootstrapResult.End(BootstrapStatus.Cancelled); }
        catch (BootstrapFailureException ex) { return BootstrapResult.Failed(ex.Code); }
        catch (Exception) { return BootstrapResult.Failed(BootstrapErrorCode.IoFailure); }
    }
}