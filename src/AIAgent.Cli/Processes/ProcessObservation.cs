namespace AIAgent.Cli.Processes;

// Local observations only; not a host receipt, authorization or public protocol.
internal enum ProcessObservationPhase { BudgetStarted, DescriptorSent, TargetStarted, CleanupStarted, Finished }

internal readonly record struct ProcessObservation(ProcessObservationPhase Phase, long Timestamp,
    int RemainingMilliseconds = 0, int TargetPid = 0);