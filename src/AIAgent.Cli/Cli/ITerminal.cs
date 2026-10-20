namespace AIAgent.Cli.Cli;

internal interface ITerminal
{
    bool IsInteractive { get; }
    IDisposable BeginInput();
    ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken);
    void DiscardBufferedInput();
    void Write(string text);
}