using System.Diagnostics;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.ProcessHost;

namespace AIAgent.Cli.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RvaNativeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RVAAC006_ProductBuildUsesRealSandboxAndPreservesSource(bool invalid)
    {
        await using var fixture = await RvaNativeFixture.CreateAsync(invalid ? "invalid" : "success");
        var result = await fixture.RunProductAsync(default);
        Assert.AreEqual(ProcessStartState.Started, result.Summary?.StartState, result.ErrorCode);
        Assert.IsTrue(result.Summary!.CleanupConfirmed);
        Assert.AreEqual(invalid ? ValidationOutcome.NotVerified : ValidationOutcome.BuildSucceeded, result.Summary.ValidationOutcome);
        Assert.AreEqual(invalid, result.Summary.ExitCode != 0);
        fixture.AssertSourcesUnchanged();
    }

    [TestMethod]
    [Timeout(170000)]
    public async Task RVAAC006_ProductEnforcesFull120SecondExecutionBudget()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("wait", 150000);
        var timer = Stopwatch.StartNew();
        var result = await fixture.RunProductAsync(default);
        Assert.IsTrue(timer.Elapsed >= TimeSpan.FromSeconds(118), "A short cancellation is not the full timeout case.");
        Assert.IsTrue(timer.Elapsed <= TimeSpan.FromSeconds(128), "Execution and cleanup must remain bounded.");
        Assert.AreEqual(ProcessStartState.Started, result.Summary?.StartState);
        Assert.AreEqual(ProcessEndState.TimedOut, result.Summary!.EndState);
        Assert.IsTrue(result.Summary.CleanupConfirmed);
        Assert.IsNull(result.Summary.ExitCode);
        Assert.AreEqual(ValidationOutcome.NotVerified, result.Summary.ValidationOutcome);
        Assert.IsFalse(fixture.SessionStopped);
        var budget = fixture.Observations.Single(value => value.Phase == ProcessObservationPhase.BudgetStarted);
        var sent = fixture.Observations.Single(value => value.Phase == ProcessObservationPhase.DescriptorSent);
        var target = fixture.Observations.Single(value => value.Phase == ProcessObservationPhase.TargetStarted);
        var cleanup = fixture.Observations.Single(value => value.Phase == ProcessObservationPhase.CleanupStarted);
        var finished = fixture.Observations.Single(value => value.Phase == ProcessObservationPhase.Finished);
        Assert.AreEqual(120000, budget.RemainingMilliseconds);
        Assert.IsTrue(budget.Timestamp <= sent.Timestamp && sent.Timestamp <= target.Timestamp);
        Assert.IsTrue(fixture.TargetAliveAtReady && target.Timestamp <= fixture.HelperReadyTimestamp);
        Assert.IsTrue(fixture.HelperReadyTimestamp < cleanup.Timestamp);
        Assert.IsTrue(Stopwatch.GetElapsedTime(budget.Timestamp, cleanup.Timestamp).TotalMilliseconds is >= 118000 and <= 122000);
        Assert.IsTrue(Math.Abs(sent.RemainingMilliseconds - (120000 - Stopwatch.GetElapsedTime(budget.Timestamp, sent.Timestamp).TotalMilliseconds)) <= 2000);
        Assert.IsTrue(Stopwatch.GetElapsedTime(sent.Timestamp, cleanup.Timestamp).TotalMilliseconds >= sent.RemainingMilliseconds - 2000);
        Assert.IsTrue(Stopwatch.GetElapsedTime(budget.Timestamp, fixture.LastTargetAliveTimestamp).TotalMilliseconds >= 118000);
        Assert.IsTrue(Stopwatch.GetElapsedTime(cleanup.Timestamp, finished.Timestamp) <= TimeSpan.FromSeconds(8));
        fixture.AssertSourcesUnchanged();
    }

    [TestMethod]
    public async Task RVAAC007_ObservedTermIgnoringDescendantsRequireKill()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("ignore-term");
        using var stop = new CancellationTokenSource();
        long triggered = 0;
        var result = await fixture.RunSupervisorAsync(stop.Token, known =>
        {
            if (known.Count >= 2 && fixture.HasGate("ready") && triggered == 0)
            {
                triggered = Stopwatch.GetTimestamp();
                stop.Cancel();
            }
        });
        Assert.IsTrue(triggered != 0, "Cancellation requires a real observed descendant and ready handler.");
        Assert.IsTrue(fixture.HasGate("term"), "Helper must have received TERM and remained alive.");
        Assert.IsTrue(Stopwatch.GetElapsedTime(triggered) >= TimeSpan.FromMilliseconds(1900));
        Assert.IsTrue(Stopwatch.GetElapsedTime(triggered) <= TimeSpan.FromSeconds(8));
        Assert.IsTrue(result.CleanupConfirmed);
        Assert.IsFalse(result.Exited);
    }

    [TestMethod]
    [DataRow("hold-pipe")]
    [DataRow("pipe-output")]
    public async Task RVAAC007_ObservedChildOutlivesRootAndIsReaped(string scenario)
    {
        await using var fixture = await RvaNativeFixture.CreateAsync(scenario);
        var observed = false;
        var result = await fixture.RunSupervisorAsync(default, known =>
        {
            if (known.Count >= 2 && fixture.HasGate("ready"))
            {
                observed = true;
                fixture.Gate("release-root");
            }
        });
        Assert.IsTrue(observed);
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(result.CleanupConfirmed);
        // Separate product entry proves real redirected pipes are bounded;
        // it does not claim the in-process observer runs in ProcessHost.
        await using var product = await RvaNativeFixture.CreateAsync(scenario);
        var operation = product.RunProductAsync(default);
        await product.WaitGateAsync("ready");
        product.Gate("release-root");
        var productResult = await operation;
        Assert.IsTrue(productResult.Summary!.CleanupConfirmed);
        Assert.AreEqual(ValidationOutcome.BuildSucceeded, productResult.Summary.ValidationOutcome);
    }

    [TestMethod]
    public async Task RVAAC008_ObservedSameIdentityChangingGroupCannotConfirmCleanup()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("group");
        var observed = false;
        var sameIdentityChangedGroup = false;
        var result = await fixture.RunSupervisorAsync(default, known =>
        {
            sameIdentityChangedGroup |= known.Any(previous => Native.Inspect(previous.Pid) is { } current &&
                previous.SameProcess(current) && previous.Group != current.Group);
            if (known.Count >= 2 && fixture.HasGate("ready"))
            {
                observed = true;
                fixture.Gate("change-group");
            }
        });
        Assert.IsTrue(observed);
        Assert.IsTrue(fixture.HasGate("group-changed"));
        Assert.IsTrue(sameIdentityChangedGroup);
        Assert.IsFalse(result.CleanupConfirmed);
    }

    [TestMethod]
    [Timeout(75000)]
    public async Task RVAAC008_Real255256257IdentitiesIncludeRootAndFailClosedAtLimit()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("count", 60000, allowGuardRescue: true);
        var saw255 = false;
        var saw256 = false;
        var max = 0;
        var result = await fixture.RunSupervisorAsync(default, known =>
        {
            max = Math.Max(max, known.Count);
            if (known.Count == 255) { saw255 = true; fixture.Gate("grow256"); }
            if (known.Count == 256) { saw256 = true; fixture.Gate("grow257"); }
        });
        if (fixture.HasGate("resource-blocked")) Assert.Inconclusive("RVA native identity resources unavailable; not a passed limit case.");
        Assert.IsTrue(saw255 && saw256 && fixture.HasGate("created257"));
        Assert.AreEqual(256, max);
        Assert.IsTrue(result.IdentityLimit);
        Assert.IsFalse(result.CleanupConfirmed);
    }

    [TestMethod]
    [DataRow("group-on-term")]
    [DataRow("burst")]
    public async Task RVAAC008_RealNativeUncertaintyExitsApplicationBeforeClearKeyOrModel(string scenario)
    {
        await using var fixture = await RvaNativeFixture.CreateAsync(scenario, scenario == "burst" ? 60000 : 30000, allowGuardRescue: scenario == "burst");
        Assert.AreEqual(1, await fixture.RunApplicationFaultAsync(scenario));
        if (scenario == "group-on-term")
        {
            Assert.IsTrue(fixture.HasGate("term"));
            Assert.IsTrue(fixture.HasGate("group-changed"));
        }
    }

    [TestMethod]
    [DataRow("control-invalid")]
    [DataRow("results")]
    [DataRow("control-eof")]
    [DataRow("wrapper-crash")]
    public async Task RVAAC009_RealHostPipeLossAndCrashNeverFabricateCleanup(string fault)
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("hold-pipe", allowGuardRescue: fault == "wrapper-crash");
        var outcome = await fixture.RunHostFaultAsync(fault);
        Assert.IsTrue(outcome.Started);
        Assert.IsFalse(outcome.CleanupConfirmed);
        Assert.IsTrue(outcome.ObservedDescendants >= 2);
        Assert.AreEqual(fault is "control-invalid" or "control-eof", outcome.ReceiptReceived);
        if (fault != "wrapper-crash")
        {
            Assert.IsTrue(outcome.IndependentlyGone, "A living host must clean owned targets before guard rescue.");
            Assert.IsTrue(fixture.HasGate("term"), "The observed descendant must actually receive host TERM.");
        }
    }

    [TestMethod]
    public async Task RVAAC009_ProductWrapperCrashLocksSessionAndNeverValidates()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("wait", allowGuardRescue: true);
        var operation = fixture.RunProductAsync(default);
        await fixture.WaitGateAsync("ready");
        fixture.KillOwnedWrapper();
        var result = await operation;
        Assert.AreEqual(ProcessEndState.Unconfirmed, result.Summary?.EndState);
        Assert.AreEqual(ValidationOutcome.NotVerified, result.Summary?.ValidationOutcome);
        Assert.IsNull(result.Summary?.ExitCode);
        Assert.IsTrue(fixture.SessionStopped);
    }

    [TestMethod]
    [DataRow("project")]
    [DataRow("bin")]
    [DataRow("obj")]
    [DataRow("ancestor")]
    public async Task RVAAC010_ApprovalCannotAuthorizeExchangedBytesOrOutputAncestors(string exchange)
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("success");
        var result = await fixture.RunProductAsync(default, () => fixture.ExchangeAtApproval(exchange));
        Assert.AreEqual(ProcessStartState.NotStarted, result.Summary?.StartState ?? ProcessStartState.NotStarted);
        Assert.IsFalse(fixture.WrapperStarted);
        Assert.IsFalse(fixture.HasGate("ready"));
        Assert.AreNotEqual(ValidationOutcome.BuildSucceeded, result.Summary?.ValidationOutcome);
        fixture.AssertSourceCanaryUnchanged();
    }

    [TestMethod]
    [DataRow("symlink")]
    [DataRow("hardlink")]
    [DataRow("source-alias")]
    [DataRow("case")]
    [DataRow("unicode")]
    public async Task RVAAC010_OutputAliasesCannotWriteSourceOrSyntheticSensitiveInodes(string alias)
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("success");
        fixture.CreateOutputAlias(alias);
        var result = await fixture.RunProductAsync(default);
        Assert.AreNotEqual(ValidationOutcome.BuildSucceeded, result.Summary?.ValidationOutcome);
        Assert.IsFalse(fixture.WrapperStarted);
        Assert.IsFalse(fixture.HasGate("ready"));
        fixture.AssertSourceCanaryUnchanged();
    }

    [TestMethod]
    public async Task RVAAC011_ProductSandboxAllowsOutputsAndDeniesSyntheticSourceNetworkAndIpc()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("policy");
        var result = await fixture.RunProductAsync(default);
        Assert.IsTrue(fixture.HasGate("policy-passed"), fixture.PolicyStage + "/" + result.ErrorCode);
        Assert.IsTrue(fixture.HasGate("policy-private-ipc"));
        Assert.IsTrue(fixture.HasGate("policy-private-collision"));
        Assert.IsTrue(fixture.HasGate("policy-dns-transport"));
        Assert.AreEqual(ValidationOutcome.BuildSucceeded, result.Summary?.ValidationOutcome);
        Assert.IsTrue(result.Summary!.CleanupConfirmed);
        fixture.AssertSourcesUnchanged();
    }

    [TestMethod]
    public async Task RVAAC010_ActiveSandboxDeniesAncestorExchangeAndNewInodeAliases()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("race");
        var operation = fixture.RunProductAsync(default);
        await fixture.WaitGateAsync("ready");
        fixture.Gate("race");
        var result = await operation;
        Assert.IsTrue(fixture.HasGate("race-passed"));
        Assert.AreEqual(ValidationOutcome.BuildSucceeded, result.Summary?.ValidationOutcome);
        fixture.AssertSourcesUnchanged();
    }

    [TestMethod]
    public async Task RVAAC010_PhysicalRuntimeCopyChangeInvalidatesAdmissionSnapshot()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("success");
        await fixture.AssertRuntimeExchangeRejectedAsync();
        Assert.IsFalse(fixture.WrapperStarted);
        Assert.IsFalse(fixture.HasGate("ready"));
    }

    [TestMethod]
    public void RVAAC011_PrivateRootCollisionNeverAdoptsExistingState()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native private state requires macOS."); return; }
        var root = "/private/tmp/aia-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
        PrivateProcessDirectory.Create(root, "10.0.403");
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(root));
        Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(root + "/tmp/probe.sock") < 104);
        var sentinel = root + "/home/retained-canary";
        File.WriteAllText(sentinel, "synthetic retained state");
        Assert.ThrowsExactly<IOException>(() => PrivateProcessDirectory.Create(root, "10.0.403"));
        Assert.AreEqual("synthetic retained state", File.ReadAllText(sentinel));
    }

    [TestMethod]
    public async Task RVAAC011_FailedRealProbeStartsNoTargetAndDoesNotFallback()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("success");
        var result = await fixture.RunProductAsync(default, breakProbe: true);
        Assert.IsTrue(fixture.WrapperStarted, "The real host must run the probe.");
        Assert.AreEqual("capability_unavailable", result.ErrorCode);
        Assert.AreEqual(ProcessStartState.NotStarted, result.Summary?.StartState);
        Assert.AreEqual(ValidationOutcome.NotVerified, result.Summary?.ValidationOutcome);
        Assert.IsFalse(fixture.HasGate("ready"));
        fixture.AssertSourcesUnchanged();
    }

    [TestMethod]
    public async Task RVAAC012_PreObservationDoubleForkRemainsAnExplicitUnsupportedBoundary()
    {
        await using var fixture = await RvaNativeFixture.CreateAsync("detach", allowGuardRescue: true);
        var result = await fixture.RunSupervisorAsync(default, _ => { }, waitBeforeObservation: "detached");
        Assert.IsTrue(fixture.HasGate("detached"));
        Assert.IsTrue(fixture.HasLiveRegisteredHelper(), "Unobserved helper must remain independently guarded.");
        Assert.IsTrue(result.Exited);
        // CleanupConfirmed concerns only the known set, not this escaped process.
        Assert.IsTrue(fixture.RegisteredHelpers > 1);
        await using var product = await RvaNativeFixture.CreateAsync("detach", allowGuardRescue: true);
        var productResult = await product.RunProductAsync(default);
        Assert.AreEqual(ProcessStartState.Started, productResult.Summary?.StartState);
        Assert.IsTrue(product.HasGate("detached"));
        Assert.IsTrue(productResult.Summary!.EndState is ProcessEndState.Exited or ProcessEndState.Unconfirmed);
        Assert.AreEqual(productResult.Summary.EndState == ProcessEndState.Unconfirmed, product.SessionStopped);
        if (productResult.Summary.EndState == ProcessEndState.Unconfirmed)
            Assert.AreEqual(ValidationOutcome.NotVerified, productResult.Summary.ValidationOutcome);
        product.AssertSourcesUnchanged();
    }
}