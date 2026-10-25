using System.Text;

#if GIT_PROCESS_HOST
namespace AIAgent.ProcessHost.Git;
#else
namespace AIAgent.Cli.Git;
#endif

internal enum GitReadRecipe
{
    Version, InsideWorkTree, BareRepository, ObjectFormat, SymbolicHead, VerifyHead, VerifyHeadRef,
    SparseCheckoutConfig, SparseIndexConfig, WorktreeConfig, BareConfig, HeadManifest, IndexManifest,
    IntentToAddVisible, IntentToAddInvisible, BatchCheck, Batch, CheckIgnore
}

internal sealed class GitReadRequest
{
    private readonly string[] _paths;
    private readonly string[] _ignoreReadFiles;
    internal GitReadRequest(string scope = ".", string? objectId = null, string? headReference = null,
        IEnumerable<string>? paths = null, IEnumerable<string>? ignoreReadFiles = null, string objectFormat = "sha1")
    {
        Scope = scope; ObjectId = objectId; HeadReference = headReference; ObjectFormat = objectFormat;
        _paths = paths?.ToArray() ?? []; _ignoreReadFiles = ignoreReadFiles?.ToArray() ?? [];
    }
    internal string Scope { get; }
    internal string? ObjectId { get; }
    internal string? HeadReference { get; }
    internal string ObjectFormat { get; }
    internal IReadOnlyList<string> Paths => Array.AsReadOnly((string[])_paths.Clone());
    internal IReadOnlyList<string> IgnoreReadFiles => Array.AsReadOnly((string[])_ignoreReadFiles.Clone());
    public override string ToString() => "[GitReadRequest]";
}

