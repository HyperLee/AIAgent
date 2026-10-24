using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Processes;

internal enum DotnetTaskKind { Build, Test }
internal enum ProcessStartState { NotStarted, Started, StartFailed }
internal enum ProcessEndState { NotStarted, Exited, Cancelled, TimedOut, OutputLimit, Failed, Unconfirmed }
internal enum ValidationOutcome { NotVerified, BuildSucceeded, TestsPassed }

internal sealed class TestCounts
{
    internal TestCounts(long total, long passed, long failed, long skipped)
    {
        if (total < 0 || passed < 0 || failed < 0 || skipped < 0) throw new ArgumentOutOfRangeException(nameof(total));
        var executed = checked(passed + failed);
        if (total != checked(executed + skipped)) throw new ArgumentException("測試計數不一致");
        Total = total; Passed = passed; Failed = failed; Skipped = skipped; Executed = executed;
    }
    internal long Total { get; }
    internal long Passed { get; }
    internal long Failed { get; }
    internal long Skipped { get; }
    internal long Executed { get; }
    public override string ToString() => "[TestCounts]";
}

internal sealed class ProcessExecutionSummary
{
    internal ProcessExecutionSummary(DotnetTaskKind task, string projectPath, ProcessStartState startState,
        ProcessEndState endState, int? exitCode, long elapsedMilliseconds, bool cleanupConfirmed, bool cleanupFailed,
        ValidationOutcome validationOutcome, TestCounts? tests = null, long stdoutBytes = 0, long stderrBytes = 0,
        bool stdoutTruncated = false, bool stderrTruncated = false, string outputState = "safe")
    {
        if (!Enum.IsDefined(task) || !Enum.IsDefined(startState) || !Enum.IsDefined(endState) || !Enum.IsDefined(validationOutcome) ||
            !WorkspacePathRules.IsCanonical(projectPath) || WorkspacePathRules.IsSensitive(projectPath) ||
            !projectPath.EndsWith(".csproj", StringComparison.Ordinal) || projectPath.IndexOfAny(['*', '?', '[', ']']) >= 0 ||
            elapsedMilliseconds < 0 || stdoutBytes < 0 || stderrBytes < 0 ||
            outputState is not ("safe" or "invalid_utf8" or "sensitive" or "unavailable") || cleanupConfirmed && cleanupFailed ||
            (endState == ProcessEndState.Exited) != exitCode.HasValue ||
            endState == ProcessEndState.Exited && startState != ProcessStartState.Started ||
            startState == ProcessStartState.Started && endState == ProcessEndState.NotStarted ||
            startState == ProcessStartState.StartFailed && endState is not (ProcessEndState.NotStarted or ProcessEndState.Failed or ProcessEndState.Unconfirmed) ||
            task == DotnetTaskKind.Build && tests is not null ||
            cleanupFailed && endState != ProcessEndState.Unconfirmed ||
            endState == ProcessEndState.Unconfirmed && cleanupConfirmed)
            throw new ArgumentException("程序摘要無效");
        _ = checked(stdoutBytes + stderrBytes);
        if (validationOutcome != ValidationOutcome.NotVerified &&
            (startState != ProcessStartState.Started || endState != ProcessEndState.Exited || exitCode != 0 ||
             !cleanupConfirmed || cleanupFailed || outputState != "safe" ||
             task == DotnetTaskKind.Build && validationOutcome != ValidationOutcome.BuildSucceeded ||
             task == DotnetTaskKind.Test && (validationOutcome != ValidationOutcome.TestsPassed || tests is null || tests.Executed == 0 || tests.Failed != 0)))
            throw new ArgumentException("驗證結果無效");
        Task = task; ProjectPath = projectPath; StartState = startState; EndState = endState; ExitCode = exitCode;
        ElapsedMilliseconds = elapsedMilliseconds; CleanupConfirmed = cleanupConfirmed; CleanupFailed = cleanupFailed;
        ValidationOutcome = validationOutcome; Tests = tests; StdoutBytes = stdoutBytes; StderrBytes = stderrBytes;
        StdoutTruncated = stdoutTruncated; StderrTruncated = stderrTruncated; OutputState = outputState;
    }
    internal DotnetTaskKind Task { get; }
    internal string ProjectPath { get; }
    internal ProcessStartState StartState { get; }
    internal ProcessEndState EndState { get; }
    internal int? ExitCode { get; }
    internal long ElapsedMilliseconds { get; }
    internal bool CleanupConfirmed { get; }
    internal bool CleanupFailed { get; }
    internal ValidationOutcome ValidationOutcome { get; }
    internal TestCounts? Tests { get; }
    internal long StdoutBytes { get; }
    internal long StderrBytes { get; }
    internal bool StdoutTruncated { get; }
    internal bool StderrTruncated { get; }
    internal string OutputState { get; }
    public override string ToString() => "[ProcessExecutionSummary]";
}