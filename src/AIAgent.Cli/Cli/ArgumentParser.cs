using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed record ParsedArguments(bool Help, BootstrapOptions Options);

internal static class ArgumentParser
{
    internal static ParsedArguments Parse(string[] args, string startupDirectory)
    {
        if (args.Length == 1 && args[0] == "--help")
            return new(true, new(startupDirectory, null));
        if (args.Length == 0)
            return new(false, new(startupDirectory, null));
        if (args.Length == 2 && args[0] == "--workspace" &&
            !string.IsNullOrWhiteSpace(args[1]) && !args[1].StartsWith("--", StringComparison.Ordinal))
            return new(false, new(startupDirectory, args[1]));
        throw new BootstrapFailureException(BootstrapErrorCode.InvalidArguments);
    }
}