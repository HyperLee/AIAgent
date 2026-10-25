using System.Text;

using AIAgent.Cli.Git;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitAdapterTests
{
    [TestMethod]
    public async Task GITAC013_InvalidLocalInputsNeverStartAProcess()
    {
        await using var adapter = new GitReadAdapter("/does-not-exist-git-fixture");
        foreach (var request in new[]
        {
            new GitReadRequest(scope: "../escape"), new GitReadRequest(scope: "/tmp"),
            new GitReadRequest(scope: "a//b"), new GitReadRequest(objectId: "HEAD:path"),
            new GitReadRequest(objectFormat: "SHA1"), new GitReadRequest(paths: ["../outside"]),
            new GitReadRequest(ignoreReadFiles: ["/tmp/.gitignore"])
        })
            Assert.AreEqual("git_failed", (await adapter.RunAsync(GitReadRecipe.IndexManifest, request, default)).ErrorCode);
        Assert.IsNull(adapter.ExecutableIdentity);
    }

    [TestMethod]
    public void GITAC013_FixedRecipesTreatScopeAsLiteralAndRejectRevisionExpressions()
    {
        var args = GitProcessRecipe.Arguments("/repository", GitReadRecipe.HeadManifest, new GitReadRequest(scope: "-a:b[*]", objectId: new string('a', 64), objectFormat: "sha256"));
        Assert.AreEqual("--literal-pathspecs", args[4]); Assert.AreEqual("--", args[^2]); Assert.AreEqual("-a:b[*]", args[^1]);
        var ignoreArgs = GitProcessRecipe.Arguments("/repository", GitReadRecipe.CheckIgnore, new(paths: [":literal", "wild[*].cs"]));
        Assert.IsFalse(ignoreArgs.Contains("--literal-pathspecs", StringComparer.Ordinal));
        Assert.AreEqual("./:literal\0./wild[*].cs\0", Encoding.UTF8.GetString(GitProcessRecipe.Input(GitReadRecipe.CheckIgnore, new(paths: [":literal", "wild[*].cs"]))));
        Assert.ThrowsExactly<ArgumentException>(() => GitProcessRecipe.Arguments("/repository", GitReadRecipe.Batch, new GitReadRequest(objectId: "HEAD:path")));
        Assert.ThrowsExactly<ArgumentException>(() => GitProcessRecipe.Arguments("/repository", GitReadRecipe.VerifyHeadRef, new GitReadRequest(headReference: "--heads")));
        Assert.ThrowsExactly<ArgumentException>(() => GitProcessRecipe.Arguments("/repository", GitReadRecipe.VerifyHeadRef, new GitReadRequest(headReference: "refs/heads/a..b")));
    }

    [TestMethod]
    public void GITAC007_EnvironmentNeverInheritsCallerValues()
    {
        var environment = GitProcessRecipe.Environment();
        Assert.AreEqual("/var/empty", environment["HOME"]); Assert.AreEqual("C", environment["LC_ALL"]);
        Assert.AreEqual("1", environment["GIT_NO_LAZY_FETCH"]); Assert.AreEqual("/dev/null", environment["GIT_CONFIG_GLOBAL"]);
        Assert.IsFalse(environment.ContainsKey("GIT_CONFIG_COUNT")); Assert.IsFalse(environment.ContainsKey("DYLD_INSERT_LIBRARIES"));
        Assert.IsFalse(environment.Keys.Any(key => key.Contains("KEY", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void GITAC008_BinaryResultsAndRequestsDefensivelyCopyCollections()
    {
        var paths = new[] { "safe.cs" }; var request = new GitReadRequest(paths: paths); paths[0] = "changed.cs";
        Assert.AreEqual("safe.cs", request.Paths[0]);
        var bytes = Encoding.UTF8.GetBytes("a\0b"); var result = new GitReadResult("none", 0, bytes); bytes[0] = 0;
        Assert.AreEqual((byte)'a', result.Stdout.Span[0]);
        var retained = result.Stdout.ToArray(); retained[0] = 0; Assert.AreEqual((byte)'a', result.Stdout.Span[0]);
        Assert.AreEqual("[GitReadResult]", result.ToString()); Assert.AreEqual("[GitReadRequest]", request.ToString());
    }

    [TestMethod]
    public void GITAC003_ByteIdenticalReplacementStillChangesInstallationIdentity()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Installation identity fixture requires macOS."); return; }
        var root = "/private/tmp/aia-git-identity-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        var path = root + "/synthetic-executable"; File.WriteAllText(path, "synthetic fixture");
        var created = File.GetCreationTimeUtc(path); var modified = File.GetLastWriteTimeUtc(path);
        var capture = typeof(GitInstallation).GetMethod("Fingerprint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var before = (string?)capture.Invoke(null, [path, Array.Empty<string>()]); Assert.IsNotNull(before);
        File.Move(path, path + ".retained"); File.WriteAllText(path, "synthetic fixture");
        File.SetCreationTimeUtc(path, created); File.SetLastWriteTimeUtc(path, modified);
        var after = (string?)capture.Invoke(null, [path, Array.Empty<string>()]); Assert.IsNotNull(after);
        Assert.AreNotEqual(before, after, "A different inode cannot inherit an admitted executable identity even when bytes and timestamps match.");
    }

    [TestMethod]
    public async Task GITAC003_AdmissionTriesNextTrustedCandidateButProductFailureNeverRetries()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Candidate fixture requires macOS."); return; }
        var candidates = GitInstallation.Discover().ToArray();
        if (candidates.Length < 2) { Assert.Inconclusive("Candidate fallback fixture requires two fixed trusted installations."); return; }
        var runner = new AdmissionRunner(candidates[0].Identity);
        await using var adapter = new GitReadAdapter("/private/tmp", runner);
        var result = await adapter.RunAsync(GitReadRecipe.Version, new(), default);
        Assert.AreEqual("none", result.ErrorCode);
        Assert.AreEqual(candidates[1].Identity, adapter.ExecutableIdentity);
        Assert.AreEqual(1, runner.Calls.Count(call => call.ExecutableIdentity == candidates[0].Identity));
        var failed = await adapter.RunAsync(GitReadRecipe.IndexManifest, new(), default);
        Assert.AreEqual("git_failed", failed.ErrorCode);
        Assert.AreEqual(1, runner.Calls.Count(call => call.Recipe == GitReadRecipe.IndexManifest));
        Assert.AreEqual(candidates[1].Identity, adapter.ExecutableIdentity);
    }

    private sealed class AdmissionRunner(string rejectedIdentity) : IGitProcessRunner
    {
        internal List<GitProcessDescriptor> Calls { get; } = [];
        public Task<GitReadResult> RunAsync(GitProcessDescriptor descriptor, CancellationToken cancellationToken)
        {
            Calls.Add(descriptor);
            return Task.FromResult(new GitReadResult(descriptor.ExecutableIdentity == rejectedIdentity ||
                descriptor.Recipe == GitReadRecipe.IndexManifest ? "git_failed" : "none", 0));
        }
    }

    [TestMethod]
    public void GITAC008_OneAbsoluteDeadlineCapsEveryCleanupWait()
    {
        var clock = new GitDeadlineClock(); var deadline = new GitProcessDeadline(clock);
        Assert.AreEqual(TimeSpan.FromSeconds(25), deadline.ActiveRemaining);
        clock.Milliseconds = 25000;
        Assert.AreEqual(TimeSpan.Zero, deadline.ActiveRemaining);
        Assert.AreEqual(TimeSpan.FromSeconds(5), deadline.Remaining(TimeSpan.FromSeconds(10)));
        clock.Milliseconds = 29000;
        Assert.AreEqual(TimeSpan.FromSeconds(1), deadline.Remaining(TimeSpan.FromSeconds(5)));
        var lastSecond = deadline.BeginCleanup();
        Assert.AreEqual(TimeSpan.FromSeconds(1), lastSecond.Remaining(TimeSpan.FromSeconds(5)));
        clock.Milliseconds = 31000;
        Assert.AreEqual(TimeSpan.Zero, deadline.Remaining(TimeSpan.FromSeconds(1)));
        var externalClock = new GitDeadlineClock(); var command = new GitProcessDeadline(externalClock);
        externalClock.Milliseconds = 10000; var cleanup = command.BeginCleanup();
        Assert.AreEqual(TimeSpan.FromSeconds(5), cleanup.Remaining(TimeSpan.FromSeconds(20)));
        externalClock.Milliseconds = 14000;
        Assert.AreEqual(TimeSpan.FromSeconds(1), cleanup.Remaining(TimeSpan.FromSeconds(20)));
        externalClock.Milliseconds = 16000;
        Assert.AreEqual(TimeSpan.Zero, cleanup.Remaining(TimeSpan.FromSeconds(20)));
    }

    [TestMethod]
    public async Task GITAC008_ControlWriteCannotBlockPastTheRemainingDeadline()
    {
        var clock = new GitDeadlineClock { Milliseconds = 0 }; var deadline = new GitProcessDeadline(clock);
        clock.Milliseconds = 29999;
        using var stream = new GitBlockedControlStream();
        Assert.IsFalse(await GitProcessRunner.RequestStopAsync(stream, deadline));
        Assert.AreEqual(1, stream.Writes);
        clock.Milliseconds = 30001;
        Assert.IsFalse(await GitProcessRunner.RequestStopAsync(stream, deadline));
        Assert.AreEqual(1, stream.Writes, "An expired deadline must not begin another write.");
    }

    private sealed class GitDeadlineClock : TimeProvider
    {
        internal long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
    private sealed class GitBlockedControlStream : MemoryStream
    {
        private readonly TaskCompletionSource _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Writes { get; private set; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { Writes++; return new(_pending.Task); }
    }

    [TestMethod]
    public void GITAC009_DescriptorRejectsOversizedPayloadBeforeWritingToThePipe()
    {
        var paths = Enumerable.Repeat(new string('x', 4093), 256);
        var descriptor = new GitProcessDescriptor("/repository", new string('a', 64), GitReadRecipe.CheckIgnore, new(paths: paths));
        using var pipe = new MemoryStream();
        Assert.ThrowsExactly<ArgumentException>(() => descriptor.Write(pipe));
        Assert.AreEqual(0L, pipe.Length);
    }

    [TestMethod]
    public void GITAC009_InputHasAnExactByteCeilingAndInvalidUnicodeCannotChangeAPath()
    {
        var paths = Enumerable.Repeat(new string('x', 4093), 256).ToArray();
        Assert.AreEqual(GitProcessRecipe.MetadataLimit, GitProcessRecipe.Input(GitReadRecipe.CheckIgnore, new(paths: paths)).Length);
        Assert.ThrowsExactly<ArgumentException>(() => GitProcessRecipe.Input(GitReadRecipe.CheckIgnore, new(paths: paths.Append("extra"))));
        Assert.IsFalse(GitProcessRecipe.ValidRequest("/repository", GitReadRecipe.IndexManifest, new(scope: "bad\ud800")));
    }

    [TestMethod]
    public async Task GITAC008_CancellationAndDisposalRejectBeforeDiscovery()
    {
        var adapter = new GitReadAdapter("/does-not-exist-git-fixture");
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.AreEqual("turn_cancelled", (await adapter.RunAsync(GitReadRecipe.Version, new(), stop.Token)).ErrorCode);
        await adapter.DisposeAsync();
        Assert.AreEqual("turn_cancelled", (await adapter.RunAsync(GitReadRecipe.Version, new(), default)).ErrorCode);
        Assert.IsNull(adapter.ExecutableIdentity);
    }
}