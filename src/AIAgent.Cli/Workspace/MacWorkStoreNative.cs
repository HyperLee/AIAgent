using System.Runtime.InteropServices;

namespace AIAgent.Cli.Workspace;

// Darwin SDK: sys/stat.h, sys/fcntl.h, sys/dirent.h, sys/acl.h and membership.h.
// Both supported architectures use the 144-byte 64-bit-inode stat layout.
internal static class MacWorkStoreNative
{
    internal const int NoFollow = 0x100;
    internal const int CloseOnExec = 0x01000000;
    internal const int NonBlocking = 4;
    internal const int Directory = 0x00100000;
    internal const int CreateExclusive = 0x200 | 0x800;
    internal const int ReadWrite = 2;
    private const string Library = "/usr/lib/libSystem.B.dylib";

    [StructLayout(LayoutKind.Sequential)]
    internal struct Stat
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

        internal readonly WorkspaceNodeState ToState() => new((Mode & 0xf000) switch
        {
            0x8000 => WorkspaceNodeKind.File,
            0x4000 => WorkspaceNodeKind.Directory,
            0xa000 => WorkspaceNodeKind.Link,
            _ => WorkspaceNodeKind.Other
        }, Size, ModifiedSeconds, ModifiedNanoseconds, (uint)Device, Inode, Links, User, Mode);

        internal readonly bool SameIdentity(Stat other) => Device == other.Device && Inode == other.Inode;
        internal readonly bool SameContentState(Stat other) => ToState() == other.ToState() &&
            ChangedSeconds == other.ChangedSeconds && ChangedNanoseconds == other.ChangedNanoseconds && Flags == other.Flags;
    }

    internal static int StatHandle(int descriptor, out Stat state) => RuntimeInformation.ProcessArchitecture == Architecture.X64
        ? FStatX64(descriptor, out state) : FStat(descriptor, out state);
    internal static int StatAt(int descriptor, string name, out Stat state) => RuntimeInformation.ProcessArchitecture == Architecture.X64
        ? FStatAtX64(descriptor, name, out state, 0x20) : FStatAt(descriptor, name, out state, 0x20);
    internal static IntPtr ReadDirectory(IntPtr directory) => RuntimeInformation.ProcessArchitecture == Architecture.X64
        ? ReadDirectoryX64(directory) : ReadDirectoryArm64(directory);

    [DllImport(Library, EntryPoint = "open", SetLastError = true)]
    internal static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport(Library, EntryPoint = "openat", SetLastError = true)]
    internal static extern int OpenAt(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int flags, uint mode = 0);
    [DllImport(Library, EntryPoint = "mkdirat", SetLastError = true)]
    internal static extern int MakeDirectoryAt(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, uint mode);
    [DllImport(Library, EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStat(int descriptor, out Stat state);
    [DllImport(Library, EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static extern int FStatX64(int descriptor, out Stat state);
    [DllImport(Library, EntryPoint = "fstatat", SetLastError = true)]
    private static extern int FStatAt(int descriptor, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out Stat state, int flags);
    [DllImport(Library, EntryPoint = "fstatat$INODE64", SetLastError = true)]
    private static extern int FStatAtX64(int descriptor, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out Stat state, int flags);
    [DllImport(Library, EntryPoint = "close", SetLastError = true)]
    internal static extern int Close(int descriptor);
    [DllImport(Library, EntryPoint = "getuid")]
    internal static extern uint GetUser();
    [DllImport(Library, EntryPoint = "flock", SetLastError = true)]
    internal static extern int Lock(int descriptor, int operation);
    [DllImport(Library, EntryPoint = "pread", SetLastError = true)]
    internal static extern nint Read(int descriptor, [Out] byte[] buffer, nuint count, long offset);
    [DllImport(Library, EntryPoint = "pwrite", SetLastError = true)]
    internal static extern nint Write(int descriptor, byte[] buffer, nuint count, long offset);
    [DllImport(Library, EntryPoint = "fsync", SetLastError = true)]
    internal static extern int Flush(int descriptor);
    [DllImport(Library, EntryPoint = "renameatx_np", SetLastError = true)]
    internal static extern int Publish(int sourceDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        int targetDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string target, uint flags);
    [DllImport(Library, EntryPoint = "unlinkat", SetLastError = true)]
    internal static extern int Unlink(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int flags);
    [DllImport(Library, EntryPoint = "fdopendir", SetLastError = true)]
    internal static extern IntPtr OpenDirectory(int descriptor);
    [DllImport(Library, EntryPoint = "readdir", SetLastError = true)]
    private static extern IntPtr ReadDirectoryArm64(IntPtr directory);
    [DllImport(Library, EntryPoint = "readdir$INODE64", SetLastError = true)]
    private static extern IntPtr ReadDirectoryX64(IntPtr directory);
    [DllImport(Library, EntryPoint = "closedir", SetLastError = true)]
    internal static extern int CloseDirectory(IntPtr directory);
    [DllImport(Library, EntryPoint = "__error")]
    internal static extern IntPtr ErrorPointer();
    [DllImport(Library, EntryPoint = "acl_get_fd_np", SetLastError = true)]
    internal static extern IntPtr GetAcl(int descriptor, int type);
    [DllImport(Library, EntryPoint = "acl_valid", SetLastError = true)]
    internal static extern int ValidateAcl(IntPtr acl);
    [DllImport(Library, EntryPoint = "acl_get_entry", SetLastError = true)]
    internal static extern int GetAclEntry(IntPtr acl, int entryId, out IntPtr entry);
    [DllImport(Library, EntryPoint = "acl_get_tag_type", SetLastError = true)]
    internal static extern int GetAclTag(IntPtr entry, out int tag);
    [DllImport(Library, EntryPoint = "acl_get_permset", SetLastError = true)]
    internal static extern int GetPermissions(IntPtr entry, out IntPtr permissions);
    [DllImport(Library, EntryPoint = "acl_get_perm_np", SetLastError = true)]
    internal static extern int HasPermission(IntPtr permissions, int permission);
    [DllImport(Library, EntryPoint = "acl_get_qualifier", SetLastError = true)]
    internal static extern IntPtr GetQualifier(IntPtr entry);
    [DllImport(Library, EntryPoint = "mbr_uuid_to_id", SetLastError = true)]
    internal static extern int ResolveIdentity(IntPtr uuid, out uint identity, out int type);
    [DllImport(Library, EntryPoint = "acl_free", SetLastError = true)]
    internal static extern int FreeAcl(IntPtr acl);
}