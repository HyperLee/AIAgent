namespace AIAgent.Cli.Cli;

internal sealed class ConsoleInterruptRouter : IDisposable
{
    private Action _target;
    private readonly ConsoleCancelEventHandler _handler;
    internal ConsoleInterruptRouter(Action initialTarget)
    {
        _target = initialTarget;
        _handler = (_, e) => { e.Cancel = true; Volatile.Read(ref _target)(); };
        Console.CancelKeyPress += _handler;
    }
    internal void RouteTo(Action target) => Interlocked.Exchange(ref _target, target);
    public void Dispose() => Console.CancelKeyPress -= _handler;
}