using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed record ParsedArguments(bool Help, BootstrapOptions Options, string? ResumeWorkId = null);

internal static class ArgumentParser
{
    internal static ParsedArguments Parse(string[] args, string startupDirectory)
    {
        if (args.Length == 1 && args[0] == "--help")
            return new(true, new(startupDirectory, null));
        if (args.Length == 0)
            return new(false, new(startupDirectory, null));
        string? workspace = null; string? resume = null;
        if (args.Length is not (2 or 4)) throw new BootstrapFailureException(BootstrapErrorCode.InvalidArguments);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (args[i] == "--workspace" && workspace is null && !string.IsNullOrWhiteSpace(args[i + 1]) &&
                !args[i + 1].StartsWith("--", StringComparison.Ordinal)) workspace = args[i + 1];
            else if (args[i] == "--resume" && resume is null && WorkCommands.ValidId(args[i + 1])) resume = args[i + 1];
            else throw new BootstrapFailureException(BootstrapErrorCode.InvalidArguments);
        }
        return new(false, new(startupDirectory, workspace), resume);
    }
}
