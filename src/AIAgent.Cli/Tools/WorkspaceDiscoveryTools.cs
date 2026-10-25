using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tools;

internal sealed class SearchFilesTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public IProjectRulesWorkspace? ProjectRulesWorkspace => workspace.ProjectRulesWorkspace;
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("search_files",
        "遞迴搜尋指定工作區相對目錄中的安全檔名或路徑，使用有限 glob 樣式",
        """{"type":"object","properties":{"path":{"type":"string"},"pattern":{"type":"string"}},"required":["path","pattern"],"additionalProperties":false}""");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!ToolArguments.TryStrings(arguments, ["path", "pattern"], out var values))
            return ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy);
        var result = await workspace.SearchFilesAsync(values[0], values[1], cancellationToken);
        if (!WorkspaceResponseWriter.TryCreateResponse(result, policy, out var response, out var error, cancellationToken))
            return ToolResponseWriter.Create(ToolStatus.Rejected, WorkspaceToolProjection.Code(error), policy);
        return new(WorkspaceToolProjection.Status(result.Status), WorkspaceToolProjection.Code(result.ErrorCode), response,
            result.Data?.Path, truncated: result.Data?.Truncated ?? false, discoverySummary: result.Data is { } data ?
                new(DiscoveryKind.FileSearch, data.Matches.Count) : null);
    }
    public override string ToString() => "[SearchFilesTool]";
}

internal sealed class SearchTextTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public IProjectRulesWorkspace? ProjectRulesWorkspace => workspace.ProjectRulesWorkspace;
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("search_text",
        "在小型 UTF-8 文字檔中搜尋單行字面文字，回傳行號及附近內容",
        """{"type":"object","properties":{"path":{"type":"string"},"query":{"type":"string"},"file_pattern":{"type":"string"},"case_sensitive":{"type":"boolean"},"context_lines":{"type":"integer"}},"required":["path","query"],"additionalProperties":false}""");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!DiscoveryToolArguments.Text(arguments, out var path, out var query, out var pattern, out var sensitive, out var context))
            return ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy);
        var result = await workspace.SearchTextAsync(path, query, pattern, sensitive, context, cancellationToken);
        if (!WorkspaceResponseWriter.TryCreateResponse(result, policy, out var response, out var error, cancellationToken))
            return ToolResponseWriter.Create(ToolStatus.Rejected, WorkspaceToolProjection.Code(error), policy);
        return new(WorkspaceToolProjection.Status(result.Status), WorkspaceToolProjection.Code(result.ErrorCode), response,
            result.Data?.Path, truncated: result.Data?.Truncated ?? false, discoverySummary: result.Data is { } data ?
                new(DiscoveryKind.TextSearch, data.Matches.Count) : null);
    }
    public override string ToString() => "[SearchTextTool]";
}

internal sealed class ReadFileRangeTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public IProjectRulesWorkspace? ProjectRulesWorkspace => workspace.ProjectRulesWorkspace;
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = DiscoveryToolArguments.Definition("read_file_range",
        "取得小型 UTF-8 文字檔的指定行範圍，不建立修改快照",
        """{"type":"object","properties":{"path":{"type":"string"},"start_line":{"type":"integer"},"end_line":{"type":"integer"}},"required":["path","start_line","end_line"],"additionalProperties":false}""");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!DiscoveryToolArguments.Range(arguments, out var path, out var start, out var end))
            return ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy);
        var result = await workspace.ReadFileRangeAsync(path, start, end, cancellationToken);
        if (!WorkspaceResponseWriter.TryCreateResponse(result, policy, out var response, out var error, cancellationToken))
            return ToolResponseWriter.Create(ToolStatus.Rejected, WorkspaceToolProjection.Code(error), policy);
        return new(WorkspaceToolProjection.Status(result.Status), WorkspaceToolProjection.Code(result.ErrorCode), response,
            result.Data?.Path, discoverySummary: result.Data is { } data ? new(DiscoveryKind.LineRange,
                data.StartLine is { } first ? data.EndLine!.Value - first + 1 : 0,
                data.StartLine, data.EndLine, data.TotalLines, data.ContentByteCount) : null);
    }
    public override string ToString() => "[ReadFileRangeTool]";
}

internal static class DiscoveryToolArguments
{
    internal static LlmToolDefinition Definition(string name, string description, string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return new(name, description, document.RootElement);
    }
    private static bool Fields(JsonElement arguments, string[] allowed, out Dictionary<string, JsonElement> fields)
    {
        fields = new(StringComparer.Ordinal);
        if (arguments.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in arguments.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !fields.TryAdd(property.Name, property.Value)) return false;
        return true;
    }
    private static bool String(Dictionary<string, JsonElement> fields, string name, out string value)
    {
        value = "";
        if (!fields.TryGetValue(name, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString()!; return true;
    }
    private static bool Integer(Dictionary<string, JsonElement> fields, string name, out int value)
    {
        value = 0;
        return fields.TryGetValue(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }
    internal static bool Text(JsonElement arguments, out string path, out string query, out string pattern, out bool sensitive, out int context)
    {
        path = query = ""; pattern = "**/*"; sensitive = true; context = WorkspaceLimits.DefaultSearchContextLines;
        if (!Fields(arguments, ["path", "query", "file_pattern", "case_sensitive", "context_lines"], out var fields) ||
            !String(fields, "path", out path) || !String(fields, "query", out query)) return false;
        if (fields.ContainsKey("file_pattern") && !String(fields, "file_pattern", out pattern)) return false;
        if (fields.TryGetValue("case_sensitive", out var boolean))
        {
            if (boolean.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
            sensitive = boolean.GetBoolean();
        }
        return !fields.ContainsKey("context_lines") || Integer(fields, "context_lines", out context) && context is >= 0 and <= WorkspaceLimits.MaxSearchContextLines;
    }
    internal static bool Range(JsonElement arguments, out string path, out int start, out int end)
    {
        path = ""; start = end = 0;
        return Fields(arguments, ["path", "start_line", "end_line"], out var fields) && String(fields, "path", out path) &&
            Integer(fields, "start_line", out start) && Integer(fields, "end_line", out end);
    }
}