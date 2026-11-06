using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Workspace;
using AIAgent.ProcessHost;

namespace AIAgent.Cli.Tests;

internal sealed class RvaNativeFixture : IAsyncDisposable
{
    private static bool s_cleanupUnconfirmed;
    private readonly Process _guard;
    private readonly bool _allowGuardRescue;
    private readonly Dictionary<string, byte[]> _sources = new(StringComparer.Ordinal);
    private sealed record OwnedIdentity(Native.Identity Value);
    private OwnedIdentity? _wrapper;
    private OwnedIdentity? _target;
    private bool _started;
    private Socket? _outsideSocket;
    private readonly ConcurrentQueue<ProcessObservation> _observations = new();
    private long _helperReadyAt;
    private long _lastTargetAliveAt;
    internal ProcessObservation[] Observations => _observations.ToArray();
    internal long HelperReadyTimestamp => Volatile.Read(ref _helperReadyAt);
    internal long LastTargetAliveTimestamp => Volatile.Read(ref _lastTargetAliveAt);
    internal bool TargetAliveAtReady => HelperReadyTimestamp != 0;
    internal string Root { get; }
    internal string Helper => Root + "/rva-helper";
    internal string Gates => Root + "/obj/rva";
    internal int HardDeadline { get; }
    internal bool SessionStopped { get; private set; }
    internal bool WrapperStarted => Volatile.Read(ref _wrapper) is not null;
    internal string PolicyStage => string.Join(",", Directory.EnumerateFiles(Gates, "policy-*").Select(Path.GetFileName));
    internal int RegisteredHelpers => Directory.EnumerateFiles(Gates, "pid-*").Count();

    private RvaNativeFixture(string root, Process guard, int deadline, bool rescue)
    { Root = root; _guard = guard; HardDeadline = deadline; _allowGuardRescue = rescue; }

