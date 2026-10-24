using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace AIAgent.Cli.Workspace;

/// <summary>macOS 的無跟隨種類檢查；不以「非目錄」推論普通檔案。</summary>
internal sealed class MacWorkspaceFileSystem : IWorkspaceWriteFileSystem
{
    public WorkspaceNodeState Inspect(string fullPath)
    {
        EnsureSupported();
        var code = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? StatPathX64(fullPath, out var state) : StatPath(fullPath, out state);
        if (code == 0) return ConvertState(state);
        var error = Marshal.GetLastPInvokeError();
        if (error is 2 or 20) return new(WorkspaceNodeKind.Missing);
        ThrowError(error);
        return default;
    }

    public IEnumerable<string> EnumerateNames(string fullDirectoryPath)
    {
        EnsureSupported();
        foreach (var path in Directory.EnumerateFileSystemEntries(fullDirectoryPath)) yield return Path.GetFileName(path);
    }

    public IWorkspaceReadFile OpenRead(string fullPath)
    {
        EnsureSupported();
        var handle = File.OpenHandle(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try { return new ReadFile(new FileStream(handle, FileAccess.Read, 4096, isAsync: true)); }
        catch { handle.Dispose(); throw; }
    }

    public UnixFileMode GetMode(string fullPath)
    {
        EnsureSupported();
        var code = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? StatPathX64(fullPath, out var state) : StatPath(fullPath, out state);
        if (code != 0) ThrowError(Marshal.GetLastPInvokeError());
        return (UnixFileMode)(state.Mode & 0x0fff);
    }

    public IWorkspaceStagedFile CreateStagedFile(string fullPath)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        EnsureSupported();
        var stream = new FileStream(fullPath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
        return new StagedFile(stream);
    }

    public void PublishNew(string stagedPath, string targetPath)
    {
        EnsureSupported();
        int result;
        try { result = RenameExclusive(stagedPath, targetPath, 0x00000004); }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
        { throw new WorkspacePublishException(WorkspaceErrorCode.PublishUnsupported); }
        if (result == 0) return;
        var error = Marshal.GetLastPInvokeError();
        if (error == 17) throw new WorkspacePublishException(WorkspaceErrorCode.Conflict);
        if (error is 18 or 22 or 45 or 78 or 102) throw new WorkspacePublishException(WorkspaceErrorCode.PublishUnsupported);
        ThrowError(error);
    }

    public void PublishReplacement(string stagedPath, string targetPath)
    {
        EnsureSupported();
        if (Rename(stagedPath, targetPath) != 0) ThrowError(Marshal.GetLastPInvokeError());
    }

    public void DeleteOwnedStagedFile(string fullPath) => File.Delete(fullPath);

    private sealed class StagedFile(FileStream stream) : IWorkspaceStagedFile
    {
        public Stream Stream => stream;
        public WorkspaceNodeState GetState()
        {
            var code = RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? StatHandleX64(stream.SafeFileHandle, out var state) : StatHandle(stream.SafeFileHandle, out state);
            if (code != 0) ThrowError(Marshal.GetLastPInvokeError());
            return ConvertState(state);
        }
        public void SetMode(UnixFileMode mode)
        {
            if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
            File.SetUnixFileMode(stream.SafeFileHandle, mode);
        }
        public void FlushToDisk() => stream.Flush(true);
        public ValueTask DisposeAsync() => stream.DisposeAsync();
        public override string ToString() => "[WorkspaceStagedFile]";
    }

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture is not (Architecture.Arm64 or Architecture.X64))
            throw new PlatformNotSupportedException("無法可靠辨識此平台的檔案種類");
    }

    private static WorkspaceNodeState ConvertState(NativeStat state) => new((state.Mode & 0xf000) switch
    {
        0x8000 => WorkspaceNodeKind.File,
        0x4000 => WorkspaceNodeKind.Directory,
        0xa000 => WorkspaceNodeKind.Link,
        _ => WorkspaceNodeKind.Other
    }, state.Size, state.ModifiedSeconds, state.ModifiedNanoseconds, (uint)state.Device, state.Inode, state.Links, state.User, state.Mode);

    private static void ThrowError(int error)
    {
        if (error is 1 or 13) throw new UnauthorizedAccessException("工作區項目無法存取");
        if (error == 63) throw new PathTooLongException("工作區路徑無效");
        throw new IOException("工作區狀態無法取得");
    }

    private sealed class ReadFile(FileStream stream) : IWorkspaceReadFile
    {
        public Stream Stream => stream;
        public WorkspaceNodeState GetState()
        {
            var code = RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? StatHandleX64(stream.SafeFileHandle, out var state) : StatHandle(stream.SafeFileHandle, out state);
            if (code != 0) ThrowError(Marshal.GetLastPInvokeError());
            return ConvertState(state);
        }
        public UnixFileMode GetMode()
        {
            if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
            return File.GetUnixFileMode(stream.SafeFileHandle);
        }
        public ValueTask DisposeAsync() => stream.DisposeAsync();
        public override string ToString() => "[WorkspaceReadFile]";
    }

    // Darwin's 64-bit inode ABI: 144 bytes, on both macOS arm64 and x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeStat
    {
        internal int Device;
        internal ushort Mode;
        internal ushort Links;
        internal ulong Inode;
        internal uint User;
        internal uint Group;
        internal int SpecialDevice;
        internal long AccessSeconds;
        internal long AccessNanoseconds;
        internal long ModifiedSeconds;
        internal long ModifiedNanoseconds;
        internal long ChangedSeconds;
        internal long ChangedNanoseconds;
        internal long BirthSeconds;
        internal long BirthNanoseconds;
        internal long Size;
        internal long Blocks;
        internal int BlockSize;
        internal uint Flags;
        internal uint Generation;
        internal int Spare;
        internal long Reserved1;
        internal long Reserved2;
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "renamex_np", SetLastError = true)]
    private static extern int RenameExclusive([MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string target, uint flags);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename([MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string target);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "lstat", SetLastError = true)]
    private static extern int StatPath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out NativeStat state);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "lstat$INODE64", SetLastError = true)]
    private static extern int StatPathX64([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out NativeStat state);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "fstat", SetLastError = true)]
    private static extern int StatHandle(SafeFileHandle handle, out NativeStat state);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static extern int StatHandleX64(SafeFileHandle handle, out NativeStat state);
    public override string ToString() => "[MacWorkspaceFileSystem]";
}