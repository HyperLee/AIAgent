using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Native = AIAgent.Cli.Workspace.MacWorkStoreNative;

namespace AIAgent.Cli.Workspace;

/// <summary>Purpose-limited byte storage. Every operation obtains and revalidates its own directory chain.</summary>
internal sealed class MacWorkSaveStore : IWorkSaveStore
{
    private const int MaxBytes = 65536;
    private const int MaxFiles = 100;
    private const int MaxEntries = 1024;
    private readonly string _home;
    private readonly Action<string>? _testBoundary;
    private readonly object _gate = new();
    private TaskCompletionSource? _idle;
    private bool _active;
    private bool _disposeRequested;
    private bool _stopped;
    private (ulong Device, ulong Identity)[]? _knownChain;

    internal MacWorkSaveStore() : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) { }
    internal MacWorkSaveStore(string testHome, Action<string>? testBoundary = null) { _home = testHome; _testBoundary = testBoundary; }
    public bool Stopped { get { lock (_gate) return _stopped || _disposeRequested; } }

    public Task<string> PreflightSaveAsync(CancellationToken token)
    {
        try
        {
            Query(operation =>
            {
                OpenRoot(operation, create: true);
                AcquireLock(operation, create: true);
                CheckCapacity(Enumerate(operation));
                Revalidate(operation);
                return true;
            }, token);
            return Task.FromResult("none");
        }
        catch (WorkStoreException exception) { return Task.FromResult(exception.Code); }
    }

    public Task<IReadOnlyList<WorkStoreFile>> ListAsync(CancellationToken token) => Task.FromResult(Query<IReadOnlyList<WorkStoreFile>>(operation =>
    {
        if (!OpenRoot(operation, create: false)) return Array.Empty<WorkStoreFile>();
        AcquireLock(operation, create: false);
        var names = Enumerate(operation);
        if (names.Count(IsFormalName) > MaxFiles) Fail("store_limit");
        var result = new List<WorkStoreFile>();
        foreach (var name in names.Where(IsFormalName).Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            result.Add(ReadFile(operation, name[..32], missingCode: "work_conflict"));
        }
        if (!names.Order(StringComparer.Ordinal).SequenceEqual(Enumerate(operation).Order(StringComparer.Ordinal))) Fail("work_conflict");
        foreach (var file in result)
        {
            token.ThrowIfCancellationRequested();
            if (!file.Matches(ReadFile(operation, file.WorkId, "work_conflict"))) Fail("work_conflict");
        }
        Revalidate(operation);
        return result.AsReadOnly();
    }, token));

    public Task<WorkStoreFile> ReadAsync(string workId, CancellationToken token) => Task.FromResult(Query(operation =>
    {
        ValidateId(workId);
        if (!OpenRoot(operation, create: false)) Fail("work_not_found");
        AcquireLock(operation, create: false);
        var file = ReadFile(operation, workId, "work_not_found");
        token.ThrowIfCancellationRequested();
        Revalidate(operation);
        return file;
    }, token));

    public Task<WorkStoreMutation> PublishAsync(string workId, ReadOnlyMemory<byte> bytes, CancellationToken token) => Task.FromResult(Mutate(operation =>
    {
        ValidateId(workId);
        if (bytes.Length > MaxBytes) Fail("store_limit");
        var ownedBytes = bytes.ToArray();
        if (!OpenRoot(operation, create: false)) Fail("work_conflict");
        AcquireLock(operation, create: false);
        CheckCapacity(Enumerate(operation));
        var name = workId + ".json";
        EnsureAbsent(operation.Root, name, "work_conflict");
        Revalidate(operation);
        token.ThrowIfCancellationRequested();
        operation.TemporaryName = ".aiagent-work-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)) + ".tmp";
        var descriptor = Native.OpenAt(operation.Root, operation.TemporaryName,
            Native.ReadWrite | Native.CreateExclusive | Native.NoFollow | Native.CloseOnExec | Native.NonBlocking, 384);
        if (descriptor < 0) Fail("work_publish_failed");
        operation.Track(descriptor);
        operation.OwnTemporary = true;
        operation.TemporaryState = GetState(descriptor);
        ValidateFile(descriptor, operation.TemporaryState.Value);
        var offset = 0;
        while (offset < ownedBytes.Length)
        {
            token.ThrowIfCancellationRequested();
            var remaining = ownedBytes.AsSpan(offset).ToArray();
            var written = Native.Write(descriptor, remaining, (nuint)remaining.Length, offset);
            if (written <= 0 || written > remaining.Length) Fail("work_publish_failed");
            offset += checked((int)written);
        }
        _testBoundary?.Invoke("before_flush");
        if (Native.Flush(descriptor) != 0) Fail("work_publish_failed");
        var ready = GetState(descriptor);
        ValidateFile(descriptor, ready);
        if (ready.Size != ownedBytes.Length || !operation.TemporaryState.Value.SameIdentity(ready)) Fail("work_conflict");
        operation.TemporaryState = ready;
        if (!ReadBytes(descriptor, ready).AsSpan().SequenceEqual(ownedBytes)) Fail("work_publish_failed");
        RequireNamedIdentity(operation.Root, operation.TemporaryName, ready);
        Revalidate(operation);
        EnsureAbsent(operation.Root, name, "work_conflict");
        token.ThrowIfCancellationRequested();
        _testBoundary?.Invoke("before_publish");
        token.ThrowIfCancellationRequested();
        // The publication decision is now owned. Cancellation cannot bypass settlement.
        operation.PublicationState = WorkPublicationState.PublishedUnconfirmed;
        var published = Native.Publish(operation.Root, operation.TemporaryName, operation.Root, name, 4);
        if (published != 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error is 17 or 22 or 45 or 78 or 102)
            {
                operation.PublicationState = WorkPublicationState.NotPublished;
                Fail(error == 17 ? "work_conflict" : "work_publish_failed");
            }
            Fail("work_publish_unconfirmed");
        }
        operation.OwnTemporary = false;
        _testBoundary?.Invoke("after_publish");
        _testBoundary?.Invoke("before_readback");
        var actual = ReadFile(operation, workId, "work_publish_unconfirmed");
        if (actual.FileState != ready.ToState() || !actual.Bytes.Span.SequenceEqual(ownedBytes)) Fail("work_publish_unconfirmed");
        Revalidate(operation);
        operation.PublicationState = WorkPublicationState.PublishedVerified;
    }, token));

    public Task<WorkStoreMutation> DeleteAsync(WorkStoreFile expected, CancellationToken token) => Task.FromResult(Mutate(operation =>
    {
        ValidateId(expected.WorkId);
        if (!OpenRoot(operation, create: false)) Fail("work_conflict");
        AcquireLock(operation, create: false);
        var root = operation.Directories[^1].State;
        if (expected.RootDevice != (uint)root.Device || expected.RootIdentity != root.Inode) Fail("work_conflict");
        var actual = ReadFile(operation, expected.WorkId, "work_conflict");
        if (!expected.Matches(actual)) Fail("work_conflict");
        Revalidate(operation);
        RequireNamedIdentity(operation.Root, expected.WorkId + ".json", actual.FileState);
        token.ThrowIfCancellationRequested();
        operation.DeleteState = WorkDeleteState.DeleteUnconfirmed;
        _testBoundary?.Invoke("before_delete_unlink");
        if (Native.Unlink(operation.Root, expected.WorkId + ".json", 0) != 0) Fail("work_delete_unconfirmed");
        _testBoundary?.Invoke("after_delete_unlink");
        // Never retry unlink, including when another item appears under the same name.
        EnsureAbsent(operation.Root, expected.WorkId + ".json", "work_delete_unconfirmed");
        Revalidate(operation);
        operation.DeleteState = WorkDeleteState.DeletedVerified;
    }, token));

    private T Query<T>(Func<Operation, T> action, CancellationToken token)
    {
        var operation = Begin();
        T result = default!;
        var error = "none";
        try { token.ThrowIfCancellationRequested(); EnsureSupported(); result = action(operation); }
        catch (WorkStoreException exception) { error = exception.Code; }
        catch (OperationCanceledException) { error = "work_cancelled"; }
        catch (Exception exception) when (IsControlledFailure(exception)) { error = "store_unavailable"; }
        finally { operation.Close(); End(operation); }
        if (operation.CleanupFailed) Fail("work_cleanup_failed");
        if (error != "none") Fail(error);
        return result;
    }

    private WorkStoreMutation Mutate(Action<Operation> action, CancellationToken token)
    {
        Operation operation;
        try { operation = Begin(); }
        catch (WorkStoreException exception) { return new(exception.Code, storeStopped: Stopped); }
        var error = "none";
        try { token.ThrowIfCancellationRequested(); EnsureSupported(); action(operation); }
        catch (WorkStoreException exception) { error = exception.Code; }
        catch (OperationCanceledException) { error = "work_cancelled"; }
        catch (Exception exception) when (IsControlledFailure(exception)) { error = "store_unavailable"; }
        finally { operation.Close(); End(operation); }
        if (operation.PublicationState == WorkPublicationState.PublishedUnconfirmed) error = "work_publish_unconfirmed";
        else if (operation.DeleteState == WorkDeleteState.DeleteUnconfirmed) error = "work_delete_unconfirmed";
        else if (operation.CleanupFailed) error = "work_cleanup_failed";
        return new(error, operation.PublicationState, operation.DeleteState, operation.CleanupFailed, Stopped,
            operation.CleanupFailed && operation.OwnTemporary ? operation.TemporaryName : null);
    }

    private Operation Begin()
    {
        lock (_gate)
        {
            if (_stopped || _disposeRequested) Fail("store_unavailable");
            if (_active) Fail("store_busy");
            _active = true;
            _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new Operation(_testBoundary);
        }
    }

    private void End(Operation operation)
    {
        lock (_gate)
        {
            if (operation.CleanupFailed || operation.PublicationState == WorkPublicationState.PublishedUnconfirmed ||
                operation.DeleteState == WorkDeleteState.DeleteUnconfirmed) _stopped = true;
            _active = false;
            _idle?.TrySetResult();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposeRequested = true;
            return _active && _idle is not null ? new ValueTask(_idle.Task) : ValueTask.CompletedTask;
        }
    }

    private bool OpenRoot(Operation operation, bool create)
    {
        if (string.IsNullOrEmpty(_home) || !_home.StartsWith('/') || _home == "/" || _home.EndsWith('/') || _home.Contains('\0')) Fail("store_unsafe");
        var homeParts = _home[1..].Split('/');
        if (homeParts.Any(part => part.Length == 0 || part is "." or "..")) Fail("store_unsafe");
        var root = Native.Open("/", Native.NoFollow | Native.CloseOnExec | Native.Directory | Native.NonBlocking);
        if (root < 0) Fail("store_unavailable");
        operation.Track(root);
        var rootState = GetState(root);
        ValidateDirectory(root, rootState, requireUser: false, privateMode: false);
        operation.Directories.Add(new(root, "/", rootState, false, false));
        var parts = homeParts.Concat([".aiagent", "work"]).ToArray();
        for (var index = 0; index < parts.Length; index++)
        {
            var isPrivate = index >= homeParts.Length;
            var requireUser = index >= homeParts.Length - 1;
            var parent = operation.Directories[^1].Descriptor;
            if (Native.StatAt(parent, parts[index], out var before) != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error != 2) Fail(error is 20 or 40 or 62 ? "store_unsafe" : "store_unavailable");
                if (!isPrivate) Fail("store_unsafe");
                if (!create)
                {
                    Revalidate(operation);
                    if (_knownChain is not null) Fail("work_conflict");
                    return false;
                }
                Revalidate(operation);
                if (Native.MakeDirectoryAt(parent, parts[index], 448) != 0 && Marshal.GetLastPInvokeError() != 17) Fail("store_unavailable");
                if (Native.StatAt(parent, parts[index], out before) != 0) Fail("store_unsafe");
            }
            CheckDirectoryState(before, requireUser, isPrivate);
            var descriptor = Native.OpenAt(parent, parts[index], Native.NoFollow | Native.CloseOnExec | Native.Directory | Native.NonBlocking);
            if (descriptor < 0) Fail("store_unsafe");
            operation.Track(descriptor);
            var state = GetState(descriptor);
            ValidateDirectory(descriptor, state, requireUser, isPrivate);
            if (!before.SameIdentity(state) || before.Mode != state.Mode || before.User != state.User) Fail("work_conflict");
            RequireNamedIdentity(parent, parts[index], state);
            operation.Directories.Add(new(descriptor, parts[index], state, requireUser, isPrivate));
        }
        Revalidate(operation);
        var identities = operation.Directories.Select(item => ((ulong)(uint)item.State.Device, item.State.Inode)).ToArray();
        if (_knownChain is not null && !_knownChain.SequenceEqual(identities)) Fail("work_conflict");
        _knownChain ??= identities;
        return true;
    }

    private static void AcquireLock(Operation operation, bool create)
    {
        const string name = ".store.lock";
        var present = Native.StatAt(operation.Root, name, out var before) == 0;
        if (!present && Marshal.GetLastPInvokeError() != 2) Fail("store_unsafe");
        if (!present && !create) Fail("store_unsafe");
        if (present) CheckFileState(before);
        var flags = Native.ReadWrite | Native.NoFollow | Native.CloseOnExec | Native.NonBlocking;
        if (!present) flags |= Native.CreateExclusive;
        var descriptor = Native.OpenAt(operation.Root, name, flags, 384);
        if (descriptor < 0)
        {
            if (!present && Marshal.GetLastPInvokeError() == 17) Fail("store_busy");
            Fail("store_unsafe");
        }
        operation.Track(descriptor);
        var state = GetState(descriptor);
        ValidateFile(descriptor, state);
        if (state.Size != 0 || present && !before.SameIdentity(state)) Fail("store_unsafe");
        RequireNamedIdentity(operation.Root, name, state);
        if (Native.Lock(descriptor, 2 | 4) != 0) Fail(Marshal.GetLastPInvokeError() == 35 ? "store_busy" : "store_unavailable");
        operation.LockDescriptor = descriptor;
        operation.LockState = state;
        Revalidate(operation);
    }

    private static List<string> Enumerate(Operation operation)
    {
        Revalidate(operation);
        var before = GetState(operation.Root);
        var descriptor = Native.OpenAt(operation.Root, ".", Native.Directory | Native.NoFollow | Native.CloseOnExec | Native.NonBlocking);
        if (descriptor < 0) Fail("store_unavailable");
        var directory = Native.OpenDirectory(descriptor);
        if (directory == IntPtr.Zero)
        {
            if (Native.Close(descriptor) != 0) operation.CleanupFailed = true;
            Fail("store_unavailable");
        }
        var names = new List<string>();
        try
        {
            while (true)
            {
                Marshal.WriteInt32(Native.ErrorPointer(), 0);
                var entry = Native.ReadDirectory(directory);
                if (entry == IntPtr.Zero)
                {
                    if (Marshal.GetLastPInvokeError() != 0) Fail("store_unavailable");
                    break;
                }
                // Darwin inode64 dirent: inode(8), seekoff(8), reclen(2), namlen(2), type(1), name.
                var length = (ushort)Marshal.ReadInt16(entry, 18);
                var recordLength = (ushort)Marshal.ReadInt16(entry, 16);
                if (length == 0 || length > 1023 || recordLength < 22 + length) Fail("store_unsafe");
                var raw = new byte[length];
                Marshal.Copy(IntPtr.Add(entry, 21), raw, 0, length);
                if (raw.AsSpan().SequenceEqual("."u8) || raw.AsSpan().SequenceEqual(".."u8)) continue;
                if (names.Count == MaxEntries) Fail("store_limit");
                // Non-ASCII names are never formal and are counted without opening their contents.
                names.Add(raw.Any(value => value > 127 || value == 0) ? "\u0080" + names.Count : System.Text.Encoding.ASCII.GetString(raw));
            }
        }
        finally { if (Native.CloseDirectory(directory) != 0) operation.CleanupFailed = true; }
        if (!before.SameContentState(GetState(operation.Root))) Fail("work_conflict");
        Revalidate(operation);
        return names;
    }

    private static WorkStoreFile ReadFile(Operation operation, string workId, string missingCode)
    {
        var name = workId + ".json";
        if (Native.StatAt(operation.Root, name, out var before) != 0) Fail(Marshal.GetLastPInvokeError() == 2 ? missingCode : "store_unsafe");
        CheckFileState(before);
        if (before.Size < 0 || before.Size > MaxBytes) Fail("store_limit");
        var descriptor = Native.OpenAt(operation.Root, name, Native.NoFollow | Native.CloseOnExec | Native.NonBlocking);
        if (descriptor < 0) Fail(Marshal.GetLastPInvokeError() == 2 ? "work_conflict" : "store_unsafe");
        operation.Track(descriptor);
        var state = GetState(descriptor);
        ValidateFile(descriptor, state);
        if (!before.SameContentState(state)) Fail("work_conflict");
        var bytes = ReadBytes(descriptor, state);
        var second = ReadBytes(descriptor, state);
        if (!bytes.AsSpan().SequenceEqual(second)) Fail("work_conflict");
        ValidateFile(descriptor, GetState(descriptor));
        RequireNamedIdentity(operation.Root, name, state);
        var root = operation.Directories[^1].State;
        return new(workId, bytes, state.ToState(), (uint)root.Device, root.Inode);
    }

    private static byte[] ReadBytes(int descriptor, Native.Stat expected)
    {
        if (expected.Size < 0 || expected.Size > MaxBytes) Fail("store_limit");
        var buffer = new byte[MaxBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var chunk = new byte[buffer.Length - total];
            var read = Native.Read(descriptor, chunk, (nuint)chunk.Length, total);
            if (read < 0 || read > chunk.Length) Fail("store_unavailable");
            if (read == 0) break;
            chunk.AsSpan(0, (int)read).CopyTo(buffer.AsSpan(total));
            total += checked((int)read);
        }
        if (total > MaxBytes) Fail("store_limit");
        if (total != expected.Size || !expected.SameContentState(GetState(descriptor))) Fail("work_conflict");
        return buffer.AsSpan(0, total).ToArray();
    }

    private static void Revalidate(Operation operation)
    {
        for (var index = 0; index < operation.Directories.Count; index++)
        {
            var directory = operation.Directories[index];
            var state = GetState(directory.Descriptor);
            ValidateDirectory(directory.Descriptor, state, directory.RequireUser, directory.PrivateMode);
            if (!directory.State.SameIdentity(state)) Fail("work_conflict");
            if (index > 0) RequireNamedIdentity(operation.Directories[index - 1].Descriptor, directory.Name, state);
        }
        if (operation.LockDescriptor >= 0)
        {
            var state = GetState(operation.LockDescriptor);
            ValidateFile(operation.LockDescriptor, state);
            if (!state.SameContentState(operation.LockState)) Fail("work_conflict");
            RequireNamedIdentity(operation.Root, ".store.lock", state);
        }
    }

    private static void ValidateDirectory(int descriptor, Native.Stat state, bool requireUser, bool privateMode)
    {
        CheckDirectoryState(state, requireUser, privateMode);
        ValidateAcl(descriptor, allowSafeAncestors: !privateMode);
    }

    private static void CheckDirectoryState(Native.Stat state, bool requireUser, bool privateMode)
    {
        var user = Native.GetUser();
        if ((state.Mode & 0xf000) != 0x4000 || (requireUser ? state.User != user : state.User != user && state.User != 0) ||
            (privateMode ? (state.Mode & 0x0fff) != 448 : (state.Mode & 0x0c12) != 0)) Fail("store_unsafe");
    }

    private static void CheckFileState(Native.Stat state)
    {
        if ((state.Mode & 0xf000) != 0x8000 || (state.Mode & 0x0fff) != 384 || state.User != Native.GetUser() || state.Links != 1) Fail("store_unsafe");
    }

    private static void ValidateFile(int descriptor, Native.Stat state)
    {
        CheckFileState(state);
        ValidateAcl(descriptor, allowSafeAncestors: false);
    }

    private static void ValidateAcl(int descriptor, bool allowSafeAncestors)
    {
        var acl = Native.GetAcl(descriptor, 0x100);
        if (acl == IntPtr.Zero)
        {
            // Darwin reports ENOENT for an absent extended ACL, including root-owned
            // system directories. This descriptor still has to name a live object;
            // unsupported ACL retrieval and all other errors remain unsafe.
            if (Marshal.GetLastPInvokeError() == 2 && Native.StatHandle(descriptor, out _) == 0) return;
            Fail("store_unsafe");
        }
        try
        {
            if (Native.ValidateAcl(acl) != 0) Fail("store_unsafe");
            for (var index = 0; index <= 169; index++)
            {
                if (Native.GetAclEntry(acl, index == 0 ? 0 : -1, out var entry) != 0)
                {
                    if (Marshal.GetLastPInvokeError() == 22) return;
                    Fail("store_unsafe");
                }
                if (index == 169 || !allowSafeAncestors) Fail("store_unsafe");
                if (Native.GetAclTag(entry, out var tag) != 0 || tag is not (1 or 2)) Fail("store_unsafe");
                if (tag == 2) continue; // A normal home deny-delete ACE does not grant another user access.
                if (Native.GetPermissions(entry, out var permissions) != 0) Fail("store_unsafe");
                var dangerous = false;
                foreach (var permission in new[] { 1 << 2, 1 << 4, 1 << 5, 1 << 6, 1 << 8, 1 << 10, 1 << 12, 1 << 13 })
                {
                    var has = Native.HasPermission(permissions, permission);
                    if (has < 0) Fail("store_unsafe");
                    dangerous |= has != 0;
                }
                if (!dangerous) continue;
                var qualifier = Native.GetQualifier(entry);
                if (qualifier == IntPtr.Zero) Fail("store_unsafe");
                try
                {
                    if (Native.ResolveIdentity(qualifier, out var identity, out var type) != 0 || type != 0 ||
                        identity != 0 && identity != Native.GetUser()) Fail("store_unsafe");
                }
                finally { if (Native.FreeAcl(qualifier) != 0) Fail("store_unsafe"); }
            }
            Fail("store_unsafe");
        }
        finally { if (Native.FreeAcl(acl) != 0) Fail("store_unsafe"); }
    }

    private static Native.Stat GetState(int descriptor)
    {
        if (Native.StatHandle(descriptor, out var state) != 0) Fail("store_unavailable");
        return state;
    }

    private static void RequireNamedIdentity(int directory, string name, Native.Stat expected)
    {
        if (Native.StatAt(directory, name, out var current) != 0 || !current.SameContentState(expected)) Fail("work_conflict");
    }

    private static void RequireNamedIdentity(int directory, string name, WorkspaceNodeState expected)
    {
        if (Native.StatAt(directory, name, out var current) != 0 || current.ToState() != expected) Fail("work_conflict");
    }

    private static void EnsureAbsent(int directory, string name, string errorCode)
    {
        if (Native.StatAt(directory, name, out _) == 0 || Marshal.GetLastPInvokeError() != 2) Fail(errorCode);
    }

    private static void CheckCapacity(IReadOnlyCollection<string> names)
    {
        // A new temporary/formal entry must also fit the direct-entry ceiling.
        if (names.Count >= MaxEntries || names.Count(IsFormalName) >= MaxFiles) Fail("store_limit");
    }

    private static bool IsFormalName(string name) => name.Length == 37 && name.EndsWith(".json", StringComparison.Ordinal) && IsId(name[..32]);
    private static bool IsId(string? id) => id is { Length: 32 } && id.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void ValidateId(string id) { if (!IsId(id)) Fail("invalid_work_id"); }
    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture is not (Architecture.Arm64 or Architecture.X64)) Fail("store_unavailable");
    }
    private static bool IsControlledFailure(Exception exception) => exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string code) => throw new WorkStoreException(code);
    public override string ToString() => "[MacWorkSaveStore]";

    private sealed class DirectoryNode(int descriptor, string name, Native.Stat state, bool requireUser, bool privateMode)
    {
        internal int Descriptor { get; } = descriptor;
        internal string Name { get; } = name;
        internal Native.Stat State { get; } = state;
        internal bool RequireUser { get; } = requireUser;
        internal bool PrivateMode { get; } = privateMode;
        public override string ToString() => "[WorkStoreDirectory]";
    }

    private sealed class Operation(Action<string>? testBoundary)
    {
        private readonly List<int> _descriptors = [];
        internal List<DirectoryNode> Directories { get; } = [];
        internal int Root => Directories[^1].Descriptor;
        internal int LockDescriptor { get; set; } = -1;
        internal Native.Stat LockState { get; set; }
        internal WorkPublicationState PublicationState { get; set; }
        internal WorkDeleteState DeleteState { get; set; }
        internal bool CleanupFailed { get; set; }
        internal bool OwnTemporary { get; set; }
        internal string? TemporaryName { get; set; }
        internal Native.Stat? TemporaryState { get; set; }
        internal void Track(int descriptor) => _descriptors.Add(descriptor);

        internal void Close()
        {
            if (OwnTemporary)
            {
                try
                {
                    if (TemporaryName is null || TemporaryState is null) Fail("work_cleanup_failed");
                    Revalidate(this);
                    if (Native.StatAt(Root, TemporaryName, out var current) != 0 || !current.SameIdentity(TemporaryState.Value) ||
                        current.ToState().Kind != WorkspaceNodeKind.File || current.User != Native.GetUser() || current.Links != 1) Fail("work_cleanup_failed");
                    testBoundary?.Invoke("before_temp_unlink");
                    if (Native.Unlink(Root, TemporaryName, 0) != 0) Fail("work_cleanup_failed");
                    EnsureAbsent(Root, TemporaryName, "work_cleanup_failed");
                    OwnTemporary = false;
                }
                catch (Exception exception) when (IsControlledFailure(exception)) { CleanupFailed = true; }
            }
            try { testBoundary?.Invoke("before_close"); }
            catch (Exception exception) when (IsControlledFailure(exception)) { CleanupFailed = true; }
            for (var index = _descriptors.Count - 1; index >= 0; index--)
            {
                try { if (Native.Close(_descriptors[index]) != 0) CleanupFailed = true; }
                catch (Exception exception) when (IsControlledFailure(exception)) { CleanupFailed = true; }
            }
        }
    }
}