using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>工廠共用的回合橋接；session 與 lease 必須同時相符。</summary>
internal sealed class FileChangeApprovalBridge
{
    private readonly object _gate = new();
    private Binding? _binding;
    internal IFileChangeApproval ForSession(BootstrapSession session) => new SessionApproval(this, session);
    internal IDisposable Bind(BootstrapSession session, Func<FileChangePreview, CancellationToken, ValueTask<FileApprovalDecision>> confirm)
    {
        lock (_gate)
        {
            if (_binding is not null) throw new InvalidOperationException("核准回合已綁定");
            var binding = new Binding(session, confirm);
            _binding = binding;
            return new Lease(this, binding);
        }
    }
    private async ValueTask<FileApprovalDecision> ConfirmAsync(BootstrapSession session, FileChangePreview preview, CancellationToken token)
    {
        Binding? binding;
        lock (_gate) binding = _binding;
        if (binding is null || !ReferenceEquals(session, binding.Session)) return FileApprovalDecision.Unavailable;
        var decision = await binding.Confirm(preview, token);
        lock (_gate)
            return ReferenceEquals(_binding, binding) ? decision : token.IsCancellationRequested ? FileApprovalDecision.Cancelled : FileApprovalDecision.Unavailable;
    }
    private sealed class Binding(BootstrapSession session, Func<FileChangePreview, CancellationToken, ValueTask<FileApprovalDecision>> confirm)
    {
        internal BootstrapSession Session { get; } = session;
        internal Func<FileChangePreview, CancellationToken, ValueTask<FileApprovalDecision>> Confirm { get; } = confirm;
        public override string ToString() => "[FileApprovalBinding]";
    }
    private sealed class SessionApproval(FileChangeApprovalBridge owner, BootstrapSession session) : IFileChangeApproval
    {
        public ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken token) => owner.ConfirmAsync(session, preview, token);
        public override string ToString() => "[SessionFileChangeApproval]";
    }
    private sealed class Lease(FileChangeApprovalBridge owner, Binding binding) : IDisposable
    {
        public void Dispose() { lock (owner._gate) { if (ReferenceEquals(owner._binding, binding)) owner._binding = null; } }
    }
    public override string ToString() => "[FileChangeApprovalBridge]";
}