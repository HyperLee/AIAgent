using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BootstrapTests
{
    [TestMethod]
    public async Task AC001_ReadyUsesOrderedServicesAndReleasesSession()
    {
        var fake = new BootstrapFake();
        using var result = await fake.Create().StartAsync(["--workspace", "relative"], default);
        Assert.IsTrue(result.Status == BootstrapStatus.Ready && result.ExitCode == 0 && result.Session is not null, "AC-001");
        Assert.IsTrue(string.Join(',', fake.Calls) == "settings,terminal,resolve,confirm,revalidate,key", "AC-001");
        Assert.IsTrue(fake.Options == new BootstrapOptions("/startup", "relative"), "AC-003");
        var secrets = result.Session!.Secrets;
        result.Dispose();
        Assert.IsNull(result.Session, "AC-010");
        try { _ = secrets.AuthenticationKey.Length; Assert.Fail("AC-010"); } catch (ObjectDisposedException) { }
    }

    [TestMethod]
    public async Task AC002_HelpAndArgumentsDoNotReadServices()
    {
        var fake = new BootstrapFake();
        using var help = await fake.Create().StartAsync(["--help"], default);
        Assert.IsTrue(help.Status == BootstrapStatus.Help && help.ExitCode == 0 && fake.Calls.Count == 0, "AC-002");
        using var error = await fake.Create().StartAsync(["--key", "FAKE_ARGUMENT_ONLY"], default);
        Assert.IsTrue(error.ErrorCode == BootstrapErrorCode.InvalidArguments && error.ExitCode == 2 && fake.Calls.Count == 0, "AC-002");
    }

    [TestMethod]
    public async Task AC006_RefusalEofCancellationAndVanishedDirectoryNeverReadKey()
    {
        foreach (var mode in new[] { "declined", "eof", "cancelled", "vanished", "terminal", "settings" })
        {
            var fake = new BootstrapFake { Mode = mode };
            using var result = await fake.Create().StartAsync([], default);
            var status = mode switch { "declined" => BootstrapStatus.Declined, "eof" => BootstrapStatus.EndOfInput, "cancelled" => BootstrapStatus.Cancelled, _ => BootstrapStatus.Failed };
            var exit = mode switch { "cancelled" => 130, "settings" => 2, "vanished" or "terminal" => 1, _ => 0 };
            Assert.IsTrue(result.Status == status && result.ExitCode == exit && result.Session is null && !fake.Calls.Contains("key"), "AC-006");
            if (mode == "terminal") Assert.IsFalse(fake.Calls.Contains("confirm"), "AC-009");
        }
    }

    [TestMethod]
    public async Task AC009_KeyStatusesAndLateCancellation()
    {
        foreach (var status in new[] { KeyInputStatus.Cancelled, KeyInputStatus.EndOfInput, KeyInputStatus.Unavailable })
        {
            var fake = new BootstrapFake { KeyStatus = status };
            using var result = await fake.Create().StartAsync([], default);
            Assert.IsTrue(result.Session is null && result.ExitCode == (status == KeyInputStatus.Cancelled ? 130 : status == KeyInputStatus.Unavailable ? 1 : 0), "AC-009");
        }
        using var cancellation = new CancellationTokenSource();
        var late = new BootstrapFake { OnKey = cancellation.Cancel };
        using var cancelled = await late.Create().StartAsync([], cancellation.Token);
        Assert.IsTrue(cancelled.Status == BootstrapStatus.Cancelled && cancelled.Session is null, "AC-009");
        try { late.LastKey!.TakeSecrets(); Assert.Fail("AC-010"); } catch (InvalidOperationException) { }
    }
}

internal sealed class BootstrapFake : IPublicSettingsLoader, IWorkspaceResolver, ITerminalCapability, IBootstrapInteraction
{
    internal List<string> Calls { get; } = [];
    internal string Mode { get; set; } = "";
    internal KeyInputStatus KeyStatus { get; set; } = KeyInputStatus.Accepted;
    internal BootstrapOptions? Options { get; private set; }
    internal KeyInputResult? LastKey { get; private set; }
    internal Action? OnKey { get; set; }
    internal Bootstrapper Create() => new(this, this, this, this, () => "/startup");
    public PublicSettings Load() { Calls.Add("settings"); if (Mode == "settings") throw new BootstrapFailureException(BootstrapErrorCode.InvalidSettings); return new(null); }
    public ConfirmedWorkspace Resolve(BootstrapOptions options) { Calls.Add("resolve"); Options = options; return new("/actual"); }
    public void Revalidate(ConfirmedWorkspace workspace) { Calls.Add("revalidate"); if (Mode == "vanished") throw new BootstrapFailureException(BootstrapErrorCode.WorkspaceUnavailable); }
    public void EnsureAvailable() { Calls.Add("terminal"); if (Mode == "terminal") throw new BootstrapFailureException(BootstrapErrorCode.TerminalUnavailable); }
    public ValueTask<bool> ConfirmWorkspaceAsync(ConfirmedWorkspace workspace, PublicSettings settings, CancellationToken cancellationToken)
    {
        Calls.Add("confirm");
        if (Mode == "eof") throw new BootstrapEndOfInputException();
        if (Mode == "cancelled") throw new OperationCanceledException();
        return ValueTask.FromResult(Mode != "declined");
    }
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken)
    {
        Calls.Add("key"); OnKey?.Invoke();
        LastKey = KeyStatus == KeyInputStatus.Accepted ? KeyInputResult.Accept(SecurityTests.FakeKey) : KeyInputResult.End(KeyStatus);
        return ValueTask.FromResult(LastKey);
    }
    public void ShowStatus(string safeMessage) { }
}
