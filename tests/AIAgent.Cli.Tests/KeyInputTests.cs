using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class KeyInputTests
{
    [TestMethod]
    public void AC007_DeleteTrimRetryAndNoEcho()
    {
        var terminal = new FakeTerminal();
        terminal.Add(Key(ConsoleKey.Backspace), Key(ConsoleKey.Delete));
        terminal.Text("   "); terminal.Enter();
        terminal.Text("  FAKE_KEY_FOR_TESTS_ONLX"); terminal.Add(Key(ConsoleKey.Backspace));
        terminal.Text("YZ"); terminal.Add(Key(ConsoleKey.Delete)); terminal.Text("  "); terminal.Enter();
        using var result = new HiddenKeyReader(terminal).Read(default);
        using var secrets = result.TakeSecrets();
        Assert.IsTrue(result.Status == KeyInputStatus.Accepted && secrets.AuthenticationKey.SequenceEqual(SecurityTests.FakeKey), "AC-007");
        Assert.IsFalse(terminal.Output.Contains("FAKE_", StringComparison.Ordinal) || terminal.Output.Contains('*'), "AC-007");
        Assert.IsTrue(terminal.ScopeCount == 0, "AC-007");
        try { result.TakeSecrets(); Assert.Fail("AC-010"); } catch (InvalidOperationException) { }
    }

    [TestMethod]
    public void AC008_512Accepted513RejectedAndInvalidAttemptCannotBeRepaired()
    {
        var terminal = new FakeTerminal(); terminal.Text(new string('F', 512)); terminal.Enter();
        using (var result = new HiddenKeyReader(terminal).Read(default))
        using (var secrets = result.TakeSecrets()) Assert.IsTrue(secrets.AuthenticationKey.Length == 512, "AC-008");
        foreach (var invalid in new[] { new string('F', 513), "FAKE\tINPUT", "FAKE測試", "FAKE\u001bINPUT", "FAKE\0INPUT" })
        {
            terminal = new FakeTerminal(); terminal.Text(invalid); terminal.Add(Key(ConsoleKey.Backspace)); terminal.Enter();
            terminal.Text(SecurityTests.FakeKey); terminal.Add(Key(ConsoleKey.LeftArrow), Key(ConsoleKey.Home)); terminal.Enter();
            using var result = new HiddenKeyReader(terminal).Read(default);
            using var secrets = result.TakeSecrets();
            Assert.IsTrue(secrets.AuthenticationKey.SequenceEqual(SecurityTests.FakeKey) && terminal.Remaining == 0, "AC-008");
            Assert.IsFalse(terminal.Output.Contains("FAKE", StringComparison.Ordinal), "AC-008");
        }
    }

    [TestMethod]
    public void AC009_PartialKeyDiscardedForEveryTermination()
    {
        foreach (var status in new[] { KeyInputStatus.EndOfInput, KeyInputStatus.Cancelled, KeyInputStatus.Unavailable })
        {
            var terminal = new FakeTerminal(); terminal.Text("FAKE_PARTIAL_ONLY");
            if (status == KeyInputStatus.Cancelled) terminal.Add(new ConsoleKeyInfo('\u0003', ConsoleKey.C, false, false, true));
            if (status == KeyInputStatus.Unavailable) terminal.ReadError = new IOException("FAKE_IO_ONLY");
            using var result = new HiddenKeyReader(terminal).Read(default);
            Assert.IsTrue(result.Status == status && terminal.ScopeCount == 0, "AC-009");
            try { result.TakeSecrets(); Assert.Fail("AC-009"); } catch (InvalidOperationException) { }
        }
        var ctrlD = new FakeTerminal(); ctrlD.Text("FAKE_PARTIAL_ONLY"); ctrlD.Add(new ConsoleKeyInfo('\u0004', ConsoleKey.D, false, false, true));
        using var eof = new HiddenKeyReader(ctrlD).Read(default);
        Assert.IsTrue(eof.Status == KeyInputStatus.EndOfInput && ctrlD.ScopeCount == 0, "AC-009");
    }

    [TestMethod]
    public void AC009_UnavailableAndCancellationNeverFallback()
    {
        var terminal = new FakeTerminal { IsInteractive = false };
        using var result = new HiddenKeyReader(terminal).Read(default);
        Assert.IsTrue(result.Status == KeyInputStatus.Unavailable && terminal.ReadCount == 0, "AC-009");
        terminal = new FakeTerminal();
        using var cancelled = new HiddenKeyReader(terminal).Read(new CancellationToken(true));
        Assert.IsTrue(cancelled.Status == KeyInputStatus.Cancelled && terminal.ScopeCount == 0, "AC-009");
    }

    internal static ConsoleKeyInfo Key(ConsoleKey key) => new(key == ConsoleKey.Enter ? '\r' : '\0', key, false, false, false);
}

internal sealed class FakeTerminal : ITerminal
{
    private readonly Queue<ConsoleKeyInfo> keys = new();
    public bool IsInteractive { get; set; } = true;
    internal string Output { get; private set; } = "";
    internal int ScopeCount { get; private set; }
    internal int ReadCount { get; private set; }
    internal int Remaining => keys.Count;
    internal Exception? ReadError { get; set; }
    internal void Add(params ConsoleKeyInfo[] values) { foreach (var key in values) keys.Enqueue(key); }
    internal void Text(string text) { foreach (var c in text) Add(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)); }
    internal void Enter() => Add(KeyInputTests.Key(ConsoleKey.Enter));
    public IDisposable BeginInput() { ScopeCount++; return new Scope(() => ScopeCount--); }
    public ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); ReadCount++;
        if (keys.TryDequeue(out var key)) return key;
        if (ReadError is not null) throw ReadError;
        return null;
    }
    public void Write(string text) => Output += text;
    public void DiscardBufferedInput() { }
    private sealed class Scope(Action dispose) : IDisposable { public void Dispose() => dispose(); }
}