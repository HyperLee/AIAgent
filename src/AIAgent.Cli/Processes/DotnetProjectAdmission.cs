using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Processes;

internal sealed class ProcessAdmissionException(string code) : IOException("受控程序預檢失敗")
{
    internal string Code { get; } = code;
}

internal sealed class DotnetInstallation(string root, string sdkVersion, string hostDirectory, string packages)
{
    internal string Root { get; } = root;
    internal string Executable => Root + "/dotnet";
    internal string SdkVersion { get; } = sdkVersion;
    internal string HostDirectory { get; } = hostDirectory;
    internal string HostExecutable => HostDirectory + "/AIAgent.ProcessHost";
    internal string Packages { get; } = packages;
    internal static bool PlatformSupported => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64 && ProductVersion() == "26.7.1";
    internal static DotnetInstallation? Discover()
    {
        if (!PlatformSupported || !File.Exists("/usr/bin/sandbox-exec")) return null;
        foreach (var root in new[] { "/usr/local/share/dotnet", "/opt/homebrew/share/dotnet" })
        {
            if (!Directory.Exists(root) || new DirectoryInfo(root).ResolveLinkTarget(true) is { } resolved && resolved.FullName != root) continue;
            var sdkRoot = root + "/sdk";
            if (!Directory.Exists(sdkRoot) || new DirectoryInfo(sdkRoot).ResolveLinkTarget(true) is not null) continue;
            var sdk = SelectInstalledSdkVersion(Directory.EnumerateFileSystemEntries(sdkRoot).Select(Path.GetFileName)!);
            var host = Path.Combine(AppContext.BaseDirectory, "ProcessHost");
            var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            if (sdk is not null && new DirectoryInfo(sdkRoot + "/" + sdk).ResolveLinkTarget(true) is null &&
                File.Exists(root + "/dotnet") && File.Exists(host + "/AIAgent.ProcessHost") && Directory.Exists(packages))
                return new(root, sdk, host, packages);
        }
        return null;
    }
    internal static string? SelectInstalledSdkVersion(IEnumerable<string> sdkEntries)
    {
        using var entries = sdkEntries.GetEnumerator();
        if (!entries.MoveNext()) return null;
        var only = entries.Current;
        if (entries.MoveNext() || only is null || !Version.TryParse(only, out var version) ||
            version.Major != 10 || version.Minor != 0 || version.Build < 0 || version.Revision >= 0 ||
            version.ToString() != only) return null;
        return only;
    }
    private static string ProductVersion()
    {
        if (!OperatingSystem.IsMacOS()) return "";
        var bytes = new byte[64]; nuint size = (nuint)bytes.Length;
        return Sysctl("kern.osproductversion", bytes, ref size, IntPtr.Zero, 0) == 0 && size is > 0 and < 65
            ? Encoding.ASCII.GetString(bytes, 0, (int)size - 1) : "";
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "sysctlbyname")]
    private static extern int Sysctl(string name, byte[] output, ref nuint size, IntPtr input, nuint inputSize);
    public override string ToString() => "[DotnetInstallation]";
}

