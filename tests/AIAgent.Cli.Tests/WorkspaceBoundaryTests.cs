using System.Globalization;
using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkspaceBoundaryTests
{
    [TestMethod]
    public async Task WSAC003_LeadingDotsCannotNormalizeIntoDriveForms()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        fs.File("C:a", [97]); fs.Directory("C:"); fs.File("C:/a", [97]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var path in new[] { "./C:a", ".//C:/a", "././c:a" })
        {
            WorkspaceFixtures.Check(await files.ReadFileAsync(path, default), WorkspaceStatus.Rejected, WorkspaceErrorCode.InvalidPath, "WS-AC-003-drive");
            WorkspaceFixtures.Check(await files.ListFilesAsync(path, default), WorkspaceStatus.Rejected, WorkspaceErrorCode.InvalidPath, "WS-AC-003-drive");
        }
        Assert.IsTrue(fs.Inspected.Count == 0 && fs.Opened == 0 && files.Snapshots.Count == 0, "WS-AC-003-drive");
    }

    [TestMethod]
    public async Task WSAC018_RootLossDuringFailingIOClearsAllBaselines()
    {
        foreach (var mode in new[] { "inspect", "open", "enumerate", "read" })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]); fs.File("b", [98]);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            await files.ReadFileAsync("a", default); await files.ReadFileAsync("b", default);
            void LoseRoot() { fs.Nodes.Remove("/ws"); fs.Nodes.Remove("/ws/a"); }
            if (mode == "inspect") fs.OnInspect = path => { if (path == "/ws/a") LoseRoot(); };
            if (mode == "open") fs.OnOpen = _ => LoseRoot();
            if (mode == "enumerate") fs.OnEnumerate = _ => { LoseRoot(); throw new DirectoryNotFoundException("FAKE_ONLY"); };
            if (mode == "read") fs.OnRead = _ => { LoseRoot(); throw new IOException("FAKE_ONLY"); };
            if (mode == "enumerate") WorkspaceFixtures.Check(await files.ListFilesAsync(".", default), WorkspaceStatus.Failed, WorkspaceErrorCode.WorkspaceUnavailable, "WS-AC-018-root-io");
            else WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Failed, WorkspaceErrorCode.WorkspaceUnavailable, "WS-AC-018-root-io");
            Assert.IsTrue(files.Snapshots.Count == 0 && fs.Handles.All(handle => handle.Disposed), "WS-AC-018-root-io");
        }
    }

    [TestMethod]
    public async Task WSAC024_ClearDuringRootFailureStillCancelsTheOldGeneration()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var failing = false;
        fs.OnOpen = _ => { failing = true; throw new IOException("FAKE_ONLY"); };
        fs.OnInspect = path => { if (failing && path == "/ws") { files.Snapshots.Clear(); fs.Nodes.Remove("/ws"); } };
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Cancelled, WorkspaceErrorCode.None, "WS-AC-024-root-clear");
        Assert.IsTrue(files.Snapshots.Count == 0, "WS-AC-024-root-clear");
    }

    [TestMethod]
    public async Task WSAC004_ActualReadsRoundTripLiteralAndNormalizedPaths()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.Directory("docs");
        foreach (var path in new[] { " notes ", "   ", "中文😀", "~", "$HOME", "%HOME%", "docs/a", new string('a', 4096) }) fs.File(path, [97]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var path in new[] { " notes ", "   ", "中文😀", "~", "$HOME", "%HOME%", "docs//./a", new string('a', 4096) })
        {
            var result = await files.ReadFileAsync(path, default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Success && result.Data!.Path == (path == "docs//./a" ? "docs/a" : path), "WS-AC-004");
            Assert.IsTrue(files.Snapshots.TryGet(result.Data!.Path, out _), "WS-AC-004");
        }
    }

    [TestMethod]
    public async Task WSAC008_RestrictedNamesAreRejectedBeforeAnyInspection()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var name in new[] { ".git", ".agents", ".codex", ".aws", ".ssh", ".secrets", "secrets", ".env", ".env.example", "gemini-api-key.txt", "api-key.txt", "appsettings.Local.json", "credentials", "credentials.json", "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519", "identity", "a.secrets.json", "a.pem", "a.key", "a.p12", "a.pfx", "a.ppk", "a.keystore", "a.jks" })
        {
            foreach (var path in new[] { name, "nested/" + name.ToUpperInvariant() + "/child" })
            {
                WorkspaceFixtures.Check(await files.ReadFileAsync(path, default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitivePath, "WS-AC-008");
                WorkspaceFixtures.Check(await files.ListFilesAsync(path, default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitivePath, "WS-AC-008");
            }
        }
        Assert.IsTrue(fs.Inspected.Count == 0 && fs.Opened == 0 && fs.Enumerated.Count == 0, "WS-AC-008");
        foreach (var name in new[] { "mysecrets", "secrets-notes.md", ".gitignore", "appsettings.json" })
        { fs.File(name, [97]); Assert.IsTrue((await files.ReadFileAsync(name, default)).Status == WorkspaceStatus.Success, "WS-AC-008"); }
    }

    [TestMethod]
    public async Task WSAC010_OrderingIsIndependentOfCultureAndEnumerationOrder()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "tr-TR", "zh-TW" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
                foreach (var name in new[] { "中文", "a", "I", "😀", " A", "A", "i" }) fs.File(name, []);
                await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
                var result = await files.ListFilesAsync(".", default);
                Assert.IsTrue(result.Data!.Entries.Select(entry => entry.Path).SequenceEqual(new[] { " A", "A", "I", "a", "i", "中文", "😀" }), "WS-AC-010");
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    public async Task WSAC012_AllowedEntryInspectionFailureRejectsWholeList()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", []); fs.File("b", []);
        fs.OnInspect = path => { if (path == "/ws/b") throw new UnauthorizedAccessException("FAKE_ACCESS_ONLY"); };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        WorkspaceFixtures.Check(await files.ListFilesAsync(".", default), WorkspaceStatus.Failed, WorkspaceErrorCode.AccessDenied, "WS-AC-012");
        Assert.IsTrue(fs.EnumeratorsClosed == 1, "WS-AC-012");
    }

    [TestMethod]
    public async Task WSAC015_AllForbiddenControlsAndPermittedWhitespaceAreHandled()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var number in Enumerable.Range(0, 32).Concat(Enumerable.Range(127, 33)))
        {
            fs.File("a", Encoding.UTF8.GetBytes(((char)number).ToString()));
            var permitted = number is 9 or 10 or 13;
            WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), permitted ? WorkspaceStatus.Success : WorkspaceStatus.Rejected, permitted ? WorkspaceErrorCode.None : WorkspaceErrorCode.BinaryContent, "WS-AC-015");
        }
    }

    [TestMethod]
    public async Task WSAC016_BomCountsAgainstRawByteLimit()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var size in new[] { 131071, 131072, 131073 })
        {
            fs.File("a", new byte[] { 239, 187, 191 }.Concat(Enumerable.Repeat((byte)97, size - 3)).ToArray());
            var result = await files.ReadFileAsync("a", default);
            if (size <= 131072) Assert.IsTrue(result.Data is { HasUtf8Bom: true } && result.Data.ByteCount == size && result.Data.Content.Length == size - 3, "WS-AC-016");
            else WorkspaceFixtures.Check(result, WorkspaceStatus.Rejected, WorkspaceErrorCode.FileTooLarge, "WS-AC-016");
        }
    }

    [TestMethod]
    public async Task WSAC018_DisappearanceBeforeOpenAndRootLossAreDistinct()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]); fs.File("b", [98]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        await files.ReadFileAsync("a", default); await files.ReadFileAsync("b", default);
        fs.OnOpen = path => fs.Nodes.Remove(path);
        WorkspaceFixtures.Check(await files.ReadFileAsync("a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.PathChanged, "WS-AC-018");
        Assert.IsTrue(files.Snapshots.Count == 1, "WS-AC-018");
        fs.Nodes.Remove("/ws");
        WorkspaceFixtures.Check(await files.ReadFileAsync("missing", default), WorkspaceStatus.Failed, WorkspaceErrorCode.WorkspaceUnavailable, "WS-AC-018");
        Assert.IsTrue(files.Snapshots.Count == 0, "WS-AC-018");
    }

    [TestMethod]
    public async Task WSAC019_NormalizedCombinedAndEscapedValuesUseActualKeyPolicy()
    {
        using var session = WorkspaceFixtures.Session(key: "a/b"); var fs = new ControlledWorkspaceFileSystem(); fs.Directory("a"); fs.File("a/b", []);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        WorkspaceFixtures.Check(await files.ReadFileAsync("a//b", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-019");
        WorkspaceFixtures.Check(await files.ListFilesAsync("a", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-019");
        using var escapedSession = WorkspaceFixtures.Session(key: "x\"y\\z"); fs.File("safe", Encoding.UTF8.GetBytes("x\"y\\z"));
        await using var escaped = new WorkspaceFiles(escapedSession.Workspace, escapedSession.Policy, fs);
        WorkspaceFixtures.Check(await escaped.ReadFileAsync("safe", default), WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitiveContent, "WS-AC-019");
    }

    [TestMethod]
    public async Task WSAC021_RereadUpdatesBomBytesFingerprintAndOrdinalKeys()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        await files.ReadFileAsync("a", default); files.Snapshots.TryGet("a", out var old);
        fs.File("a", [239, 187, 191, 98]); await files.ReadFileAsync("./a", default);
        Assert.IsTrue(files.Snapshots.TryGet("a", out var updated) && updated!.HasUtf8Bom && updated.OriginalByteCount == 4 && updated.Sha256Hex != old!.Sha256Hex, "WS-AC-021");
        Assert.IsTrue(old!.CopyOriginalBytes().SequenceEqual(new byte[] { 97 }), "WS-AC-021");
        foreach (var path in new[] { "A", "é", "e\u0301" }) { fs.File(path, [99]); await files.ReadFileAsync(path, default); }
        var io = fs.Inspected.Count;
        Assert.IsTrue(files.Snapshots.Count == 4 && files.Snapshots.TryGet("é", out _) && files.Snapshots.TryGet("e\u0301", out _) && fs.Inspected.Count == io, "WS-AC-021");
    }

    [TestMethod]
    public async Task WSAC023_EveryFailedRereadDropsThePreviousBaseline()
    {
        foreach (var mode in new[] { "missing", "access", "size", "binary", "sensitive", "json", "io", "cancel" })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ReadFileAsync("a", default);
            using var cancellation = new CancellationTokenSource();
            if (mode == "missing") fs.Nodes.Remove("/ws/a");
            if (mode == "access") fs.OnOpen = _ => throw new UnauthorizedAccessException("FAKE_ONLY");
            if (mode == "size") fs.File("a", new byte[131073]);
            if (mode == "binary") fs.File("a", [0]);
            if (mode == "sensitive") fs.File("a", Encoding.UTF8.GetBytes(GeminiFixtures.Key));
            if (mode == "json") fs.File("a", Encoding.UTF8.GetBytes(new string('"', 100000)));
            if (mode == "io") fs.OnRead = _ => throw new IOException("FAKE_ONLY");
            if (mode == "cancel") fs.OnRead = _ => { cancellation.Cancel(); return Task.CompletedTask; };
            Assert.IsTrue((await files.ReadFileAsync("a", cancellation.Token)).Status != WorkspaceStatus.Success && !files.Snapshots.TryGet("a", out _), "WS-AC-023");
        }
    }

    [TestMethod]
    public async Task WSAC024_ListCancellationOrClearClosesEnumeratorWithoutPartialData()
    {
        foreach (var clear in new[] { false, true })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", []); fs.File("b", []);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            using var cancellation = new CancellationTokenSource();
            fs.OnEnumerate = _ => { if (clear) files.Snapshots.Clear(); else cancellation.Cancel(); };
            WorkspaceFixtures.Check(await files.ListFilesAsync(".", cancellation.Token), WorkspaceStatus.Cancelled, WorkspaceErrorCode.None, "WS-AC-024");
            Assert.IsTrue(fs.Scanned == 1 && fs.EnumeratorsClosed == 1, "WS-AC-024");
        }
    }

    [TestMethod]
    public async Task WSAC027_CancellationCallbackFailureStillWaitsAndClearsResources()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]); fs.File("b", [98]);
        var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ReadFileAsync("b", default);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fs.OnRead = async token =>
        {
            using var registration = token.Register(() => throw new InvalidOperationException("FAKE_CALLBACK_ONLY"));
            started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        var read = files.ReadFileAsync("a", default); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { await files.DisposeAsync(); } catch (InvalidOperationException) { }
        Assert.IsTrue((await read).Status == WorkspaceStatus.Cancelled && fs.Handles.All(handle => handle.Disposed) && files.Snapshots.Count == 0, "WS-AC-027");
    }
}