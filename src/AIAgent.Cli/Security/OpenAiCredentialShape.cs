using System.Text.RegularExpressions;

namespace AIAgent.Cli.Security;

// Product heuristic, not an authentication-format or credential-validity claim.
internal static class OpenAiCredentialShape
{
    private static readonly Regex Shape = new("s" + "k-[A-Za-z0-9_-]{16,}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    internal static bool Contains(string value) => Shape.IsMatch(value);
    internal static string Redact(string value) => Shape.Replace(value, "[REDACTED]");
}