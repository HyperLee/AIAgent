using System.Diagnostics;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Workspace;
using AIAgent.ProcessHost;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildNativeTests
{
    [TestMethod]
    public async Task BUILDAC014_ThrowingStartedReceiptStillStopsAndReapsSpawnedGroup()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native fixture requires macOS."); return; }
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = "/private/tmp/aia-host-fault-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        var script = root + "/sleep.sh";
        await File.WriteAllTextAsync(script, "#!/bin/sh\nexec /bin/sleep 20\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var descriptor = new Descriptor(0, root, script, root + "/Fixture.csproj", root, root, 15000);
        var previous = Directory.GetCurrentDirectory(); var pid = 0;
        try
        {
            await Assert.ThrowsExactlyAsync<IOException>(async () => await Supervisor.Run(descriptor, default,
                value => { pid = value; throw new IOException("Synthetic broken control receipt."); }, () => { }));
            Assert.IsTrue(pid > 0); Assert.IsFalse(NativeFixtureAlive(pid), "Target must be stopped before callback failure returns.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (pid > 0 && NativeFixtureAlive(pid)) Native.SignalGroup(pid, 9);
        }
    }

    [TestMethod]
    public async Task BUILDAC022_LostSnapshotStillStopsVerifiedSpawnedRoot()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native fixture requires macOS."); return; }
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = "/private/tmp/aia-host-observation-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        var script = root + "/sleep.sh";
        await File.WriteAllTextAsync(script, "#!/bin/sh\nexec /bin/sleep 20\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var descriptor = new Descriptor(0, root, script, root + "/Fixture.csproj", root, root, 15000);
        var previous = Directory.GetCurrentDirectory(); var pid = 0;
        try
        {
            var result = await Supervisor.Run(descriptor, default, value => pid = value, () => { }, () => null);
            Assert.IsTrue(pid > 0); Assert.IsFalse(NativeFixtureAlive(pid), "Known root must stop despite failed process-list observation.");
            Assert.IsFalse(result.CleanupConfirmed, "Lost observation cannot certify cleanup.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (pid > 0 && NativeFixtureAlive(pid)) Native.SignalGroup(pid, 9);
        }
    }

    [TestMethod]
    public async Task BUILDAC014_ThrowingCleanupReceiptStillStopsAndReapsSpawnedGroup()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native fixture requires macOS."); return; }
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = "/private/tmp/aia-host-cleanup-fault-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        var script = root + "/sleep.sh";
        await File.WriteAllTextAsync(script, "#!/bin/sh\nexec /bin/sleep 20\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var descriptor = new Descriptor(0, root, script, root + "/Fixture.csproj", root, root, 15000);
        var previous = Directory.GetCurrentDirectory(); var pid = 0;
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try
        {
            await Assert.ThrowsExactlyAsync<IOException>(async () => await Supervisor.Run(descriptor, stop.Token,
                value => pid = value, () => throw new IOException("Synthetic broken cleanup receipt.")));
            Assert.IsTrue(pid > 0); Assert.IsFalse(NativeFixtureAlive(pid), "Target must be stopped before cleanup callback failure returns.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (pid > 0 && NativeFixtureAlive(pid)) Native.SignalGroup(pid, 9);
        }
    }

    [TestMethod]
    public async Task BUILDAC014_UnavailableInitialIdentityStillStopsOwnedSpawn()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native fixture requires macOS."); return; }
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = "/private/tmp/aia-host-no-identity-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        var script = root + "/sleep.sh";
        await File.WriteAllTextAsync(script, "#!/bin/sh\nexec /bin/sleep 20\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var descriptor = new Descriptor(0, root, script, root + "/Fixture.csproj", root, root, 15000);
        var previous = Directory.GetCurrentDirectory(); var pid = 0;
        try
        {
            var result = await Supervisor.Run(descriptor, default, _ => Assert.Fail("Unverified target cannot get Started receipt."), () => { },
                initialIdentityProvider: value => { pid = value; return null; });
            Assert.IsTrue(pid > 0); Assert.IsFalse(result.CleanupConfirmed);
            Assert.IsFalse(NativeFixtureAlive(pid), "Spawn-owned PID must stop before unconfirmed return.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (pid > 0 && NativeFixtureAlive(pid)) Native.SignalGroup(pid, 9);
        }
    }

    [TestMethod]
    public async Task BUILDAC022_LostSnapshotAndIdentityStillStopsOwnedSpawn()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native fixture requires macOS."); return; }
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = "/private/tmp/aia-host-no-observation-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        var script = root + "/sleep.sh";
        await File.WriteAllTextAsync(script, "#!/bin/sh\nexec /bin/sleep 20\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var descriptor = new Descriptor(0, root, script, root + "/Fixture.csproj", root, root, 15000);
        var previous = Directory.GetCurrentDirectory(); var pid = 0;
        try
        {
            var result = await Supervisor.Run(descriptor, default, value => pid = value, () => { },
                snapshotProvider: () => null, subsequentIdentityProvider: _ => null);
            Assert.IsTrue(pid > 0); Assert.IsFalse(result.CleanupConfirmed);
            Assert.IsFalse(NativeFixtureAlive(pid), "An unreaped spawned root must stop despite both observation failures.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (pid > 0 && NativeFixtureAlive(pid)) Native.SignalGroup(pid, 9);
        }
    }

    [TestMethod]
    public async Task BUILDAC015_RealSandboxBuildsOfflineTargetAndConfirmsCleanup()
    {
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = await NativeBuildFixture.CreateAsync(false);
        using var session = WorkspaceFixtures.Session(root);
        var execution = new AgentExecutionPolicy(new(session.Policy)); var approvals = 0;
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (preview, _) => { approvals++; Assert.IsTrue(preview.DisplayBlock.Contains("禁止 IPv4/IPv6", StringComparison.Ordinal)); return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => { });
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", default);
        Assert.AreEqual(1, approvals, result.ErrorCode);
        Assert.AreEqual("none", result.ErrorCode, result.Response?.GetRawText());
        Assert.IsNotNull(result.Summary); Assert.AreEqual(ValidationOutcome.BuildSucceeded, result.Summary.ValidationOutcome);
        Assert.AreEqual(ProcessStartState.Started, result.Summary.StartState);
        Assert.IsTrue(result.Summary.CleanupConfirmed); Assert.IsTrue(File.Exists(root + "/bin/Debug/net10.0/Fixture.dll"));
        Assert.AreEqual(NativeBuildFixture.BuildSource, await File.ReadAllTextAsync(root + "/Program.cs"));
    }

    [TestMethod]
    public async Task BUILDAC005_KnownSdkMtpIpcLayoutRejectsBeforeProjectIoOrApproval()
    {
        using var session = WorkspaceFixtures.Session("/does-not-exist-native-mtp"); var execution = new AgentExecutionPolicy(new(session.Policy));
        var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { Assert.Fail("Unsupported MTP must not request approval."); return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => Assert.Fail());
        var result = await tasks.RunAsync(DotnetTaskKind.Test, "Fixture.csproj", default);
        Assert.AreEqual("capability_unavailable", result.ErrorCode); Assert.IsNull(result.Summary);
        Assert.AreEqual(0, fs.Inspected.Count);
    }

    [TestMethod]
    public async Task BUILDAC022_RealSandboxReapsObservedChildAfterBuildRootExits()
    {
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = await NativeBuildFixture.CreateAsync(false);
        var project = await File.ReadAllTextAsync(root + "/Fixture.csproj");
        project = project.Replace("</Project>", "<Target Name=\"SpawnFixture\" BeforeTargets=\"CoreCompile\"><Exec Command=\"/bin/sh -c '/bin/sleep 30 &gt;/dev/null 2&gt;&amp;1 &amp; echo $! &gt; obj/childpid; /bin/sleep 0.5'\" /></Target></Project>", StringComparison.Ordinal);
        // XML decodes the explicit shell fixture; the product still receives only its fixed build recipe.
        project = project.Replace("&amp;quot;", "&quot;", StringComparison.Ordinal).Replace("&amp;gt;", "&gt;", StringComparison.Ordinal).Replace("&amp;amp;", "&amp;", StringComparison.Ordinal);
        await File.WriteAllTextAsync(root + "/Fixture.csproj", project);
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => ValueTask.FromResult(ProcessApprovalDecision.Approved), _ => { });
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", default);
        Assert.AreEqual("none", result.ErrorCode, result.Response?.GetRawText());
        var pid = int.Parse(await File.ReadAllTextAsync(root + "/obj/childpid"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsFalse(NativeFixtureAlive(pid), "An observed child must not remain running after a normal build root exit.");
        Assert.IsTrue(result.Summary!.CleanupConfirmed);
    }

    private static bool NativeFixtureAlive(int pid)
    {
        var info = new byte[136];
        return ProcInfo(pid, 3, 0, info, info.Length) == info.Length && BitConverter.ToUInt32(info, 4) != 5;
    }
    [System.Runtime.InteropServices.DllImport("/usr/lib/libproc.dylib", EntryPoint = "proc_pidinfo")]
    private static extern int ProcInfo(int pid, int flavor, ulong argument, byte[] output, int size);

    [TestMethod]
    public async Task BUILDAC023_RealCancellationStopsBuildAndObservedChildren()
    {
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = await NativeBuildFixture.CreateAsync(false);
        var project = await File.ReadAllTextAsync(root + "/Fixture.csproj");
        await File.WriteAllTextAsync(root + "/Fixture.csproj", project.Replace("</Project>", "<Target Name=\"WaitFixture\" BeforeTargets=\"CoreCompile\"><Exec Command=\"/bin/sleep 20\" /></Target></Project>", StringComparison.Ordinal));
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        using var cancel = new CancellationTokenSource();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { cancel.CancelAfter(1500); return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => { });
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", cancel.Token);
        Assert.AreEqual("turn_cancelled", result.ErrorCode, result.Response?.GetRawText());
        Assert.AreEqual(ProcessStartState.Started, result.Summary?.StartState);
        Assert.AreEqual(ProcessEndState.Cancelled, result.Summary?.EndState);
        Assert.IsTrue(result.Summary!.CleanupConfirmed); Assert.IsFalse(execution.ProcessSessionStopped);
        Assert.IsFalse(File.Exists(root + "/bin/Debug/net10.0/Fixture.dll"));
    }

    [TestMethod]
    public async Task BUILDAC007_NonApprovalDecisionsNeverCreatePrivateStateOrStartTarget()
    {
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = await NativeBuildFixture.CreateAsync(false);
        foreach (var decision in new[] { ProcessApprovalDecision.Declined, ProcessApprovalDecision.Cancelled, ProcessApprovalDecision.EndOfInput, ProcessApprovalDecision.Unavailable })
        {
            using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
            string? privateRoot = null;
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
            await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
                (preview, _) =>
                {
                    privateRoot = preview.DisplayBlock.Split('\n').Single(line => line.StartsWith("私人 0700 暫存", StringComparison.Ordinal)).Split('：')[1];
                    return ValueTask.FromResult(decision);
                }, _ => Assert.Fail("A non-approved operation cannot start progress."));
            var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", default);
            Assert.AreEqual(decision == ProcessApprovalDecision.Declined ? "approval_declined" : decision == ProcessApprovalDecision.Unavailable ? "process_io_failure" : "turn_cancelled", result.ErrorCode);
            Assert.AreEqual(decision == ProcessApprovalDecision.Declined ? AIAgent.Cli.Tools.ToolStatus.Rejected : decision == ProcessApprovalDecision.Unavailable ? AIAgent.Cli.Tools.ToolStatus.Failed : AIAgent.Cli.Tools.ToolStatus.Cancelled, result.Status);
            Assert.AreEqual(ProcessStartState.NotStarted, result.Summary?.StartState);
            Assert.IsNotNull(privateRoot); Assert.IsFalse(Directory.Exists(privateRoot));
        }
    }

    [TestMethod]
    public async Task BUILDAC008_ApprovalCannotAuthorizeReplacedProjectBytes()
    {
        if (!DotnetInstallation.PlatformSupported) Assert.Inconclusive("Native fixture requires the declared macOS arm64 platform.");
        var root = await NativeBuildFixture.CreateAsync(false);
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            async (_, _) => { await File.AppendAllTextAsync(root + "/Fixture.csproj", " "); return ProcessApprovalDecision.Approved; }, _ => Assert.Fail("Changed project must not start the host."));
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", default);
        Assert.AreEqual("path_changed", result.ErrorCode); Assert.AreEqual(ProcessStartState.NotStarted, result.Summary?.StartState);
        Assert.IsFalse(File.Exists(root + "/bin/Debug/net10.0/Fixture.dll"));
    }
}

internal static class NativeBuildFixture
{
    internal const string BuildSource = "System.Console.WriteLine(42);";
    internal static async Task<string> CreateAsync(bool tests)
    {
        var root = "/private/tmp/aia-fixture-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(root + "/Fixture.csproj", tests
            ? "<Project Sdk=\"MSTest.Sdk/4.5.1\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><TestingExtensionsProfile>None</TestingExtensionsProfile><UseAppHost>false</UseAppHost></PropertyGroup></Project>"
            : "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><UseAppHost>false</UseAppHost></PropertyGroup></Project>");
        await File.WriteAllTextAsync(root + "/Program.cs", tests
            ? "using Microsoft.VisualStudio.TestTools.UnitTesting; [TestClass] public sealed class NativeCases { [TestMethod] public void Pass() { Assert.AreEqual(2, 1 + 1); } [TestMethod, Ignore(\"fixture\")] public void Skip() { } }"
            : BuildSource);
        await File.WriteAllTextAsync(root + "/global.json", "{\"sdk\":{\"version\":\"10.0.100\"},\"test\":{\"runner\":\"Microsoft.Testing.Platform\"}}");
        Directory.CreateDirectory(root + "/empty-packages"); Directory.CreateDirectory(root + "/home"); Directory.CreateDirectory(root + "/tmp");
        await File.WriteAllTextAsync(root + "/home/offline.config", "<configuration><packageSources><clear/><add key=\"offline\" value=\"" + root + "/empty-packages\" /></packageSources></configuration>");
        var installation = DotnetInstallation.Discover(); Assert.IsNotNull(installation, "Packaged native host or trusted SDK missing.");
        Directory.CreateDirectory(root + "/home/.dotnet");
        foreach (var sentinel in new[] { ".dotnetFirstUseSentinel", ".aspNetCertificateSentinel", ".toolpath.sentinel" })
            using (File.Create(root + "/home/.dotnet/" + installation.SdkVersion + sentinel)) { }
        var start = new ProcessStartInfo(installation.Executable) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Clear(); foreach (var pair in DotnetRecipe.Environment(installation, root)) start.Environment.Add(pair.Key, pair.Value);
        foreach (var argument in new[] { "restore", root + "/Fixture.csproj", "--configfile", root + "/home/offline.config", "--packages", installation.Packages, "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode, await output + await error);
        return root;
    }
}