    internal static async Task<RvaNativeFixture> CreateAsync(string scenario, int deadline = 30000, bool allowGuardRescue = false)
    {
        if (!OperatingSystem.IsMacOS() || !DotnetInstallation.PlatformSupported)
            Assert.Inconclusive("RVA native fixture requires the declared macOS arm64 platform.");
        if (s_cleanupUnconfirmed) Assert.Inconclusive("RVA native batch stopped after unconfirmed watchdog cleanup.");
        var root = await NativeBuildFixture.CreateAsync(false);
        SetMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateDirectory(root + "/obj/rva"); Directory.CreateDirectory(root + "/bin");
        Directory.CreateDirectory(root + "/secrets");
        await File.WriteAllTextAsync(root + "/secrets/rva-canary", "synthetic private canary");
        await File.WriteAllTextAsync(root + "/obj/rva/scenario", scenario, new UTF8Encoding(false));
        if (scenario == "invalid") await File.WriteAllTextAsync(root + "/Program.cs", "this is a synthetic syntax error;");
        var compiler = new ProcessStartInfo("/usr/bin/xcrun")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        compiler.Environment.Clear(); compiler.Environment["PATH"] = "/usr/bin:/bin";
        foreach (var argument in new[] { "clang", "-std=c11", "-Wall", "-Wextra", "-Werror", "-O2", HelperSource(), "-o", root + "/rva-helper" }) compiler.ArgumentList.Add(argument);
        using (var process = Process.Start(compiler)!)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await Task.WhenAll(stdout, stderr);
            Assert.AreEqual(0, process.ExitCode, "RVA helper compilation failed; diagnostics withheld.");
        }
        var project = await File.ReadAllTextAsync(root + "/Fixture.csproj");
        // Compile the trusted task during developer preparation, outside the
        // operation budget. Exec would capture child pipes before build exits.
        var installation = DotnetInstallation.Discover(); Assert.IsNotNull(installation);
        var taskRoot = root + "-task"; Directory.CreateDirectory(taskRoot);
        SetMode(taskRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Copy(root + "/global.json", taskRoot + "/global.json");
        var sdk = installation.Root + "/sdk/" + installation.SdkVersion;
        var taskProject = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><UseAppHost>false</UseAppHost></PropertyGroup><ItemGroup>" +
            "<Reference Include=\"Microsoft.Build.Framework\" HintPath=\"" + sdk + "/Microsoft.Build.Framework.dll\" Private=\"false\"/>" +
            "<Reference Include=\"Microsoft.Build.Utilities.Core\" HintPath=\"" + sdk + "/Microsoft.Build.Utilities.Core.dll\" Private=\"false\"/></ItemGroup></Project>";
        await File.WriteAllTextAsync(taskRoot + "/Helper.csproj", taskProject);
        await File.WriteAllTextAsync(taskRoot + "/Helper.cs",
            "public sealed class RvaNativeTarget : Microsoft.Build.Utilities.Task { " +
            "[System.Runtime.InteropServices.DllImport(\"/usr/lib/libSystem.B.dylib\", EntryPoint=\"system\")] private static extern int Run(string command); " +
            "public override bool Execute() { return Run(\"" + root + "/rva-helper build\") == 0; } }");
        await PrepareAsync(installation, root, "restore", taskRoot + "/Helper.csproj", "--configfile", root + "/home/offline.config", "--packages", installation.Packages, "-p:NuGetAudit=false");
        await PrepareAsync(installation, root, "build", taskRoot + "/Helper.csproj", "--no-restore", "--disable-build-servers", "-nodeReuse:false", "-p:UseSharedCompilation=false");
        File.Copy(taskRoot + "/bin/Debug/net10.0/Helper.dll", root + "/rva-task.dll");
        var target = "<UsingTask TaskName=\"RvaNativeTarget\" AssemblyFile=\"" + root + "/rva-task.dll\"/>" +
            "<Target Name=\"RvaFixture\" BeforeTargets=\"CoreCompile\"><RvaNativeTarget /></Target>";
        await File.WriteAllTextAsync(root + "/Fixture.csproj", project.Replace("</Project>", target + "</Project>", StringComparison.Ordinal));
        var start = new ProcessStartInfo(root + "/rva-helper")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        start.Environment.Clear(); start.Environment["PATH"] = "/usr/bin:/bin";
        foreach (var argument in new[] { "watchdog", root, deadline.ToString(CultureInfo.InvariantCulture) }) start.ArgumentList.Add(argument);
        var guard = Process.Start(start)!; guard.StandardInput.Close();
        var fixture = new RvaNativeFixture(root, guard, deadline, allowGuardRescue);
        foreach (var name in new[] { "Program.cs", "Fixture.csproj", "rva-helper", "rva-task.dll", "secrets/rva-canary" }) fixture._sources.Add(name, await File.ReadAllBytesAsync(root + "/" + name));
        if (scenario == "policy")
        {
            fixture._outsideSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            fixture._outsideSocket.Bind(new UnixDomainSocketEndPoint(root + "-outside.sock")); fixture._outsideSocket.Listen(1);
        }
        await fixture.WaitGateAsync("watchdog-ready");
        return fixture;
    }

    private static async Task PrepareAsync(DotnetInstallation installation, string root, params string[] arguments)
    {
        var start = new ProcessStartInfo(installation.Executable)
        { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Clear(); foreach (var pair in DotnetRecipe.Environment(installation, root)) start.Environment.Add(pair.Key, pair.Value);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); await Task.WhenAll(stdout, stderr);
        Assert.AreEqual(0, process.ExitCode, "RVA offline task preparation failed; diagnostics withheld.");
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        File.SetUnixFileMode(path, mode);
    }