internal sealed class ProcessAdmissionSnapshot(string root, string project, DotnetInstallation installation,
    IReadOnlyList<string> outputs, IReadOnlyList<string> denied, IReadOnlyDictionary<string, WorkspaceNodeState> states,
    IReadOnlyDictionary<string, byte[]> contents)
{
    internal string Root { get; } = root;
    internal string Project { get; } = project;
    internal DotnetInstallation Installation { get; } = installation;
    internal IReadOnlyList<string> Outputs { get; } = Array.AsReadOnly(outputs.ToArray());
    internal IReadOnlyList<string> Denied { get; } = Array.AsReadOnly(denied.ToArray());
    internal IReadOnlyDictionary<string, WorkspaceNodeState> States { get; } = new Dictionary<string, WorkspaceNodeState>(states, StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, byte[]> Contents { get; } = contents.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    internal bool Matches(ProcessAdmissionSnapshot other) => Root == other.Root && Project == other.Project &&
        Installation.Root == other.Installation.Root && Installation.SdkVersion == other.Installation.SdkVersion &&
        Installation.HostDirectory == other.Installation.HostDirectory && Installation.Packages == other.Installation.Packages &&
        Outputs.SequenceEqual(other.Outputs) && Denied.SequenceEqual(other.Denied) && States.Count == other.States.Count &&
        States.All(pair => other.States.TryGetValue(pair.Key, out var state) && pair.Value == state) && Contents.Count == other.Contents.Count &&
        Contents.All(pair => other.Contents.TryGetValue(pair.Key, out var bytes) && pair.Value.AsSpan().SequenceEqual(bytes));
    public override string ToString() => "[ProcessAdmissionSnapshot]";
}

internal sealed class DotnetProjectAdmission(IWorkspaceReadFileSystem fileSystem, ISensitiveDataPolicy policy)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly HashSet<string> OutputProperties = new(StringComparer.OrdinalIgnoreCase)
    { "OutputPath", "BaseOutputPath", "IntermediateOutputPath", "BaseIntermediateOutputPath", "MSBuildProjectExtensionsPath", "RestorePackagesPath", "ArtifactsPath", "UseArtifactsOutput" };

    internal async Task<ProcessAdmissionSnapshot> InspectAsync(string root, string path, DotnetTaskKind kind,
        DotnetInstallation installation, CancellationToken token)
    {
        var states = new Dictionary<string, WorkspaceNodeState>(StringComparer.Ordinal);
        var contents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var outputs = new SortedSet<string>(StringComparer.Ordinal); var denied = new SortedSet<string>(StringComparer.Ordinal);
        void InspectAncestors(string full, bool file = false)
        {
            var segments = full.Split('/', StringSplitOptions.RemoveEmptyEntries); var current = "";
            for (var i = 0; i < segments.Length; i++)
            {
                current += "/" + segments[i]; var state = fileSystem.Inspect(current);
                if (state.Kind == WorkspaceNodeKind.Link) throw new ProcessAdmissionException("symbolic_link");
                if (state.Kind != (file && i == segments.Length - 1 ? WorkspaceNodeKind.File : WorkspaceNodeKind.Directory))
                    throw new ProcessAdmissionException("wrong_entry_type");
                if (state.Kind == WorkspaceNodeKind.File && state.Links != 1) throw new ProcessAdmissionException("unsafe_output_path");
                states[current] = state;
            }
        }
        InspectAncestors(root); var project = root + "/" + path; InspectAncestors(project, true);
        var runtime = new[] { installation.Root, installation.HostDirectory, AppContext.BaseDirectory.TrimEnd('/') };
        foreach (var directory in runtime) InspectAncestors(directory);
        InspectAncestors(installation.Packages);
        var visited = 0;
        async Task<byte[]> Read(string full)
        {
            var before = fileSystem.Inspect(full);
            if (before.Kind != WorkspaceNodeKind.File || before.Links != 1 || before.Length > 131072) throw new ProcessAdmissionException("unsupported_project_layout");
            await using var handle = fileSystem.OpenRead(full); if (handle.GetState() != before) throw new ProcessAdmissionException("path_changed");
            using var memory = new MemoryStream(); var buffer = new byte[4096];
            while (true)
            {
                var read = await handle.Stream.ReadAsync(buffer, token); if (read == 0) break;
                if (memory.Length + read > 131072) throw new ProcessAdmissionException("unsupported_project_layout");
                memory.Write(buffer, 0, read);
            }
            var bytes = memory.ToArray(); if (handle.GetState() != before || fileSystem.Inspect(full) != before || bytes.LongLength != before.Length)
                throw new ProcessAdmissionException("path_changed");
            var text = Utf8.GetString(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? bytes.AsSpan(3) : bytes);
            if (!policy.CanSendOrUse(text, ContentBoundary.ToolResult)) throw new ProcessAdmissionException("sensitive_content");
            states[full] = before; contents[full] = bytes; return bytes;
        }
        async Task Walk(string full, string relative, int depth, bool inOutput)
        {
            if (depth > 32) throw new ProcessAdmissionException("unsupported_project_layout");
            var names = new List<string>();
            using (var entries = fileSystem.EnumerateNames(full).GetEnumerator())
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (!entries.MoveNext()) break;
                    if (++visited > 8192) throw new ProcessAdmissionException("unsupported_project_layout");
                    names.Add(entries.Current);
                }
            }
            names.Sort(StringComparer.Ordinal);
            foreach (var name in names)
            {
                token.ThrowIfCancellationRequested();
                if (name is "." or ".." || name.Contains('/') || name.Contains('\\')) throw new ProcessAdmissionException("unsupported_project_layout");
                var childRelative = relative.Length == 0 ? name : relative + "/" + name; var child = full + "/" + name;
                if (childRelative.Length > 4096) throw new ProcessAdmissionException("unsupported_project_layout");
                var state = fileSystem.Inspect(child); states[child] = state;
                if (WorkspacePathRules.IsSensitive(childRelative) || name.Equals("NuGet.Config", StringComparison.OrdinalIgnoreCase))
                { denied.Add(child); continue; }
                if (state.Kind is WorkspaceNodeKind.Link or WorkspaceNodeKind.Other || state.Kind == WorkspaceNodeKind.File && state.Links != 1)
                    throw new ProcessAdmissionException(inOutput ? "unsafe_output_path" : "symbolic_link");
                if (state.Kind == WorkspaceNodeKind.Directory) await Walk(child, childRelative, depth + 1, inOutput || name is "bin" or "obj");
                if (!inOutput && state.Kind == WorkspaceNodeKind.File && name.EndsWith(".csproj", StringComparison.Ordinal))
                { outputs.Add(full + "/bin"); outputs.Add(full + "/obj"); }
                if (!inOutput && state.Kind == WorkspaceNodeKind.File && (name is "Directory.Build.props" or "Directory.Build.targets" || child == project))
                    CheckProject(await Read(child));
            }
        }
        await Walk(root, "", 0, false);
        if (!contents.ContainsKey(project)) CheckProject(await Read(project));
        foreach (var output in outputs)
        {
            foreach (var directory in runtime)
                if (WorkspacePathRules.IsWithinRoot(output, directory) || WorkspacePathRules.IsWithinRoot(directory, output))
                    throw new ProcessAdmissionException("protected_runtime_path");
            states[output] = fileSystem.Inspect(output);
            if (states[output].Kind is not (WorkspaceNodeKind.Missing or WorkspaceNodeKind.Directory)) throw new ProcessAdmissionException("unsafe_output_path");
        }
        // Global SDK selection also searches ancestors of the workspace; observe every candidate.
        var search = root; var foundGlobal = false;
        while (true)
        {
            var global = search + "/global.json"; var state = fileSystem.Inspect(global); states[global] = state;
            if (!foundGlobal && state.Kind != WorkspaceNodeKind.Missing)
            {
                using var document = JsonDocument.Parse(await Read(global)); var json = document.RootElement;
                if (json.TryGetProperty("sdk", out var sdk) && !MatchesFixedSdkSelection(sdk, installation.SdkVersion))
                    throw new ProcessAdmissionException("capability_unavailable");
                if (kind == DotnetTaskKind.Test && (!json.TryGetProperty("test", out var test) || !test.TryGetProperty("runner", out var runner) || runner.GetString() != "Microsoft.Testing.Platform"))
                    throw new ProcessAdmissionException("unsupported_project_layout");
                foundGlobal = true;
            }
            if (search == "/") break; search = Path.GetDirectoryName(search) ?? "/";
        }
        if (kind == DotnetTaskKind.Test && !foundGlobal) throw new ProcessAdmissionException("unsupported_project_layout");
        var assets = Path.GetDirectoryName(project) + "/obj/project.assets.json";
        using (var document = JsonDocument.Parse(await Read(assets)))
        {
            var json = document.RootElement;
            if (!json.TryGetProperty("packageFolders", out var folders) || folders.EnumerateObject().Any(property => property.Name.TrimEnd('/') != installation.Packages))
                throw new ProcessAdmissionException("unsupported_project_layout");
            if (json.TryGetProperty("project", out var info) && info.TryGetProperty("restore", out var restore) &&
                restore.TryGetProperty("packagesPath", out var packages) && packages.GetString()?.TrimEnd('/') != installation.Packages)
                throw new ProcessAdmissionException("unsupported_project_layout");
            if (kind == DotnetTaskKind.Test && (!json.TryGetProperty("targets", out var targets) || targets.EnumerateObject().Count() != 1))
                throw new ProcessAdmissionException("unsupported_project_layout");
        }
        foreach (var full in new[] { installation.Executable, installation.HostExecutable, installation.HostDirectory + "/AIAgent.ProcessHost.dll",
            installation.HostDirectory + "/AIAgent.ProcessHost.deps.json", installation.HostDirectory + "/AIAgent.ProcessHost.runtimeconfig.json", typeof(IControlledDotnetTasks).Assembly.Location })
        {
            InspectAncestors(full, true);
            // Hash only this fixed required binary/manifest set, with a fixed bound and constant-size buffers.
            var before = fileSystem.Inspect(full);
            if (before.Length is <= 0 or > 16777216) throw new ProcessAdmissionException("capability_unavailable");
            await using var handle = fileSystem.OpenRead(full);
            if (handle.GetState() != before) throw new ProcessAdmissionException("path_changed");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[4096]; long total = 0;
            while (true)
            {
                var read = await handle.Stream.ReadAsync(buffer, token); if (read == 0) break;
                total = checked(total + read);
                if (total > 16777216) throw new ProcessAdmissionException("capability_unavailable");
                hash.AppendData(buffer.AsSpan(0, read));
            }
            if (total != before.Length || handle.GetState() != before || fileSystem.Inspect(full) != before)
                throw new ProcessAdmissionException("path_changed");
            contents[full] = hash.GetHashAndReset();
        }
        return new(root, project, installation, outputs.ToArray(), denied.ToArray(), states, contents);
    }
    private static void CheckProject(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 131072 });
        var project = XDocument.Load(reader);
        if (project.Descendants().Any(element => OutputProperties.Contains(element.Name.LocalName))) throw new ProcessAdmissionException("unsupported_project_layout");
    }
    private static bool MatchesFixedSdkSelection(JsonElement sdk, string selectedVersion)
    {
        if (sdk.ValueKind != JsonValueKind.Object || !sdk.TryGetProperty("version", out var version) ||
            version.ValueKind != JsonValueKind.String || version.GetString() != selectedVersion) return false;
        foreach (var property in sdk.EnumerateObject())
        {
            if (property.Name == "version") continue;
            if (property.Name == "rollForward" && property.Value.ValueKind == JsonValueKind.String &&
                property.Value.GetString() is "disable" or "latestPatch") continue;
            if (property.Name == "allowPrerelease" && property.Value.ValueKind == JsonValueKind.False) continue;
            return false;
        }
        return true;
    }
    public override string ToString() => "[DotnetProjectAdmission]";
}