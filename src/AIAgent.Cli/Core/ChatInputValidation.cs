namespace AIAgent.Cli.Core;

internal static class ChatInputValidation
{
    internal const int MaxCodeUnits = 16384;
    internal static bool IsValid(string text)
    {
        if (text.Length > MaxCodeUnits) return false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if ((char.IsControl(c) && c is not ('\n' or '\t')) || c is '\u2028' or '\u2029') return false;
            if (char.IsHighSurrogate(c))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i])) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }
}