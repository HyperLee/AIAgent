using System.Text;

#if GIT_PROCESS_HOST
namespace AIAgent.ProcessHost.Git;
#else
namespace AIAgent.Cli.Git;
#endif

internal sealed class GitProcessDescriptor(string root, string executableIdentity, GitReadRecipe recipe, GitReadRequest request)
{
    internal string Root { get; } = root;
    internal string ExecutableIdentity { get; } = executableIdentity;
    internal GitReadRecipe Recipe { get; } = recipe;
    internal GitReadRequest Request { get; } = request;
    internal void Write(Stream output) => output.Write(Encode());
    internal byte[] Encode()
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(0x41494731); Field(Root); Field(ExecutableIdentity); writer.Write((byte)Recipe);
        Field(Request.Scope); Field(Request.ObjectId ?? ""); Field(Request.HeadReference ?? ""); Field(Request.ObjectFormat);
        writer.Write(Request.Paths.Count); foreach (var path in Request.Paths) Field(path);
        writer.Write(Request.IgnoreReadFiles.Count); foreach (var path in Request.IgnoreReadFiles) Field(path);
        writer.Flush();
        if (output.Length > GitProcessRecipe.MetadataLimit) throw new ArgumentException("Git descriptor limit exceeded.");
        return output.ToArray();
        void Field(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > 16384 || output.Length + bytes.Length + 4 > GitProcessRecipe.MetadataLimit)
                throw new ArgumentException("Git descriptor limit exceeded.");
            writer.Write(bytes.Length); writer.Write(bytes);
        }
    }
    internal static GitProcessDescriptor Read(Stream input)
    {
        using var reader = new BinaryReader(input, new UTF8Encoding(false, true), true);
        if (reader.ReadInt32() != 0x41494731) throw new IOException();
        var total = 0;
        string Field()
        {
            var length = reader.ReadInt32(); total = checked(total + length);
            if (length is < 0 or > 16384 || total > GitProcessRecipe.MetadataLimit) throw new IOException();
            var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new IOException();
            var value = new UTF8Encoding(false, true).GetString(bytes);
            if (value.Any(char.IsControl)) throw new IOException();
            return value;
        }
        string[] Fields()
        {
            var count = reader.ReadInt32(); if (count is < 0 or > 8192) throw new IOException();
            var values = new string[count]; for (var index = 0; index < count; index++) values[index] = Field();
            return values;
        }
        var root = Field(); var identity = Field(); var recipe = (GitReadRecipe)reader.ReadByte();
        var scope = Field(); var oid = Field(); var head = Field(); var format = Field(); var paths = Fields(); var ignores = Fields();
        var request = new GitReadRequest(scope, oid.Length == 0 ? null : oid, head.Length == 0 ? null : head, paths, ignores, format);
        if (!Path.IsPathFullyQualified(root) || Path.GetFullPath(root) != root || root == "/" ||
            identity.Length != 64 || !identity.All(char.IsAsciiHexDigit) || !GitProcessRecipe.ValidRequest(root, recipe, request)) throw new IOException();
        return new(root, identity, recipe, request);
    }
    public override string ToString() => "[GitProcessDescriptor]";
}

internal sealed class GitReadResult
{
    private readonly byte[] _stdout;
    internal GitReadResult(string errorCode, int? exitCode = null, ReadOnlyMemory<byte> stdout = default, bool cleanupConfirmed = true)
    { ErrorCode = errorCode; ExitCode = exitCode; _stdout = stdout.ToArray(); CleanupConfirmed = cleanupConfirmed; }
    internal string ErrorCode { get; }
    internal int? ExitCode { get; }
    internal ReadOnlyMemory<byte> Stdout => _stdout.ToArray();
    internal bool CleanupConfirmed { get; }
    internal void Write(Stream output)
    {
        using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(0x41494732); writer.Write(ErrorCode); writer.Write(ExitCode ?? -1); writer.Write(CleanupConfirmed);
        writer.Write(_stdout.Length); writer.Write(_stdout); writer.Flush();
    }
    internal static GitReadResult Read(Stream input)
    {
        using var reader = new BinaryReader(input, new UTF8Encoding(false, true), true);
        if (reader.ReadInt32() != 0x41494732) throw new IOException();
        var codeLength = reader.ReadByte();
        if (codeLength > 64) throw new IOException();
        var codeBytes = reader.ReadBytes(codeLength); if (codeBytes.Length != codeLength) throw new IOException();
        var code = Encoding.ASCII.GetString(codeBytes);
        if (code is not ("none" or "capability_unavailable" or "git_unavailable" or "git_failed" or "git_limit" or
            "git_timed_out" or "turn_cancelled" or "process_unconfirmed" or "process_cleanup_failed")) throw new IOException();
        var exit = reader.ReadInt32(); var cleanup = reader.ReadBoolean(); var length = reader.ReadInt32();
        if (length is < 0 or > GitProcessRecipe.MetadataLimit || code != "none" && length != 0) throw new IOException();
        var bytes = reader.ReadBytes(length); if (bytes.Length != length || input.ReadByte() != -1) throw new IOException();
        return new(code, exit < 0 ? null : exit, bytes, cleanup);
    }
    public override string ToString() => "[GitReadResult]";
}