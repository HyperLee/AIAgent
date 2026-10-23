using System.Text;

namespace AIAgent.Cli.Workspace;

/// <summary>有界、Ordinal 的 Unicode scalar glob；不使用正規式或回溯。</summary>
internal sealed class DiscoveryPattern
{
    private readonly string[] _segments;
    private DiscoveryPattern(string pattern) => _segments = pattern.Split('/');

    internal static bool TryParse(string? pattern, out DiscoveryPattern? parsed)
    {
        parsed = null;
        if (!ValidText(pattern, WorkspaceLimits.MaxSearchPatternCodeUnits, false) || pattern![0] == '/' ||
            pattern.Length >= 2 && char.IsAsciiLetter(pattern[0]) && pattern[1] == ':' || pattern.Contains('\\')) return false;
        foreach (var segment in pattern.Split('/'))
            if (segment is "" or "." or ".." || segment.Contains("**", StringComparison.Ordinal) && segment != "**") return false;
        parsed = new(pattern);
        return true;
    }

    internal static bool ValidQuery(string? query) => ValidText(query, WorkspaceLimits.MaxSearchQueryCodeUnits, true);

    private static bool ValidText(string? value, int maximum, bool allowTab)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximum) return false;
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (c is '\u2028' or '\u2029' || char.IsControl(c) && !(allowTab && c == '\t')) return false;
            if (char.IsHighSurrogate(c))
            { if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false; }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }

    internal bool Matches(string relative, CancellationToken token)
    {
        var parts = relative.Split('/');
        if (_segments.Length == 1) return _segments[0] == "**" || SegmentMatches(_segments[0], parts[^1], token);
        var previous = new bool[parts.Length + 1]; previous[0] = true;
        foreach (var segment in _segments)
        {
            token.ThrowIfCancellationRequested();
            var next = new bool[parts.Length + 1];
            if (segment == "**")
            {
                next[0] = previous[0];
                for (var index = 1; index <= parts.Length; index++) next[index] = previous[index] || next[index - 1];
            }
            else
                for (var index = 1; index <= parts.Length; index++)
                    next[index] = previous[index - 1] && SegmentMatches(segment, parts[index - 1], token);
            previous = next;
        }
        return previous[^1];
    }

    private static bool SegmentMatches(string pattern, string name, CancellationToken token)
    {
        var text = name.EnumerateRunes().ToArray();
        var previous = new bool[text.Length + 1]; previous[0] = true;
        foreach (var rune in pattern.EnumerateRunes())
        {
            token.ThrowIfCancellationRequested();
            var next = new bool[text.Length + 1];
            if (rune.Value == '*') next[0] = previous[0];
            for (var index = 1; index <= text.Length; index++)
            {
                if ((index & 127) == 0) token.ThrowIfCancellationRequested();
                next[index] = rune.Value == '*' ? previous[index] || next[index - 1] :
                    previous[index - 1] && (rune.Value == '?' || rune == text[index - 1]);
            }
            previous = next;
        }
        return previous[^1];
    }
    public override string ToString() => "[DiscoveryPattern]";
}