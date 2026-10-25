using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Git;

internal sealed class CodeReviewFinding(string severity, string title, string body, string path, GitLayer layer, GitSide side, int start, int end)
{
    internal string Severity { get; } = severity; internal string Title { get; } = title; internal string Body { get; } = body;
    internal string Path { get; } = path; internal GitLayer Layer { get; } = layer; internal GitSide Side { get; } = side;
    internal int StartLine { get; } = start; internal int EndLine { get; } = end;
    public override string ToString() => "[CodeReviewFinding]";
}

internal sealed class CodeReviewReport(string id, string scope, GitSelection selection, string summary,
    IEnumerable<CodeReviewFinding> findings, GitValidationManifest manifest, IEnumerable<GitExclusion> exclusions,
    int eligible, int delivered, bool complete, IEnumerable<int> layerCounts, IEnumerable<GitReviewChange> changes)
{
    internal int Version => 1;
    internal string SnapshotId { get; } = id; internal string Scope { get; } = scope; internal GitSelection Selection { get; } = selection;
    internal string Summary { get; } = summary; internal GitValidationManifest Manifest { get; } = manifest;
    internal IReadOnlyList<CodeReviewFinding> Findings { get; } = Array.AsReadOnly(findings.ToArray());
    internal IReadOnlyList<GitExclusion> Exclusions { get; } = Array.AsReadOnly(exclusions.ToArray());
    internal IReadOnlyList<int> LayerCounts { get; } = Array.AsReadOnly(layerCounts.ToArray());
    internal IReadOnlyList<GitReviewChange> Changes { get; } = Array.AsReadOnly(changes.ToArray());
    internal int EligibleChangeCount { get; } = eligible; internal int DeliveredChangeCount { get; } = delivered;
    internal bool Complete { get; } = complete; internal DateTimeOffset ValidatedAt { get; } = DateTimeOffset.UtcNow;
    public override string ToString() => "[CodeReviewReport]";
}

internal sealed class GitReviewChange(string path, GitLayer layer, string kind)
{
    internal string Path { get; } = path; internal GitLayer Layer { get; } = layer; internal string Kind { get; } = kind;
    public override string ToString() => "[GitReviewChange]";
}

