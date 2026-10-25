using AIAgent.Cli.Core;

namespace AIAgent.Cli.Llm;

internal sealed class ChatRuntimeFactory : IChatRuntimeFactory
{
    public BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput)
    {
        var secrets = acceptedInput.TakeSecrets();
        try { return new BootstrapSession(workspace, settings, secrets); }
        catch { secrets.Dispose(); throw; }
    }
    public ILlmClient CreateClient(BootstrapSession session) => new GeminiClient(session);
    public ILlmContextInspector? CreateContextInspector(ILlmClient client) => (client as GeminiClient)?.CreateContextInspector();
}