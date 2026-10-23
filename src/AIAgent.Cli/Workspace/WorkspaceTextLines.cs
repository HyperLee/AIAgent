namespace AIAgent.Cli.Workspace;

internal static class WorkspaceTextLines
{
    internal readonly record struct Line(int Start, int ContentEnd, int End);

    internal static IReadOnlyList<Line> Split(string content, CancellationToken token)
    {
        var lines = new List<Line>(); var start = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if ((index & 127) == 0) token.ThrowIfCancellationRequested();
            if (content[index] is not ('\r' or '\n')) continue;
            var end = index;
            if (content[index] == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
            lines.Add(new(start, end, index + 1)); start = index + 1;
        }
        if (start < content.Length) lines.Add(new(start, content.Length, content.Length));
        return lines;
    }
}