using System.Globalization;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal enum CheckpointCommandKind { Invalid, List, Show, Restore, Forget }

internal sealed class CheckpointCommand(CheckpointCommandKind kind, long checkpointId = 0, string errorCode = "invalid_checkpoint_command")
{
    internal CheckpointCommandKind Kind { get; } = kind;
    internal long CheckpointId { get; } = checkpointId;
    internal string ErrorCode { get; } = kind == CheckpointCommandKind.Invalid ? errorCode : "none";
    public override string ToString() => "[CheckpointCommand]";
}

internal static class CheckpointCommands
{
    internal const string RestorePrompt = CheckpointLimits.RestorePrompt;
    internal const string ForgetPrompt = CheckpointLimits.ForgetPrompt;

    internal static CheckpointCommand Parse(string raw)
    {
        if (!ChatInputValidation.IsSingleLineCommand(raw)) return new(CheckpointCommandKind.Invalid);
        var text = raw.Trim();
        if (text == "/checkpoint list") return new(CheckpointCommandKind.List);
        foreach (var (prefix, kind) in new[] { ("/checkpoint show ", CheckpointCommandKind.Show),
            ("/checkpoint restore ", CheckpointCommandKind.Restore), ("/checkpoint forget ", CheckpointCommandKind.Forget) })
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var id = text[prefix.Length..];
            if (id.Any(char.IsWhiteSpace)) return new(CheckpointCommandKind.Invalid);
            if (id.Length < 2 || id[0] != 'c' || id[1] is < '1' or > '9' || id.AsSpan(1).ContainsAnyExceptInRange('0', '9'))
                return new(CheckpointCommandKind.Invalid, errorCode: "invalid_checkpoint_id");
            if (!long.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return new(CheckpointCommandKind.Invalid, errorCode: "checkpoint_unavailable");
            return new(kind, number);
        }
        return new(CheckpointCommandKind.Invalid);
    }
}