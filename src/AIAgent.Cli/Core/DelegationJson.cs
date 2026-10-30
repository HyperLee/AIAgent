using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using AIAgent.Cli.Git;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Core;

internal static class DelegationJson
{
    internal static string ParseTasks(JsonElement root, out IReadOnlyList<DelegationTask>? tasks)
    {
        tasks = null;
        try
        {
            if (!GitArguments.Exact(root, "tasks")) return "invalid_arguments";
            var array = root.GetProperty("tasks");
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 2) return "invalid_arguments";
            if (Bytes(root) > 16384) return "invalid_arguments";
            var result = new List<DelegationTask>();
            foreach (var item in array.EnumerateArray())
            {
                if (!GitArguments.Exact(item, "role", "objective", "scope_path", "acceptance")) return "invalid_arguments";
                var role = Text(item, "role"); var objective = Text(item, "objective"); var scope = Text(item, "scope_path"); var acceptance = Text(item, "acceptance");
                if (role is not ("explore" or "review") || !ValidText(objective, 2048) || !ValidText(acceptance, 2048) || scope is null || scope.Length > 4096) return "invalid_arguments";
                result.Add(new(role == "explore" ? DelegationRole.Explore : DelegationRole.Review, objective!, scope, acceptance!));
            }
            tasks = result.AsReadOnly(); return "none";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return "invalid_arguments"; }
    }
    internal static bool ValidText(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > max || text.Any(char.IsControl)) return false;
        for (var i = 0; i < text.Length; i++)
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        return true;
    }
    internal static string? Text(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    internal static int Bytes(JsonElement value) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) value.WriteTo(writer); return checked((int)stream.Length); }
    internal static JsonElement Write(Action<Utf8JsonWriter> write, int limit = 65536)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) write(writer);
        if (stream.Length > limit) throw new DelegationFailure("result_too_large");
        using var document = JsonDocument.Parse(stream.ToArray()); return document.RootElement.Clone();
    }
    internal static string Name<T>(T value) where T : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    internal static JsonElement Coverage(DelegationRole role, int sources = 0, int delivered = 0, bool complete = false, bool truncated = false, params string[] limitations) => Write(w =>
    {
        w.WriteStartObject(); w.WriteString("basis", role == DelegationRole.Explore ? "workspace_observation" : "git_snapshot");
        w.WriteBoolean("complete", role == DelegationRole.Review && complete); w.WriteNumber("source_count", sources); w.WriteNumber("delivered_count", delivered); w.WriteBoolean("truncated", truncated);
        w.WriteStartArray("limitations"); foreach (var item in limitations.Distinct(StringComparer.Ordinal)) w.WriteStringValue(item); w.WriteEndArray(); w.WriteEndObject();
    });
    internal static void Child(Utf8JsonWriter w, DelegationChildResult child)
    {
        w.WriteStartObject(); w.WriteString("agent_id", child.AgentId); w.WriteString("role", Name(child.Task.Role)); w.WriteString("scope_path", child.Task.ScopePath);
        w.WriteString("state", Name(child.State)); w.WriteString("error_code", child.ErrorCode); w.WritePropertyName("report");
        if (child.Report is { } report) report.WriteTo(w); else w.WriteNullValue();
        w.WritePropertyName("coverage"); child.Coverage.WriteTo(w); w.WriteEndObject();
    }
    internal static ToolExecutionResult Response(DelegationBatchResult result, ISensitiveDataPolicy policy)
    {
        var value = Write(w =>
        {
            w.WriteStartObject(); w.WriteString("status", Name(result.Status)); w.WriteString("error_code", result.ErrorCode); w.WritePropertyName("data");
            if (result.Data is { } data) data.WriteTo(w); else w.WriteNullValue(); w.WriteEndObject();
        });
        return new(result.Status, result.ErrorCode, SensitiveJson.Contains(value, policy, ContentBoundary.ToolResult) ? null : value, delegationOutcome: result);
    }
}