using System.Globalization;
using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkspaceFilesTests
{
    [TestMethod]
    public async Task WSAC001_FactoryDoesNoIOAndEndedServiceRejectsOperations()
    {
        using var session = WorkspaceFixtures.Session("/missing");
        var files = new WorkspaceFilesFactory().Create(session);
        await files.DisposeAsync(); await files.DisposeAsync();
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-001");
    }

    [TestMethod]
    public async Task WSAC003_InvalidAndSensitiveArgumentsNeverReachFileSystem()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var path in new string?[] { null, "", "../a", "/etc/a", "C:a", "a\\b", "a\0b", new('a', 4097) })
            WorkspaceFixtures.Check(await files.ReadFileAsync(path!, default), WorkspaceStatus.Rejected, WorkspaceErrorCode.InvalidPath, "WS-AC-003");
        WorkspaceFixtures.Check(await files.ReadFileAsync("nested/.ENV.example/a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitivePath, "WS-AC-008");
        WorkspaceFixtures.Check(await files.ReadFileAsync(GeminiFixtures.Key, default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-019");
        Assert.IsTrue(fs.Inspected.Count == 0 && fs.Opened == 0, "WS-AC-003");
    }

    [TestMethod]
    public async Task WSAC005_SensitiveRootRejectsBeforeExistenceChecks()
    {
        using var session = WorkspaceFixtures.Session("/safe/.AWS/work"); var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        WorkspaceFixtures.Check(await files.ListFilesAsync(".", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitivePath, "WS-AC-005");
        Assert.IsTrue(fs.Inspected.Count == 0, "WS-AC-005");
    }

    [TestMethod]
    public async Task WSAC006_LinksAndSpecialNodesNeverOpenTargets()
    {
        foreach (var kind in new[] { WorkspaceNodeKind.Link, WorkspaceNodeKind.Other })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            fs.Nodes["/ws/a"] = new(new(kind));
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Rejected, kind == WorkspaceNodeKind.Link ? WorkspaceErrorCode.SymbolicLink : WorkspaceErrorCode.WrongEntryType, "WS-AC-006");
            var list = await files.ListFilesAsync(".", default);
            Assert.IsTrue(list.Data is { Entries.Count: 0, SafetyFiltered: true, Truncated: false } && fs.Opened == 0, "WS-AC-006");
        }
        using var nestedSession = WorkspaceFixtures.Session(); var nested = new ControlledWorkspaceFileSystem();
        nested.Nodes["/ws/link"] = new(new(WorkspaceNodeKind.Link));
        await using var service = new WorkspaceFiles(nestedSession.Workspace, nestedSession.Policy, nested);
        WorkspaceFixtures.Check(await service.ReadFileAsync("link/a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SymbolicLink, "WS-AC-006");
        Assert.IsTrue(nested.Opened == 0 && !nested.Inspected.Contains("/ws/link/a"), "WS-AC-006");
    }

    [TestMethod]
    public async Task WSAC007_InitialAbsenceWrongTypesAndAccessFailuresAreClassified()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.Directory("dir"); fs.File("file", []);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        WorkspaceFixtures.Check(await files.ReadFileAsync("missing", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.NotFound, "WS-AC-007");
        WorkspaceFixtures.Check(await files.ReadFileAsync("dir", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.WrongEntryType, "WS-AC-007");
        WorkspaceFixtures.Check(await files.ReadFileAsync("file/", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.WrongEntryType, "WS-AC-004");
        WorkspaceFixtures.Check(await files.ListFilesAsync("file", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.WrongEntryType, "WS-AC-007");
        fs.OnInspect = path => { if (path.EndsWith("file", StringComparison.Ordinal)) throw new UnauthorizedAccessException(GeminiFixtures.Key); };
        WorkspaceFixtures.Check(await files.ReadFileAsync("file", default), WorkspaceStatus.Failed, WorkspaceErrorCode.AccessDenied, "WS-AC-007");
    }

    [TestMethod]
    public async Task WSAC009_ListIsFirstLevelFilteredAndNeverReadsContents()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        fs.Directory("docs"); fs.File("a", [1]); fs.File(".env", [2]); fs.File("bad\nname", [3]); fs.File("docs/nested", [4]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var list = await files.ListFilesAsync(".", default);
        Assert.IsTrue(list.Data is { Path: ".", Entries.Count: 2, SafetyFiltered: true, Truncated: false }, "WS-AC-009");
        Assert.IsTrue(list.Data!.Entries.Select(entry => entry.Path).SequenceEqual(new[] { "a", "docs" }), "WS-AC-009");
        Assert.IsTrue(fs.Opened == 0 && fs.Enumerated.SequenceEqual(new[] { "/ws" }) && files.Snapshots.Count == 0, "WS-AC-009");
        Assert.IsFalse(fs.Inspected.Contains("/ws/.env"), "WS-AC-009");
        var nested = await files.ListFilesAsync("docs/", default);
        Assert.IsTrue(nested.Data!.Entries[0].Path == "docs/nested", "WS-AC-009");
    }

    [TestMethod]
    public async Task WSAC010_ListKeepsOrdinalPrefixAtEntryLimit()
    {
        foreach (var count in new[] { 199, 200, 201 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            for (var index = count - 1; index >= 0; index--) fs.File(index.ToString("D4", CultureInfo.InvariantCulture), []);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var result = await files.ListFilesAsync(".", default);
            Assert.IsTrue(result.Data!.Entries.Count == Math.Min(count, 200) && result.Data.Truncated == (count > 200), "WS-AC-010");
            Assert.IsTrue(result.Data.Entries[0].Path == "0000" && result.Data.Entries[^1].Path == (Math.Min(count, 200) - 1).ToString("D4", CultureInfo.InvariantCulture), "WS-AC-010");
        }
    }

    [TestMethod]
    public async Task WSAC011_ListNeverObtainsEntry4097EvenWhenHidden()
    {
        foreach (var count in new[] { 4095, 4096, 4100 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            fs.Names = Enumerable.Range(0, count).Select(index => ".env." + index).ToList();
            fs.OnEnumerate = index => { if (index > 4096) Assert.Fail("WS-AC-011"); };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var result = await files.ListFilesAsync(".", default);
            Assert.IsTrue(result.Data!.Entries.Count == 0 && result.Data.Truncated == (count >= 4096) && fs.Scanned == Math.Min(count, 4096), "WS-AC-011");
        }
    }

    [TestMethod]
    public async Task WSAC012_ListTruncatesWholeEntriesAndDropsPartialErrors()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        for (var index = 0; index < 200; index++) fs.File(index.ToString("D3", CultureInfo.InvariantCulture) + new string('"', 1900), []);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var result = await files.ListFilesAsync(".", default);
        Assert.IsTrue(result.Data is { Truncated: true } && result.Data.Entries.Count is > 0 and < 200, "WS-AC-012");
        Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var json, out _) && Encoding.UTF8.GetByteCount(json.GetRawText()) <= 196608, "WS-AC-012");
        fs.OnEnumerate = index => { if (index % 3 == 0) throw new IOException(GeminiFixtures.Key); };
        WorkspaceFixtures.Check(await files.ListFilesAsync(".", default), WorkspaceStatus.Failed, WorkspaceErrorCode.IoFailure, "WS-AC-012");
    }

    [TestMethod]
    public async Task WSAC013_ReadPreservesBomUnicodeNewlinesAndOriginalBytes()
    {
        foreach (var content in new[] { "", "Hello\n", "中文😀\ufeff\r\nLF\nCR\rend", "no-final-newline" })
            foreach (var bom in new[] { false, true })
            {
                using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
                var bytes = (bom ? new byte[] { 239, 187, 191 } : []).Concat(Encoding.UTF8.GetBytes(content)).ToArray(); fs.File("a", bytes);
                await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
                var result = await files.ReadFileAsync("./a", default);
                Assert.IsTrue(result.Data is not null && result.Data.Content == content && result.Data.Path == "a" && result.Data.ByteCount == bytes.Length && result.Data.HasUtf8Bom == bom, "WS-AC-013");
                Assert.IsTrue(files.Snapshots.TryGet("a", out var snapshot) && snapshot!.CopyOriginalBytes().SequenceEqual(bytes) && snapshot.HasUtf8Bom == bom, "WS-AC-013");
                Assert.IsTrue(bytes.SequenceEqual(fs.Nodes["/ws/a"].Bytes), "WS-AC-013");
            }
    }

    [TestMethod]
    public async Task WSAC014_ReadRejectsInvalidUtf8AndOtherEncodingBoms()
    {
        foreach (var bytes in new byte[][] { [255, 254], [254, 255], [255, 254, 0, 0], [0, 0, 254, 255], [192, 175], [237, 160, 128], [240, 159], [164, 164], [255] })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a.txt", bytes);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            WorkspaceFixtures.Check(await files.ReadFileAsync("a.txt", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.InvalidUtf8, "WS-AC-014");
            Assert.IsTrue(files.Snapshots.Count == 0, "WS-AC-014");
        }
    }

    [TestMethod]
    public async Task WSAC015_ReadRejectsControlCharactersButPreservesTextWhitespace()
    {
        foreach (var character in new[] { '\0', '\u001b', '\u007f', '\u0085', '\u0001' })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", Encoding.UTF8.GetBytes("text" + character));
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.BinaryContent, "WS-AC-015");
        }
    }

    [TestMethod]
    public async Task WSAC016_SizeLimitUsesActualReadBytesAndStopsAtSentinel()
    {
        foreach (var length in new[] { 131071, 131072, 131073, 150000 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", Enumerable.Repeat((byte)97, length).ToArray());
            fs.Nodes["/ws/a"].State = fs.Nodes["/ws/a"].State with { Length = Math.Min(length, 131072) };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var result = await files.ReadFileAsync("a", default);
            WorkspaceFixtures.Check(result, length <= 131072 ? WorkspaceStatus.Success : WorkspaceStatus.Rejected, length <= 131072 ? WorkspaceErrorCode.None : WorkspaceErrorCode.FileTooLarge, "WS-AC-016");
            Assert.IsTrue(fs.TotalReadBytes <= 131073, "WS-AC-016");
        }
    }

    [TestMethod]
    public async Task WSAC017_ExactJsonByteBoundaryIncludesEnvelopeAndEscaping()
    {
        // For path "a" and a five-digit byte count, the envelope is 113 UTF-8 bytes.
        foreach (var extra in new[] { 0, 1 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            var content = new string('\\', 98247) + new string('a', 1 + extra);
            // Independently measure the literal wire format, not the production writer.
            var expected = "{\"status\":\"success\",\"error_code\":\"none\",\"data\":{\"path\":\"a\",\"content\":\"" + new string('\\', 196494) + new string('a', 1 + extra) + "\",\"byte_count\":" + (98248 + extra) + ",\"has_utf8_bom\":false}}";
            Assert.IsTrue(Encoding.UTF8.GetByteCount(expected) == 196608 + extra, "WS-AC-017-fixture");
            fs.File("a", Encoding.UTF8.GetBytes(content));
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var result = await files.ReadFileAsync("a", default);
            WorkspaceFixtures.Check(result, extra == 0 ? WorkspaceStatus.Success : WorkspaceStatus.Rejected, extra == 0 ? WorkspaceErrorCode.None : WorkspaceErrorCode.ResultTooLarge, "WS-AC-017");
            Assert.IsTrue(files.Snapshots.Count == (extra == 0 ? 1 : 0), "WS-AC-017");
        }
    }

    [TestMethod]
    public async Task WSAC018_ChangesAfterInspectionNeverPublishAndRootLossClearsAll()
    {
        foreach (var change in new[] { 0, 1, 2, 3 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            Assert.IsTrue((await files.ReadFileAsync("a", default)).Status == WorkspaceStatus.Success, "WS-AC-018");
            fs.OnRead = _ =>
            {
                if (change == 0) fs.Nodes.Remove("/ws/a");
                if (change == 1) fs.Nodes["/ws/a"].State = new(WorkspaceNodeKind.Link);
                if (change == 2) fs.Nodes["/ws/a"].State = fs.Nodes["/ws/a"].State with { ModifiedSeconds = 1 };
                if (change == 3) fs.Nodes["/ws"].State = new(WorkspaceNodeKind.Link);
                return Task.CompletedTask;
            };
            WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), change == 3 ? WorkspaceStatus.Failed : WorkspaceStatus.Rejected, change == 3 ? WorkspaceErrorCode.WorkspaceUnavailable : WorkspaceErrorCode.PathChanged, "WS-AC-018");
            Assert.IsTrue(files.Snapshots.Count == 0, "WS-AC-018");
        }
    }

    [TestMethod]
    public async Task WSAC019_SensitiveActualDataRejectsRatherThanRedacts()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", Encoding.UTF8.GetBytes("before" + GeminiFixtures.Key));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-019");
        fs.File(GeminiFixtures.Key, []);
        WorkspaceFixtures.Check(await files.ListFilesAsync(".", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-020");
        Assert.IsTrue(files.Snapshots.Count == 0, "WS-AC-019");
        using var shortSession = WorkspaceFixtures.Session(key: "content");
        await using var shortFiles = new WorkspaceFiles(shortSession.Workspace, shortSession.Policy, fs);
        fs.File("a", [97]);
        WorkspaceFixtures.Check(await shortFiles.ReadFileAsync("a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-020");
    }

    [TestMethod]
    public async Task WSAC021_ReadOrderEvictionAndCanonicalQueries()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        for (var index = 0; index < 21; index++) fs.File("a" + index, []);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        for (var index = 0; index < 20; index++) await files.ReadFileAsync("a" + index, default);
        Assert.IsTrue(files.Snapshots.Count == 20 && files.Snapshots.TotalBytes == 0, "WS-AC-022");
        files.Snapshots.TryGet("a0", out _); await files.ListFilesAsync(".", default);
        await files.ReadFileAsync("a1", default); await files.ReadFileAsync("a20", default);
        Assert.IsFalse(files.Snapshots.TryGet("a0", out _), "WS-AC-021");
        Assert.IsTrue(files.Snapshots.TryGet("a1", out _), "WS-AC-021");
        foreach (var path in new[] { "./a1", "a1/", "/ws/a1", "a1//.", "A1" }) Assert.IsFalse(files.Snapshots.TryGet(path, out _), "WS-AC-021");
    }

    [TestMethod]
    public async Task WSAC022_ByteCapacityEvictsOldestAndReplacesSamePath()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        for (var index = 0; index < 9; index++) fs.File("a" + index, Enumerable.Repeat((byte)97, 131072).ToArray());
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        for (var index = 0; index < 8; index++) Assert.IsTrue((await files.ReadFileAsync("a" + index, default)).Status == WorkspaceStatus.Success, "WS-AC-022");
        Assert.IsTrue(files.Snapshots.TotalBytes == 1048576 && files.Snapshots.Count == 8, "WS-AC-022");
        await files.ReadFileAsync("a0", default); await files.ReadFileAsync("a8", default);
        Assert.IsFalse(files.Snapshots.TryGet("a1", out _), "WS-AC-022");
        Assert.IsTrue(files.Snapshots.TryGet("a0", out _) && files.Snapshots.TotalBytes == 1048576, "WS-AC-022");
    }

    [TestMethod]
    public async Task WSAC023_FailedRereadInvalidatesButPreCancellationPreservesSnapshot()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        await files.ReadFileAsync("a", default);
        var io = fs.Inspected.Count;
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", new(true)), WorkspaceStatus.Cancelled, WorkspaceErrorCode.None, "WS-AC-023");
        Assert.IsTrue(fs.Inspected.Count == io && files.Snapshots.Count == 1, "WS-AC-023");
        fs.File("a", [255]); await files.ReadFileAsync("a", default);
        Assert.IsFalse(files.Snapshots.TryGet("a", out _), "WS-AC-023");
    }

    [TestMethod]
    public async Task WSAC024_ClearAndCancellationPreventLateSuccess()
    {
        foreach (var clear in new[] { false, true })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            using var cancellation = new CancellationTokenSource();
            fs.OnRead = _ => { if (clear) files.Snapshots.Clear(); else cancellation.Cancel(); return Task.CompletedTask; };
            WorkspaceFixtures.Check(await files.ReadFileAsync("a", cancellation.Token), WorkspaceStatus.Cancelled, WorkspaceErrorCode.None, "WS-AC-024");
            Assert.IsTrue(files.Snapshots.Count == 0 && fs.Handles.All(handle => handle.Disposed), "WS-AC-024");
            fs.OnRead = null; Assert.IsTrue((await files.ReadFileAsync("a", default)).Status == WorkspaceStatus.Success, "WS-AC-024");
            cancellation.Cancel(); Assert.IsTrue(files.Snapshots.Count == 1, "WS-AC-024");
        }
    }

    [TestMethod]
    public async Task WSAC027_ConcurrentAndDisposedCallsDoNoIOAndDisposalWaits()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
        var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fs.OnRead = async _ => { started.TrySetResult(); await finish.Task; };
        var read = files.ReadFileAsync("a", default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var io = fs.Inspected.Count;
        WorkspaceFixtures.Check(await files.ListFilesAsync(".", default), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-027");
        var disposal = files.DisposeAsync().AsTask();
        Assert.IsFalse(disposal.IsCompleted, "WS-AC-027");
        Assert.IsTrue(fs.Inspected.Count == io, "WS-AC-027");
        finish.SetResult();
        WorkspaceFixtures.Check(await read, WorkspaceStatus.Cancelled, WorkspaceErrorCode.None, "WS-AC-027");
        await disposal.WaitAsync(TimeSpan.FromSeconds(5)); await files.DisposeAsync();
        io = fs.Inspected.Count;
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-027");
        Assert.IsTrue(fs.Inspected.Count == io && fs.Handles.All(handle => handle.Disposed) && files.Snapshots.Count == 0, "WS-AC-027");
    }

    [TestMethod]
    public async Task WSAC027_SynchronousFileSystemWorkDoesNotHoldTheAdmissionLock()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fs.OnInspect = _ => { started.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); };
        var first = Task.Run(() => files.ReadFileAsync("a", default));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = Task.Run(() => files.ListFilesAsync(".", default));
        var immediate = await Task.WhenAny(second, Task.Delay(1000)) == second;
        release.Set(); await first;
        Assert.IsTrue(immediate, "WS-AC-027-admission");
        WorkspaceFixtures.Check(await second, WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-027-admission");
    }

    [TestMethod]
    public async Task WSAC027_StreamCloseFailureCannotLeavePublishedSnapshot()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]); fs.FailDispose = true;
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Failed, WorkspaceErrorCode.IoFailure, "WS-AC-027-close");
        Assert.IsTrue(files.Snapshots.Count == 0, "WS-AC-027-close");
    }

    [TestMethod]
    public async Task WSAC028_ExceptionsExposeOnlyFixedStatusAndCode()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", []);
        fs.OnOpen = _ => throw new InvalidOperationException(GeminiFixtures.Key + "/private/outside");
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var result = await files.ReadFileAsync("a", default);
        WorkspaceFixtures.Check(result, WorkspaceStatus.Failed, WorkspaceErrorCode.IoFailure, "WS-AC-028");
        Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var response, out _), "WS-AC-028");
        Assert.IsTrue(response.GetRawText() == "{\"status\":\"failed\",\"error_code\":\"io_failure\"}" && result.ToString() == "[WorkspaceReadResult]", "WS-AC-028");
    }
}