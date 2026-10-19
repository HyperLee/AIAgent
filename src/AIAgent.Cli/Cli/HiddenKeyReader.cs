using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed class HiddenKeyReader(ITerminal terminal)
{
    internal KeyInputResult Read(CancellationToken cancellationToken)
    {
        KeyInputResult? result = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!terminal.IsInteractive) return KeyInputResult.End(KeyInputStatus.Unavailable);
            using (terminal.BeginInput()) result = ReadCore(cancellationToken);
            return result;
        }
        catch (OperationCanceledException) { result?.Dispose(); return KeyInputResult.End(KeyInputStatus.Cancelled); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            result?.Dispose();
            return KeyInputResult.End(KeyInputStatus.Unavailable);
        }
    }

    private KeyInputResult ReadCore(CancellationToken cancellationToken)
    {
        var buffer = new char[512];
        try
        {
            while (true)
            {
                terminal.Write("請輸入 API Key（不回顯）：");
                var length = 0;
                var invalid = false;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var input = terminal.ReadKey(cancellationToken);
                    if (input is null || IsControl(input.Value, ConsoleKey.D, '\u0004')) return KeyInputResult.End(KeyInputStatus.EndOfInput);
                    var key = input.Value;
                    if (IsControl(key, ConsoleKey.C, '\u0003')) return KeyInputResult.End(KeyInputStatus.Cancelled);
                    if (key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n')
                    {
                        terminal.Write(Environment.NewLine);
                        var start = 0;
                        while (start < length && buffer[start] == ' ') start++;
                        while (length > start && buffer[length - 1] == ' ') length--;
                        if (!invalid && length > start) return KeyInputResult.Accept(buffer.AsSpan(start, length - start));
                        Array.Clear(buffer);
                        terminal.Write("輸入無效，請重新輸入。" + Environment.NewLine);
                        break;
                    }
                    if (key.Key is ConsoleKey.Backspace or ConsoleKey.Delete || key.KeyChar is '\b' or '\u007f')
                    {
                        if (length > 0) buffer[--length] = '\0';
                        continue;
                    }
                    if (IsNavigation(key.Key)) continue;
                    if (key.KeyChar is < ' ' or > '~' || length == buffer.Length) { invalid = true; continue; }
                    buffer[length++] = key.KeyChar;
                }
            }
        }
        finally { Array.Clear(buffer); }
    }

    internal static bool IsControl(ConsoleKeyInfo key, ConsoleKey letter, char control) =>
        key.KeyChar == control || (key.Key == letter && key.Modifiers.HasFlag(ConsoleModifiers.Control));

    internal static bool IsNavigation(ConsoleKey key) => key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or
        ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.Home or ConsoleKey.End or ConsoleKey.PageUp or ConsoleKey.PageDown or ConsoleKey.Insert;
}
