using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Processes;

internal sealed class ControlledDotnetTasksFactory : IControlledDotnetTasksFactory
{
    public bool IsAvailable
    {
        get
        {
            try { return DotnetInstallation.Discover() is not null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { return false; }
        }
    }
    public bool Supports(DotnetTaskKind kind) => kind == DotnetTaskKind.Build && IsAvailable;
    public IControlledDotnetTasks Create(ConfirmedWorkspace workspace, ISensitiveDataPolicy policy,
        AgentExecutionPolicy execution, IWorkspaceFiles files,
        Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve, Action<string> progress) =>
        new ControlledDotnetTasks(workspace, policy, execution, files, approve, progress);
    public override string ToString() => "[ControlledDotnetTasksFactory]";
}

internal sealed class ControlledDotnetTasks(ConfirmedWorkspace workspace, ISensitiveDataPolicy policy,
    AgentExecutionPolicy execution, IWorkspaceFiles files,
    Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve, Action<string> progress)
    : IControlledDotnetTasks, IProcessTurnLifecycle
{
    private readonly object _session = new();
    private readonly ProcessTurnBudget _budget = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private TaskCompletionSource? _active;
    private bool _disposed;
    public void BeginTurn() => _budget.BeginTurn();
    public async Task<DotnetTaskResult> RunAsync(DotnetTaskKind task, string projectPath, CancellationToken cancellationToken)
    {
        // Pure checks precede any filesystem/platform/temporary/probe work, including this direct entry point.
        if (!policy.CanSendOrUse(projectPath, ContentBoundary.ToolArguments)) return Reject("sensitive_arguments");
        if (!Enum.IsDefined(task) || !WorkspacePathRules.TryNormalize(projectPath, out var path, out var directory) || directory ||
            !path.EndsWith(".csproj", StringComparison.Ordinal) || path.IndexOfAny(['*', '?', '[', ']']) >= 0) return Reject("invalid_path");
        if (WorkspacePathRules.IsSensitive(workspace.RootPath) || WorkspacePathRules.IsSensitive(path)) return Reject("sensitive_path");
        if (!policy.CanSendOrUse(path, ContentBoundary.ToolArguments)) return Reject("sensitive_arguments");
        if (execution.ProcessSessionStopped) return Reject("process_unconfirmed");
        var permission = execution.Plans.CheckProcess(task, path); if (permission != "none") return Reject(permission);
        // SDK 10.0.100 hardcodes MTP IPC under /tmp, outside the approved private IPC root.
        // Unknown SDK/runner layouts remain unavailable until separately verified; no target probe or fallback.
        if (task == DotnetTaskKind.Test) return Reject("capability_unavailable");
        lock (_gate)
        {
            if (_disposed || _active is not null) return Reject("workspace_unavailable");
            _active = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        WorkspaceOperationLease? lease = null; TaskEvidenceReservation? reservation = null; var granted = false;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        using var generation = execution.Plans.BorrowGeneration();
        try
        {
            if (lifetime.IsCancellationRequested) return Reject("turn_cancelled", ToolStatus.Cancelled);
            if (!execution.WorkspaceOperations.TryAcquire(lifetime.Token, out lease)) return Reject("workspace_unavailable");
            if (files is not WorkspaceFiles workspaceFiles) return Reject("capability_unavailable");
            if (execution.Rules is not { } rules) return Reject("rules_unavailable");
            var rulesError = await rules.ValidateAsync(lease!.CancellationToken, lease);
            if (rulesError != "none") return Reject(rulesError);
            var boundaries = new List<ProjectRuleObservation>();
            var scopeError = await workspaceFiles.CheckProjectRulesPathAsync(rules.Capture().Scope, path, false, boundaries, lease, lease.CancellationToken);
            if (scopeError != WorkspaceErrorCode.None) return Reject(WorkspaceToolProjection.Code(scopeError));
            var rulesStamp = rules.Capture();
            if (execution.Skills is not { } skills) return Reject("skills_unavailable");
            var skillsError = await skills.ValidateAsync(lease.CancellationToken, lease);
            if (skillsError != "none") return Reject(skillsError);
            var skillsStamp = skills.Capture();
            if (!_budget.CanStart) return Reject("process_limit");
            var installation = DotnetInstallation.Discover(); if (installation is null) return Reject("capability_unavailable");
            var admission = new DotnetProjectAdmission(new MacWorkspaceFileSystem(), policy);
            var snapshot = await admission.InspectAsync(workspace.RootPath, path, task, installation, lease!.CancellationToken);
            permission = execution.Plans.ReserveProcess(task, path, out reservation); if (permission != "none") return Reject(permission);
            var privateRoot = "/private/tmp/aia-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            var timeout = _budget.PreviewTimeout(task);
            var display = DotnetRecipe.Preview(snapshot, task, privateRoot, timeout, reservation?.Context) + ProcessApprovalText.Prompt;
            if (Encoding.UTF8.GetByteCount(display) > 65536) return Reject("preview_too_large");
            if (!policy.CanSendOrUse(display, ContentBoundary.ToolResult)) return Reject("sensitive_content");
            var preview = new ProcessApprovalPreview(display, _session, execution.Plans.Generation, reservation?.Context,
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
            var decision = await approve(preview, lease.CancellationToken);
            DotnetTaskResult NotStarted(string error, ToolStatus status = ToolStatus.Rejected)
            {
                var summary = new ProcessExecutionSummary(task, path, ProcessStartState.NotStarted, ProcessEndState.NotStarted,
                    null, 0, true, false, ValidationOutcome.NotVerified);
                var result = new DotnetTaskResult(ProcessResultWriter.Create(status, error, summary, null, policy, true));
                execution.Plans.RecordProcess(reservation, status, error, summary); reservation = null; return result;
            }
            if (decision != ProcessApprovalDecision.Approved)
                return NotStarted(decision is ProcessApprovalDecision.Cancelled or ProcessApprovalDecision.EndOfInput ? "turn_cancelled" : decision == ProcessApprovalDecision.Unavailable ? "process_io_failure" : "approval_declined",
                    decision is ProcessApprovalDecision.Cancelled or ProcessApprovalDecision.EndOfInput ? ToolStatus.Cancelled : decision == ProcessApprovalDecision.Unavailable ? ToolStatus.Failed : ToolStatus.Rejected);
            if (!lease.IsCurrent || preview.Generation != execution.Plans.Generation || lifetime.IsCancellationRequested)
                return NotStarted("turn_cancelled", ToolStatus.Cancelled);
            rulesError = await rules.ValidateAsync(lease.CancellationToken, lease);
            if (rulesError != "none" || !rules.IsCurrent(rulesStamp)) return NotStarted(rulesError == "none" ? "rules_changed" : rulesError);
            skillsError = await skills.ValidateAsync(lease.CancellationToken, lease);
            if (skillsError != "none" || !skills.IsCurrent(skillsStamp)) return NotStarted(skillsError == "none" ? "skills_changed" : skillsError);
            scopeError = workspaceFiles.RevalidateProjectRulesPaths(boundaries, lease);
            if (scopeError != WorkspaceErrorCode.None) return NotStarted(WorkspaceToolProjection.Code(scopeError));
            permission = execution.Plans.CheckProcess(task, path); if (permission != "none") return NotStarted(permission);
            var renewedInstallation = DotnetInstallation.Discover();
            if (renewedInstallation is null || renewedInstallation.Root != installation.Root ||
                renewedInstallation.SdkVersion != installation.SdkVersion || renewedInstallation.HostDirectory != installation.HostDirectory ||
                renewedInstallation.Packages != installation.Packages) return NotStarted("path_changed");
            var recheck = await admission.InspectAsync(workspace.RootPath, path, task, installation, lease.CancellationToken);
            if (!snapshot.Matches(recheck)) return NotStarted("path_changed");
            if (!lease.IsCurrent || lifetime.IsCancellationRequested) return NotStarted("turn_cancelled", ToolStatus.Cancelled);
            rulesError = await rules.ValidateAsync(lease.CancellationToken, lease);
            if (rulesError != "none") return NotStarted(rulesError);
            scopeError = workspaceFiles.RevalidateProjectRulesPaths(boundaries, lease);
            if (scopeError != WorkspaceErrorCode.None) return NotStarted(WorkspaceToolProjection.Code(scopeError));
            skillsError = await skills.ValidateAsync(lease.CancellationToken, lease);
            if (skillsError != "none" || !skills.IsCurrent(skillsStamp)) return NotStarted(skillsError == "none" ? "skills_changed" : skillsError);
            workspaceFiles.InvalidateForProcess(lease);
            // No await separates final authority checks from the single budget/consent consumption point.
            if (!_budget.TryStart(task, out var budget)) return NotStarted("process_limit");
            granted = true;
            DotnetTaskResult result;
            using (budget)
            {
                result = await RunNativeAsync(snapshot, task, path, privateRoot, budget!, lease.CancellationToken);
            }
            if (result.Summary is { } fact) execution.Plans.RecordProcess(reservation, result.Status, result.ErrorCode, fact);
            reservation = null;
            try { if (lease.IsCurrent) _ = await rules.ValidateAsync(CancellationToken.None, lease); }
            catch (Exception) { rules.MarkFatalFailure(); }
            try { if (lease.IsCurrent) _ = await skills.ValidateAsync(CancellationToken.None, lease); }
            catch (Exception) { skills.MarkFatalFailure(); }
            return result;
        }
        catch (ProcessAdmissionException ex) { return Reject(ex.Code); }
        catch (OperationCanceledException) { return Reject("turn_cancelled", ToolStatus.Cancelled); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or XmlException or DecoderFallbackException or ArgumentException or InvalidOperationException or PlatformNotSupportedException)
        {
            if (granted)
            {
                execution.StopProcesses();
                var summary = new ProcessExecutionSummary(task, path, ProcessStartState.NotStarted, ProcessEndState.Unconfirmed,
                    null, 0, false, true, ValidationOutcome.NotVerified);
                execution.Plans.RecordProcess(reservation, ToolStatus.Failed, "process_cleanup_failed", summary); reservation = null;
                return new(new(ToolStatus.Failed, "process_cleanup_failed", null, processSummary: summary, processCleanupFailed: true, stopAfterOperation: true));
            }
            return Reject("capability_unavailable");
        }
        finally
        {
            try { if (granted && lease is not null && files is WorkspaceFiles workspaceFiles) workspaceFiles.InvalidateAfterProcess(lease); }
            finally
            {
                if (reservation is not null) execution.Plans.Release(reservation);
                lease?.Dispose();
                execution.Rules?.Settle();
                execution.Skills?.Settle();
                lock (_gate) { _active?.TrySetResult(); _active = null; }
            }
        }
    }

    private async Task<DotnetTaskResult> RunNativeAsync(ProcessAdmissionSnapshot snapshot, DotnetTaskKind task, string path,
        string privateRoot, ProcessBudgetLease budget, CancellationToken token)
    {
        var started = 0; var cleanupStarted = 0; string? requested = null;
        using var request = new CancellationTokenSource();
        using var timeout = new ProcessExecutionTimeout(TimeSpan.FromMilliseconds(Math.Max(1, budget.Timeout.TotalMilliseconds - budget.ElapsedMilliseconds)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token, request.Token);
        var output = new ProcessOutputCollector(policy, reason => { Interlocked.CompareExchange(ref requested, reason, null); request.Cancel(); });
        Process? wrapper = null; Task<HostReply>? control = null; Task? stdout = null; Task? stderr = null;
        using var pipeReadCancellation = new CancellationTokenSource();
        using var commands = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        using var events = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var reply = new HostReply(false, false, false, false, -1); var wrapperStarted = false; var controlFailed = false;
        long cleanupStartedAt = 0;
        void StartCleanupDeadline() => Interlocked.CompareExchange(ref cleanupStartedAt, Stopwatch.GetTimestamp(), 0);
        int RemainingCleanupMilliseconds()
        {
            var startedAt = Volatile.Read(ref cleanupStartedAt);
            return startedAt == 0 ? 6000 : Math.Max(0, 6000 - (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        try
        {
            PrivateProcessDirectory.Create(privateRoot, snapshot.Installation.SdkVersion);
            var start = new ProcessStartInfo("/usr/bin/sandbox-exec")
            {
                WorkingDirectory = snapshot.Root,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.Environment.Clear(); foreach (var pair in DotnetRecipe.Environment(snapshot.Installation, privateRoot)) start.Environment.Add(pair.Key, pair.Value);
            start.ArgumentList.Add("-p"); start.ArgumentList.Add(MacSandboxPolicy.Create(snapshot, privateRoot));
            start.ArgumentList.Add(snapshot.Installation.HostExecutable); start.ArgumentList.Add(commands.GetClientHandleAsString()); start.ArgumentList.Add(events.GetClientHandleAsString());
            progress("受控 .NET 任務：正在驗證隔離與啟動監督器。");
            wrapper = new() { StartInfo = start };
            if (!wrapper.Start()) throw new IOException(); wrapperStarted = true; wrapper.StandardInput.Close();
            commands.DisposeLocalCopyOfClientHandle(); events.DisposeLocalCopyOfClientHandle();
            stdout = output.ReadAsync(wrapper.StandardOutput.BaseStream, false, pipeReadCancellation.Token);
            stderr = output.ReadAsync(wrapper.StandardError.BaseStream, true, pipeReadCancellation.Token);
            using (var writer = new BinaryWriter(commands, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0x41494131); writer.Write((byte)(task == DotnetTaskKind.Build ? 0 : 1));
                foreach (var field in new[] { snapshot.Root, snapshot.Installation.Executable, snapshot.Project, privateRoot, snapshot.Installation.Packages })
                { var bytes = Encoding.UTF8.GetBytes(field); writer.Write(bytes.Length); writer.Write(bytes); }
                writer.Write(Math.Max(1, (int)(budget.Timeout.TotalMilliseconds - budget.ElapsedMilliseconds))); writer.Flush();
            }
            control = Task.Run(() =>
            {
                using var reader = new BinaryReader(events, Encoding.UTF8, leaveOpen: true);
                while (true)
                {
                    switch (reader.ReadByte())
                    {
                        case 1:
                            if (Interlocked.Exchange(ref started, reader.ReadInt32()) != 0 || Volatile.Read(ref started) <= 0) throw new IOException();
                            break;
                        case 5:
                            if (Interlocked.Exchange(ref cleanupStarted, 1) != 0) throw new IOException();
                            StartCleanupDeadline(); budget.Dispose(); timeout.OnCleanupStarted(); break;
                        case 2: return new HostReply(true, reader.ReadBooleanAfterInt32(out var exit), reader.ReadBoolean(), reader.ReadBoolean(), exit);
                        case 3: return new HostReply(false, false, true, false, -1);
                        case 4: throw new IOException();
                        default: throw new IOException();
                    }
                }
            });
            var tick = Stopwatch.StartNew();
            while (!control.IsCompleted && !linked.IsCancellationRequested)
            {
                await Task.WhenAny(control, Task.Delay(100, CancellationToken.None));
                if (tick.Elapsed >= TimeSpan.FromSeconds(5))
                { progress("受控 .NET 任務仍在執行（已執行 " + budget.ElapsedMilliseconds / 1000 + " 秒）。"); tick.Restart(); }
            }
            if (!control.IsCompleted)
            {
                budget.Dispose();
                StartCleanupDeadline();
                try { commands.WriteByte(1); commands.Flush(); } catch (IOException) { controlFailed = true; }
                if (await Task.WhenAny(control, Task.Delay(RemainingCleanupMilliseconds())) != control) controlFailed = true;
            }
            if (!controlFailed) { reply = await control; commands.Dispose(); }
        }
        catch (Exception)
        { controlFailed = wrapperStarted; }
        finally
        {
            budget.Dispose();
            if (wrapperStarted && wrapper is not null)
            {
                // A lost/faulted receipt still gives the host its full bounded TERM/KILL window.
                StartCleanupDeadline();
                if (control is null || !control.IsCompletedSuccessfully)
                {
                    try { commands.WriteByte(1); commands.Flush(); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { controlFailed = true; }
                    if (control is not null && !control.IsCompleted &&
                        await Task.WhenAny(control, Task.Delay(RemainingCleanupMilliseconds())) != control) controlFailed = true;
                    if (control is { IsCompletedSuccessfully: true })
                    { reply = await control; commands.Dispose(); }
                    else controlFailed = true;
                }
                try
                {
                    var wrapperExit = wrapper.WaitForExitAsync();
                    if (await Task.WhenAny(wrapperExit, Task.Delay(RemainingCleanupMilliseconds())) != wrapperExit && !wrapper.HasExited)
                    { controlFailed = true; wrapper.Kill(); }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { controlFailed = true; }
                if (stdout is not null && stderr is not null)
                {
                    var readers = Task.WhenAll(stdout, stderr);
                    if (await Task.WhenAny(readers, Task.Delay(Math.Min(1000, RemainingCleanupMilliseconds()))) != readers) { controlFailed = true; pipeReadCancellation.Cancel(); }
                }
            }
            pipeReadCancellation.Cancel(); wrapper?.Dispose();
        }
        var actualStarted = Volatile.Read(ref started) > 0;
        var unconfirmed = controlFailed || wrapperStarted && !reply.CleanupConfirmed;
        var end = unconfirmed ? ProcessEndState.Unconfirmed : token.IsCancellationRequested ? ProcessEndState.Cancelled :
            timeout.Expired ? ProcessEndState.TimedOut : output.OutputLimit || reply.IdentityLimit ? ProcessEndState.OutputLimit :
            output.State != "safe" || requested is not null ? ProcessEndState.Failed : actualStarted && reply.Exited ? ProcessEndState.Exited :
            actualStarted ? ProcessEndState.Failed : ProcessEndState.NotStarted;
        var startState = actualStarted ? ProcessStartState.Started : !reply.TargetAttempted ? ProcessStartState.NotStarted : ProcessStartState.StartFailed;
        var counts = task == DotnetTaskKind.Test ? output.Tests : null;
        var validation = end == ProcessEndState.Exited && reply.ExitCode == 0 && reply.CleanupConfirmed && output.State == "safe"
            ? task == DotnetTaskKind.Build ? ValidationOutcome.BuildSucceeded : counts is { Executed: > 0, Failed: 0 } ? ValidationOutcome.TestsPassed : ValidationOutcome.NotVerified
            : ValidationOutcome.NotVerified;
        var error = unconfirmed ? "process_cleanup_failed" : end == ProcessEndState.Cancelled ? "turn_cancelled" :
            end == ProcessEndState.TimedOut ? "process_timed_out" : end == ProcessEndState.OutputLimit ? "process_output_limit" :
            output.State == "sensitive" ? "sensitive_content" : requested == "process_io_failure" ? "process_io_failure" : output.State != "safe" ? "process_output_invalid" :
            !reply.TargetAttempted ? "capability_unavailable" : !actualStarted ? "process_start_failed" :
            end != ProcessEndState.Exited || reply.ExitCode != 0 ? "process_failed" : validation == ValidationOutcome.NotVerified ? "validation_inconclusive" : "none";
        if (unconfirmed) execution.StopProcesses();
        var summary = new ProcessExecutionSummary(task, path, startState, end, end == ProcessEndState.Exited ? reply.ExitCode : null,
            budget.ElapsedMilliseconds, !unconfirmed, unconfirmed, validation, counts, output.StdoutBytes, output.StderrBytes,
            output.StdoutTruncated, output.StderrTruncated, output.State);
        var status = error == "none" ? ToolStatus.Success : end == ProcessEndState.Cancelled ? ToolStatus.Cancelled :
            !reply.TargetAttempted && !unconfirmed ? ToolStatus.Rejected : ToolStatus.Failed;
        try { return new(ProcessResultWriter.Create(status, error, summary, output, policy, error != "none")); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        { return new(new(status, error, null, processSummary: summary, processCleanupFailed: summary.CleanupFailed, stopAfterOperation: true)); }
    }
    private DotnetTaskResult Reject(string code, ToolStatus status = ToolStatus.Rejected)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { status = PlanProjection.Name(status), error_code = code });
        using var document = JsonDocument.Parse(bytes);
        var response = PlanProjection.SafeJson(bytes, policy, ContentBoundary.ToolResult) ? (JsonElement?)document.RootElement : null;
        return new(new(status, code, response, stopAfterOperation: true));
    }
    public async ValueTask DisposeAsync()
    {
        Task? pending;
        lock (_gate) { _disposed = true; _lifetime.Cancel(); pending = _active?.Task; }
        if (pending is not null) await pending;
        _lifetime.Dispose();
    }
    public override string ToString() => "[ControlledDotnetTasks]";
    private sealed class HostReply(bool targetAttempted, bool exited, bool cleanupConfirmed, bool identityLimit, int exitCode)
    {
        internal bool TargetAttempted { get; } = targetAttempted;
        internal bool Exited { get; } = exited;
        internal bool CleanupConfirmed { get; } = cleanupConfirmed;
        internal bool IdentityLimit { get; } = identityLimit;
        internal int ExitCode { get; } = exitCode;
    }
}

internal sealed class ProcessExecutionTimeout : IDisposable
{
    private readonly CancellationTokenSource _source;
    private int _cleanupStarted;
    private int _expiredBeforeCleanup;
    internal ProcessExecutionTimeout(TimeSpan duration) => _source = new(duration);
    internal CancellationToken Token => _source.Token;
    internal bool Expired => Volatile.Read(ref _expiredBeforeCleanup) != 0 ||
        Volatile.Read(ref _cleanupStarted) == 0 && _source.IsCancellationRequested;
    internal void OnCleanupStarted()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0) throw new InvalidOperationException();
        _source.CancelAfter(Timeout.InfiniteTimeSpan);
        if (_source.IsCancellationRequested) Interlocked.Exchange(ref _expiredBeforeCleanup, 1);
    }
    public void Dispose() => _source.Dispose();
}

internal static class BinaryControlReader
{
    internal static bool ReadBooleanAfterInt32(this BinaryReader reader, out int value) { value = reader.ReadInt32(); return reader.ReadBoolean(); }
}

internal static class PrivateProcessDirectory
{
    internal static void Create(string path, string sdkVersion)
    {
        if (Mkdir(path, 0x1c0) != 0) throw new IOException("私人程序目錄不可用");
        var fileSystem = new MacWorkspaceFileSystem(); var state = fileSystem.Inspect(path);
        if (state.Kind != WorkspaceNodeKind.Directory || state.User != GetUid() || (state.Mode & 0x1ff) != 0x1c0) throw new IOException("私人程序目錄不可用");
        foreach (var child in new[] { "home", "tmp", "results", "probe-source" }) if (Mkdir(path + "/" + child, 0x1c0) != 0) throw new IOException();
        // An empty private profile has no old NuGet state to migrate and must not provision certificates.
        if (Mkdir(path + "/home/.dotnet", 0x1c0) != 0) throw new IOException();
        foreach (var sentinel in new[] { ".dotnetFirstUseSentinel", ".aspNetCertificateSentinel", ".toolpath.sentinel" })
            using (File.Create(path + "/home/.dotnet/" + sdkVersion + sentinel)) { }
        Directory.CreateDirectory(path + "/home/.local/share/NuGet/Migrations");
        using (File.Create(path + "/home/.local/share/NuGet/Migrations/1")) { }
        File.WriteAllText(path + "/probe-source/source", "isolated source probe");
        File.WriteAllText(path + "/probe-denied", "non-secret read-denial probe");
        if (fileSystem.Inspect(path) != state && fileSystem.Inspect(path).Identity != state.Identity) throw new IOException();
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mkdir", SetLastError = true)] private static extern int Mkdir(string path, ushort mode);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "geteuid")] private static extern uint GetUid();
}