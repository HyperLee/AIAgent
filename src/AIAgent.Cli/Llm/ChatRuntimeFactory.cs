using AIAgent.Cli.Core;

namespace AIAgent.Cli.Llm;

internal sealed class ChatRuntimeFactory(Func<ModelProfile, HttpMessageHandler>? handlerFactory = null) : IChatRuntimeFactory
{
    public BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput)
    {
        var secrets = acceptedInput.TakeSecrets();
        try { return new BootstrapSession(workspace, settings, secrets); }
        catch { secrets.Dispose(); throw; }
    }
    public ILlmClient CreateClient(BootstrapSession session)
    {
        var profile = ModelCatalog.Find(session.Settings) ?? throw new InvalidOperationException("模型能力不可用");
        var handler = handlerFactory?.Invoke(profile);
        return profile.ProtocolKind switch
        {
            ModelProtocolKind.GeminiGenerateContent => handler is null ? new GeminiClient(session) : new GeminiClient(session, handler),
            ModelProtocolKind.OpenAiResponses => handler is null ? new OpenAiClient(session) : new OpenAiClient(session, handler),
            _ => throw new InvalidOperationException("模型能力不可用")
        };
    }
    public ILlmContextInspector? CreateContextInspector(ILlmClient client) => client is GeminiClient gemini ? gemini.CreateContextInspector() : client as OpenAiClient;
}