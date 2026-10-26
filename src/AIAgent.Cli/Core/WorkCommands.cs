namespace AIAgent.Cli.Core;

internal enum WorkCommandKind { Invalid, Save, List, Show, Delete }

internal sealed class WorkCommand(WorkCommandKind kind, string? value = null, string errorCode = "invalid_work_command")
{
    internal WorkCommandKind Kind { get; } = kind;
    internal string? Value { get; } = value;
    internal string ErrorCode { get; } = errorCode;
    public override string ToString() => "[WorkCommand]";
}

internal static class WorkCommands
{
    internal static bool ValidId(string? id) => id is { Length: 32 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static WorkCommand Parse(string raw)
    {
        if (raw.Any(c => c is '\r' or '\n')) return new(WorkCommandKind.Invalid);
        var leading = raw.TrimStart();
        const string save = "/work save ";
        if (leading.StartsWith(save, StringComparison.Ordinal))
        {
            var title = leading[save.Length..];
            return title.Length is >= 1 and <= 80 && !string.IsNullOrWhiteSpace(title) && PlanArguments.ValidUnicode(title)
                ? new(WorkCommandKind.Save, title, "none") : new(WorkCommandKind.Invalid, errorCode: "invalid_work_title");
        }
        var text = raw.Trim();
        if (text == "/work list") return new(WorkCommandKind.List, errorCode: "none");
        foreach (var (prefix, kind) in new[] { ("/work show ", WorkCommandKind.Show), ("/work delete ", WorkCommandKind.Delete) })
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                var id = text[prefix.Length..];
                return ValidId(id) ? new(kind, id, "none") : new(WorkCommandKind.Invalid, errorCode: "invalid_work_id");
            }
        return new(WorkCommandKind.Invalid);
    }
}