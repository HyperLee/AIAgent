namespace AIAgent.Cli.Core;

internal enum SkillsCommandKind { Invalid, Status, List, Show, Use, Reload, Off }
internal sealed class SkillsCommand(SkillsCommandKind kind, string? id = null)
{
    internal SkillsCommandKind Kind { get; } = kind;
    internal string? Id { get; } = id;
    public override string ToString() => "[SkillsCommand]";
}
internal static class SkillsCommands
{
    internal static SkillsCommand Parse(string raw)
    {
        var command = raw.Trim();
        if (command.Contains('\n') || command.Contains('\r')) return new(SkillsCommandKind.Invalid);
        if (command == "/skills") return new(SkillsCommandKind.Status);
        if (command == "/skills list") return new(SkillsCommandKind.List);
        if (command == "/skills reload") return new(SkillsCommandKind.Reload);
        if (command == "/skills off") return new(SkillsCommandKind.Off);
        foreach (var (prefix, kind) in new[] { ("/skills show ", SkillsCommandKind.Show), ("/skills use ", SkillsCommandKind.Use) })
            if (command.StartsWith(prefix, StringComparison.Ordinal) && SkillFormat.ValidId(command[prefix.Length..])) return new(kind, command[prefix.Length..]);
        return new(SkillsCommandKind.Invalid);
    }
}