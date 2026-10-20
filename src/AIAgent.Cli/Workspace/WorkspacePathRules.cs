namespace AIAgent.Cli.Workspace;

internal static class WorkspacePathRules
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".agents", ".codex", ".aws", ".ssh", ".secrets", "secrets",
        "gemini-api-key.txt", "api-key.txt", "appsettings.Local.json", "credentials", "credentials.json",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519", "identity"
    };
    private static readonly string[] SensitiveSuffixes = [".secrets.json", ".pem", ".key", ".p12", ".pfx", ".ppk", ".keystore", ".jks"];

    internal static bool TryNormalize(string? path, out string normalized, out bool directoryRequired)
    {
        normalized = "";
        directoryRequired = false;
        if (string.IsNullOrEmpty(path) || path.Length > WorkspaceLimits.MaxRelativePathCodeUnits || path[0] == '/' ||
            path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':') return false;
        for (var index = 0; index < path.Length; index++)
        {
            var character = path[index];
            if (character == '\\' || char.IsControl(character) || character is '\u2028' or '\u2029') return false;
            if (char.IsHighSurrogate(character))
            {
                if (++index >= path.Length || !char.IsLowSurrogate(path[index])) return false;
            }
            else if (char.IsLowSurrogate(character)) return false;
        }
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Contains("..", StringComparer.Ordinal)) return false;
        normalized = string.Join('/', segments.Where(part => part != "."));
        if (normalized.Length == 0) normalized = ".";
        if (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':') return false;
        directoryRequired = path.EndsWith('/');
        return true;
    }

    internal static bool IsWithinRoot(string root, string candidate) =>
        string.Equals(root, candidate, StringComparison.Ordinal) || candidate.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal);

    internal static bool IsSensitive(string path) => path.Split('/').Any(segment =>
        SensitiveNames.Contains(segment) || segment.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
        segment.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
        SensitiveSuffixes.Any(suffix => segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));

    internal static bool IsCanonical(string? path) => TryNormalize(path, out var normalized, out var directory) && !directory && normalized == path;
}