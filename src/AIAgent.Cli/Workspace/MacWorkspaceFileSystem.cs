using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace AIAgent.Cli.Workspace;

/// <summary>macOS 的無跟隨種類檢查；不以「非目錄」推論普通檔案。</summary>
internal sealed class MacWorkspaceFileSystem : IWorkspaceReadFileSystem
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
    }, state.Size, state.ModifiedSeconds, state.ModifiedNanoseconds, (uint)state.Device, state.Inode);

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