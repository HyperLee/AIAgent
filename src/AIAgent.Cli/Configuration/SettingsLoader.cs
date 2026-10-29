using System.Text.Json;

using AIAgent.Cli.Core;

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
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw Invalid();
            var roots = root.EnumerateObject().ToArray();
            if (roots.Length != 1) throw Invalid();
            var provider = "gemini";
            JsonElement model;
            if (roots[0].Name == "Gemini") model = OnlyProperty(roots[0].Value, "Model");
            else if (roots[0].Name == "Llm")
            {
                var llm = roots[0].Value;
                if (llm.ValueKind != JsonValueKind.Object) throw Invalid();
                var fields = llm.EnumerateObject().ToArray();
                if (fields.Length != 2 || fields.Count(p => p.Name == "Provider") != 1 || fields.Count(p => p.Name == "Model") != 1) throw Invalid();
                var value = llm.GetProperty("Provider");
                if (value.ValueKind != JsonValueKind.String || value.GetString()!.Any(char.IsControl)) throw Invalid();
                provider = value.GetString()!.Trim();
                model = llm.GetProperty("Model");
            }
            else throw Invalid();
            if (model.ValueKind != JsonValueKind.String) throw Invalid();
            var modelValue = model.GetString()!;
            if (modelValue.Any(char.IsControl) || ModelCatalog.Find(provider, modelValue.Trim()) is null) throw Invalid();
            return new(provider, modelValue.Trim());
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