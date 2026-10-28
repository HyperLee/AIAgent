namespace AIAgent.Cli.Core;

/// <summary>Pure admission of the original Learn article URL; never normalizes or performs I/O.</summary>
internal static class WebDocumentUrlPolicy
{
    internal static string Validate(string url, IMcpContentGuard guard, out string? requestedView)
    {
        requestedView = null;
        CheckSensitive(url, guard);
        if (url.Length is < 1 or > 1024 || url.Any(c => !char.IsAscii(c) || char.IsWhiteSpace(c) || char.IsControl(c) || c is '\\' or '%' or '#') ||
            !url.StartsWith("https://learn.microsoft.com/", StringComparison.Ordinal)) return "web_url_rejected";
        var remainder = url["https://learn.microsoft.com/".Length..];
        var queryAt = remainder.IndexOf('?');
        var path = queryAt < 0 ? remainder : remainder[..queryAt];
        var segments = path.Split('/');
        foreach (var segment in segments) CheckSensitive(segment, guard);
        if (segments.Length < 3 || segments[0].Length != 5 || segments[0][2] != '-' ||
            segments[0].Where((_, i) => i != 2).Any(c => c is < 'a' or > 'z') ||
            segments.Skip(1).Any(s => s.Length == 0 || !char.IsAsciiLetterOrDigit(s[0]) || s.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '~' or '-')))) return "web_url_rejected";
        if (new[] { "api", "answers", "users", "search", "profile", "account" }.Contains(segments[1], StringComparer.OrdinalIgnoreCase)) return "web_url_rejected";
        if (queryAt < 0) return "none";
        var pairs = remainder[(queryAt + 1)..].Split('&');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            foreach (var part in parts) CheckSensitive(part, guard);
            if (parts.Length != 2 || !seen.Add(parts[0])) return "web_url_rejected";
            if (parts[0] == "view")
            {
                if (parts[1].Length is < 1 or > 64 || parts[1].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))) return "web_url_rejected";
                requestedView = parts[1];
            }
            else if (parts[0] != "preserve-view" || parts[1] != "true") return "web_url_rejected";
        }
        return seen.Contains("preserve-view") && requestedView is null ? "web_url_rejected" : "none";
    }
    private static void CheckSensitive(string value, IMcpContentGuard guard)
    { if (!guard.IsSafe(value)) throw new McpFailure("sensitive_arguments"); }
}