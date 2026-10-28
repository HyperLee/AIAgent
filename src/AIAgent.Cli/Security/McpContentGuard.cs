using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Security;

/// <summary>外部邊界只取得檢查能力，不取得政策或受保護值。</summary>
internal sealed class McpContentGuard(ISensitiveDataPolicy policy, IReadOnlyList<string> protectedInstructions) : IMcpContentGuard
{
    private readonly string[] _protected = protectedInstructions.Where(s => !string.IsNullOrEmpty(s)).ToArray();
    public bool IsSafe(string value) => policy.CanSendOrUse(value, ContentBoundary.ToolResult) &&
        !_protected.Any(p => value.Contains(p, StringComparison.Ordinal));
    public bool IsSafe(JsonElement value)
    {
        var combined = new StringBuilder(); var values = new StringBuilder(); var names = new StringBuilder();
        bool Visit(JsonElement current)
        {
            switch (current.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in current.EnumerateObject())
                    {
                        if (!IsSafe(p.Name)) return false;
                        combined.Append(p.Name); names.Append(p.Name);
                        if (!Visit(p.Value)) return false;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in current.EnumerateArray()) if (!Visit(item)) return false;
                    break;
                default:
                    var text = current.ValueKind == JsonValueKind.String ? current.GetString()! : current.GetRawText();
                    if (!IsSafe(text)) return false;
                    combined.Append(text); values.Append(text); break;
            }
            return true;
        }
        return IsSafe(value.GetRawText()) && Visit(value) && IsSafe(combined.ToString()) && IsSafe(values.ToString()) && IsSafe(names.ToString());
    }
    public override string ToString() => "[McpContentGuard]";
}