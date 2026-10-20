namespace AIAgent.Cli.Configuration;

internal static class ModelIdentifier
{
    internal static bool IsValid(string? value) => value is { Length: >= 1 and <= 128 } &&
        IsAlphaNumeric(value[0]) && value.All(c => IsAlphaNumeric(c) || c is '.' or '-' or '_');

    private static bool IsAlphaNumeric(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9';
}