    private static string HelperSource([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../native/rva_fixture.c"));
    internal bool HasGate(string name) => File.Exists(Gates + "/" + name);
    internal void Gate(string name)
    {
        var path = Gates + "/" + name;
        if (File.Exists(path)) return;
        try { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); stream.WriteByte(49); }
        catch (IOException) when (File.Exists(path)) { }
    }
    internal async Task WaitGateAsync(string name)
    {
        var timer = Stopwatch.StartNew();
        while (!HasGate(name))
        {
            if (_guard.HasExited || timer.Elapsed > TimeSpan.FromSeconds(10)) Assert.Fail("RVA required native gate unavailable: " + name);
            await Task.Delay(20);
        }
    }
    private async Task StartAsync()
    {
        Assert.IsFalse(_started, "Each fixture owns one operation."); _started = true;
        Gate("start"); await WaitGateAsync("armed");
    }
    private void Register(Native.Identity identity)
    {
        var path = Gates + "/pid-" + identity.Pid.ToString(CultureInfo.InvariantCulture);
        if (File.Exists(path)) return;
        var temporary = Gates + "/.registration-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, FormattableString.Invariant($"{identity.Pid} {identity.StartSeconds} {identity.StartMicros}\n"));
        SetMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try { File.Move(temporary, path); }
        catch (IOException) when (File.Exists(path)) { }
    }
    private void ObserveOwnedTree(Native.Identity wrapper)
    {
        var snapshot = Native.Snapshot(); if (snapshot is null) return;
        var owned = new Dictionary<int, Native.Identity> { [wrapper.Pid] = wrapper };
        for (var pass = 0; pass < snapshot.Length; pass++)
        {
            var added = false;
            foreach (var item in snapshot)
                if (!owned.ContainsKey(item.Pid) && owned.TryGetValue(item.Parent, out var parent) && snapshot.Any(value => value.SameProcess(parent)))
                { owned.Add(item.Pid, item); added = true; }
            if (!added) break;
        }
        foreach (var item in owned.Values) Register(item);
    }

    internal async Task<SupervisionResult> RunSupervisorAsync(CancellationToken token, Action<IReadOnlyList<Native.Identity>> observed, string? waitBeforeObservation = null)
    {
        await StartAsync(); var previous = Directory.GetCurrentDirectory();
        try
        {
            return await Supervisor.Run(new(0, Root, Helper, Root + "/Fixture.csproj", Root, Root, HardDeadline == 60000 ? 50000 : 25000), token,
                pid =>
                {
                    Register(Native.Inspect(pid)!.Value);
                    if (waitBeforeObservation is not null)
                    {
                        var timer = Stopwatch.StartNew();
                        while (!HasGate(waitBeforeObservation) && timer.Elapsed < TimeSpan.FromSeconds(5)) Thread.Sleep(10);
                        Assert.IsTrue(HasGate(waitBeforeObservation));
                    }
                }, () => Gate("cleanup-started"), observed: known =>
                { foreach (var identity in known) Register(identity); observed(known); });
        }
        finally { Directory.SetCurrentDirectory(previous); }
    }

    internal async Task<DotnetTaskResult> RunProductAsync(CancellationToken token, Action? atApproval = null, bool breakProbe = false)
    {
        await StartAsync();
        string? privateRoot = null;
        using var session = WorkspaceFixtures.Session(Root);
        var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        using var observing = new CancellationTokenSource();
        var observer = Task.Run(async () =>
        {
            while (!observing.IsCancellationRequested)
            {
                if (Volatile.Read(ref _wrapper) is { } wrapper) ObserveOwnedTree(wrapper.Value);
                if (Volatile.Read(ref _target) is { } target && Native.Inspect(target.Value.Pid) is { Status: not 5 } current && current.SameProcess(target.Value))
                {
                    var now = Stopwatch.GetTimestamp(); Volatile.Write(ref _lastTargetAliveAt, now);
                    if (HasGate("ready")) Interlocked.CompareExchange(ref _helperReadyAt, now, 0);
                }
                await Task.Delay(20);
            }
        });
        try
        {
            await using var tasks = new ControlledDotnetTasksFactory(observeWrapper: pid =>
            {
                var identity = Native.Inspect(pid)!.Value; Register(identity); Volatile.Write(ref _wrapper, new(identity));
                if (breakProbe)
                {
                    Assert.IsNotNull(privateRoot);
                    Assert.IsTrue(privateRoot.StartsWith("/private/tmp/aia-", StringComparison.Ordinal) && privateRoot.Length == 33);
                    Directory.CreateDirectory(privateRoot + "/tmp/allowed");
                }
            }, observeProcess: observation =>
            {
                _observations.Enqueue(observation);
                if (observation.Phase == ProcessObservationPhase.TargetStarted && Native.Inspect(observation.TargetPid) is { } identity) Volatile.Write(ref _target, new(identity));
            }).Create(session.Workspace, session.Policy, execution, files,
                (preview, _) =>
                {
                    if (breakProbe) privateRoot = preview.DisplayBlock.Split('\n').Single(line => line.StartsWith("私人 0700 暫存", StringComparison.Ordinal)).Split('：')[1];
                    atApproval?.Invoke(); return ValueTask.FromResult(ProcessApprovalDecision.Approved);
                }, _ => { });
            var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", token).WaitAsync(TimeSpan.FromMilliseconds(HardDeadline + 8000));
            SessionStopped = execution.ProcessSessionStopped;
            File.WriteAllText(Root + "-evidence.json", System.Text.Json.JsonSerializer.Serialize(new
            {
                entry = "product-controlled-service",
                root = Root,
                result = result.ErrorCode,
                start = result.Summary?.StartState.ToString(),
                end = result.Summary?.EndState.ToString(),
                cleanup = result.Summary?.CleanupConfirmed,
                elapsedMs = result.Summary?.ElapsedMilliseconds,
                helperReadyAt = HelperReadyTimestamp,
                lastTargetAliveAt = LastTargetAliveTimestamp,
                events = Observations.Select(value => new { phase = value.Phase.ToString(), at = value.Timestamp, remainingMs = value.RemainingMilliseconds, pid = value.TargetPid })
            }));
            return result;
        }
        finally { observing.Cancel(); await observer; }
    }

    internal void KillOwnedWrapper()
    {
        var wrapper = Volatile.Read(ref _wrapper); Assert.IsNotNull(wrapper);
        var current = Native.Inspect(wrapper.Value.Pid);
        Assert.IsTrue(current is { } value && value.SameProcess(wrapper.Value));
        Native.Signal(wrapper.Value.Pid, 9);
    }

    internal async Task<int> RunApplicationFaultAsync(string scenario)
    {
        await StartAsync();
        using var session = WorkspaceFixtures.Session(Root);
        var client = new ScriptClient { Session = session };
        client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, "rva-native", "run_dotnet_task",
            System.Text.Json.JsonSerializer.SerializeToElement(new { task = "build", project_path = "Fixture.csproj" }))));
        var runtime = new ScriptFactory(client); runtime.Sessions.Add(session);
        var interaction = new ProcessChatInteraction { Decision = ProcessApprovalDecision.Approved };
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("build"));
        foreach (var command in new[] { "/clear", "/key", "/model", "retry", "/exit" })
            interaction.Inner.Inputs.Enqueue(() => { Assert.Fail("Native uncertainty must stop before another input: " + command); return ChatInputResult.Submitted(command); });
        var factory = new ControlledDotnetTasksFactory(observeWrapper: pid =>
        { var identity = Native.Inspect(pid)!.Value; Register(identity); Volatile.Write(ref _wrapper, new(identity)); });
        var app = new ChatApplication(interaction, runtime, new(), new(), "synthetic instruction", new WorkspaceFilesFactory(), factory);
        using var observing = new CancellationTokenSource();
        var observer = Task.Run(async () =>
        {
            while (!observing.IsCancellationRequested)
            {
                if (Volatile.Read(ref _wrapper) is { } wrapper) ObserveOwnedTree(wrapper.Value);
                await Task.Delay(20);
            }
        });
        try
        {
            var operation = app.RunAsync(session, default);
            await WaitGateAsync("ready");
            if (scenario == "burst") Gate("burst");
            else { await WaitGateAsync("child-ready"); app.Interrupt(); }
            var exit = await operation.WaitAsync(TimeSpan.FromMilliseconds(HardDeadline + 8000));
            Assert.AreEqual(1, client.Requests.Count);
            Assert.IsTrue(interaction.Inner.Events.Any(value => value.StopReason == AgentStopReason.ProcessUnconfirmed));
            Assert.IsTrue(interaction.Inner.Events.Any(value => value.Agent?.Operation?.ProcessSummary?.EndState == ProcessEndState.Unconfirmed));
            return exit;
        }
        finally { observing.Cancel(); await observer; }
    }

    internal sealed record HostOutcome(bool Started, bool CleanupConfirmed, bool ReceiptReceived = false, int ObservedDescendants = 0, bool IndependentlyGone = false);
    internal async Task<HostOutcome> RunHostFaultAsync(string fault)
    {
        await StartAsync();
        var installation = DotnetInstallation.Discover(); Assert.IsNotNull(installation);
        using var session = WorkspaceFixtures.Session(Root);
        var snapshot = await new DotnetProjectAdmission(new MacWorkspaceFileSystem(), session.Policy)
            .InspectAsync(Root, "Fixture.csproj", DotnetTaskKind.Build, installation, default);
        var temporary = "/private/tmp/aia-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        PrivateProcessDirectory.Create(temporary, installation.SdkVersion);
        using var commands = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        using var events = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var start = new ProcessStartInfo("/usr/bin/sandbox-exec")
        { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Clear(); foreach (var pair in DotnetRecipe.Environment(installation, temporary)) start.Environment.Add(pair.Key, pair.Value);
        foreach (var argument in new[] { "-p", MacSandboxPolicy.Create(snapshot, temporary), installation.HostExecutable, commands.GetClientHandleAsString(), events.GetClientHandleAsString() }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!; process.StandardInput.Close();
        commands.DisposeLocalCopyOfClientHandle(); events.DisposeLocalCopyOfClientHandle();
        var wrapper = Native.Inspect(process.Id)!.Value; Register(wrapper);
        using var pipeCancellation = new CancellationTokenSource();
        var stdout = process.StandardOutput.ReadToEndAsync(pipeCancellation.Token); var stderr = process.StandardError.ReadToEndAsync(pipeCancellation.Token);
        using (var writer = new BinaryWriter(commands, Encoding.UTF8, true))
        {
            writer.Write(0x41494131); writer.Write((byte)0);
            foreach (var field in new[] { Root, installation.Executable, Root + "/Fixture.csproj", temporary, installation.Packages })
            { var bytes = Encoding.UTF8.GetBytes(field); writer.Write(bytes.Length); writer.Write(bytes); }
            writer.Write(25000); writer.Flush();
        }
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerTask = Task.Run(() =>
        {
            try
            {
                using var reader = new BinaryReader(events, Encoding.UTF8, true);
                while (true)
                {
                    switch (reader.ReadByte())
                    {
                        case 1:
                            Register(Native.Inspect(reader.ReadInt32())!.Value); started.TrySetResult();
                            if (fault == "results") return new HostOutcome(true, false);
                            break;
                        case 5: break;
                        case 2: reader.ReadInt32(); reader.ReadBoolean(); var cleanup = reader.ReadBoolean(); reader.ReadBoolean(); return new HostOutcome(started.Task.IsCompletedSuccessfully, cleanup, ReceiptReceived: true);
                        default: return new HostOutcome(started.Task.IsCompletedSuccessfully, false);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { return new HostOutcome(started.Task.IsCompletedSuccessfully, false); }
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10)); await WaitGateAsync("ready");
        ObserveOwnedTree(wrapper);
        var observedDescendants = RegisteredHelpers - 1;
        Assert.IsTrue(observedDescendants >= 2, "Fault gate requires independently observed target identities.");
        if (fault == "control-eof") commands.Dispose();
        else if (fault == "control-invalid") { commands.WriteByte(0); commands.Flush(); }
        else if (fault == "results")
        {
            await readerTask; events.Dispose(); commands.WriteByte(1); commands.Flush();
        }
        else
        {
            Assert.IsTrue(Native.Inspect(wrapper.Pid) is { } current && current.SameProcess(wrapper)); Native.Signal(wrapper.Pid, 9);
        }
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
        // Closing the reader before awaiting avoids a blocked read on a lost host.
        events.Dispose();
        var outcome = await readerTask.WaitAsync(TimeSpan.FromSeconds(2));
        pipeCancellation.CancelAfter(1000);
        try { await Task.WhenAll(stdout, stderr); }
        catch (OperationCanceledException) { /* A missing drain never becomes confirmed cleanup. */ }
        var stopped = Stopwatch.StartNew();
        while (fault != "wrapper-crash" && HasLiveRegisteredHelper() && stopped.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(20);
        return outcome with { ObservedDescendants = observedDescendants, IndependentlyGone = !HasLiveRegisteredHelper() };
    }

    internal async Task AssertRuntimeExchangeRejectedAsync()
    {
        var original = DotnetInstallation.Discover(); Assert.IsNotNull(original);
        var runtime = Root + "-runtime"; Directory.CreateDirectory(runtime);
        SetMode(runtime, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        foreach (var suffix in new[] { "", ".dll", ".deps.json", ".runtimeconfig.json" })
            File.Copy(original.HostDirectory + "/AIAgent.ProcessHost" + suffix, runtime + "/AIAgent.ProcessHost" + suffix);
        var installation = new DotnetInstallation(original.Root, original.SdkVersion, runtime, original.Packages);
        using var session = WorkspaceFixtures.Session(Root);
        var admission = new DotnetProjectAdmission(new MacWorkspaceFileSystem(), session.Policy);
        var before = await admission.InspectAsync(Root, "Fixture.csproj", DotnetTaskKind.Build, installation, default);
        var originalHash = SHA256.HashData(await File.ReadAllBytesAsync(original.HostDirectory + "/AIAgent.ProcessHost.dll"));
        using (var stream = new FileStream(runtime + "/AIAgent.ProcessHost.dll", FileMode.Append, FileAccess.Write)) stream.WriteByte(0);
        var after = await admission.InspectAsync(Root, "Fixture.csproj", DotnetTaskKind.Build, installation, default);
        Assert.IsFalse(before.Matches(after));
        CollectionAssert.AreEqual(originalHash, SHA256.HashData(await File.ReadAllBytesAsync(original.HostDirectory + "/AIAgent.ProcessHost.dll")));
    }

    internal void ExchangeAtApproval(string exchange)
    {
        if (exchange == "project") { File.AppendAllText(Root + "/Fixture.csproj", " "); return; }
        var path = exchange == "ancestor" ? Root : Root + "/" + exchange;
        Directory.Move(path, path + "-previous"); Directory.CreateDirectory(path);
        if (exchange == "ancestor")
        {
            SetMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            foreach (var name in new[] { "Program.cs", "Fixture.csproj", "rva-helper", "rva-task.dll", "global.json" }) File.Copy(path + "-previous/" + name, Root + "/" + name);
            Directory.CreateDirectory(Root + "/secrets"); File.Copy(path + "-previous/secrets/rva-canary", Root + "/secrets/rva-canary");
        }
        if (exchange is "obj" or "ancestor")
        {
            Directory.CreateDirectory(Gates);
            var prior = exchange == "obj" ? Root + "/obj-previous/rva" : Root + "-previous/obj/rva";
            foreach (var file in Directory.EnumerateFiles(prior)) File.Copy(file, Gates + "/" + Path.GetFileName(file));
        }
    }
    internal void CreateOutputAlias(string kind)
    {
        var source = Root + "/Program.cs";
        var destination = Root + "/bin/alias.cs";
        if (kind == "symlink") { File.CreateSymbolicLink(destination, source); return; }
        if (kind == "hardlink") source = Root + "/secrets/rva-canary";
        if (kind == "case") destination = Root + "/bin/PROGRAM.CS";
        if (kind == "unicode") destination = Root + "/bin/來源e\u0301.cs";
        Assert.AreEqual(0, Link(source, destination));
    }
    internal void AssertSourceCanaryUnchanged() => CollectionAssert.AreEqual(_sources["Program.cs"], File.ReadAllBytes(Root + "/Program.cs"));
    internal void AssertSourcesUnchanged()
    { foreach (var item in _sources) CollectionAssert.AreEqual(item.Value, File.ReadAllBytes(Root + "/" + item.Key), "RVA source canary changed."); }
    internal bool HasLiveRegisteredHelper()
    {
        foreach (var path in Directory.EnumerateFiles(Gates, "pid-*"))
        {
            var values = File.ReadAllText(path).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 3 || !int.TryParse(values[0], out var pid))
            { s_cleanupUnconfirmed = true; Assert.Fail("RVA ownership record malformed."); return true; }
            var inspected = Native.Inspect(pid, out var observationLost);
            if (observationLost) { s_cleanupUnconfirmed = true; Assert.Fail("RVA owned identity observation lost."); return true; }
            if (inspected is { Status: not 5 } identity && identity.StartSeconds.ToString(CultureInfo.InvariantCulture) == values[1] && identity.StartMicros.ToString(CultureInfo.InvariantCulture) == values[2]) return true;
        }
        return false;
    }
    public async ValueTask DisposeAsync()
    {
        if (!_started) { Gate("start"); await WaitGateAsync("armed"); }
        Gate("finish");
        try
        {
            await _guard.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            if ((_guard.ExitCode != 0 || !HasGate("guard-clean")) && (_guard.ExitCode != 1 || !HasGate("guard-reaped")) || HasGate("guard-unconfirmed")) { s_cleanupUnconfirmed = true; Assert.Fail("RVA watchdog cleanup unconfirmed; native batch stopped."); }
            Assert.IsFalse(HasGate("watchdog-cutoff"), "RVA watchdog deadline exceeded; not product cleanup evidence.");
            Assert.IsTrue(_allowGuardRescue || _guard.ExitCode == 0, "RVA guard rescued a process; product cleanup was insufficient.");
            Assert.IsFalse(HasLiveRegisteredHelper(), "RVA owned process still alive.");
        }
        catch (TimeoutException) { s_cleanupUnconfirmed = true; throw; }
        finally { _outsideSocket?.Dispose(); _guard.Dispose(); }
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "link", SetLastError = true)] private static extern int Link(string source, string destination);
}