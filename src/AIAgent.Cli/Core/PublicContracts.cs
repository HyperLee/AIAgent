namespace AIAgent.Cli.Core;

public sealed record BootstrapOptions(string StartupDirectory, string? WorkspaceArgument);
public sealed record PublicSettings(string? Model);
public sealed record ConfirmedWorkspace(string RootPath);

public enum BootstrapStatus { Ready, Help, Declined, EndOfInput, Cancelled, Failed }
public enum KeyInputStatus { Accepted, EndOfInput, Cancelled, Unavailable }
public enum ContentBoundary { ModelInput, ToolArguments, ToolResult, WriteCandidate }

public enum BootstrapErrorCode { None, InvalidArguments, InvalidSettings, WorkspaceUnavailable, TerminalUnavailable, IoFailure }

internal sealed class BootstrapFailureException(BootstrapErrorCode code) : Exception("啟動失敗")
{
    internal BootstrapErrorCode Code { get; } = code;
}

internal sealed class BootstrapEndOfInputException() : Exception("輸入結束");