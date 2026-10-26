using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using AIAgent.Cli.Workspace;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIAgent.Cli.Tests;

[TestClass]
[SupportedOSPlatform("macos")]
public sealed class WorkStoreNativeTests
{
    private const string WorkId = "0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task SAVEAC004_MissingStoreQueriesDoNotCreateAndExplicitSaveCreatesPrivateNodes()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual(0, (await store.ListAsync(default)).Count);
        Assert.IsFalse(Directory.Exists(Path.Combine(home, ".aiagent")));
        await AssertCode("work_not_found", () => store.ReadAsync(WorkId, default));
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        foreach (var path in new[] { Path.Combine(home, ".aiagent"), StorePath(home) })
            Assert.AreEqual((UnixFileMode)448, File.GetUnixFileMode(path));
        Assert.AreEqual((UnixFileMode)384, File.GetUnixFileMode(Path.Combine(StorePath(home), ".store.lock")));
    }

    [TestMethod]
    public async Task SAVEAC016_ExclusiveNativePublishReadsExactBytesAndDeleteChecksPreview()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        byte[] bytes = Encoding.UTF8.GetBytes("synthetic saved work");
        var published = await store.PublishAsync(WorkId, bytes, default);
        Assert.AreEqual("none", published.ErrorCode);
        Assert.AreEqual(WorkPublicationState.PublishedVerified, published.PublicationState);
        var file = await store.ReadAsync(WorkId, default);
        CollectionAssert.AreEqual(bytes, file.Bytes.ToArray());
        var conflict = await store.PublishAsync(WorkId, Encoding.UTF8.GetBytes("other"), default);
        Assert.AreEqual("work_conflict", conflict.ErrorCode);
        CollectionAssert.AreEqual(bytes, (await store.ReadAsync(WorkId, default)).Bytes.ToArray());
        Assert.AreEqual(1, (await store.ListAsync(default)).Count);
        var deleted = await store.DeleteAsync(file, default);
        Assert.AreEqual("none", deleted.ErrorCode);
        Assert.AreEqual(WorkDeleteState.DeletedVerified, deleted.DeleteState);
        await AssertCode("work_not_found", () => store.ReadAsync(WorkId, default));
    }

    [TestMethod]
    public async Task SAVEAC006_ReplacedPreviewAndChangedRootNeverDelete()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        Assert.AreEqual("none", (await store.PublishAsync(WorkId, new byte[] { 1 }, default)).ErrorCode);
        var file = await store.ReadAsync(WorkId, default);
        File.WriteAllBytes(Path.Combine(StorePath(home), WorkId + ".json"), [2]);
        var changed = await store.DeleteAsync(file, default);
        Assert.AreEqual("work_conflict", changed.ErrorCode);
        Assert.IsTrue(File.Exists(Path.Combine(StorePath(home), WorkId + ".json")));
        Directory.Move(StorePath(home), StorePath(home) + "-old");
        Directory.CreateDirectory(StorePath(home), (UnixFileMode)448);
        var rootChanged = await store.DeleteAsync(file, default);
        Assert.AreEqual("work_conflict", rootChanged.ErrorCode);
    }

    [TestMethod]
    public async Task SAVEAC004_NativeModeAclLinksAndSpecialFilesFailClosed()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        var path = Path.Combine(StorePath(home), WorkId + ".json");
        File.WriteAllBytes(path, [1]);
        File.SetUnixFileMode(path, (UnixFileMode)420);
        await AssertCode("store_unsafe", () => store.ReadAsync(WorkId, default));
        File.SetUnixFileMode(path, (UnixFileMode)384);
        Assert.AreEqual(0, Link(path, path + ".hard"));
        await AssertCode("store_unsafe", () => store.ReadAsync(WorkId, default));
        File.Delete(path + ".hard");
        await FixtureCommand("/bin/chmod", "+a", "everyone deny delete", path);
        await AssertCode("store_unsafe", () => store.ReadAsync(WorkId, default));
        await FixtureCommand("/bin/chmod", "-N", path);
        File.Delete(path);
        File.CreateSymbolicLink(path, Path.Combine(home, "absent"));
        await AssertCode("store_unsafe", () => store.ReadAsync(WorkId, default));
        File.Delete(path);
        Assert.AreEqual(0, MakeFifo(path, 384));
        await AssertCode("store_unsafe", () => store.ReadAsync(WorkId, default));
    }

    [TestMethod]
    public async Task SAVEAC004_AncestryRejectsWritableDirectoriesAndAcceptsDenyOnlyHomeAcl()
    {
        var home = CreateHome();
        await FixtureCommand("/bin/chmod", "+a", "everyone deny delete", home);
        await using (var safe = new MacWorkSaveStore(home))
            Assert.AreEqual("none", await safe.PreflightSaveAsync(default));
        var unsafeHome = CreateHome();
        File.SetUnixFileMode(unsafeHome, (UnixFileMode)511);
        await using (var unsafeStore = new MacWorkSaveStore(unsafeHome))
            Assert.AreEqual("store_unsafe", await unsafeStore.PreflightSaveAsync(default));
        var tmpHome = Path.Combine("/private/tmp", "aiagent-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpHome, (UnixFileMode)448);
        await using (var tmpStore = new MacWorkSaveStore(tmpHome))
            Assert.AreEqual("store_unsafe", await tmpStore.PreflightSaveAsync(default));
        Assert.IsFalse(Directory.Exists(Path.Combine(tmpHome, ".aiagent")));
    }

    [TestMethod]
    public async Task SAVEAC005_FormalEntriesCountEvenWhenCorruptAndNonformalEnumerationIsBounded()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        for (var index = 0; index < 100; index++)
            File.CreateSymbolicLink(Path.Combine(StorePath(home), index.ToString("x32") + ".json"), "absent");
        Assert.AreEqual("store_limit", await store.PreflightSaveAsync(default));
        Assert.AreEqual("store_limit", (await store.PublishAsync(WorkId, new byte[] { 1 }, default)).ErrorCode);
        var crowdedHome = CreateHome();
        await using var crowded = new MacWorkSaveStore(crowdedHome);
        Assert.AreEqual("none", await crowded.PreflightSaveAsync(default));
        for (var index = 0; index < 1023; index++) File.WriteAllBytes(Path.Combine(StorePath(crowdedHome), "other-" + index), []);
        Assert.AreEqual(0, (await crowded.ListAsync(default)).Count);
        File.WriteAllBytes(Path.Combine(StorePath(crowdedHome), "one-more"), []);
        await AssertCode("store_limit", () => crowded.ListAsync(default));
    }

    [TestMethod]
    public async Task SAVEAC005_ShortNativeTransactionLockReturnsBusyWithoutWaiting()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        var descriptor = Open(Path.Combine(StorePath(home), ".store.lock"), 2);
        Assert.IsTrue(descriptor >= 0);
        try
        {
            Assert.AreEqual(0, Flock(descriptor, 6));
            Assert.AreEqual("store_busy", await store.PreflightSaveAsync(default));
            await AssertCode("store_busy", () => store.ListAsync(default));
        }
        finally { Assert.AreEqual(0, Close(descriptor)); }
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
    }

    [TestMethod]
    public async Task SAVEAC007_NativeDocumentBytesAreBoundedBeforeReading()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home);
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        var max = await store.PublishAsync(WorkId, new byte[65536], default);
        Assert.AreEqual(WorkPublicationState.PublishedVerified, max.PublicationState);
        Assert.AreEqual(65536, (await store.ReadAsync(WorkId, default)).Bytes.Length);
        File.WriteAllBytes(Path.Combine(StorePath(home), WorkId + ".json"), new byte[65537]);
        await AssertCode("store_limit", () => store.ReadAsync(WorkId, default));
        var tooLarge = await store.PublishAsync(new string('a', 32), new byte[65537], default);
        Assert.AreEqual("store_limit", tooLarge.ErrorCode);
    }

    private static string CreateHome()
    {
        if (!OperatingSystem.IsMacOS()) Assert.Inconclusive("Native private-store tests require macOS.");
        var home = Path.Combine(AppContext.BaseDirectory, "work-store-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home, (UnixFileMode)448);
        return home;
    }

    private static string StorePath(string home) => Path.Combine(home, ".aiagent", "work");

    private static async Task AssertCode(string expected, Func<Task> action)
    {
        try { await action(); Assert.Fail("Expected a controlled store rejection."); }
        catch (WorkStoreException exception) { Assert.AreEqual(expected, exception.Code); }
    }

    private static async Task FixtureCommand(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync();
        Assert.AreEqual(0, process.ExitCode, "Native fixture command failed.");
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string source, string target);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo(string path, ushort mode);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int descriptor, int flags);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int descriptor);
}