namespace AIAgent.Cli.Cli;

internal sealed class SystemTerminal : ITerminal
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public IDisposable BeginInput() => new InputScope();

    public ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Console.KeyAvailable) return Console.ReadKey(intercept: true);
            // No background worker: keep external cancellation responsive while waiting for a key.
            cancellationToken.WaitHandle.WaitOne(20);
        }
    }

    public void Write(string text) => Console.Write(text);

    public void DiscardBufferedInput()
    {
        while (Console.KeyAvailable) _ = Console.ReadKey(intercept: true);
    }

    private sealed class InputScope : IDisposable
    {
        private readonly bool original;
        private bool disposed;
        internal InputScope()
        {
            original = Console.TreatControlCAsInput;
            try
            {
                Console.TreatControlCAsInput = true;
                _ = Console.KeyAvailable;
            }
            catch
            {
                Console.TreatControlCAsInput = original;
                throw;
            }
        }
        public void Dispose()
        {
            if (!disposed) { disposed = true; Console.TreatControlCAsInput = original; }
        }
    }
}