namespace AIAgent.Cli.Core;

internal enum ModelCommandKind { Show, List, Use, Invalid }
internal sealed class ModelCommand(ModelCommandKind kind, ModelProfile? profile = null, string errorCode = "invalid_model_command")
{
    internal ModelCommandKind Kind { get; } = kind;
    internal ModelProfile? Profile { get; } = profile;
    internal string ErrorCode { get; } = errorCode;
    public override string ToString() => "[ModelCommand]";
}

internal static class ModelCommands
{
    internal static ModelCommand Parse(string raw)
    {
        if (raw.Contains('\n') || raw.Contains('\r')) return new(ModelCommandKind.Invalid);
        var text = raw.Trim();
        if (text == "/model") return new(ModelCommandKind.Show);
        if (text == "/model list") return new(ModelCommandKind.List);
        var tokens = text.Split(' ');
        if (tokens.Length != 4 || tokens[0] != "/model" || tokens[1] != "use" || tokens.Any(t => t.Length == 0 || t.Any(char.IsWhiteSpace))) return new(ModelCommandKind.Invalid);
        var profile = ModelCatalog.Find(tokens[2], tokens[3]);
        return profile is null ? new(ModelCommandKind.Invalid, errorCode: "model_not_supported") : new(ModelCommandKind.Use, profile);
    }

    internal static string Describe(ModelProfile profile) => "Provider：" + profile.Provider + "；Model：" + profile.Model +
        "\n官方目的地：" + profile.Destination + "\n能力：文字、多輪、function calling、非串流、安全歷史投影、容量量測；請求上限 262144 UTF-8 bytes。";
}