internal sealed class CodeReviewCoordinator(GitSnapshotCoordinator snapshots, ISensitiveDataPolicy policy)
{
    internal GitSnapshotCoordinator Snapshots => snapshots;
    internal CodeReviewReport? LastReport { get; private set; }
    internal CodeReviewReport? Candidate { get; private set; }
    internal string? LastAttemptError { get; private set; }
    internal bool LastReportStale { get; private set; }
    internal void BeginAttempt() { Candidate = null; LastAttemptError = null; snapshots.Clear(); }
    internal void Failed(string code) { Candidate = null; LastAttemptError = code; }
    internal void Invalidate(string? scope = null)
    {
        snapshots.Clear(); Candidate = null;
        if (scope is not null && LastReport?.Scope != scope) LastReport = null;
        else LastReportStale = true;
    }
    internal void Clear() { snapshots.Clear(); LastReport = null; Candidate = null; LastAttemptError = null; LastReportStale = false; }
    internal string Appendix() => GitProjection.Appendix(snapshots, policy);
    internal CodeReviewReport? Validate(string json)
    {
        Candidate = null;
        try
        {
            if (snapshots.Current is not { } snapshot || snapshots.Stale || !snapshots.Bound || Encoding.UTF8.GetByteCount(json) > GitLimits.MaxResultBytes || !policy.CanSendOrUse(json, ContentBoundary.ToolResult)) return null;
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 }); var root = document.RootElement;
            if (!GitArguments.Exact(root, "version", "snapshot_id", "selection", "summary", "findings") || SensitiveJson.Contains(root, policy, ContentBoundary.ToolResult) ||
                root.GetProperty("version").ValueKind != JsonValueKind.Number || !root.GetProperty("version").TryGetInt32(out var version) || version != 1 ||
                Text(root, "snapshot_id") != snapshot.SnapshotId || Text(root, "selection") != GitLimits.Name(snapshots.Selection) || !Safe(Text(root, "summary"), 2048)) return null;
            var array = root.GetProperty("findings"); if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > GitLimits.MaxFindings) return null;
            var findings = new List<CodeReviewFinding>(); var duplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in array.EnumerateArray())
            {
                if (!GitArguments.Exact(item, "severity", "title", "body", "path", "layer", "side", "start_line", "end_line")) return null;
                var severity = Text(item, "severity"); var title = Text(item, "title"); var body = Text(item, "body");
                var path = Text(item, "path"); var layerText = Text(item, "layer"); var sideText = Text(item, "side");
                if (severity is not ("P0" or "P1" or "P2" or "P3") || !Safe(title, 256) || !Safe(body, 2048) ||
                    layerText is not ("staged" or "unstaged" or "untracked") || sideText is not ("old" or "new") ||
                    !WorkspacePathRules.IsCanonical(path) || WorkspacePathRules.IsSensitive(path!) ||
                    item.GetProperty("start_line").ValueKind != JsonValueKind.Number || item.GetProperty("end_line").ValueKind != JsonValueKind.Number ||
                    !item.GetProperty("start_line").TryGetInt32(out var start) || !item.GetProperty("end_line").TryGetInt32(out var end) || start < 1 || end < start || (long)end - start + 1 > GitLimits.MaxFindingLines) return null;
                var layer = layerText == "staged" ? GitLayer.Staged : layerText == "unstaged" ? GitLayer.Unstaged : GitLayer.Untracked;
                var side = sideText == "old" ? GitSide.Old : GitSide.New;
                var change = snapshots.Require(snapshot.SnapshotId, layer, path!); var source = side == GitSide.Old ? change.Old : change.New;
                if (!change.ContentAvailable || !source.Exists || source.Content is null || end > source.TotalLines || !snapshots.Delivered(path!, layer, side, start, end)) return null;
                var diff = GitDiffBuilder.Create(snapshot.SnapshotId, change, default);
                var changed = side == GitSide.Old ? diff.OldChangedLines : diff.NewChangedLines;
                if (!changed.Any(line => line >= start && line <= end) || !duplicates.Add(JsonSerializer.Serialize(new[] { path!, layerText, sideText, start.ToString(System.Globalization.CultureInfo.InvariantCulture), end.ToString(System.Globalization.CultureInfo.InvariantCulture), title! }))) return null;
                findings.Add(new(severity, title!, body!, path!, layer, side, start, end));
            }
            var complete = snapshots.EligibleChangeCount == snapshots.DeliveredChangeCount && snapshot.EnumerationComplete && snapshots.Exclusions.Count == 0;
            var report = new CodeReviewReport(snapshot.SnapshotId, snapshot.Scope, snapshots.Selection, Text(root, "summary")!, findings, snapshot.Manifest, snapshots.Exclusions,
                snapshots.EligibleChangeCount, snapshots.DeliveredChangeCount, complete, Enum.GetValues<GitLayer>().Select(layer => snapshot.Changes.Count(c => c.Layer == layer)),
                snapshot.Changes.Where(c => GitLimits.Selected(snapshots.Selection, c.Layer)).Select(c => new GitReviewChange(c.Path, c.Layer, c.ChangeKind)));
            if (Display(report, false) is null) return null;
            Candidate = report; return report;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or GitFailure or OverflowException or ArgumentException) { return null; }
    }
    internal bool Publish(CodeReviewReport candidate)
    {
        if (!ReferenceEquals(candidate, Candidate) || snapshots.Stale || snapshots.Current?.SnapshotId != candidate.SnapshotId || !snapshots.Bound) return false;
        LastReport = candidate; LastReportStale = false; LastAttemptError = null; Candidate = null; return true;
    }
    internal async Task<string?> ShowAsync(string currentScope, CancellationToken token)
    {
        if (LastReport is not { } report) return "尚無成功審查報告。";
        if (report.Scope != currentScope) { Clear(); return "尚無目前 Scope 的成功審查報告。"; }
        var error = await snapshots.ValidateManifestAsync(report.Manifest, token);
        if (error is "process_unconfirmed" or "process_cleanup_failed" or "sensitive_content" or "workspace_unavailable") throw new GitFailure(error);
        LastReportStale |= error != "none";
        return Display(report, LastReportStale);
    }
    internal string? Display(CodeReviewReport report, bool stale)
    {
        var text = new StringBuilder("程式碼審查 v1；快照：").Append(report.SnapshotId).Append("；最後重驗：").Append(report.ValidatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture))
            .Append("\nScope：").Append(FileChangePreviewBuilder.EscapePath(report.Scope)).Append("；selection：").Append(GitLimits.Name(report.Selection))
            .Append(stale ? "；Stale（歷史結果）" : "；狀態限定最後重驗時點").Append("\n原始 bytes 視角；Scope 限定、安全過濾。\n")
            .Append("staged：").Append(report.LayerCounts[0]).Append("；unstaged：").Append(report.LayerCounts[1]).Append("；untracked：").Append(report.LayerCounts[2]).Append('\n')
            .Append("完整差異提供：").Append(report.DeliveredChangeCount).Append('/').Append(report.EligibleChangeCount).Append(report.Complete ? "；完整提供\n" : "；部分審查（review_incomplete）\n")
            .Append("未檢查未選層、其他 Scope、未變更依賴、被忽略及不支援內容；資料提供不保證逐行理解或找出全部缺陷。\n");
        foreach (var exclusion in report.Exclusions) text.Append("未覆蓋：").Append(exclusion.Path is null ? "受限範圍" : FileChangePreviewBuilder.EscapePath(exclusion.Path)).Append("；").Append(exclusion.Reason).Append('\n');
        foreach (var change in report.Changes) text.Append(GitLimits.Name(change.Layer)).Append(" old/new ").Append(FileChangePreviewBuilder.EscapePath(change.Path)).Append("；").Append(change.Kind).Append('\n');
        text.Append("模型摘要：").Append(FileChangePreviewBuilder.EscapePath(report.Summary)).Append('\n');
        if (report.Findings.Count == 0) text.Append("在已提供範圍未提出可確認問題。\n");
        foreach (var finding in report.Findings)
            text.Append(finding.Severity).Append(' ').Append(FileChangePreviewBuilder.EscapePath(finding.Title)).Append("\n  ").Append(FileChangePreviewBuilder.EscapePath(finding.Path))
                .Append(" / ").Append(GitLimits.Name(finding.Layer)).Append(" / ").Append(GitLimits.Name(finding.Side)).Append(" / ").Append(finding.StartLine).Append('-').Append(finding.EndLine)
                .Append("\n  ").Append(FileChangePreviewBuilder.EscapePath(finding.Body)).Append('\n');
        if (LastAttemptError is { } failure) text.Append("最近審查嘗試：").Append(failure).Append("；以上為先前成功報告。\n");
        text.Append("本回合未執行建置或測試。\n"); var result = text.ToString();
        return Encoding.UTF8.GetByteCount(result) <= GitLimits.MaxDisplayBytes && policy.CanSendOrUse(result, ContentBoundary.ToolResult) ? result : null;
    }
    private bool Safe(string? text, int max) => text is not null && text.Length <= max && !string.IsNullOrWhiteSpace(text) &&
        !text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) && ValidUnicode(text) && policy.CanSendOrUse(text, ContentBoundary.ToolResult);
    private static bool ValidUnicode(string text)
    {
        for (var i = 0; i < text.Length; i++)
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        return true;
    }
    private static string? Text(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.String ? value.GetProperty(name).GetString() : null;
    public override string ToString() => "[CodeReviewCoordinator]";
}