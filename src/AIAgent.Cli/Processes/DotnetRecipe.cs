using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Processes;

internal static class DotnetRecipe
{
    internal const string Version = "dotnet-debug-v1";
    internal static string[] Arguments(DotnetTaskKind kind, string project, string privateRoot) => kind == DotnetTaskKind.Build
        ? ["build", project, "--configuration", "Debug", "--no-restore", "--nologo", "--disable-build-servers", "--tl:off", "--verbosity", "minimal", "-maxcpucount:1", "-nodeReuse:false", "-property:UseSharedCompilation=false"]
        : ["test", "--project", project, "--configuration", "Debug", "--no-restore", "--no-launch-profile", "--no-ansi", "--no-progress", "--output", "Normal", "--results-directory", privateRoot + "/results", "-maxcpucount:1", "-nodeReuse:false", "-property:UseSharedCompilation=false", "--", "--minimum-expected-tests", "1"];
    internal static IReadOnlyDictionary<string, string> Environment(DotnetInstallation installation, string privateRoot) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin:/bin:" + installation.Root,
            ["DOTNET_ROOT"] = installation.Root,
            ["DOTNET_HOST_PATH"] = installation.Executable,
            ["NUGET_PACKAGES"] = installation.Packages,
            ["HOME"] = privateRoot + "/home",
            ["DOTNET_CLI_HOME"] = privateRoot + "/home",
            ["TMPDIR"] = privateRoot + "/tmp/",
            ["LANG"] = "en_US.UTF-8",
            ["LC_ALL"] = "en_US.UTF-8",
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            ["TZ"] = "UTC",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_EnableDiagnostics"] = "0",
            ["DOTNET_NOLOGO"] = "1"
        };
    internal static string Preview(ProcessAdmissionSnapshot snapshot, DotnetTaskKind kind, string privateRoot, TimeSpan timeout,
        AIAgent.Cli.Core.TaskExecutionContext? context)
    {
        var text = new StringBuilder("受控 .NET 單次任務：僅限您信任的本機專案；專案建置及測試可能執行專案程式碼。\n");
        text.Append("任務：").Append(kind == DotnetTaskKind.Build ? "build" : "test").Append(" / Debug\n專案：").Append(FileChangePreviewBuilder.EscapePath(snapshot.Project))
            .Append("\n工作目錄：").Append(FileChangePreviewBuilder.EscapePath(snapshot.Root)).Append("\nSDK：").Append(snapshot.Installation.SdkVersion)
            .Append("\n執行檔：").Append(FileChangePreviewBuilder.EscapePath(snapshot.Installation.Executable)).Append("\n監督器：").Append(FileChangePreviewBuilder.EscapePath(snapshot.Installation.HostExecutable))
            .Append("\nrecipe：").Append(Version).Append("\nsandbox：").Append(MacSandboxPolicy.Version).Append("\nargv（各行一個完整參數）：\n");
        foreach (var argument in Arguments(kind, snapshot.Project, privateRoot)) text.Append(FileChangePreviewBuilder.EscapePath(argument)).Append('\n');
        text.Append("唯讀目錄子樹：\n");
        foreach (var root in MacSandboxPolicy.ReadRoots(snapshot)) text.Append(FileChangePreviewBuilder.EscapePath(root)).Append('\n');
        text.Append("唯讀系統單一路徑：\n");
        foreach (var file in MacSandboxPolicy.SystemReadFiles) text.Append(FileChangePreviewBuilder.EscapePath(file)).Append('\n');
        text.Append("全機唯讀 metadata（不授予檔案內容權限）；/dev/null 可寫。禁止敏感檔案、NuGet.Config、Keychain、父程序記憶體／task port。\n可寫標準輸出：\n");
        foreach (var output in snapshot.Outputs) text.Append(FileChangePreviewBuilder.EscapePath(output)).Append('\n');
        text.Append("私人 0700 暫存（含 home/tmp/results）：").Append(FileChangePreviewBuilder.EscapePath(privateRoot))
            .Append("\n允許在上述產物及私人暫存建立、修改、移除工具管理檔案；來源禁止寫入、刪除及 rename。禁止建立 symlink/hard-link 與交換輸出祖先。\n環境（不繼承其他變數）：\n");
        foreach (var pair in Environment(snapshot.Installation, privateRoot)) text.Append(pair.Key).Append('=').Append(FileChangePreviewBuilder.EscapePath(pair.Value)).Append('\n');
        text.Append("禁止 IPv4/IPv6、TCP/UDP/DNS、代理及 loopback；只允許私人 tmp 內的 AF_UNIX IPC。\n期限：")
            .Append((long)timeout.TotalMilliseconds).Append(" ms；合計原始輸出上限 1048576 bytes；每回合最多 2 次／300 秒。\n")
            .Append("先執行內建隔離探針；失敗零目標啟動，不放寬政策。產物與私人目錄保留；整批清理由使用者手動處理。\n")
            .Append("原生監督只涵蓋工作群組與每 100ms 已觀測的最多 256 個後代；不承諾偵測所有 setsid/double-fork/重新掛接或惡意逃離。觀測遺失／收尾不能確認會鎖定本 session 並退出。\n");
        if (context is not null) text.Append("PlanId：").Append(context.PlanId).Append(" / Revision：").Append(context.PlanRevision).Append(" / TaskId：").Append(context.TaskId).Append('\n');
        return text.ToString();
    }
}

