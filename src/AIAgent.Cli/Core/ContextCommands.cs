using System.Globalization;

namespace AIAgent.Cli.Core;

internal enum ContextCommandKind { Invalid, Context, Show, Pin, Unpin, Compact, Budget, TurnLimit, ProgramLimit, Reset }
internal sealed class ContextCommand(ContextCommandKind kind, string? text = null, int number = 0)
{
    internal ContextCommandKind Kind { get; } = kind;
    internal string? Text { get; } = text;
    internal int Number { get; } = number;
    public override string ToString() => "[ContextCommand]";
}
internal static class ContextCommands
{
    internal static ContextCommand Parse(string raw)
    {
        var start = raw.TrimStart();
        if (start.StartsWith("/context pin ", StringComparison.Ordinal)) return new(ContextCommandKind.Pin, start[13..]);
        var text = raw.Trim();
        var simple = text switch
        {
            "/context" => ContextCommandKind.Context,
            "/context show" => ContextCommandKind.Show,
            "/compact" => ContextCommandKind.Compact,
            "/budget" => ContextCommandKind.Budget,
            "/budget reset" => ContextCommandKind.Reset,
            _ => ContextCommandKind.Invalid
        };
        if (simple != ContextCommandKind.Invalid) return new(simple);
        if (text.StartsWith("/context unpin n", StringComparison.Ordinal) && Positive(text[16..], long.MaxValue, out _))
            return new(ContextCommandKind.Unpin, text[15..]);
        foreach (var (prefix, kind, max) in new[] { ("/budget set turn ", ContextCommandKind.TurnLimit, 20), ("/budget set program ", ContextCommandKind.ProgramLimit, 1000) })
            if (text.StartsWith(prefix, StringComparison.Ordinal) && Positive(text[prefix.Length..], max, out var value)) return new(kind, number: (int)value);
        return new(ContextCommandKind.Invalid);
    }
    private static bool Positive(string text, long max, out long value)
    {
        value = 0;
        return text.Length > 0 && text[0] is >= '1' and <= '9' && text.All(c => c is >= '0' and <= '9') &&
            long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= max;
    }
}