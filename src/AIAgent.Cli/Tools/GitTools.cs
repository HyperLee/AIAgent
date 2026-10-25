using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tools;

internal abstract class GitTool(GitSnapshotCoordinator snapshots, ISensitiveDataPolicy policy, AgentExecutionPolicy? execution = null) : ITool
{
    public ToolCapability Capability => ToolCapability.GitRead;
    public abstract LlmToolDefinition Definition { get; }
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ToolExecutionResult Error(string code) => GitProjection.Result(code == "turn_cancelled" ? ToolStatus.Cancelled : code is "git_failed" or "git_timed_out" or "process_cleanup_failed" or "process_unconfirmed" ? ToolStatus.Failed : ToolStatus.Rejected,
            code, policy, new(snapshots.Current?.SnapshotId, snapshots.Selection, code));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (execution is not null && !execution.Allows(Capability)) return Error("mode_restricted");
            if (Encoding.UTF8.GetByteCount(arguments.GetRawText()) > GitLimits.MaxResultBytes) return Error("result_too_large");
            if (SensitiveJson.Contains(arguments, policy, ContentBoundary.ToolArguments)) return Error("sensitive_arguments");
            if (!GitArguments.Parse(Definition.Name, arguments, out var input)) return Error("invalid_arguments");
            if (Definition.Name == "git_status")
            {
                var statusError = snapshots.Bound ? await snapshots.ValidateAsync(cancellationToken) : await snapshots.CaptureAsync(cancellationToken);
                if (statusError != "none") return Error(statusError);
                return GitProjection.Result(ToolStatus.Success, "none", policy, Summary(), w => GitProjection.Status(w, snapshots));
            }
            var change = snapshots.Require(input!.SnapshotId, input.Layer, input.Path);
            if (change.ExclusionReason is not null) return Error("git_source_unavailable");
            var error = await snapshots.ValidateAsync(cancellationToken);
            if (error != "none") return Error(error);
            if (Definition.Name == "git_diff")
            {
                var diff = GitDiffBuilder.Create(input.SnapshotId, change, cancellationToken);
                var result = GitProjection.Result(ToolStatus.Success, "none", policy, Summary(), w =>
                {
                    w.WriteStartObject(); w.WriteString("snapshot_id", input.SnapshotId); w.WriteString("path", input.Path); w.WriteString("layer", GitLimits.Name(input.Layer));
                    w.WritePropertyName("old"); GitProjection.Source(w, change.Old); w.WritePropertyName("new"); GitProjection.Source(w, change.New); w.WriteString("diff", diff.Text);
                    w.WriteStartArray("hunks"); foreach (var h in diff.Hunks) { w.WriteStartObject(); w.WriteNumber("old_start", h.OldStart); w.WriteNumber("old_count", h.OldCount); w.WriteNumber("new_start", h.NewStart); w.WriteNumber("new_count", h.NewCount); w.WriteEndObject(); }
                    w.WriteEndArray();
                    w.WritePropertyName("coverage"); GitProjection.Coverage(w, snapshots); w.WriteEndObject();
                });
                if (result.Status == ToolStatus.Success) snapshots.PendingDiff(change, diff);
                return result;
            }
            var source = input.Side == GitSide.Old ? change.Old : change.New;
            if (!source.Exists || source.Content is null) return Error("git_source_unavailable");
            var lines = WorkspaceTextLines.Split(source.Content, cancellationToken);
            int? start = lines.Count == 0 || input.Start > lines.Count ? null : input.Start;
            int? end = start is null ? null : Math.Min(input.End, lines.Count);
            var content = start is { } first ? source.Content[lines[first - 1].Start..lines[end!.Value - 1].End] : "";
            var range = GitProjection.Result(ToolStatus.Success, "none", policy, Summary(), w =>
            {
                w.WriteStartObject(); w.WriteString("snapshot_id", input.SnapshotId); w.WriteString("path", input.Path); w.WriteString("layer", GitLimits.Name(input.Layer)); w.WriteString("side", GitLimits.Name(input.Side));
                if (start is { } a) w.WriteNumber("start_line", a); else w.WriteNull("start_line");
                if (end is { } b) w.WriteNumber("end_line", b); else w.WriteNull("end_line");
                w.WriteNumber("total_lines", lines.Count); w.WriteString("content", content); w.WriteNumber("file_byte_count", source.ByteCount!.Value);
                w.WriteNumber("content_byte_count", Encoding.UTF8.GetByteCount(content)); w.WriteBoolean("has_utf8_bom", source.HasUtf8Bom!.Value); w.WriteBoolean("has_more", end is { } last && last < lines.Count); w.WriteEndObject();
            });
            if (range.Status == ToolStatus.Success && start is { } rangeStart) snapshots.PendingRange(change, input.Side, rangeStart, end!.Value);
            return range;
        }
        catch (GitFailure failure) { return Error(failure.Code); }
        catch (OperationCanceledException) { return Error("turn_cancelled"); }
        GitSummary Summary() => new(snapshots.Current!.SnapshotId, snapshots.Selection, "observed", snapshots.EligibleChangeCount);
    }
    public override string ToString() => "[GitTool]";
}

