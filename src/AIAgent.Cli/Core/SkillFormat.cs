namespace AIAgent.Cli.Core;

internal static class SkillFormat
{
    internal static bool ValidId(string? id)
    {
        if (id is not { Length: >= 1 and <= 64 } || id[0] is < 'a' or > 'z' || id[^1] == '-') return false;
        for (var i = 0; i < id.Length; i++)
            if (!(id[i] is >= 'a' and <= 'z' or >= '0' and <= '9' || id[i] == '-' && i > 0 && id[i - 1] != '-')) return false;
        return true;
    }

    internal static bool TryParse(string id, string text, out string description, out string body)
    {
        description = ""; body = "";
        if (!ValidId(id)) return false;
        for (var i = 0; i < text.Length; i++)
            if (text[i] == '\r' && (i + 1 == text.Length || text[i + 1] != '\n') || char.IsControl(text[i]) && text[i] is not ('\r' or '\n' or '\t')) return false;
        var lines = new string[4]; var start = 0;
        for (var i = 0; i < 4; i++)
        {
            var end = text.IndexOf('\n', start); if (end < 0) return false;
            lines[i] = text[start..(end > start && text[end - 1] == '\r' ? end - 1 : end)]; start = end + 1;
        }
        if (lines[0] != "---" || lines[3] != "---") return false;
        string? name = null;
        foreach (var line in lines.Skip(1).Take(2))
        {
            var split = line.IndexOf(": ", StringComparison.Ordinal); if (split < 0) return false;
            var key = line[..split]; var value = line[(split + 2)..];
            if (string.IsNullOrWhiteSpace(value) || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]) || value.Contains('\t')) return false;
            if (key == "name" && name is null) name = value;
            else if (key == "description" && description.Length == 0 && value.Length <= 512 && !"\"'|>&*!{[".Contains(value[0])) description = value;
            else return false;
        }
        if (name != id || description.Length == 0 || string.IsNullOrWhiteSpace(text[start..])) return false;
        body = text[start..]; return true;
    }
}