internal static class GitProcessRecipe
{
    internal const int MetadataLimit = 1048576;
    internal const int BlobLimit = 131072;
    internal const int DiagnosticLimit = 8192;
    internal const int CommandTimeoutMilliseconds = 30000;
    internal static bool SafeRelative(string? value)
    {
        if (value is null || value.Length is < 1 or > 4096 || value[0] == '/' || value.Contains('\\') ||
            value.Split('/').Any(part => part.Length == 0 || part is "." or "..")) return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsControl(character) || character is '\u2028' or '\u2029') return false;
            if (char.IsHighSurrogate(character))
            { if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false; }
            else if (char.IsLowSurrogate(character)) return false;
        }
        return true;
    }
    internal static bool ValidOid(string? oid, string format) => oid is not null &&
        oid.Length == (format == "sha1" ? 40 : format == "sha256" ? 64 : -1) && oid.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool ValidRequest(string root, GitReadRecipe recipe, GitReadRequest request)
    {
        if (!Enum.IsDefined(recipe) || request.ObjectFormat is not "sha1" and not "sha256" ||
            request.Scope != "." && !SafeRelative(request.Scope) || request.Paths.Count > 8192 || request.IgnoreReadFiles.Count > 8192) return false;
        if (request.ObjectId is not null && !ValidOid(request.ObjectId, request.ObjectFormat)) return false;
        if (recipe is GitReadRecipe.HeadManifest or GitReadRecipe.BatchCheck or GitReadRecipe.Batch && request.ObjectId is null) return false;
        if (recipe == GitReadRecipe.VerifyHeadRef && (request.HeadReference is null ||
            !request.HeadReference.StartsWith("refs/heads/", StringComparison.Ordinal) || !SafeRelative(request.HeadReference) ||
            request.HeadReference.Contains("..", StringComparison.Ordinal) || request.HeadReference.Contains('@') ||
            request.HeadReference.Any(c => c is ' ' or '~' or '^' or ':' or '?' or '*' or '[') ||
            request.HeadReference.Split('/').Any(c => c.StartsWith('.') || c.EndsWith('.') || c.EndsWith(".lock", StringComparison.Ordinal)))) return false;
        if (request.Paths.Any(path => !SafeRelative(path))) return false;
        var prefix = root.TrimEnd('/') + "/";
        return request.IgnoreReadFiles.All(path => path.StartsWith(prefix, StringComparison.Ordinal) &&
            !path.Any(char.IsControl) && Path.GetFullPath(path) == path &&
            (Path.GetFileName(path) == ".gitignore" || path == prefix + ".git/info/exclude"));
    }
    internal static string[] Arguments(string root, GitReadRecipe recipe, GitReadRequest request)
    {
        if (!ValidRequest(root, recipe, request)) throw new ArgumentException("Invalid Git recipe input.");
        var args = new List<string>
        {
            "--no-pager", "--no-optional-locks", "--no-replace-objects", "--no-lazy-fetch", "--literal-pathspecs",
            "--git-dir=" + root + "/.git", "--work-tree=" + root
        };
        // check-ignore rejects PATHSPEC_LITERAL; its stdin names use a fixed ./ prefix instead.
        if (recipe == GitReadRecipe.CheckIgnore) args.Remove("--literal-pathspecs");
        foreach (var setting in new[] { "core.fsmonitor=false", "credential.helper=", "core.excludesFile=/dev/null", "core.attributesFile=/dev/null", "maintenance.auto=false", "gc.auto=0", "diff.relative=false" })
        { args.Add("-c"); args.Add(setting); }
        switch (recipe)
        {
            case GitReadRecipe.Version: args.Add("--version"); break;
            case GitReadRecipe.InsideWorkTree: args.AddRange(["rev-parse", "--is-inside-work-tree"]); break;
            case GitReadRecipe.BareRepository: args.AddRange(["rev-parse", "--is-bare-repository"]); break;
            case GitReadRecipe.ObjectFormat: args.AddRange(["rev-parse", "--show-object-format"]); break;
            case GitReadRecipe.SymbolicHead: args.AddRange(["symbolic-ref", "-q", "HEAD"]); break;
            case GitReadRecipe.VerifyHead: args.AddRange(["rev-parse", "--verify", "HEAD^{commit}"]); break;
            case GitReadRecipe.VerifyHeadRef: args.AddRange(["show-ref", "--verify", "--quiet", request.HeadReference!]); break;
            case GitReadRecipe.SparseCheckoutConfig: Config("core.sparseCheckout"); break;
            case GitReadRecipe.SparseIndexConfig: Config("index.sparse"); break;
            case GitReadRecipe.WorktreeConfig: Config("core.worktree"); break;
            case GitReadRecipe.BareConfig: Config("core.bare"); break;
            case GitReadRecipe.HeadManifest: args.AddRange(["ls-tree", "-r", "-z", "--full-tree", request.ObjectId!, "--", request.Scope]); break;
            case GitReadRecipe.IndexManifest: args.AddRange(["ls-files", "--full-name", "--stage", "--sparse", "-v", "-z", "--", request.Scope]); break;
            case GitReadRecipe.IntentToAddVisible:
            case GitReadRecipe.IntentToAddInvisible:
                args.AddRange(["diff", "--cached", "--raw", "-z", "--no-renames", "--no-ext-diff", "--no-textconv", "--no-color", "--no-abbrev", "--ignore-submodules=all", "-O/dev/null",
                    recipe == GitReadRecipe.IntentToAddVisible ? "--ita-visible-in-index" : "--ita-invisible-in-index"]);
                if (request.ObjectId is not null) args.Add(request.ObjectId);
                args.AddRange(["--", request.Scope]); break;
            case GitReadRecipe.BatchCheck: args.AddRange(["cat-file", "--batch-check"]); break;
            case GitReadRecipe.Batch: args.AddRange(["cat-file", "--batch"]); break;
            case GitReadRecipe.CheckIgnore: args.AddRange(["check-ignore", "--stdin", "-z", "--verbose", "--non-matching", "--no-index"]); break;
            default: throw new ArgumentException("Invalid Git recipe.");
        }
        return args.ToArray();
        void Config(string name) => args.AddRange(["config", "--local", "--null", "--get", name]);
    }
    internal static bool WithinInputLimit(GitReadRecipe recipe, GitReadRequest request)
    {
        if (recipe != GitReadRecipe.CheckIgnore) return true;
        long length = 0;
        foreach (var path in request.Paths)
        { length = checked(length + Encoding.UTF8.GetByteCount(path) + 3); if (length > MetadataLimit) return false; }
        return true;
    }
    internal static byte[] Input(GitReadRecipe recipe, GitReadRequest request)
    {
        if (!WithinInputLimit(recipe, request)) throw new ArgumentException("Git stdin limit exceeded.");
        return recipe switch
        {
            GitReadRecipe.BatchCheck or GitReadRecipe.Batch => Encoding.ASCII.GetBytes(request.ObjectId + "\n"),
            GitReadRecipe.CheckIgnore => Encoding.UTF8.GetBytes(string.Concat(request.Paths.Select(path => "./" + path + "\0"))),
            _ => []
        };
    }
    internal static Dictionary<string, string> Environment() => new(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin:/bin",
        ["HOME"] = "/var/empty",
        ["XDG_CONFIG_HOME"] = "/var/empty",
        ["TMPDIR"] = "/var/empty",
        ["LANG"] = "C",
        ["LC_ALL"] = "C",
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_CONFIG_GLOBAL"] = "/dev/null",
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_NO_LAZY_FETCH"] = "1",
        ["GIT_NO_REPLACE_OBJECTS"] = "1",
        ["GIT_ATTR_NOSYSTEM"] = "1"
    };
    internal static bool AcceptExit(GitReadRecipe recipe, int exit) => exit == 0 || exit == 1 && recipe is
        GitReadRecipe.SymbolicHead or GitReadRecipe.VerifyHeadRef or GitReadRecipe.SparseCheckoutConfig or
        GitReadRecipe.SparseIndexConfig or GitReadRecipe.WorktreeConfig or GitReadRecipe.BareConfig or GitReadRecipe.CheckIgnore ||
        exit == 128 && recipe == GitReadRecipe.VerifyHead;
}

// Cleanup expires at the earlier of the command deadline and five seconds after cleanup starts.
internal sealed class GitProcessDeadline
{
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly TimeSpan _total;
    private readonly TimeSpan _active;
    internal GitProcessDeadline(TimeProvider? clock = null)
        : this(clock ?? TimeProvider.System, TimeSpan.FromMilliseconds(GitProcessRecipe.CommandTimeoutMilliseconds), TimeSpan.FromMilliseconds(GitProcessRecipe.CommandTimeoutMilliseconds - 5000)) { }
    private GitProcessDeadline(TimeProvider clock, TimeSpan total, TimeSpan active)
    { _clock = clock; _started = clock.GetTimestamp(); _total = total; _active = active; }
    internal TimeSpan ActiveRemaining => Bound(_active, TimeSpan.MaxValue);
    internal TimeSpan Remaining(TimeSpan maximum) => Bound(_total, maximum);
    internal GitProcessDeadline BeginCleanup()
    {
        var remaining = Remaining(TimeSpan.FromSeconds(5));
        return new(_clock, remaining, remaining);
    }
    private TimeSpan Bound(TimeSpan budget, TimeSpan maximum)
    {
        var remaining = budget - _clock.GetElapsedTime(_started);
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining < maximum ? remaining : maximum;
    }
    public override string ToString() => "[GitProcessDeadline]";
}