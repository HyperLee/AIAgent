using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AIAgent.ProcessHost;

internal static class SandboxProbe
{
    internal static bool Check(Descriptor descriptor)
    {
        try
        {
            // Safe probe fixtures are created by the parent only after this operation is approved.
            var fixture = descriptor.PrivateRoot + "/probe-source";
            using (File.OpenRead(descriptor.Dotnet)) { }
            File.WriteAllText(descriptor.PrivateRoot + "/tmp/allowed", "probe");
            if (!Denied(() => File.ReadAllText(descriptor.PrivateRoot + "/probe-denied")) ||
                !Denied(() => File.WriteAllText(fixture + "/source", "changed")) ||
                !Denied(() => Directory.Move(fixture, fixture + "-moved")) ||
                !Denied(() => File.CreateSymbolicLink(descriptor.PrivateRoot + "/tmp/alias", fixture + "/source")) ||
                !Denied(() => { if (Link(fixture + "/source", descriptor.PrivateRoot + "/tmp/hardalias") == 0) return; if (Marshal.GetLastPInvokeError() is 1 or 13) throw new UnauthorizedAccessException(); throw new IOException(); }) ||
                !Denied(() => File.WriteAllText(descriptor.PrivateRoot + "/tmp/.env", "synthetic")) ||
                !Denied(() => File.WriteAllText(descriptor.PrivateRoot + "/tmp/CREDENTIALS.json", "synthetic")) ||
                !Denied(() => { using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); socket.Connect(IPAddress.Loopback, 9); }) ||
                !Denied(() => { using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp); socket.Connect(IPAddress.IPv6Loopback, 9); })) return false;
            using var ipc = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = descriptor.PrivateRoot + "/tmp/probe.sock";
            if (System.Text.Encoding.UTF8.GetByteCount(endpoint) >= 104) return false;
            ipc.Bind(new UnixDomainSocketEndPoint(endpoint));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SocketException) { return false; }
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "link", SetLastError = true)] private static extern int Link(string original, string alias);
    private static bool Denied(Action action)
    {
        try { action(); return false; }
        catch (UnauthorizedAccessException) { return true; }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 1 or 13) { return true; }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied || ex.NativeErrorCode is 1 or 13) { return true; }
    }
}