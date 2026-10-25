using System.Diagnostics;

namespace AIAgent.Cli.Git;

internal interface IGitProcessRunner
{
    Task<GitReadResult> RunAsync(GitProcessDescriptor descriptor, CancellationToken cancellationToken);
}

// The transport accepts only the closed Git descriptor, never an executable or arbitrary argv.
internal sealed class GitProcessRunner : IGitProcessRunner
{
    public async Task<GitReadResult> RunAsync(GitProcessDescriptor descriptor, CancellationToken cancellationToken)
    {
        var deadline = new GitProcessDeadline();
        GitProcessDeadline? cleanup = null;
        byte[] encoded;
        try { encoded = descriptor.Encode(); }
        catch (ArgumentException) { return new("git_limit"); }
        var executable = Path.Combine(AppContext.BaseDirectory, "ProcessHost", "AIAgent.ProcessHost");
        if (!File.Exists(executable) || new FileInfo(executable).LinkTarget is not null) return new("capability_unavailable");
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = descriptor.Root,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--git-read"); start.Environment.Clear();
        foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
        start.Environment["DOTNET_ROOT"] = Directory.Exists("/usr/local/share/dotnet") ? "/usr/local/share/dotnet" : "/opt/homebrew/share/dotnet";
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        if (deadline.ActiveRemaining == TimeSpan.Zero) return new("git_timed_out");
        using var process = Process.Start(start);
        if (process is null) return new("git_failed");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline.ActiveRemaining);
        var output = Drain(process.StandardOutput.BaseStream, GitProcessRecipe.MetadataLimit + 128);
        var error = Drain(process.StandardError.BaseStream, GitProcessRecipe.DiagnosticLimit);
        var confirmed = false; var stopped = false;
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(encoded, timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error).WaitAsync(timeout.Token);
            confirmed = true;
        }
        catch (OperationCanceledException)
        {
            stopped = true;
            cleanup = deadline.BeginCleanup();
            _ = await RequestStopAsync(process.StandardInput.BaseStream, cleanup);
            try { await process.WaitForExitAsync().WaitAsync(cleanup.Remaining(TimeSpan.FromSeconds(5))); }
            catch (TimeoutException) { }
        }
        finally
        {
            cleanup ??= deadline.BeginCleanup();
            if (!process.HasExited)
            {
                // Losing the supervisor cannot certify target cleanup, even after stopping the owned host.
                try { process.Kill(); } catch (InvalidOperationException) { }
                try { await process.WaitForExitAsync().WaitAsync(cleanup.Remaining(TimeSpan.FromSeconds(1))); }
                catch (TimeoutException) { }
            }
            process.StandardInput.Close(); process.StandardOutput.Close(); process.StandardError.Close();
            try { await Task.WhenAll(output, error).WaitAsync(cleanup.Remaining(TimeSpan.FromSeconds(1))); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException) { confirmed = false; }
        }
        if (!process.HasExited || !output.IsCompletedSuccessfully || !error.IsCompletedSuccessfully)
            return new("process_unconfirmed", cleanupConfirmed: false);
        if (process.ExitCode != 0) return new("process_unconfirmed", cleanupConfirmed: false);
        try
        {
            using var bytes = new MemoryStream(await output, false);
            var result = GitReadResult.Read(bytes);
            if (stopped && result.CleanupConfirmed)
                return new(cancellationToken.IsCancellationRequested ? "turn_cancelled" : "git_timed_out", result.ExitCode);
            return confirmed || result.CleanupConfirmed ? result : new("process_unconfirmed", cleanupConfirmed: false);
        }
        catch (IOException) { return new("process_unconfirmed", cleanupConfirmed: false); }
    }
    internal static async Task<bool> RequestStopAsync(Stream input, GitProcessDeadline deadline)
    {
        var remaining = deadline.Remaining(TimeSpan.FromMilliseconds(100));
        if (remaining == TimeSpan.Zero) return false;
        using var writeStop = new CancellationTokenSource(remaining);
        try
        {
            await input.WriteAsync(new byte[] { 1 }, writeStop.Token).AsTask().WaitAsync(remaining);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or TimeoutException) { return false; }
    }
    private static async Task<byte[]> Drain(Stream input, int limit)
    {
        using var bytes = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            if (bytes.Length + read > limit) throw new IOException();
            bytes.Write(buffer, 0, read);
        }
        return bytes.ToArray();
    }
    public override string ToString() => "[GitProcessRunner]";
}