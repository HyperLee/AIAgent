using AIAgent.Cli.Core;
using System.Text.Json;

namespace AIAgent.Cli.Configuration;

public interface IPublicSettingsLoader
{
    PublicSettings Load();
}

internal sealed class SettingsLoader(string applicationDirectory, Func<string, Stream>? openRead = null) : IPublicSettingsLoader
{
    public PublicSettings Load()
    {
        try
        {
            using var stream = (openRead ?? File.OpenRead)(Path.Combine(applicationDirectory, "appsettings.json"));
            using var document = JsonDocument.Parse(stream);
            var gemini = OnlyProperty(document.RootElement, "Gemini");
            var model = OnlyProperty(gemini, "Model");
            if (model.ValueKind != JsonValueKind.String) throw Invalid();
            var value = model.GetString()!;
            if (value.Any(char.IsControl) || !ModelIdentifier.IsValid(value.Trim())) throw Invalid();
            return new(value.Trim());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            throw Invalid();
        }
    }

    private static JsonElement OnlyProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid();
        var properties = element.EnumerateObject();
        if (!properties.MoveNext() || properties.Current.Name != name) throw Invalid();
        var value = properties.Current.Value;
        if (properties.MoveNext()) throw Invalid();
        return value;
    }

    private static BootstrapFailureException Invalid() => new(BootstrapErrorCode.InvalidSettings);
}