internal sealed class GitStatusTool(GitSnapshotCoordinator snapshots, ISensitiveDataPolicy policy, AgentExecutionPolicy? execution = null) : GitTool(snapshots, policy, execution)
{
    public override LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("git_status", "觀察目前 Scope 的原始 bytes 分層變更；審查期間只回綁定快照", """{"type":"object","properties":{},"additionalProperties":false}""");
}
internal sealed class GitDiffTool(GitSnapshotCoordinator snapshots, ISensitiveDataPolicy policy, AgentExecutionPolicy? execution = null) : GitTool(snapshots, policy, execution)
{
    public override LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("git_diff", "取得目前快照指定層與路徑的完整本機差異", """{"type":"object","properties":{"snapshot_id":{"type":"string"},"layer":{"type":"string","enum":["staged","unstaged","untracked"]},"path":{"type":"string","maxLength":4096}},"required":["snapshot_id","layer","path"],"additionalProperties":false}""");
}
internal sealed class ReadGitFileRangeTool(GitSnapshotCoordinator snapshots, ISensitiveDataPolicy policy, AgentExecutionPolicy? execution = null) : GitTool(snapshots, policy, execution)
{
    public override LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("read_git_file_range", "讀取綁定來源 old/new 的至多200行，不建立修改基準", """{"type":"object","properties":{"snapshot_id":{"type":"string"},"layer":{"type":"string","enum":["staged","unstaged","untracked"]},"path":{"type":"string","maxLength":4096},"side":{"type":"string","enum":["old","new"]},"start_line":{"type":"integer","minimum":1},"end_line":{"type":"integer","minimum":1}},"required":["snapshot_id","layer","path","side","start_line","end_line"],"additionalProperties":false}""");
}

internal sealed class GitToolInput(string id, string path, GitLayer layer, GitSide side = GitSide.New, int start = 1, int end = 1)
{
    internal string SnapshotId { get; } = id; internal string Path { get; } = path; internal GitLayer Layer { get; } = layer;
    internal GitSide Side { get; } = side; internal int Start { get; } = start; internal int End { get; } = end;
    public override string ToString() => "[GitToolInput]";
}
internal static class GitArguments
{
    internal static bool Exact(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal));
    internal static bool Parse(string name, JsonElement value, out GitToolInput? input)
    {
        input = null;
        if (name == "git_status") return Exact(value);
        var range = name == "read_git_file_range";
        if (!Exact(value, range ? ["snapshot_id", "path", "layer", "side", "start_line", "end_line"] : ["snapshot_id", "path", "layer"])) return false;
        string? Text(string key) => value.GetProperty(key).ValueKind == JsonValueKind.String ? value.GetProperty(key).GetString() : null;
        var id = Text("snapshot_id"); var path = Text("path"); var layer = Text("layer");
        if (id is null || id.Length < 2 || id[0] != 'g' || !long.TryParse(id.AsSpan(1), out var sequence) || sequence <= 0 || id != "g" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            !WorkspacePathRules.IsCanonical(path) || WorkspacePathRules.IsSensitive(path!) || layer is not ("staged" or "unstaged" or "untracked")) return false;
        var parsedLayer = layer == "staged" ? GitLayer.Staged : layer == "unstaged" ? GitLayer.Unstaged : GitLayer.Untracked;
        if (!range) { input = new(id, path!, parsedLayer); return true; }
        var side = Text("side");
        if (side is not ("old" or "new") || value.GetProperty("start_line").ValueKind != JsonValueKind.Number || value.GetProperty("end_line").ValueKind != JsonValueKind.Number || !value.GetProperty("start_line").TryGetInt32(out var start) || !value.GetProperty("end_line").TryGetInt32(out var end) || start < 1 || end < start || (long)end - start + 1 > GitLimits.MaxRangeLines) return false;
        input = new(id, path!, parsedLayer, side == "old" ? GitSide.Old : GitSide.New, start, end); return true;
    }
}