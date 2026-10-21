using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Security;

internal static class SensitiveJson
{
    internal static bool Contains(JsonElement value, ISensitiveDataPolicy policy, ContentBoundary boundary) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Any(property =>
            !policy.CanSendOrUse(property.Name, boundary) || Contains(property.Value, policy, boundary)),
        JsonValueKind.Array => value.EnumerateArray().Any(item => Contains(item, policy, boundary)),
        JsonValueKind.String => !policy.CanSendOrUse(value.GetString()!, boundary),
        _ => false
    };
}