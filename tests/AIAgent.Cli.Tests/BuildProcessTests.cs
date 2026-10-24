using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildProcessTests
{
    [TestMethod]
    public async Task BUILDAC044_CleanupDisarmsExecutionTimeoutWithoutErasingPriorExpiry()
    {
        using var onTime = new ProcessExecutionTimeout(TimeSpan.FromMilliseconds(100));
        onTime.OnCleanupStarted();
        await Task.Delay(250);
        Assert.IsFalse(onTime.Expired); Assert.IsFalse(onTime.Token.IsCancellationRequested);
        using var expired = new ProcessExecutionTimeout(TimeSpan.FromMilliseconds(20));
        await Task.Delay(100);
        expired.OnCleanupStarted();
        Assert.IsTrue(expired.Expired);
    }

    [TestMethod]
    public void BUILDAC018_SdkCatalogRejectsAmbiguousHostSelection()
    {
        Assert.AreEqual("10.0.100", DotnetInstallation.SelectInstalledSdkVersion(["10.0.100"]));
        Assert.IsNull(DotnetInstallation.SelectInstalledSdkVersion(["10.0.100", "11.0.100"]));
        Assert.IsNull(DotnetInstallation.SelectInstalledSdkVersion(["10.0.100", "10.0.101"]));
        Assert.IsNull(DotnetInstallation.SelectInstalledSdkVersion(["10.0.100", "unexpected"]));
    }

    [TestMethod]
    public async Task BUILDAC018_GlobalSdkPathsOverrideRejectsBeforePreview()
    {
        using var session = WorkspaceFixtures.Session(); var fs = AdmissionFileSystem();
        fs.File("global.json", Encoding.UTF8.GetBytes("{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"latestPatch\",\"paths\":[\"/other/sdk\"]}}"));
        var admission = new DotnetProjectAdmission(fs, session.Policy);
        try
        {
            await admission.InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build, new("/sdk", "10.0.100", "/product", "/packages"), default);
            Assert.Fail("SDK path overrides must not reach approval.");
        }
        catch (ProcessAdmissionException ex) { Assert.AreEqual("capability_unavailable", ex.Code); }
    }

    [TestMethod]
    public void BUILDAC034_SnapshotRejectsChangedSdkSelection()
    {
        var first = new ProcessAdmissionSnapshot("/ws", "/ws/a.csproj", new("/sdk", "10.0.100", "/product", "/packages"), [], [],
            new Dictionary<string, WorkspaceNodeState>(), new Dictionary<string, byte[]>());
        var changed = new ProcessAdmissionSnapshot("/ws", "/ws/a.csproj", new("/sdk", "10.0.101", "/product", "/packages"), [], [],
            new Dictionary<string, WorkspaceNodeState>(), new Dictionary<string, byte[]>());
        Assert.IsFalse(first.Matches(changed));
    }

    [TestMethod]
    public async Task BUILDAC032_FlatEnumerationStopsAtGlobalLimitBeforeSort()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new CountingEnumerationFileSystem(8200, 0);
        var admission = new DotnetProjectAdmission(fs, session.Policy);
        try
        {
            await admission.InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build, new("/sdk", "10.0.100", "/product", "/packages"), default);
            Assert.Fail("Oversized flat directory must reject.");
        }
        catch (ProcessAdmissionException ex) { Assert.AreEqual("unsupported_project_layout", ex.Code); }
        Assert.IsTrue(fs.Yielded <= 8193, $"Enumerated {fs.Yielded} entries before enforcing the limit.");
    }

    [TestMethod]
    public async Task BUILDAC032_CancellationStopsEnumerationBeforeSort()
    {
        using var session = WorkspaceFixtures.Session(); using var cancel = new CancellationTokenSource();
        var fs = new CountingEnumerationFileSystem(96, 64, cancel);
        var admission = new DotnetProjectAdmission(fs, session.Policy);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await admission.InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build, new("/sdk", "10.0.100", "/product", "/packages"), cancel.Token));
        Assert.IsTrue(fs.Yielded <= 65, $"Enumerated {fs.Yielded} entries after cancellation.");
    }

    [TestMethod]
    public async Task BUILDAC002_DirectPlanServiceRejectsBeforeMissingWorkspaceOrApproval()
    {
        using var session = WorkspaceFixtures.Session("/does-not-exist-stage10");
        var plans = new PlanStore(session.Policy); plans.SetMode(AgentMode.Plan);
        var execution = new AgentExecutionPolicy(plans); var approvals = 0;
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { approvals++; return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => Assert.Fail("Plan must not publish process progress."));
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "a.csproj", default);
        Assert.AreEqual("mode_restricted", result.ErrorCode);
        Assert.AreEqual(0, approvals); Assert.IsNull(result.Summary);
    }

    [TestMethod]
    public async Task BUILDAC004_DirectServiceRejectsSensitiveAndUnsafePathsBeforeAdmission()
    {
        using var session = WorkspaceFixtures.Session(); var execution = new AgentExecutionPolicy(new(session.Policy));
        var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { Assert.Fail("Invalid input cannot prompt."); return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => Assert.Fail());
        foreach (var path in new[] { "../a.csproj", "/a.csproj", "a.sln", "*.csproj", "a.csproj/", ".ssh/a.csproj" })
        {
            var result = await tasks.RunAsync(DotnetTaskKind.Build, path, default);
            Assert.AreEqual(AIAgent.Cli.Tools.ToolStatus.Rejected, result.Status);
            Assert.IsNull(result.Summary);
        }
        Assert.AreEqual(0, fs.Inspected.Count);
    }

    [TestMethod]
    public async Task BUILDAC004_SensitiveWorkspaceRootRejectsBeforeAdmission()
    {
        using var session = WorkspaceFixtures.Session("/ws/.ssh/project"); var execution = new AgentExecutionPolicy(new(session.Policy));
        var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { Assert.Fail("Sensitive workspace cannot prompt."); return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => Assert.Fail());
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "a.csproj", default);
        Assert.AreEqual("sensitive_path", result.ErrorCode); Assert.IsNull(result.Summary); Assert.AreEqual(0, fs.Opened);
    }

    [TestMethod]
    public async Task BUILDAC026_DirectRejectionCannotPublishSensitiveFixedJsonFields()
    {
        using var session = WorkspaceFixtures.Session(key: "status"); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: execution);
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => ValueTask.FromResult(ProcessApprovalDecision.Declined), _ => Assert.Fail());
        var result = await tasks.RunAsync(DotnetTaskKind.Test, "a.csproj", default);
        Assert.AreEqual("capability_unavailable", result.ErrorCode); Assert.IsNull(result.Response);
    }

    [TestMethod]
    public async Task BUILDAC008_RuntimeBytesRecheckRejectsSameMetadataReplacement()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        foreach (var directory in new[] { "/product", "/sdk", "/packages", "/ws/obj" }) fs.Nodes[directory] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 20));
        var current = "";
        foreach (var segment in AppContext.BaseDirectory.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        { current += "/" + segment; fs.Nodes[current] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 100)); }
        fs.File("a.csproj", Encoding.UTF8.GetBytes("<Project />"));
        fs.File("obj/project.assets.json", Encoding.UTF8.GetBytes("{\"packageFolders\":{\"/packages/\":{}},\"targets\":{\"net10.0\":{}}}"));
        foreach (var full in new[] { "/sdk/dotnet", "/product/AIAgent.ProcessHost", "/product/AIAgent.ProcessHost.dll", "/product/AIAgent.ProcessHost.deps.json", "/product/AIAgent.ProcessHost.runtimeconfig.json", typeof(IControlledDotnetTasks).Assembly.Location })
            fs.Nodes[full] = new(new(WorkspaceNodeKind.File, 5, Identity: (ulong)fs.Nodes.Count + 300), Encoding.ASCII.GetBytes("hostA"));
        var installation = new DotnetInstallation("/sdk", "10.0.100", "/product", "/packages");
        var admission = new DotnetProjectAdmission(fs, session.Policy);
        var before = await admission.InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build, installation, default);
        fs.Nodes["/product/AIAgent.ProcessHost.dll"].Bytes = Encoding.ASCII.GetBytes("hostB");
        var after = await admission.InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build, installation, default);
        Assert.IsFalse(before.Matches(after), "Same inode/size/timestamp cannot authorize changed supervisor bytes.");
    }

    [TestMethod]
    public void BUILDAC005_TestRecipeDisablesLaunchProfileAndContainsNoRestoreOrFreeArguments()
    {
        var args = DotnetRecipe.Arguments(DotnetTaskKind.Test, "/workspace/a.csproj", "/private/tmp/aia-0000000000000000");
        CollectionAssert.AreEqual(new[] { "test", "--project", "/workspace/a.csproj", "--configuration", "Debug", "--no-restore", "--no-launch-profile", "--no-ansi", "--no-progress", "--output", "Normal", "--results-directory", "/private/tmp/aia-0000000000000000/results", "-maxcpucount:1", "-nodeReuse:false", "-property:UseSharedCompilation=false", "--", "--minimum-expected-tests", "1" }, args);
        var installation = new DotnetInstallation("/usr/local/share/dotnet", "10.0.100", "/product/ProcessHost", "/user/.nuget/packages");
        var env = DotnetRecipe.Environment(installation, "/private/tmp/aia-0000000000000000");
        Assert.AreEqual(18, env.Count); Assert.AreEqual("/usr/bin:/bin:/usr/local/share/dotnet", env["PATH"]);
        Assert.AreEqual("0", env["DOTNET_EnableDiagnostics"]); Assert.AreEqual("1", env["MSBUILDDISABLENODEREUSE"]);
        Assert.AreEqual("/private/tmp/aia-0000000000000000/home", env["HOME"]);
    }

    [TestMethod]
    public async Task BUILDAC021_AdmissionRejectsHardlinkedOutputBeforeAnyContentRead()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        fs.Nodes["/product"] = new(new(WorkspaceNodeKind.Directory, Identity: 50));
        fs.Nodes["/sdk"] = new(new(WorkspaceNodeKind.Directory, Identity: 51));
        fs.Nodes["/packages"] = new(new(WorkspaceNodeKind.Directory, Identity: 52));
        var current = "";
        foreach (var segment in AppContext.BaseDirectory.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        { current += "/" + segment; fs.Nodes[current] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 100)); }
        fs.Directory("bin"); fs.File("a.csproj", Encoding.UTF8.GetBytes("<Project />"));
        fs.File("bin/alias", [1]); fs.Nodes["/ws/bin/alias"].State = new(WorkspaceNodeKind.File, 1, Identity: 500, Links: 2);
        var admission = new DotnetProjectAdmission(fs, session.Policy);
        try
        {
            await admission.InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build, new("/sdk", "10.0.100", "/product", "/packages"), default);
            Assert.Fail("Hard links in an output directory must reject admission.");
        }
        catch (ProcessAdmissionException ex) { Assert.AreEqual("unsafe_output_path", ex.Code); }
        Assert.AreEqual(1, fs.Opened);
    }

    private static ControlledWorkspaceFileSystem AdmissionFileSystem()
    {
        var fs = new ControlledWorkspaceFileSystem();
        foreach (var directory in new[] { "/product", "/sdk", "/packages", "/ws/obj" })
            fs.Nodes[directory] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 20));
        var current = "";
        foreach (var segment in AppContext.BaseDirectory.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        { current += "/" + segment; fs.Nodes[current] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 100)); }
        fs.File("a.csproj", Encoding.UTF8.GetBytes("<Project />"));
        fs.File("obj/project.assets.json", Encoding.UTF8.GetBytes("{\"packageFolders\":{\"/packages/\":{}},\"targets\":{\"net10.0\":{}}}"));
        foreach (var full in new[] { "/sdk/dotnet", "/product/AIAgent.ProcessHost", "/product/AIAgent.ProcessHost.dll",
            "/product/AIAgent.ProcessHost.deps.json", "/product/AIAgent.ProcessHost.runtimeconfig.json", typeof(IControlledDotnetTasks).Assembly.Location })
            fs.Nodes[full] = new(new(WorkspaceNodeKind.File, 5, Identity: (ulong)fs.Nodes.Count + 300), Encoding.ASCII.GetBytes("hostA"));
        return fs;
    }

    private sealed class CountingEnumerationFileSystem(int throwAfter, int cancelAt, CancellationTokenSource? cancellation = null) : IWorkspaceReadFileSystem
    {
        private readonly ControlledWorkspaceFileSystem _inner = AdmissionFileSystem();
        internal int Yielded { get; private set; }
        public WorkspaceNodeState Inspect(string path) => _inner.Inspect(path);
        public IWorkspaceReadFile OpenRead(string path) => _inner.OpenRead(path);
        public IEnumerable<string> EnumerateNames(string path)
        {
            if (path != "/ws") return _inner.EnumerateNames(path);
            return OversizedNames();
        }
        private IEnumerable<string> OversizedNames()
        {
            while (true)
            {
                Yielded++;
                if (Yielded > throwAfter) throw new InvalidOperationException("Enumeration exceeded the test bound.");
                if (Yielded == cancelAt) cancellation?.Cancel();
                yield return "item" + Yielded.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}