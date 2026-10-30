using System.Text.Json;

using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal static class DelegationReports
{
    internal static JsonElement? Explore(string text, DelegationSourceLedger ledger)
    {
        try
        {
            using var doc = JsonDocument.Parse(text, new() { MaxDepth = 16 }); var root = doc.RootElement;
            if (!GitArguments.Exact(root, "version", "summary", "observations", "limitations") || !root.GetProperty("version").TryGetInt32(out var version) || version != 1 ||
                !DelegationJson.ValidText(DelegationJson.Text(root, "summary"), 2048)) return null;
            var observations = root.GetProperty("observations"); var limitations = root.GetProperty("limitations");
            if (observations.ValueKind != JsonValueKind.Array || observations.GetArrayLength() > 8 || limitations.ValueKind != JsonValueKind.Array || limitations.GetArrayLength() > 8) return null;
            foreach (var item in limitations.EnumerateArray()) if (item.ValueKind != JsonValueKind.String || !DelegationJson.ValidText(item.GetString(), 1024)) return null;
            foreach (var item in observations.EnumerateArray())
            {
                if (!GitArguments.Exact(item, "path", "start_line", "end_line", "note") || !DelegationJson.ValidText(DelegationJson.Text(item, "note"), 1024)) return null;
                var path = DelegationJson.Text(item, "path"); if (!WorkspacePathRules.IsCanonical(path) || WorkspacePathRules.IsSensitive(path!)) return null;
                var a = item.GetProperty("start_line"); var b = item.GetProperty("end_line"); int? start = null; int? end = null;
                if (a.ValueKind != JsonValueKind.Null || b.ValueKind != JsonValueKind.Null)
                {
                    if (a.ValueKind != JsonValueKind.Number || b.ValueKind != JsonValueKind.Number || !a.TryGetInt32(out var first) || !b.TryGetInt32(out var last) || first < 1 || last < first || (long)last - first + 1 > 200) return null;
                    start = first; end = last;
                }
                if (!ledger.Delivered(path!, start, end)) return null;
            }
            return root.Clone();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }
    internal static JsonElement Batch(string id, IReadOnlyList<DelegationChildResult> children)
    {
        var completed = children.Count(c => c.State == DelegationTaskState.Completed);
        var state = completed == children.Count ? "completed" : completed > 0 ? "partial" : children.Any(c => c.State == DelegationTaskState.TimedOut) ? "timed_out" : "failed";
        return DelegationJson.Write(w =>
        {
            w.WriteStartObject(); w.WriteNumber("version", 1); w.WriteString("batch_id", id); w.WriteString("state", state);
            w.WriteStartArray("children"); foreach (var child in children) DelegationJson.Child(w, child); w.WriteEndArray();
            w.WriteStartArray("merged_findings");
            var findings = new Dictionary<string, (JsonElement Finding, List<string> Agents)>(StringComparer.Ordinal);
            foreach (var child in children.Where(c => c.State == DelegationTaskState.Completed && c.Task.Role == DelegationRole.Review && c.Report is not null))
                foreach (var finding in child.Report!.Value.GetProperty("findings").EnumerateArray())
                {
                    var key = JsonSerializer.Serialize(new object?[] { DelegationJson.Text(child.Report!.Value, "snapshot_id"),
                        finding.GetProperty("path").GetString(), finding.GetProperty("layer").GetString(), finding.GetProperty("side").GetString(),
                        finding.GetProperty("start_line").GetInt32(), finding.GetProperty("end_line").GetInt32(), finding.GetProperty("severity").GetString(),
                        finding.GetProperty("title").GetString(), finding.GetProperty("body").GetString() });
                    if (findings.TryGetValue(key, out var existing)) existing.Agents.Add(child.AgentId);
                    else findings.Add(key, (finding, [child.AgentId]));
                }
            foreach (var item in findings.Values)
            {
                w.WriteStartObject(); w.WritePropertyName("finding"); item.Finding.WriteTo(w); w.WriteStartArray("agent_ids");
                foreach (var agent in item.Agents) w.WriteStringValue(agent); w.WriteEndArray(); w.WriteEndObject();
            }
            w.WriteEndArray(); w.WriteEndObject();
        }, 32768);
    }
}