using System.Text.Json;

namespace AIAgent.Cli.Core;

internal static class McpSchema
{
    internal static JsonElement Admit(JsonElement schema)
    {
        try { return AdmitCore(schema); }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException) { throw new McpFailure("mcp_schema_unsupported"); }
    }
    internal static JsonElement AdmitFetch(JsonElement schema)
    {
        var admitted = Admit(schema);
        var properties = admitted.GetProperty("properties");
        if (properties.EnumerateObject().Count() != 1 || !properties.TryGetProperty("url", out var url) ||
            url.GetProperty("type").ValueKind != JsonValueKind.String || url.GetProperty("type").GetString() != "string" ||
            !admitted.TryGetProperty("required", out var required) || required.GetArrayLength() != 1 || required[0].GetString() != "url") Fail();
        return admitted;
    }
    private static JsonElement AdmitCore(JsonElement schema)
    {
        McpJson.Validate(schema); McpJson.Limit(McpJson.Write(schema).Length, 8192);
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type) || type.GetString() != "object" ||
            !schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object || properties.EnumerateObject().Count() is < 1 or > 16) Fail();
        foreach (var p in schema.EnumerateObject())
        {
            if (p.Name is "title" or "description") { if (p.Value.ValueKind != JsonValueKind.String) Fail(); }
            else if (p.Name == "$schema") { if (p.Value.ValueKind != JsonValueKind.String || p.Value.GetString() != "https://json-schema.org/draft/2020-12/schema") Fail(); }
            else if (p.Name == "additionalProperties") { if (p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) Fail(); }
            else if (p.Name is not ("type" or "properties" or "required" or "default")) Fail();
        }
        properties = schema.GetProperty("properties");
        foreach (var p in properties.EnumerateObject())
        {
            if (p.Name.Length is < 1 or > 64 || !(char.IsAsciiLetter(p.Name[0]) || p.Name[0] == '_') ||
                p.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) Fail();
            Property(p.Value);
        }
        if (schema.TryGetProperty("required", out var required))
        {
            if (required.ValueKind != JsonValueKind.Array) Fail();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in required.EnumerateArray())
                if (item.ValueKind != JsonValueKind.String || !seen.Add(item.GetString()!) || !properties.TryGetProperty(item.GetString()!, out _)) Fail();
        }
        if (schema.TryGetProperty("default", out var defaultValue) && !ValidArguments(schema, defaultValue)) Fail();
        var bytes = McpJson.Write(w =>
        {
            w.WriteStartObject();
            foreach (var p in schema.EnumerateObject().Where(p => p.Name != "additionalProperties").OrderBy(p => p.Name, StringComparer.Ordinal))
            { w.WritePropertyName(p.Name); McpJson.Element(McpJson.Write(p.Value)).WriteTo(w); }
            w.WriteBoolean("additionalProperties", false); w.WriteEndObject();
        });
        McpJson.Limit(bytes.Length, 8192); return McpJson.Element(bytes);
    }
    private static void Property(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type)) Fail();
        type = schema.GetProperty("type");
        if (type.ValueKind == JsonValueKind.Array)
        {
            var values = type.EnumerateArray().ToArray();
            if (values.Length != 2 || values.Any(v => v.ValueKind != JsonValueKind.String) ||
                !values.Select(v => v.GetString()).ToHashSet().SetEquals(["string", "null"])) Fail();
        }
        else if (type.ValueKind != JsonValueKind.String || type.GetString() != "string") Fail();
        foreach (var p in schema.EnumerateObject())
        {
            if (p.Name is "title" or "description") { if (p.Value.ValueKind != JsonValueKind.String) Fail(); }
            else if (p.Name is "minLength" or "maxLength") { if (!p.Value.TryGetInt32(out var n) || n < 0) Fail(); }
            else if (p.Name is not ("type" or "enum" or "default")) Fail();
        }
        if (Minimum(schema) > Maximum(schema)) Fail();
        if (schema.TryGetProperty("enum", out var choices))
        {
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() is < 1 or > 32) Fail();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in choices.EnumerateArray()) if (!ValidValue(schema, item, false) || !seen.Add(System.Text.Encoding.UTF8.GetString(McpJson.Write(item)))) Fail();
        }
        if (schema.TryGetProperty("default", out var value) && !ValidValue(schema, value, true)) Fail();
    }
    internal static bool ValidArguments(JsonElement schema, JsonElement arguments)
    {
        try
        {
            McpJson.Validate(arguments);
            if (arguments.ValueKind != JsonValueKind.Object || McpJson.Write(arguments).Length > McpProfile.MaxArgumentsBytes) return false;
            var properties = schema.GetProperty("properties");
            foreach (var p in arguments.EnumerateObject())
                if (!properties.TryGetProperty(p.Name, out var property) || !ValidValue(property, p.Value, true) || p.Value.ValueKind == JsonValueKind.String && p.Value.GetString()!.Length > 1024) return false;
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray()) if (!arguments.TryGetProperty(name.GetString()!, out _)) return false;
            return true;
        }
        catch (Exception ex) when (ex is McpFailure or InvalidOperationException or KeyNotFoundException) { return false; }
    }
    private static bool ValidValue(JsonElement schema, JsonElement value, bool checkEnum)
    {
        if (value.ValueKind == JsonValueKind.Null)
        { if (schema.GetProperty("type").ValueKind != JsonValueKind.Array) return false; }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var length = value.GetString()!.EnumerateRunes().Count();
            if (length < Minimum(schema) || length > Maximum(schema)) return false;
        }
        else return false;
        return !checkEnum || !schema.TryGetProperty("enum", out var choices) || choices.EnumerateArray().Any(c => JsonElement.DeepEquals(c, value));
    }
    private static int Minimum(JsonElement value) => value.TryGetProperty("minLength", out var n) ? n.GetInt32() : 0;
    private static int Maximum(JsonElement value) => value.TryGetProperty("maxLength", out var n) ? n.GetInt32() : int.MaxValue;
    private static void Fail() => throw new McpFailure("mcp_schema_unsupported");
}