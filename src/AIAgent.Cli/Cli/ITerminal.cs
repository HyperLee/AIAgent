namespace AIAgent.Cli.Cli;

internal interface ITerminal
{
    bool IsInteractive { get; }
    IDisposable BeginInput();
    ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken);
    void DiscardBufferedInput();
    ConsoleKeyInfo? TryReadBufferedKey() => null;
    void Write(string text);
}