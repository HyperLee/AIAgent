using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed class ChatInputReader(ITerminal terminal)
{
    internal const int MaxInputCodeUnits = 16384;
    private bool _skipLineFeed;

    internal ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken)
    {
        while (true)
        {
            var key = terminal.ReadKey(cancellationToken);
            var skip = _skipLineFeed;
            _skipLineFeed = key?.KeyChar == '\r';
            if (skip && key?.KeyChar == '\n') continue;
            return key;
        }
    }

    internal ChatInputResult Read(CancellationToken cancellationToken)
    {
        var buffer = new char[MaxInputCodeUnits];
        var length = 0;
        var invalid = false;
        try
        {
            while (true)
            {
                var input = ReadKey(cancellationToken);
                if (input is null || HiddenKeyReader.IsControl(input.Value, ConsoleKey.D, '\u0004')) return ChatInputResult.End(ChatInputStatus.EndOfInput);
                var key = input.Value;
                if (HiddenKeyReader.IsControl(key, ConsoleKey.C, '\u0003')) return ChatInputResult.End(ChatInputStatus.Cancelled);
                var c = key.KeyChar;
                if (key.Key == ConsoleKey.Enter || c is '\r' or '\n')
                {
                    if (length > 0 && char.IsHighSurrogate(buffer[length - 1])) invalid = true;
                    return invalid ? ChatInputResult.End(ChatInputStatus.Invalid) : ChatInputResult.Submitted(new string(buffer, 0, length));
                }
                if (key.Key is ConsoleKey.Backspace or ConsoleKey.Delete || c is '\b' or '\u007f')
                {
                    if (length > 0)
                    {
                        var count = char.IsLowSurrogate(buffer[length - 1]) && length > 1 && char.IsHighSurrogate(buffer[length - 2]) ? 2 : 1;
                        while (count-- > 0) buffer[--length] = '\0';
                    }
                    continue;
                }
                if (HiddenKeyReader.IsNavigation(key.Key)) continue;
                var pendingHigh = length > 0 && char.IsHighSurrogate(buffer[length - 1]);
                if (char.IsControl(c) || c is '\u2028' or '\u2029' ||
                    pendingHigh != char.IsLowSurrogate(c) || length == buffer.Length)
                { invalid = true; continue; }
                buffer[length++] = c;
            }
        }
        finally { Array.Clear(buffer); }
    }
}