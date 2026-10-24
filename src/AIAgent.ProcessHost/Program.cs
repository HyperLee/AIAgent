using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace AIAgent.ProcessHost;

// This internal apphost accepts pipe handles only; commands are never supplied as argv.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsMacOS() || args.Length != 2 || !int.TryParse(args[0], out var inputFd) ||
            !int.TryParse(args[1], out var outputFd) || inputFd < 3 || outputFd < 3 || inputFd == outputFd) return 64;
        using var input = new AnonymousPipeClientStream(PipeDirection.In, args[0]);
        using var output = new AnonymousPipeClientStream(PipeDirection.Out, args[1]);
        Native.CloseOnExec(inputFd); Native.CloseOnExec(outputFd);
        using var reader = new BinaryReader(input, new UTF8Encoding(false, true), leaveOpen: true);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        try
        {
            var descriptor = Descriptor.Read(reader);
            // The built-in gate runs inside the exact target policy, before any project execution.
            if (!SandboxProbe.Check(descriptor)) { writer.Write((byte)3); writer.Flush(); return 77; }
            using var stop = new CancellationTokenSource();
            var control = Task.Run(() =>
            {
                try { if (input.ReadByte() != 1) Interlocked.Exchange(ref s_controlLost, 1); }
                catch (IOException) { Interlocked.Exchange(ref s_controlLost, 1); }
                stop.Cancel();
            });
            var result = await Supervisor.Run(descriptor, stop.Token, pid =>
            { writer.Write((byte)1); writer.Write(pid); writer.Flush(); }, () => { writer.Write((byte)5); writer.Flush(); });
            writer.Write((byte)2); writer.Write(result.ExitCode); writer.Write(result.Exited);
            writer.Write(result.CleanupConfirmed && Volatile.Read(ref s_controlLost) == 0);
            writer.Write(result.IdentityLimit); writer.Flush();
            GC.KeepAlive(control);
            return result.CleanupConfirmed ? 0 : 78;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            try { writer.Write((byte)4); writer.Flush(); } catch (IOException) { }
            return 70;
        }
    }
    private static int s_controlLost;
}

internal sealed class Descriptor(byte kind, string root, string dotnet, string project, string privateRoot, string packages, int timeout)
{
    internal byte Kind { get; } = kind;
    internal string Root { get; } = root;
    internal string Dotnet { get; } = dotnet;
    internal string Project { get; } = project;
    internal string PrivateRoot { get; } = privateRoot;
    internal string Packages { get; } = packages;
    internal int Timeout { get; } = timeout;
    internal static Descriptor Read(BinaryReader reader)
    {
        if (reader.ReadInt32() != 0x41494131) throw new IOException();
        var kind = reader.ReadByte(); if (kind > 1) throw new IOException();
        string Field()
        {
            var length = reader.ReadInt32(); if (length is < 1 or > 16384) throw new IOException();
            var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new IOException();
            var text = new UTF8Encoding(false, true).GetString(bytes);
            if (text[0] != '/' || text.Any(char.IsControl) || text.Contains('\0')) throw new IOException();
            return text;
        }
        var root = Field(); var dotnet = Field(); var project = Field(); var privateRoot = Field(); var packages = Field();
        var timeout = reader.ReadInt32();
        if (timeout is < 1 or > 180000 || !project.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal) ||
            !project.EndsWith(".csproj", StringComparison.Ordinal) || !privateRoot.StartsWith("/private/tmp/aia-", StringComparison.Ordinal) ||
            privateRoot.Length != 33 || privateRoot[17..].Any(c => !char.IsAsciiHexDigit(c)) ||
            !(dotnet == "/usr/local/share/dotnet/dotnet" || dotnet == "/opt/homebrew/share/dotnet/dotnet")) throw new IOException();
        return new(kind, root, dotnet, project, privateRoot, packages, timeout);
    }
    internal string[] Arguments => Kind == 0
        ? [Dotnet, "build", Project, "--configuration", "Debug", "--no-restore", "--nologo", "--disable-build-servers", "--tl:off", "--verbosity", "minimal", "-maxcpucount:1", "-nodeReuse:false", "-property:UseSharedCompilation=false"]
        : [Dotnet, "test", "--project", Project, "--configuration", "Debug", "--no-restore", "--no-launch-profile", "--no-ansi", "--no-progress", "--output", "Normal", "--results-directory", PrivateRoot + "/results", "-maxcpucount:1", "-nodeReuse:false", "-property:UseSharedCompilation=false", "--", "--minimum-expected-tests", "1"];
    internal string[] Environment => ControlledEnvironment.Create(Dotnet, Packages, PrivateRoot);
    public override string ToString() => "[Descriptor]";
}

internal static class ControlledEnvironment
{
    internal static string[] Create(string dotnet, string packages, string privateRoot) =>
    [
        "PATH=/usr/bin:/bin:" + Path.GetDirectoryName(dotnet), "DOTNET_ROOT=" + Path.GetDirectoryName(dotnet),
        "DOTNET_HOST_PATH=" + dotnet, "NUGET_PACKAGES=" + packages,
        "HOME=" + privateRoot + "/home", "DOTNET_CLI_HOME=" + privateRoot + "/home", "TMPDIR=" + privateRoot + "/tmp/",
        "LANG=en_US.UTF-8", "LC_ALL=en_US.UTF-8", "DOTNET_CLI_UI_LANGUAGE=en-US", "TZ=UTC",
        "DOTNET_CLI_TELEMETRY_OPTOUT=1", "TESTINGPLATFORM_TELEMETRY_OPTOUT=1", "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1",
        "DOTNET_CLI_USE_MSBUILD_SERVER=0", "MSBUILDDISABLENODEREUSE=1", "DOTNET_EnableDiagnostics=0", "DOTNET_NOLOGO=1"
    ];
}