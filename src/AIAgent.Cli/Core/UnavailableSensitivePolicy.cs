using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

internal sealed class UnavailableSensitivePolicy : ISensitiveDataPolicy
{
    internal static UnavailableSensitivePolicy Instance { get; } = new();
    private UnavailableSensitivePolicy() { }
    public bool ContainsCurrentKey(string content) => true;
    public bool CanSendOrUse(string content, ContentBoundary boundary) => false;
    public string RedactForDisplay(string content) => "";
}