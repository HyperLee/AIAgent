using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

#if GIT_PROCESS_HOST
namespace AIAgent.ProcessHost.Git;
#else
namespace AIAgent.Cli.Git;
#endif

internal sealed class GitInstallation
{
    private readonly string[] _runtimeFiles;
    private GitInstallation(string executable, string identity, string[] runtimeFiles)
    { Executable = executable; Identity = identity; _runtimeFiles = runtimeFiles; }
    internal string Executable { get; }
    internal string Identity { get; }
    internal IReadOnlyList<string> RuntimeFiles => Array.AsReadOnly((string[])_runtimeFiles.Clone());
    internal static IEnumerable<GitInstallation> Discover()
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists("/usr/bin/sandbox-exec") || !SafeEmptyHome()) yield break;
        foreach (var candidate in new[] { "/opt/homebrew/bin/git", "/usr/local/bin/git", "/usr/bin/git" })
        {
            GitInstallation? result = null;
            try
            {
                var executable = Canonical(candidate);
                if (executable is null || !(executable == "/usr/bin/git" ||
                    executable.StartsWith("/opt/homebrew/Cellar/git/", StringComparison.Ordinal) ||
                    executable.StartsWith("/usr/local/Cellar/git/", StringComparison.Ordinal))) continue;
                var files = new List<string>();
                // Only these known, read-only Homebrew dependencies are supported. Unknown runtime layouts fail closed.
                foreach (var prefix in new[] { "/opt/homebrew", "/usr/local" })
                    foreach (var dependency in new[] { "/opt/pcre2/lib/libpcre2-8.0.dylib", "/opt/gettext/lib/libintl.8.dylib" })
                    {
                        var path = Canonical(prefix + dependency);
                        var package = dependency.Contains("pcre2", StringComparison.Ordinal) ? "pcre2" : "gettext";
                        if (path is not null && path.StartsWith(prefix + "/Cellar/" + package + "/", StringComparison.Ordinal)) files.Add(path);
                    }
                var identity = Fingerprint(executable, files);
                if (identity is not null) result = new(executable, identity, files.ToArray());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            if (result is not null) yield return result;
        }
    }
    internal bool ValidateCurrent() => Identity == Fingerprint(Executable, _runtimeFiles) && SafeEmptyHome();
    private static bool SafeEmptyHome()
    {
        if (!OperatingSystem.IsMacOS()) return false;
        var path = Canonical("/var/empty");
        return path == "/private/var/empty" && Directory.Exists(path) &&
            (File.GetUnixFileMode(path) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0;
    }
    internal static string? Canonical(string path)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        var pointer = RealPath(path, IntPtr.Zero);
        if (pointer == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(pointer); }
        finally { Free(pointer); }
    }
    private static string? Fingerprint(string executable, IEnumerable<string> dependencies)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in new[] { executable }.Concat(dependencies))
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.LinkTarget is not null || file.Length is < 1 or > 67108864 ||
                    (File.GetUnixFileMode(path) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0 || Canonical(path) != path) return null;
                var before = StatFingerprint(path);
                if (before is null) return null;
                hash.AppendData(Encoding.UTF8.GetBytes(path + "\0" + before));
                using (var stream = File.OpenRead(path))
                {
                    var buffer = new byte[16384]; var total = 0; int read;
                    while ((read = stream.Read(buffer)) > 0)
                    { total = checked(total + read); if (total > 67108864) return null; hash.AppendData(buffer.AsSpan(0, read)); }
                }
                file.Refresh();
                if (before != StatFingerprint(path)) return null;
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    private static string? StatFingerprint(string path)
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture is not (Architecture.Arm64 or Architecture.X64)) return null;
        var result = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? StatPathX64(path, out var state) : StatPath(path, out state);
        if (result != 0 || (state.Mode & 0xf000) != 0x8000) return null;
        return FormattableString.Invariant($"{state.Device}:{state.Inode}:{state.Mode}:{state.Links}:{state.User}:{state.Group}:{state.Size}:{state.ModifiedSeconds}:{state.ModifiedNanoseconds}:{state.ChangedSeconds}:{state.ChangedNanoseconds}:{state.BirthSeconds}:{state.BirthNanoseconds}");
    }
    // Darwin's 64-bit inode ABI, matching the workspace's native file-identity checks.
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
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "lstat", SetLastError = true)] private static extern int StatPath(string path, out NativeStat state);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "lstat$INODE64", SetLastError = true)] private static extern int StatPathX64(string path, out NativeStat state);
    public override string ToString() => "[GitInstallation]";
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "realpath")] private static extern IntPtr RealPath(string path, IntPtr buffer);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "free")] private static extern void Free(IntPtr pointer);
}

internal static class GitSandboxPolicy
{
    private static string PrivateWorkPattern
    {
        get
        {
            static string Ascii(string value) => string.Concat(value.Select(character => char.IsAsciiLetter(character)
                ? "[" + char.ToLowerInvariant(character) + char.ToUpperInvariant(character) + "]" : character == '.' ? "[.]" : character.ToString()));
            return "/(" + Ascii(".aiagent") + "|" + Ascii(".store.lock") + "|" + Ascii(".aiagent-work-") + string.Concat(Enumerable.Repeat("[0-9a-fA-F]", 32)) + Ascii(".tmp") + ")(/|$)";
        }
    }

    internal static string Create(string root, GitInstallation installation, GitReadRequest request)
    {
        static string Quote(string path) => "\"" + path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        var text = new StringBuilder("(version 1)\n(deny default)\n(deny process-info*)\n(allow file-read-metadata)\n");
        foreach (var system in new[] { "/System/Library", "/usr/lib", "/Library/Apple/System/Library", "/usr/share/locale", "/usr/share/icu", "/private/var/db/dyld" })
            text.Append("(allow file-read* (subpath ").Append(Quote(system)).Append("))\n");
        foreach (var file in installation.RuntimeFiles.Prepend(installation.Executable).Concat(new[]
        {
            "/", root, root + "/.git", "/dev/null", "/dev/random", "/dev/urandom", root + "/.git/HEAD", root + "/.git/config", root + "/.git/index", root + "/.git/packed-refs"
        }).Concat(request.IgnoreReadFiles))
            text.Append("(allow file-read* (literal ").Append(Quote(file)).Append("))\n");
        foreach (var directory in new[] { root + "/.git/refs", root + "/.git/objects" })
            text.Append("(allow file-read* (subpath ").Append(Quote(directory)).Append("))\n");
        // Private work storage remains denied even if a broad ancestor or a future literal is readable.
        text.Append("(deny file-read* file-write* (regex #\"").Append(PrivateWorkPattern).Append("\"))\n");
        var workStoreRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aiagent", "work");
        text.Append("(deny file-read* file-write* (subpath ").Append(Quote(workStoreRoot)).Append("))\n");
        // Explicitly authorized OS null-sink exception: no create, unlink, rename, or persistent-file writes.
        text.Append("(allow file-write-data (literal \"/dev/null\"))\n");
        text.Append("(allow process-exec (literal ").Append(Quote(installation.Executable)).Append("))\n");
        return text.ToString();
    }
}