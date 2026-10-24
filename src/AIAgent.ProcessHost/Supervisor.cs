using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace AIAgent.ProcessHost;

internal sealed class SupervisionResult(int exitCode, bool exited, bool cleanupConfirmed, bool identityLimit)
{
    internal int ExitCode { get; } = exitCode;
    internal bool Exited { get; } = exited;
    internal bool CleanupConfirmed { get; } = cleanupConfirmed;
    internal bool IdentityLimit { get; } = identityLimit;
    public override string ToString() => "[SupervisionResult]";
}

internal static class Supervisor
{
    internal static async Task<SupervisionResult> Run(Descriptor descriptor, CancellationToken stop, Action<int> started, Action cleanupStarted,
        Func<Native.Identity[]?>? snapshotProvider = null, Func<int, Native.Identity?>? initialIdentityProvider = null,
        Func<int, Native.Identity?>? subsequentIdentityProvider = null)
    {
        Directory.SetCurrentDirectory(descriptor.Root);
        var pid = Native.Spawn(descriptor.Dotnet, descriptor.Arguments, descriptor.Environment);
        if (pid <= 0) { cleanupStarted(); return new(-1, false, true, false); }
        var known = new Dictionary<int, Native.Identity>();
        Native.Identity? first = null;
        var timer = Stopwatch.StartNew();
        var invalid = false; var limited = false; var status = 0; var exited = false;
        Exception? failure = null;
        Native.Identity? InspectKnown(int target, out bool observationLost)
        {
            if (subsequentIdentityProvider is null) return Native.Inspect(target, out observationLost);
            var identity = subsequentIdentityProvider(target);
            observationLost = identity is null;
            return identity;
        }
        bool SpawnedRootAlive()
        {
            if (exited) return false;
            var waited = Native.Wait(pid, out var observedStatus);
            if (waited == pid) { exited = true; status = observedStatus; return false; }
            if (waited < 0) { invalid = true; return false; }
            return true;
        }
        bool KnownAlive()
        {
            var alive = false;
            foreach (var item in known.Values)
            {
                var current = InspectKnown(item.Pid, out var observationLost);
                if (observationLost) invalid = true;
                if (current is { } value && item.SameProcess(value) && value.Status != 5) alive = true;
            }
            return alive;
        }
        bool Observe()
        {
            if (known.Count == 0) { invalid = true; return SpawnedRootAlive(); }
            var snapshot = snapshotProvider is null ? Native.Snapshot() : snapshotProvider();
            if (snapshot is null) { invalid = true; return KnownAlive() || SpawnedRootAlive(); }
            foreach (var item in snapshot)
            {
                var belongs = item.Group == pid || known.TryGetValue(item.Parent, out var parent) &&
                    snapshot.Any(current => current.Pid == parent.Pid && current.StartSeconds == parent.StartSeconds && current.StartMicros == parent.StartMicros);
                if (!belongs && !known.ContainsKey(item.Pid)) continue;
                if (known.TryGetValue(item.Pid, out var previous))
                {
                    if (!previous.SameProcess(item)) continue;
                    if (item.Group != pid) invalid = true;
                }
                else
                {
                    if (known.Count == 256) { invalid = true; limited = true; continue; }
                    known.Add(item.Pid, item);
                }
            }
            foreach (var item in known.Values)
            {
                var current = snapshot.FirstOrDefault(value => item.SameProcess(value));
                if (current.Pid != 0 && current.Group != pid) invalid = true;
                if (current.Pid == 0) { _ = InspectKnown(item.Pid, out var observationLost); if (observationLost) invalid = true; }
            }
            return snapshot.Any(item => known.TryGetValue(item.Pid, out var previous) && previous.SameProcess(item) && item.Status != 5);
        }
        void Signal(int signal)
        {
            // Before an identity is available, waitpid proves this spawned child still owns its PID.
            if (first is null)
            {
                if (SpawnedRootAlive()) Native.SignalGroup(pid, signal);
                return;
            }
            // Otherwise only identities verified immediately before signaling are eligible.
            var root = InspectKnown(pid, out var observationLost);
            if (observationLost) invalid = true;
            if (first is { } original && root is { } currentRoot && original.SameProcess(currentRoot) && currentRoot.Group == pid)
                Native.SignalGroup(pid, signal);
            else if (first is { Group: var originalGroup } && originalGroup == pid && root is null && observationLost && SpawnedRootAlive())
                Native.SignalGroup(pid, signal);
            foreach (var item in known.Values)
            {
                var current = InspectKnown(item.Pid, out observationLost);
                if (observationLost) invalid = true;
                if (current is { } value && item.SameProcess(value)) Native.Signal(item.Pid, signal);
            }
        }
        try
        {
            // A very short process may have become a zombie; its identity still exists until waitpid.
            first = initialIdentityProvider is null ? Native.Inspect(pid, out _) : initialIdentityProvider(pid);
            var observationLost = first is null;
            if (observationLost || first is null) invalid = true;
            if (first is { } identity)
            {
                known.Add(pid, identity);
                started(pid);
                while (true)
                {
                    Observe();
                    var wait = Native.Wait(pid, out status);
                    if (wait == pid) { exited = true; break; }
                    if (wait < 0) { invalid = true; break; }
                    if (stop.IsCancellationRequested || invalid || timer.ElapsedMilliseconds >= descriptor.Timeout) break;
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex) { invalid = true; failure = ex; }
        finally
        {
            try { cleanupStarted(); }
            catch (Exception ex) { invalid = true; failure ??= ex; }
            // A broken control receipt cannot bypass the host-owned TERM/KILL sequence.
            try
            {
                if (Observe())
                {
                    Signal(15); var term = Stopwatch.StartNew();
                    while (term.Elapsed < TimeSpan.FromSeconds(2) && Observe()) await Task.Delay(100);
                    if (Observe())
                    {
                        Signal(9); var kill = Stopwatch.StartNew();
                        while (kill.Elapsed < TimeSpan.FromSeconds(3) && Observe()) await Task.Delay(100);
                    }
                }
                if (!exited)
                {
                    var waited = Native.Wait(pid, out var finalStatus);
                    if (waited == pid) { exited = true; status = finalStatus; }
                    else if (waited < 0) invalid = true;
                }
            }
            catch (Exception ex) { invalid = true; failure ??= ex; }
        }
        var confirmed = !Observe() && !invalid && exited;
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return new((status >> 8) & 255, exited && (status & 127) == 0, confirmed, limited);
    }
}

internal static class Native
{
    private const string Libc = "/usr/lib/libSystem.B.dylib";
    private const string Libproc = "/usr/lib/libproc.dylib";
    internal readonly record struct Identity(int Pid, int Parent, int Group, uint Status, ulong StartSeconds, ulong StartMicros)
    {
        internal bool SameProcess(Identity other) => Pid == other.Pid && StartSeconds == other.StartSeconds && StartMicros == other.StartMicros;
    }
    internal static void CloseOnExec(int descriptor)
    {
        var flags = Fcntl(descriptor, 1, 0);
        if (flags < 0 || Fcntl(descriptor, 2, flags | 1) != 0) throw new IOException();
    }
    internal static int Spawn(string executable, string[] arguments, string[] environment)
    {
        if (SpawnAttrInit(out var attr) != 0) throw new IOException();
        IntPtr actions = IntPtr.Zero;
        using var argv = new Strings(arguments); using var env = new Strings(environment);
        try
        {
            if (SpawnActionsInit(out actions) != 0 || SpawnAttrGroup(ref attr, 0) != 0 ||
                SpawnAttrFlags(ref attr, 0x4002) != 0 || SpawnAddOpen(ref actions, 0, "/dev/null", 0, 0) != 0 ||
                SpawnDup(ref actions, 1, 1) != 0 || SpawnDup(ref actions, 2, 2) != 0) throw new IOException();
            var error = PosixSpawn(out var pid, executable, ref actions, ref attr, argv.Pointer, env.Pointer);
            // posix_spawn reports the actual exec error synchronously, not merely wrapper creation.
            return error == 0 ? pid : -1;
        }
        finally { if (actions != IntPtr.Zero) SpawnActionsDestroy(ref actions); SpawnAttrDestroy(ref attr); }
    }
    internal static Identity? Inspect(int pid) => Inspect(pid, out _);
    internal static Identity? Inspect(int pid, out bool observationLost)
    {
        observationLost = false;
        var bytes = new byte[136];
        if (ProcInfo(pid, 3, 0, bytes, bytes.Length) != bytes.Length)
        {
            // ESRCH is an observed disappearance; denied or malformed information is lost observation.
            observationLost = Marshal.GetLastPInvokeError() is not (2 or 3);
            return null;
        }
        return new(BitConverter.ToInt32(bytes, 12), BitConverter.ToInt32(bytes, 16), BitConverter.ToInt32(bytes, 100),
            BitConverter.ToUInt32(bytes, 4), BitConverter.ToUInt64(bytes, 120), BitConverter.ToUInt64(bytes, 128));
    }
    internal static Identity[]? Snapshot()
    {
        var count = ProcList(1, 0, null, 0); if (count <= 0 || count > 1048576) return null;
        var ids = new int[(count / 4) + 1024];
        var read = ProcList(1, 0, ids, ids.Length * 4); if (read <= 0 || read >= ids.Length * 4) return null;
        var result = new List<Identity>();
        foreach (var pid in ids.Take(read / 4)) if (pid > 0 && Inspect(pid) is { } item) result.Add(item);
        return result.ToArray();
    }
    internal static int Wait(int pid, out int status) => WaitPid(pid, out status, 1);
    internal static void Signal(int pid, int signal) => Kill(pid, signal);
    internal static void SignalGroup(int group, int signal) => Kill(-group, signal);
    private sealed class Strings : IDisposable
    {
        private readonly IntPtr[] _strings;
        internal IntPtr Pointer { get; }
        internal Strings(string[] values)
        {
            _strings = values.Select(Marshal.StringToCoTaskMemUTF8).ToArray(); Pointer = Marshal.AllocHGlobal((_strings.Length + 1) * IntPtr.Size);
            for (var i = 0; i < _strings.Length; i++) Marshal.WriteIntPtr(Pointer, i * IntPtr.Size, _strings[i]);
            Marshal.WriteIntPtr(Pointer, _strings.Length * IntPtr.Size, IntPtr.Zero);
        }
        public void Dispose() { foreach (var value in _strings) Marshal.FreeCoTaskMem(value); Marshal.FreeHGlobal(Pointer); }
    }
    [DllImport(Libc, EntryPoint = "fcntl", SetLastError = true)] private static extern int Fcntl(int fd, int command, int argument);
    [DllImport(Libc, EntryPoint = "posix_spawnattr_init")] private static extern int SpawnAttrInit(out IntPtr attr);
    [DllImport(Libc, EntryPoint = "posix_spawnattr_destroy")] private static extern int SpawnAttrDestroy(ref IntPtr attr);
    [DllImport(Libc, EntryPoint = "posix_spawnattr_setpgroup")] private static extern int SpawnAttrGroup(ref IntPtr attr, int group);
    [DllImport(Libc, EntryPoint = "posix_spawnattr_setflags")] private static extern int SpawnAttrFlags(ref IntPtr attr, short flags);
    [DllImport(Libc, EntryPoint = "posix_spawn_file_actions_init")] private static extern int SpawnActionsInit(out IntPtr actions);
    [DllImport(Libc, EntryPoint = "posix_spawn_file_actions_destroy")] private static extern int SpawnActionsDestroy(ref IntPtr actions);
    [DllImport(Libc, EntryPoint = "posix_spawn_file_actions_addopen")] private static extern int SpawnAddOpen(ref IntPtr actions, int fd, string path, int flags, ushort mode);
    [DllImport(Libc, EntryPoint = "posix_spawn_file_actions_adddup2")] private static extern int SpawnDup(ref IntPtr actions, int original, int target);
    [DllImport(Libc, EntryPoint = "posix_spawn")] private static extern int PosixSpawn(out int pid, string path, ref IntPtr actions, ref IntPtr attr, IntPtr argv, IntPtr env);
    [DllImport(Libc, EntryPoint = "waitpid", SetLastError = true)] private static extern int WaitPid(int pid, out int status, int options);
    [DllImport(Libc, EntryPoint = "kill", SetLastError = true)] private static extern int Kill(int pid, int signal);
    [DllImport(Libproc, EntryPoint = "proc_pidinfo", SetLastError = true)] private static extern int ProcInfo(int pid, int flavor, ulong argument, byte[] output, int size);
    [DllImport(Libproc, EntryPoint = "proc_listpids", SetLastError = true)] private static extern int ProcList(uint type, uint info, int[]? output, int size);
}