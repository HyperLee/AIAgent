using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tools;

internal static class ToolResponseWriter
{
    internal static ToolExecutionResult Create(ToolStatus status, string code, ISensitiveDataPolicy policy)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("status", status switch
            {
                ToolStatus.Rejected => "rejected",
                ToolStatus.Cancelled => "cancelled",
                ToolStatus.Failed => "failed",
                ToolStatus.NotExecuted => "not_executed",
                _ => throw new ArgumentException("工具分類無效")
            });
            writer.WriteString("error_code", code);
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(output.ToArray());
        var value = document.RootElement;
        return new(status, code, output.Length > Workspace.WorkspaceLimits.MaxToolResultBytes ||
            SensitiveJson.Contains(value, policy, ContentBoundary.ToolResult) ? null : value);
    }
}