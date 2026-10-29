using AIAgent.Cli.Configuration;

namespace AIAgent.Cli.Core;

internal enum ModelProtocolKind { GeminiGenerateContent, OpenAiResponses }

internal sealed class ModelProfile(string provider, string model, ModelProtocolKind protocol, string destination)
{
    internal string Provider { get; } = provider;
    internal string Model { get; } = model;
    internal ModelProtocolKind ProtocolKind { get; } = protocol;
    internal string Destination { get; } = destination;
    internal bool SupportsText => true;
    internal bool SupportsFunctionCalling => true;
    internal bool SupportsNonStreaming => true;
    internal bool SupportsSafeHistory => true;
    internal bool SupportsCapacityMeasurement => true;
    internal int RequestBodyLimit => 262144;
    internal PublicSettings Settings => new(Provider, Model);
    public override string ToString() => "[ModelProfile]";
}

internal static class ModelCatalog
{
    internal static IReadOnlyList<ModelProfile> Profiles { get; } = Array.AsReadOnly(new[]
    {
        Gemini("gemini-3.5-flash-lite"), Gemini("gemini-3.6-flash"),
        new ModelProfile("openai", "gpt-6.1-terra", ModelProtocolKind.OpenAiResponses, "https://api.openai.com/v1/responses")
    });

    private static ModelProfile Gemini(string model) => new("gemini", model, ModelProtocolKind.GeminiGenerateContent,
        "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent");

    internal static ModelProfile? Find(string? provider, string? model) => ModelIdentifier.IsValid(model)
        ? Profiles.SingleOrDefault(p => p.Provider == provider && p.Model == model) : null;
    internal static ModelProfile? Find(PublicSettings settings) => Find(settings.Provider, settings.Model);
}