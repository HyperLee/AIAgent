using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DiscoveryLifecycleTests
{
    [TestMethod]
    public async Task SEARCHAC023_ObservedParentsCannotBecomeInitialLookupFailures()
    {
        foreach (var range in new[] { false, true })
        foreach (var kind in new[] { WorkspaceNodeKind.Missing, WorkspaceNodeKind.Link, WorkspaceNodeKind.File })
        {
            await using var f = new DiscoveryFixture(); f.Fs.Directory("d"); f.Fs.File("d/a", [65]); f.Fs.File("b", [66]);
            await f.Files.ReadFileAsync("b", default); var fileObservations = 0; var exchanged = false;
            f.Fs.OnInspect = path =>
            {
                if (path == "/ws/d/a") fileObservations++;
                if (!exchanged && path == "/ws/d" && fileObservations >= (range ? 1 : 2))
                { exchanged = true; f.Fs.Nodes["/ws/d"].State = new(kind); }
            };
            if (range)
            {
                var result = await f.Files.ReadFileRangeAsync("d/a", 1, 1, default);
                Assert.IsTrue(exchanged && result.ErrorCode == WorkspaceErrorCode.PathChanged && result.Data is null, "SEARCH-AC-023-range-reinspect");
            }
            else
            {
                var result = await f.Files.SearchTextAsync("d", "A", "*", true, 0, default);
                Assert.IsTrue(exchanged && result.ErrorCode == WorkspaceErrorCode.PathChanged && result.Data is null, "SEARCH-AC-023-text-reinspect");
            }
            Assert.IsTrue(f.Files.Snapshots.Count == 0, "SEARCH-AC-023-reinspect-baselines");
        }
    }

    [TestMethod]
    public async Task SEARCHAC022_ChangesToUnmatchedOrUnsupportedCandidatesDiscardAllResults()
    {
        foreach (var unsupported in new[] { false, true })
        {
            await using var f = new DiscoveryFixture(); f.Fs.File("a", unsupported ? [255] : [66]); f.Fs.File("b", [65]);
            f.Fs.OnOpen = path => { if (path == "/ws/b") f.Fs.Nodes["/ws/a"].State = new(WorkspaceNodeKind.File, 1, Identity: 99); };
            var result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
            Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.PathChanged && result.Data is null && f.Fs.Handles.All(h => h.Disposed), "SEARCH-AC-022");
        }
    }

    [TestMethod]
    public async Task SEARCHAC023_DirectoryExchangeClearsAllBaselinesEvenDuringReadFailure()
    {
        await using var f = new DiscoveryFixture(); f.Fs.Directory("d"); f.Fs.File("d/a", [65]); f.Fs.File("b", [66]);
        await f.Files.ReadFileAsync("b", default);
        f.Fs.OnOpen = _ =>
        {
            f.Fs.Nodes["/ws/d"].State = new(WorkspaceNodeKind.Directory, Identity: 99);
            throw new IOException("FAKE_ONLY");
        };
        var result = await f.Files.SearchTextAsync("d", "A", "*", true, 0, default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.PathChanged && result.Data is null && f.Files.Snapshots.Count == 0, "SEARCH-AC-023");
    }

    [TestMethod]
    public async Task SEARCHAC023_RootLossWinsOverDisappearanceAndClearsSnapshots()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65]); await f.Files.ReadFileAsync("a", default);
        f.Fs.OnEnumerate = _ => { f.Fs.Nodes.Remove("/ws"); f.Fs.Nodes.Remove("/ws/a"); };
        var result = await f.Files.SearchFilesAsync(".", "*", default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.WorkspaceUnavailable && result.Status == WorkspaceStatus.Failed &&
            result.Data is null && f.Files.Snapshots.Count == 0 && f.Fs.EnumeratorsClosed == 1, "SEARCH-AC-023-root");
    }

    [TestMethod]
    public async Task SEARCHAC026_ClearDuringEnumerationAndReadPreventsLateData()
    {
        foreach (var phase in new[] { "enumeration", "read" })
        {
            await using var f = new DiscoveryFixture(); f.Fs.File("a", [65]); await f.Files.ReadFileAsync("a", default);
            if (phase == "enumeration") f.Fs.OnEnumerate = _ => f.Files.Snapshots.Clear();
            else f.Fs.OnRead = _ => { f.Files.Snapshots.Clear(); return Task.CompletedTask; };
            var result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Cancelled && result.Data is null && f.Files.Snapshots.Count == 0 &&
                f.Fs.Handles.All(h => h.Disposed), "SEARCH-AC-026");
        }
    }

    [TestMethod]
    public async Task SEARCHAC027_ConcurrentOperationsDoNotQueueAndDisposeWaitsForReadCleanup()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fs.OnRead = async _ => { started.TrySetResult(); await release.Task; };
        var first = files.SearchTextAsync(".", "A", "*", true, 0, default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); var opened = fs.Opened; var inspected = fs.Inspected.Count;
        Assert.IsTrue((await files.ReadFileAsync("a", default)).ErrorCode == WorkspaceErrorCode.Unavailable &&
            (await files.SearchFilesAsync(".", "*", default)).ErrorCode == WorkspaceErrorCode.Unavailable && fs.Opened == opened && fs.Inspected.Count == inspected, "SEARCH-AC-027-busy");
        var disposal = files.DisposeAsync().AsTask(); Assert.IsTrue(!disposal.IsCompleted, "SEARCH-AC-027-waits");
        release.SetResult(); var result = await first; await disposal; await files.DisposeAsync();
        Assert.IsTrue(result.Status == WorkspaceStatus.Cancelled && result.Data is null && fs.Handles.All(h => h.Disposed) &&
            (await files.ReadFileRangeAsync("a", 1, 1, default)).ErrorCode == WorkspaceErrorCode.Unavailable, "SEARCH-AC-027-finished");
    }

    [TestMethod]
    public async Task SEARCHAC026_PublicationChecksCancellationAndGenerationAfterJson()
    {
        foreach (var clear in new[] { false, true })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
            using var cancel = new CancellationTokenSource(); var policy = new DiscoveryPolicy(session.Policy);
            await using var files = new WorkspaceFiles(session.Workspace, policy, fs);
            policy.OnCheck = text => { if (text == "has_more") { if (clear) files.Snapshots.Clear(); else cancel.Cancel(); } };
            var result = await files.ReadFileRangeAsync("a", 1, 1, cancel.Token);
            Assert.IsTrue(result.Status == WorkspaceStatus.Cancelled && result.Data is null, "SEARCH-AC-026-publish");
        }
    }

    [TestMethod]
    public async Task SEARCHAC032_RealUtf8FilesLinksAndRangesNeverChangeSourceBytes()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var bytes = Encoding.UTF8.GetBytes("\uFEFF中文😀\r\n尾\n"); await File.WriteAllBytesAsync(Path.Combine(root, "a"), bytes);
        File.CreateSymbolicLink(Path.Combine(root, "link"), Path.Combine(root, "a"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem());
        var search = await files.SearchTextAsync(".", "中文", "*", true, 3, default);
        var range = await files.ReadFileRangeAsync("a", 1, 1, default);
        Assert.IsTrue(search.Data?.Matches.Count == 1 && search.Data.Matches[0].Path == "a" && range.Data?.HasMore == true &&
            range.Data.ContentByteCount == 12 && (await File.ReadAllBytesAsync(Path.Combine(root, "a"))).SequenceEqual(bytes), "SEARCH-AC-032-source");
        Assert.IsTrue((await files.ReadFileRangeAsync("link", 1, 1, default)).ErrorCode == WorkspaceErrorCode.SymbolicLink, "SEARCH-AC-005-link");
        Assert.IsTrue((await files.ReplaceTextAsync("a", "尾", "終", default)).ErrorCode == WorkspaceErrorCode.SnapshotRequired, "SEARCH-AC-024-write");
    }
}

internal sealed class DiscoveryPolicy(ISensitiveDataPolicy inner) : ISensitiveDataPolicy
{
    internal Action<string>? OnCheck { get; set; }
    public bool ContainsCurrentKey(string content) => inner.ContainsCurrentKey(content);
    public bool CanSendOrUse(string content, ContentBoundary boundary) { OnCheck?.Invoke(content); return inner.CanSendOrUse(content, boundary); }
    public string RedactForDisplay(string content) => inner.RedactForDisplay(content);
}