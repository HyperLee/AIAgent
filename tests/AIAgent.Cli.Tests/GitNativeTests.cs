using System.Diagnostics;
using System.Text;

using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitNativeTests
{
    [TestMethod]
    public async Task GITAC008_RealSandboxReadsUnbornIndexWithoutChangingRepositoryBytes()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = await CreateFixture(); await AssertProbe(root); await AssertGitPolicy(root); var before = FixtureFiles(root);
        await using var adapter = new GitReadAdapter(root);
        var version = await adapter.RunAsync(GitReadRecipe.Version, new(), default);
        Assert.AreEqual("none", version.ErrorCode); Assert.IsTrue(version.CleanupConfirmed);
        Assert.IsTrue(Encoding.UTF8.GetString(version.Stdout.Span).StartsWith("git version ", StringComparison.Ordinal));
        var index = await adapter.RunAsync(GitReadRecipe.IndexManifest, new(), default);
        Assert.AreEqual("none", index.ErrorCode); Assert.IsTrue(index.Stdout.Span.Contains((byte)0));
        StringAssert.Contains(Encoding.UTF8.GetString(index.Stdout.Span), "Example.cs");
        var head = await adapter.RunAsync(GitReadRecipe.VerifyHead, new(), default);
        Assert.AreEqual("none", head.ErrorCode); Assert.AreEqual(128, head.ExitCode);
        var reference = await adapter.RunAsync(GitReadRecipe.VerifyHeadRef, new(headReference: "refs/heads/main"), default);
        Assert.AreEqual("none", reference.ErrorCode); Assert.AreEqual(1, reference.ExitCode);
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), FixtureFiles(root).Keys.ToArray());
        foreach (var item in before) CollectionAssert.AreEqual(item.Value, File.ReadAllBytes(item.Key), item.Key);
    }

    [TestMethod]
    public async Task GITAC009_RealBatchPreservesBinaryNulAndRejectsOversizedOutput()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = await CreateFixture(); var blob = new byte[] { 0, 255, 13, 10, 0, 3 };
        var oid = (await FixtureGit(root, ["hash-object", "-w", "--stdin"], blob)).Trim();
        await using var adapter = new GitReadAdapter(root);
        var result = await adapter.RunAsync(GitReadRecipe.Batch, new(objectId: oid), default);
        Assert.AreEqual("none", result.ErrorCode); var bytes = result.Stdout.ToArray();
        var start = Array.IndexOf(bytes, (byte)'\n') + 1;
        CollectionAssert.AreEqual(blob, bytes.AsSpan(start, blob.Length).ToArray());
        var largeOid = (await FixtureGit(root, ["hash-object", "-w", "--stdin"], new byte[GitProcessRecipe.BlobLimit + 257])).Trim();
        var large = await adapter.RunAsync(GitReadRecipe.Batch, new(objectId: largeOid), default);
        Assert.AreEqual("git_limit", large.ErrorCode); Assert.AreEqual(0, large.Stdout.Length); Assert.IsTrue(large.CleanupConfirmed);
    }

    [TestMethod]
    public async Task GITAC007_RealExternalConfigIncludeFailsClosedWithoutExposingItsContents()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = await CreateFixture(); var outside = root + "-outside-config";
        await File.WriteAllTextAsync(outside, "[core]\nrepositoryformatversion = 0\n");
        await File.AppendAllTextAsync(root + "/.git/config", "\n[include]\npath = " + outside + "\n");
        await using var adapter = new GitReadAdapter(root);
        var result = await adapter.RunAsync(GitReadRecipe.IndexManifest, new(), default);
        Assert.IsTrue(result.ErrorCode is "git_unavailable" or "git_failed", result.ErrorCode);
        Assert.AreEqual(0, result.Stdout.Length); Assert.IsTrue(result.CleanupConfirmed);
        Assert.AreEqual("[core]\nrepositoryformatversion = 0\n", await File.ReadAllTextAsync(outside));
    }

    [TestMethod]
    public async Task GITAC002_RealWorkspaceKeepsOpposingStagedAndUnstagedSourcesSeparate()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = await CreateFixture();
        await FixtureGit(root, ["-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "--no-verify", "-qm", "Synthetic baseline"]);
        const string old = "class Example { }\n"; const string staged = "class Example { int Value; }\n";
        await File.WriteAllTextAsync(root + "/Example.cs", staged); await FixtureGit(root, ["add", "--", "Example.cs"]);
        await File.WriteAllTextAsync(root + "/Example.cs", old);
        var before = FixtureFiles(root); var snapshot = await CaptureReal(root);
        var stagedChange = snapshot.Changes.Single(change => change.Path == "Example.cs" && change.Layer == GitLayer.Staged);
        var unstagedChange = snapshot.Changes.Single(change => change.Path == "Example.cs" && change.Layer == GitLayer.Unstaged);
        Assert.AreEqual(old, stagedChange.Old.Content); Assert.AreEqual(staged, stagedChange.New.Content);
        Assert.AreEqual(staged, unstagedChange.Old.Content); Assert.AreEqual(old, unstagedChange.New.Content);
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), FixtureFiles(root).Keys.ToArray());
        foreach (var item in before) CollectionAssert.AreEqual(item.Value, File.ReadAllBytes(item.Key), item.Key);
    }

    [TestMethod]
    public async Task GITAC014_RealUnbornIntentToAddAndEmptyBlobStayDistinctAndNegationIsHonored()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = await CreateFixture();
        await File.WriteAllTextAsync(root + "/Intent.cs", "class Intent { }\n"); await FixtureGit(root, ["add", "-N", "--", "Intent.cs"]);
        await File.WriteAllBytesAsync(root + "/Empty.cs", []); await FixtureGit(root, ["add", "--", "Empty.cs"]);
        await File.WriteAllTextAsync(root + "/.gitignore", "*.tmp\n!visible.tmp\n");
        await File.WriteAllTextAsync(root + "/hidden.tmp", "hidden fixture\n"); await File.WriteAllTextAsync(root + "/visible.tmp", "visible fixture\n");
        await File.WriteAllTextAsync(root + "/:literal.cs", "class Colon { }\n");
        await File.WriteAllTextAsync(root + "/wild[*].cs", "class Wild { }\n");
        await AssertGitPolicy(root, true);
        await using (var probe = new GitReadAdapter(root))
        {
            var ignored = await probe.RunAsync(GitReadRecipe.CheckIgnore, new(paths: ["hidden.tmp", "visible.tmp", ":literal.cs", "wild[*].cs"],
                ignoreReadFiles: [root + "/.gitignore", root + "/.git/info/exclude"]), default);
            Assert.AreEqual("none", ignored.ErrorCode, "Direct check-ignore admission must succeed.");
            StringAssert.Contains(Encoding.UTF8.GetString(ignored.Stdout.Span), "!visible.tmp");
        }
        var snapshot = await CaptureReal(root); Assert.IsNull(snapshot.HeadOid);
        Assert.IsTrue(snapshot.Changes.Where(change => change.Path == "Intent.cs").All(change => change.ExclusionReason == "intent_to_add"));
        Assert.IsTrue(snapshot.Changes.Any(change => change.Path == "Intent.cs"), "ITA must be represented as unsupported metadata.");
        var empty = snapshot.Changes.Single(change => change.Path == "Empty.cs" && change.Layer == GitLayer.Staged);
        Assert.IsNull(empty.ExclusionReason); Assert.AreEqual(0L, empty.New.ByteCount); Assert.IsTrue(empty.New.Exists);
        Assert.IsFalse(snapshot.Changes.Any(change => change.Path == "hidden.tmp"));
        Assert.IsTrue(snapshot.Changes.Any(change => change.Path == ":literal.cs" && change.Layer == GitLayer.Untracked));
        Assert.IsTrue(snapshot.Changes.Any(change => change.Path == "wild[*].cs" && change.Layer == GitLayer.Untracked));
        Assert.IsTrue(snapshot.Changes.Any(change => change.Path == "visible.tmp" && change.Layer == GitLayer.Untracked), string.Join(",", snapshot.Exclusions.Select(item => item.Reason)));
    }

    [TestMethod]
    public async Task GITAC002_RealSha256RepositoryCapturesFullLocalObjectIds()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = "/private/tmp/aia-git-sha256-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        await FixtureGit(root, ["init", "--initial-branch=main", "--object-format=sha256"]);
        await File.WriteAllTextAsync(root + "/Hash.cs", "class Hash { }\n"); await FixtureGit(root, ["add", "--", "Hash.cs"]);
        var snapshot = await CaptureReal(root);
        Assert.AreEqual("sha256", snapshot.ObjectFormat);
        var source = snapshot.Changes.Single(change => change.Path == "Hash.cs" && change.Layer == GitLayer.Staged).New;
        Assert.AreEqual(64, source.BlobOid!.Length); Assert.AreEqual("class Hash { }\n", source.Content);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DoNotParallelize]
    public async Task GITAC008_RealLiveGitCancellationAndTimeoutReapTheOwnedBlockedTarget(bool cancel)
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native Git fixture requires macOS."); return; }
        var root = await CreateFixture();
        await using var adapter = new GitReadAdapter(root);
        Assert.AreEqual("none", (await adapter.RunAsync(GitReadRecipe.Version, new(), default)).ErrorCode);
        var directory = root + "/.git/objects/aa"; Directory.CreateDirectory(directory);
        const string oid = "aabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        Assert.AreEqual(40, oid.Length);
        Assert.AreEqual(0, CreateObjectFifo(directory + "/" + oid[2..], 0x180));
        using var stop = new CancellationTokenSource();
        var elapsed = Stopwatch.StartNew();
        var pending = adapter.RunAsync(GitReadRecipe.Batch, new(objectId: oid), stop.Token).AsTask();
        try
        {
            AIAgent.ProcessHost.Native.Identity? target = null;
            var observation = Stopwatch.StartNew();
            while (target is null && !pending.IsCompleted && observation.Elapsed < TimeSpan.FromSeconds(5))
            {
                foreach (var process in Process.GetProcessesByName("git"))
                {
                    using (process)
                    {
                        try
                        {
                            var identity = AIAgent.ProcessHost.Native.Inspect(process.Id);
                            if (identity is not { Status: not 5 } found) continue;
                            var host = AIAgent.ProcessHost.Native.Inspect(found.Parent);
                            if (host is { } parent && parent.Parent == Environment.ProcessId) { target = found; break; }
                        }
                        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                    }
                }
                if (target is null) await Task.Delay(25);
            }
            Assert.IsNotNull(target, "Fixture must observe the real Git child owned by this adapter's supervisor. Early outcome: " + (pending.IsCompletedSuccessfully ? pending.Result.ErrorCode : "pending"));
            await Task.Delay(200);
            Assert.IsFalse(pending.IsCompleted, "The retained FIFO must block the actual Git target before cancellation or timeout.");
            var live = AIAgent.ProcessHost.Native.Inspect(target.Value.Pid);
            Assert.IsTrue(live is { } current && target.Value.SameProcess(current) && current.Status != 5);
            var cleanup = Stopwatch.StartNew();
            if (cancel) stop.Cancel();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(35));
            Assert.AreEqual(cancel ? "turn_cancelled" : "git_timed_out", result.ErrorCode);
            Assert.IsTrue(result.CleanupConfirmed); Assert.AreEqual(0, result.Stdout.Length);
            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(30), "Total command deadline includes cleanup.");
            if (cancel) Assert.IsTrue(cleanup.Elapsed < TimeSpan.FromSeconds(5), "Cancellation cleanup has its own five-second ceiling.");
            else Assert.IsTrue(elapsed.Elapsed >= TimeSpan.FromSeconds(24), "Timeout must follow an actual blocked command, not an early refusal.");
            var after = AIAgent.ProcessHost.Native.Inspect(target.Value.Pid, out var lost);
            Assert.IsFalse(lost, "Process disappearance must be observed, not hidden by an inspection failure.");
            Assert.IsFalse(after is { } remaining && target.Value.SameProcess(remaining) && remaining.Status != 5);
        }
        finally
        {
            stop.Cancel();
            if (!pending.IsCompleted) await pending.WaitAsync(TimeSpan.FromSeconds(35));
        }
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateObjectFifo(string path, ushort mode);

    private static async Task<GitSnapshot> CaptureReal(string root)
    {
        using var session = WorkspaceFixtures.Session(root);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem());
        await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, root);
        var captured = await git.CaptureAsync(default); Assert.AreEqual("none", captured.ErrorCode);
        Assert.AreEqual(0, files.Snapshots.Count);
        return captured.Snapshot!;
    }

    private static async Task AssertGitPolicy(string root, bool includeIgnore = false)
    {
        var installation = GitInstallation.Discover().First();
        foreach (var recipe in new[] { GitReadRecipe.Version, GitReadRecipe.IntentToAddVisible, GitReadRecipe.IntentToAddInvisible }.Concat(includeIgnore ? [GitReadRecipe.CheckIgnore] : Array.Empty<GitReadRecipe>()))
        {
            var request = recipe == GitReadRecipe.CheckIgnore ? new GitReadRequest(paths: ["hidden.tmp", "visible.tmp"], ignoreReadFiles: [root + "/.gitignore", root + "/.git/info/exclude"]) : new GitReadRequest();
            var start = new ProcessStartInfo("/usr/bin/sandbox-exec")
            { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            start.Environment.Clear(); foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
            start.ArgumentList.Add("-p"); start.ArgumentList.Add(GitSandboxPolicy.Create(root, installation, request));
            start.ArgumentList.Add(installation.Executable);
            foreach (var argument in GitProcessRecipe.Arguments(root, recipe, request)) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.StandardInput.BaseStream.WriteAsync(GitProcessRecipe.Input(recipe, request)); process.StandardInput.Close();
            await process.WaitForExitAsync(); _ = await output; Assert.AreEqual(0, process.ExitCode, recipe + " " + await error);
        }
    }

    private static async Task AssertProbe(string root)
    {
        var installation = GitInstallation.Discover().First();
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ProcessHost", "AIAgent.ProcessHost"))
        { WorkingDirectory = root, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--git-probe"); start.Environment.Clear();
        foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
        start.Environment["DOTNET_ROOT"] = "/usr/local/share/dotnet";
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        new GitProcessDescriptor(root, installation.Identity, GitReadRecipe.Version, new()).Write(process.StandardInput.BaseStream);
        process.StandardInput.Close(); await process.WaitForExitAsync();
        Assert.AreEqual(0, process.ExitCode, await stderr); Assert.AreEqual("\u0001", await stdout);
    }

    private static Dictionary<string, byte[]> FixtureFiles(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
    private static async Task<string> CreateFixture()
    {
        var root = "/private/tmp/aia-git-native-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        await FixtureGit(root, ["init", "--initial-branch=main"]);
        await File.WriteAllTextAsync(root + "/Example.cs", "class Example { }\n");
        await FixtureGit(root, ["add", "--", "Example.cs"]);
        return root;
    }
    private static async Task<string> FixtureGit(string root, string[] args, byte[]? input = null)
    {
        var executable = File.Exists("/opt/homebrew/bin/git") ? "/opt/homebrew/bin/git" : "/usr/bin/git";
        var start = new ProcessStartInfo(executable)
        { WorkingDirectory = root, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        start.Environment.Clear(); foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (input is not null) await process.StandardInput.BaseStream.WriteAsync(input); process.StandardInput.Close();
        await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode, await stderr); return await stdout;
    }
}