internal static class MacSandboxPolicy
{
    internal const string Version = "macos-26.7.1-arm64-v1";
    internal static IReadOnlyList<string> SystemReadRoots { get; } = Array.AsReadOnly(new[]
    { "/System/Library", "/usr/lib", "/Library/Apple/System/Library", "/usr/share/locale", "/usr/share/icu", "/private/var/db/dyld", "/private/var/db/timezone" });
    internal static IReadOnlyList<string> SystemReadFiles { get; } = Array.AsReadOnly(new[]
    { "/", "/private/etc/dotnet/install_location_arm64", "/dev/null", "/dev/random", "/dev/urandom", "/private/etc/localtime", "/usr/share/zoneinfo/UTC" });
    internal static IEnumerable<string> ReadRoots(ProcessAdmissionSnapshot snapshot) => new[]
    { snapshot.Root, snapshot.Installation.Root, snapshot.Installation.Packages, snapshot.Installation.HostDirectory, AppContext.BaseDirectory.TrimEnd('/') }.Concat(SystemReadRoots);
    internal static string SensitivePattern
    {
        get
        {
            static string Ascii(string value) => string.Concat(value.Select(character => char.IsAsciiLetter(character)
                ? "[" + char.ToLowerInvariant(character) + char.ToUpperInvariant(character) + "]" : character == '.' ? "[.]" : character.ToString()));
            var names = new[] { ".git", ".agents", ".codex", ".aws", ".ssh", ".secrets", "secrets", "gemini-api-key.txt", "api-key.txt", "appsettings.Local.json", "credentials", "credentials.json", "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519", "identity", "NuGet.Config" };
            var suffixes = new[] { ".secrets.json", ".pem", ".key", ".p12", ".pfx", ".ppk", ".keystore", ".jks" };
            return "/(" + string.Join('|', names.Select(Ascii)) + "|" + Ascii(".env") + "([.][^/]*)?|[^/]*(" + string.Join('|', suffixes.Select(Ascii)) + "))(/|$)";
        }
    }
    internal static string Create(ProcessAdmissionSnapshot snapshot, string privateRoot)
    {
        static string Quoted(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        var text = new StringBuilder("(version 1)\n(deny default)\n(allow process-fork)\n(allow process-exec)\n(allow process-info*)\n(allow signal (target same-sandbox))\n(allow sysctl-read)\n");
        // Metadata alone does not expose file contents; content permissions remain explicit.
        text.Append("(allow file-read-metadata)\n");
        foreach (var root in ReadRoots(snapshot))
            text.Append("(allow file-read* (subpath ").Append(Quoted(root)).Append("))\n");
        foreach (var file in SystemReadFiles)
            text.Append("(allow file-read* (literal ").Append(Quoted(file)).Append("))\n");
        text.Append("(allow file-write* (literal \"/dev/null\"))\n");
        foreach (var output in snapshot.Outputs.Append(FileChangePreviewBuilder.EscapePath(privateRoot))) text.Append("(allow file-read* file-write* (subpath ").Append(Quoted(output)).Append("))\n");
        foreach (var output in snapshot.Outputs)
        {
            var ancestor = output;
            while (AIAgent.Cli.Workspace.WorkspacePathRules.IsWithinRoot(snapshot.Root, ancestor))
            {
                text.Append("(deny file-write-unlink (literal ").Append(Quoted(ancestor)).Append("))\n");
                if (ancestor == snapshot.Root) break; ancestor = Path.GetDirectoryName(ancestor)!;
            }
        }
        text.Append("(deny file-read* file-write* (regex #\"").Append(SensitivePattern).Append("\"))\n");
        foreach (var denied in snapshot.Denied) text.Append("(deny file-read-data file-write* (subpath ").Append(Quoted(denied)).Append("))\n");
        text.Append("(deny file-link)\n(deny file-write-create (vnode-type SYMLINK))\n")
            .Append("(deny file-write* (subpath ").Append(Quoted(privateRoot + "/probe-source")).Append("))\n")
            .Append("(deny file-read-data file-write* (literal ").Append(Quoted(privateRoot + "/probe-denied")).Append("))\n")
            .Append("(allow network* (local unix-socket (subpath ").Append(Quoted(privateRoot + "/tmp")).Append(")))\n")
            .Append("(allow mach-lookup (global-name \"com.apple.system.logger\"))\n");
        return text.ToString();
    }
}