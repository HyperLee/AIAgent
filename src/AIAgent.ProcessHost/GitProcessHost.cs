using System.Diagnostics;
using System.Runtime.InteropServices;

using AIAgent.ProcessHost.Git;

namespace AIAgent.ProcessHost;

internal static class GitProcessHost
{
    internal static async Task<int> Run(bool probe)
    {
        var commandDeadline = new GitProcessDeadline();
        try
        {
            var descriptor = GitProcessDescriptor.Read(Console.OpenStandardInput());
            var installation = GitInstallation.Discover().FirstOrDefault(value => value.Identity == descriptor.ExecutableIdentity);
            if (installation is null || GitInstallation.Canonical(descriptor.Root) != descriptor.Root)
                return Reply(new("git_unavailable"));
            if (probe) return GitSandboxProbe.Run(descriptor, installation);
            using var active = new CancellationTokenSource(commandDeadline.ActiveRemaining);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(active.Token);
            var control = Task.Run(() =>
            {
                try { _ = Console.OpenStandardInput().ReadByte(); } catch (IOException) { }
                stop.Cancel();
            });
            var capability = await Probe(descriptor, commandDeadline, stop.Token);
            if (!capability) return Reply(new(stop.IsCancellationRequested ? active.IsCancellationRequested ? "git_timed_out" : "turn_cancelled" : "capability_unavailable"));
            if (!installation.ValidateCurrent()) return Reply(new("git_unavailable"));
            if (stop.IsCancellationRequested) return Reply(new(active.IsCancellationRequested ? "git_timed_out" : "turn_cancelled"));
            var start = new ProcessStartInfo("/usr/bin/sandbox-exec")
            {
                WorkingDirectory = descriptor.Root,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-p"); start.ArgumentList.Add(GitSandboxPolicy.Create(descriptor.Root, installation, descriptor.Request));
            start.ArgumentList.Add(installation.Executable);
            foreach (var argument in GitProcessRecipe.Arguments(descriptor.Root, descriptor.Recipe, descriptor.Request)) start.ArgumentList.Add(argument);
            start.Environment.Clear(); foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
            using var target = Process.Start(start);
            if (target is null) return Reply(new("git_failed"));
            var limit = false; var failed = false;
            var stdoutLimit = descriptor.Recipe == GitReadRecipe.Batch ? GitProcessRecipe.BlobLimit + 256 : GitProcessRecipe.MetadataLimit;
            async Task<byte[]> Drain(Stream stream, int maximum, bool retain)
            {
                using var bytes = new MemoryStream(); var buffer = new byte[8192]; var count = 0;
                try
                {
                    int read;
                    while ((read = await stream.ReadAsync(buffer, stop.Token)) > 0)
                    {
                        count = checked(count + read);
                        if (count > maximum) { limit = true; stop.Cancel(); break; }
                        if (retain) bytes.Write(buffer, 0, read);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { failed = true; stop.Cancel(); }
                return bytes.ToArray();
            }
            var output = Drain(target.StandardOutput.BaseStream, stdoutLimit, true);
            var error = Drain(target.StandardError.BaseStream, GitProcessRecipe.DiagnosticLimit, false);
            var input = Task.Run(async () =>
            {
                try { await target.StandardInput.BaseStream.WriteAsync(GitProcessRecipe.Input(descriptor.Recipe, descriptor.Request), stop.Token); }
                catch (OperationCanceledException) { }
                catch (IOException) { failed = true; stop.Cancel(); }
                finally { target.StandardInput.Close(); }
            });
            var confirmed = false;
            try
            {
                await target.WaitForExitAsync(stop.Token);
                // Pipes must finish under the same per-command deadline.
                await Task.WhenAll(output, error, input).WaitAsync(stop.Token);
                confirmed = true;
            }
            catch (OperationCanceledException) { }
            finally
            {
                var cleanup = commandDeadline.BeginCleanup();
                if (!target.HasExited)
                {
                    try { target.Kill(); }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                    try { await target.WaitForExitAsync().WaitAsync(cleanup.Remaining(TimeSpan.FromSeconds(3))); }
                    catch (TimeoutException) { }
                }
                target.StandardOutput.Close(); target.StandardError.Close(); target.StandardInput.Close();
                try { await Task.WhenAll(output, error, input).WaitAsync(cleanup.Remaining(TimeSpan.FromSeconds(1))); }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException) { failed = true; }
                confirmed = target.HasExited && input.IsCompleted && output.IsCompleted && error.IsCompleted;
            }
            GC.KeepAlive(control);
            if (!confirmed) return Reply(new("process_unconfirmed", cleanupConfirmed: false));
            if (failed) return Reply(new("process_cleanup_failed", cleanupConfirmed: false));
            if (limit) return Reply(new("git_limit", target.ExitCode));
            if (stop.IsCancellationRequested) return Reply(new(active.IsCancellationRequested ? "git_timed_out" : "turn_cancelled", target.ExitCode));
            if (!GitProcessRecipe.AcceptExit(descriptor.Recipe, target.ExitCode)) return Reply(new("git_failed", target.ExitCode));
            return Reply(new("none", target.ExitCode, await output));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception or OverflowException or System.Text.DecoderFallbackException)
        { return probe ? 77 : Reply(new("git_failed")); }
    }
    private static int Reply(GitReadResult result) { result.Write(Console.OpenStandardOutput()); return 0; }
    private static async Task<bool> Probe(GitProcessDescriptor descriptor, GitProcessDeadline commandDeadline, CancellationToken cancellationToken)
    {
        var start = HostStart(Environment.ProcessPath!, "--git-probe", descriptor.Root);
        using var process = Process.Start(start);
        if (process is null) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(commandDeadline.Remaining(TimeSpan.FromSeconds(5)));
        try
        {
            // Probe emits one byte; stderr remains bounded even if the runtime cannot launch.
            var output = ReadSmall(process.StandardOutput.BaseStream, 1, timeout.Token);
            var errors = ReadSmall(process.StandardError.BaseStream, GitProcessRecipe.DiagnosticLimit, timeout.Token);
            await process.StandardInput.BaseStream.WriteAsync(descriptor.Encode(), timeout.Token); process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var bytes = await output; _ = await errors;
            return process.ExitCode == 0 && bytes is [1];
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException) { return false; }
        finally
        {
            var cleanup = commandDeadline.BeginCleanup();
            if (!process.HasExited)
            {
                try { process.Kill(); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync().WaitAsync(cleanup.Remaining(TimeSpan.FromSeconds(3)));
            }
        }
    }
    internal static ProcessStartInfo HostStart(string executable, string mode, string root)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(mode); start.Environment.Clear();
        foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
        start.Environment["DOTNET_ROOT"] = Directory.Exists("/usr/local/share/dotnet") ? "/usr/local/share/dotnet" : "/opt/homebrew/share/dotnet";
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        return start;
    }
    internal static async Task<byte[]> ReadSmall(Stream stream, int maximum, CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream(); var buffer = new byte[1024]; int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        { if (bytes.Length + read > maximum) throw new IOException(); bytes.Write(buffer, 0, read); }
        return bytes.ToArray();
    }
}

internal static class GitSandboxProbe
{
    internal static int Run(GitProcessDescriptor descriptor, GitInstallation installation)
    {
        var error = IntPtr.Zero;
        if (SandboxInit(GitSandboxPolicy.Create(descriptor.Root, installation, descriptor.Request), 0, out error) != 0)
        { if (error != IntPtr.Zero) SandboxFree(error); return 77; }
        try
        {
            var pid = Environment.ProcessId;
            bool Denied(string operation) => SandboxCheck(pid, operation, 0, IntPtr.Zero) != 0;
            if (!Denied("process-fork") || !Denied("network-outbound") || !Denied("file-write-create")) return 77;
            // Path-filtered sandbox_check is variadic and has a different arm64 ABI. Use real, harmless operations instead.
            var readable = Open(installation.Executable, 0);
            if (readable < 0) return 77;
            Close(readable);
            var parentIdentity = Native.Inspect(GetParent(), out var observationLost);
            if (parentIdentity is not null || !observationLost) return 77;
            var helper = Native.Spawn("/bin/sh", ["/bin/sh", "-c", "exit 0"], ["PATH=/usr/bin:/bin", "HOME=/var/empty", "LANG=C"]);
            if (helper > 0)
            {
                Native.SignalGroup(helper, 9);
                var wait = Stopwatch.StartNew();
                while (Native.Wait(helper, out _) == 0 && wait.ElapsedMilliseconds < 1000) Thread.Sleep(10);
                return 77;
            }
            // Opening an existing metadata file for writing performs no write or creation.
            var fd = Open(descriptor.Root + "/.git/config", 1);
            if (fd >= 0) { Close(fd); return 77; }
            if (Marshal.GetLastPInvokeError() is not (1 or 13)) return 77;
            fd = Open("/private/etc/passwd", 0);
            if (fd >= 0) { Close(fd); return 77; }
            if (Marshal.GetLastPInvokeError() is not (1 or 13)) return 77;
            if (!NetworkDenied(2) || !NetworkDenied(30)) return 77;
            Console.OpenStandardOutput().WriteByte(1);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 77; }
    }
    private static bool NetworkDenied(int family)
    {
        var fd = SocketNative(family, 1, 0);
        if (fd < 0) return Marshal.GetLastPInvokeError() is 1 or 13;
        try
        {
            var address = new byte[family == 2 ? 16 : 28];
            address[0] = (byte)address.Length; address[1] = (byte)family; address[3] = 9;
            if (family == 2) { address[4] = 127; address[7] = 1; } else address[23] = 1;
            return ConnectNative(fd, address, (uint)address.Length) == -1 && Marshal.GetLastPInvokeError() is 1 or 13;
        }
        finally { Close(fd); }
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "socket", SetLastError = true)] private static extern int SocketNative(int domain, int type, int protocol);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "connect", SetLastError = true)] private static extern int ConnectNative(int fd, byte[] address, uint size);
    [DllImport("/usr/lib/libsandbox.dylib", EntryPoint = "sandbox_init")] private static extern int SandboxInit(string policy, ulong flags, out IntPtr error);
    [DllImport("/usr/lib/libsandbox.dylib", EntryPoint = "sandbox_free_error")] private static extern void SandboxFree(IntPtr error);
    [DllImport("/usr/lib/libsandbox.dylib", EntryPoint = "sandbox_check")] private static extern int SandboxCheck(int pid, string operation, int filter, IntPtr argument);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "getppid")] private static extern int GetParent();
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "open", SetLastError = true)] private static extern int Open(string path, int flags);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "close")] private static extern int Close(int fd);
}