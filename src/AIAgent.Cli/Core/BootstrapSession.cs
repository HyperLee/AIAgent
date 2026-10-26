using System.Text.Json.Serialization;

using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

public sealed class BootstrapSession : IDisposable
{
    public ConfirmedWorkspace Workspace { get; }
    public PublicSettings Settings { get; }
    [JsonIgnore] public ISensitiveDataPolicy Policy { get; }
    internal SessionSecrets Secrets { get; }
    internal string? ResumeWorkId { get; }

    internal BootstrapSession(ConfirmedWorkspace workspace, PublicSettings settings, SessionSecrets secrets, string? resumeWorkId = null)
    {
        Workspace = workspace;
        Settings = settings;
        Secrets = secrets;
        ResumeWorkId = resumeWorkId;
        Policy = new SensitiveDataPolicy(secrets);
    }
    public void Dispose() => Secrets.Dispose();
    public override string ToString() => "[BootstrapSession]";
}

public sealed class BootstrapResult : IDisposable
{
    public BootstrapStatus Status { get; }
    public BootstrapErrorCode ErrorCode { get; }
    [JsonIgnore] public BootstrapSession? Session { get; private set; }
    public int ExitCode => Status == BootstrapStatus.Cancelled ? 130 : Status != BootstrapStatus.Failed ? 0 :
        ErrorCode is BootstrapErrorCode.InvalidArguments or BootstrapErrorCode.InvalidSettings ? 2 : 1;

    private BootstrapResult(BootstrapStatus status, BootstrapErrorCode errorCode, BootstrapSession? session)
    { Status = status; ErrorCode = errorCode; Session = session; }

    internal static BootstrapResult Ready(BootstrapSession session) => new(BootstrapStatus.Ready, BootstrapErrorCode.None, session);
    internal static BootstrapResult End(BootstrapStatus status) => status is BootstrapStatus.Ready or BootstrapStatus.Failed
        ? throw new ArgumentException("狀態無效") : new(status, BootstrapErrorCode.None, null);
    internal static BootstrapResult Failed(BootstrapErrorCode errorCode) => errorCode == BootstrapErrorCode.None
        ? throw new ArgumentException("錯誤碼無效") : new(BootstrapStatus.Failed, errorCode, null);
    internal BootstrapSession TakeSession()
    {
        if (Status != BootstrapStatus.Ready || Session is null) throw new InvalidOperationException("無可移交的工作階段");
        var session = Session;
        Session = null;
        return session;
    }
    public void Dispose() { Session?.Dispose(); Session = null; }
    public override string ToString() => "[BootstrapResult]";
}