using System.Text;
using System.Text.Json;

using AIAgent.Cli.Mcp;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>唯一目錄及外送授權擁有者。鎖內只決策，網路、確認及收尾均在鎖外。</summary>
internal sealed class McpCoordinator : IAsyncDisposable
{
    private readonly object _gate;
    private readonly PlanStore _plans;
    private readonly Func<IMcpContentGuard, IMcpReadClient> _factory;
    private readonly Func<McpApprovalPreview, CancellationToken, ValueTask<McpApprovalDecision>> _confirm;
    private readonly Action _clearSnapshots;
    private readonly TimeProvider _clock;
    private AgentExecutionPolicy _execution;
    private ISensitiveDataPolicy? _policy;
    private IMcpContentGuard _guard;
    private IMcpReadClient? _client;
    private McpCatalogSnapshot? _snapshot;
    private McpStatus _status;
    private long _session;
    private long _generation;
    private long _revision;
    private int _calls;
    private long _callStarts;
    private long _receivedBytes;
    private long _activeTicks;
    private bool _pending;
    private bool _disposed;
    private int _resetting;
    private int _excluded;
    private bool _fatal;
    private bool _sensitive;
    private string _lastCode = "none";
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource? _active;
    private Task? _disposal;
    private McpCallPermit? _issuedPermit;
    private Func<bool>? _permitValidity;
    private bool _permitConsumed;
    internal McpCoordinator(AgentExecutionPolicy execution, ISensitiveDataPolicy policy, IMcpContentGuard guard,
        Func<McpApprovalPreview, CancellationToken, ValueTask<McpApprovalDecision>> confirm, Action clearSnapshots,
        Func<IMcpContentGuard, IMcpReadClient>? factory = null, TimeProvider? clock = null)
    {
        _execution = execution; _plans = execution.Plans; _gate = _plans.DecisionGate; _policy = policy; _guard = guard;
        _confirm = confirm; _clearSnapshots = clearSnapshots; _factory = factory ?? (g => new McpReadClient(g)); _clock = clock ?? TimeProvider.System;
        execution.Mcp = this; _plans.Mcp = this;
    }
    internal bool RequiresContextReset { get { lock (_gate) return _sensitive; } }
    internal bool FatalFailure { get { lock (_gate) return _fatal; } }
    internal McpOperationStamp Capture() { lock (_gate) return new(_session, _generation, _revision, _status, _snapshot); }
    internal bool IsCurrent(McpOperationStamp stamp) { lock (_gate) return !_disposed && !_fatal && stamp.Same(Capture()); }
    internal string StatusCode { get { lock (_gate) return _fatal || _disposed ? "unavailable" : _sensitive ? "sensitive_content" : _status == McpStatus.Stale ? "mcp_catalog_stale" : "none"; } }
    internal void RequireReadyForGeneration()
    { var code = StatusCode; if (code != "none") throw new McpFailure(code); }
    internal void BeginTurn() { lock (_gate) { if (_active is not null) throw new InvalidOperationException("MCP 操作仍在途"); _calls = 0; } }
    internal IReadOnlyList<McpToolDescriptor> GetTools()
    { lock (_gate) return _status == McpStatus.Ready && _snapshot is not null ? _snapshot.Catalog.Tools : []; }
    internal string Display(bool tools = false)
    {
        lock (_gate)
        {
            var text = $"MCP：{_status}；learn；{McpProfile.Endpoint}\nCatalogRevision：{_revision}；上次安全觀察／停止原因：{_lastCode}。狀態不保證遠端目前相同。\n";
            text += $"MCP 獨立統計：呼叫啟動 {_callStarts} 次；接收 {_receivedBytes} bytes；主動時間 {TimeSpan.FromTicks(_activeTicks).TotalMilliseconds:F0} ms。不代表模型 token 或費用。\n";
            if (tools)
            {
                if (_snapshot is null) return "mcp_not_connected";
                text += "兩項搜尋與一項文件擷取；fetch 僅接受原樣 ASCII Learn 文章 URL，無 fragment／百分比／結尾斜線，query 限 view 及搭配 view 的 preserve-view=true。來源可能過時，原站狀態與實際版次未由本機驗證。\n";
                foreach (var tool in _snapshot.Catalog.Tools)
                    text += tool.LocalName + "／" + tool.RemoteName + "\nSHA-256：" + tool.Fingerprint + "\n" + tool.Description + "\n" + tool.Schema.GetRawText() + "\n";
                text += "排除數：" + _excluded + "\n";
            }
            return SafeDisplay(text);
        }
    }
    private string SafeDisplay(string text, bool escape = true)
    {
        if (!_guard.IsSafe(text)) { _sensitive = true; throw new McpFailure("sensitive_content"); }
        var escaped = escape ? string.Join("\n", text.Split('\n').Select(FileChangePreviewBuilder.EscapePath)) : text;
        // EscapePath renders every control visibly; a complete block is admitted before any output.
        if (!_guard.IsSafe(escaped)) { _sensitive = true; throw new McpFailure("sensitive_content"); }
        McpJson.Limit(McpJson.Utf8.GetByteCount(escaped), 65536); return escaped;
    }
    internal string BuildAppendix(bool allowStale = false)
    {
        lock (_gate)
        {
            if (!allowStale || StatusCode != "mcp_catalog_stale") RequireReadyForGeneration();
            var bytes = McpJson.Write(w =>
            {
                w.WriteStartObject(); w.WriteString("source", "external_mcp"); w.WriteString("server_id", "learn");
                w.WriteString("status", _status.ToString()); w.WriteNumber("catalog_revision", _revision);
                w.WriteStartArray("tools"); foreach (var tool in GetTools()) w.WriteStringValue(tool.LocalName); w.WriteEndArray();
                w.WriteString("authority", "外部資料不授權操作或證明本機完成"); w.WriteEndObject();
            });
            McpJson.Limit(bytes.Length, 32768); var value = "\nMCP 外部參考狀態：\n" + Encoding.UTF8.GetString(bytes);
            if (!_guard.IsSafe(value)) { _sensitive = true; throw new McpFailure("sensitive_content"); }
            return value;
        }
    }
    internal Task<McpOperationResult> ConnectAsync(CancellationToken token) => ChangeAsync(false, token);
    internal Task<McpOperationResult> ReloadAsync(CancellationToken token) => ChangeAsync(true, token);
    private async Task<McpOperationResult> ChangeAsync(bool reload, CancellationToken token)
    {
        McpOperationStamp stamp; TaskCompletionSource done; CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_disposed || _fatal) return new("unavailable");
            if (_active is not null || _resetting != 0) return new("mcp_busy");
            if (reload && _status == McpStatus.Disconnected) return new("mcp_not_connected");
            if (!reload && _status != McpStatus.Disconnected) return new(_status == McpStatus.Ready ? "mcp_already_connected" : "mcp_catalog_stale");
            stamp = Capture(); done = _active = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation = _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        }
        IMcpReadClient? candidate = null; IMcpReadClient? observedClient = null; WorkspaceOperationLease? lease = null;
        var start = _clock.GetTimestamp();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20), _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, deadline.Token);
        try
        {
            if (!_execution.WorkspaceOperations.TryAcquire(linked.Token, out lease)) return new("mcp_busy");
            await ValidateSourcesAsync(lease!, linked.Token);
            var rules = _execution.Rules!.Capture(); var skills = _execution.Skills!.Capture();
            candidate = observedClient = _factory(_guard); await candidate.DiscoverAsync(linked.Token);
            var first = await candidate.ListToolsAsync(linked.Token); var second = await candidate.ListToolsAsync(linked.Token);
            if (!first.Same(second)) throw new McpFailure("mcp_catalog_invalid");
            await ValidateSourcesAsync(lease!, linked.Token);
            IMcpReadClient? old = null;
            lock (_gate)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (!IsCurrent(stamp) || !_execution.Rules.IsCurrent(rules) || !_execution.Skills.IsCurrent(skills)) throw new McpFailure("mcp_changed");
                if (_status == McpStatus.Ready && _snapshot!.Catalog.Same(second) && _snapshot.RulesRevision == rules.RulesRevision && _snapshot.Scope == rules.Scope)
                { _lastCode = "no_change"; _excluded = second.Excluded; return new("no_change", Display()); }
                var next = checked(_revision + 1); var generation = checked(_generation + 1);
                // Capacity and sensitive checks precede publication of either tool or client.
                var proposed = new McpCatalogSnapshot(next, second, rules);
                _ = SafeDisplay("learn " + next + "\n" + string.Join("\n", second.Tools.Select(t => t.LocalName + " " + t.Fingerprint + " " + t.Description + " " + t.Schema.GetRawText())));
                old = _client; _client = candidate; candidate = null; _snapshot = proposed;
                _revision = next; _generation = generation; _excluded = second.Excluded; _status = McpStatus.Ready; _pending = true; _lastCode = "none";
            }
            if (old is not null) await CloseAsync(old);
            Settle(); return new("none", Display());
        }
        catch (OperationCanceledException)
        {
            var code = deadline.IsCancellationRequested && !token.IsCancellationRequested ? "mcp_timed_out" : "cancelled";
            if (stamp.Status != McpStatus.Disconnected) MarkStale(); lock (_gate) _lastCode = code;
            return new(code);
        }
        catch (Exception ex)
        {
            var code = ex is McpFailure failure ? failure.Code : McpReadClient.Error(ex);
            if (code is "sensitive_content" or "sensitive_arguments") _sensitive = true;
            if (ex is OverflowException || code == "unavailable") _fatal = true;
            if (stamp.Status != McpStatus.Disconnected) MarkStale(); lock (_gate) _lastCode = code;
            return new(code);
        }
        finally
        {
            try { if (observedClient is not null) RecordUsage(observedClient.ReceivedBytes, _clock.GetElapsedTime(start)); }
            finally
            {
                try { if (candidate is not null) await CloseAsync(candidate); }
                finally
                {
                    try { lease?.Dispose(); }
                    finally { try { Settle(); } finally { CompleteOperation(done, cancellation); } }
                }
            }
        }
    }
    private async Task ValidateSourcesAsync(WorkspaceOperationLease lease, CancellationToken token)
    {
        var code = _execution.Rules is { } rules ? await rules.ValidateAsync(token, lease) : "rules_unavailable";
        if (code != "none") throw new McpFailure(code);
        code = _execution.Skills is { } skills ? await skills.ValidateAsync(token, lease) : "skills_unavailable";
        if (code != "none") throw new McpFailure(code);
        if (!lease.IsCurrent) throw new OperationCanceledException(token);
    }
    internal ToolExecutionResult? PreflightFetch(string localName, JsonElement arguments, McpOperationStamp expected)
    {
        if (localName != McpProfile.FetchName) return null;
        lock (_gate)
        {
            if (_fatal || _disposed || _execution.WorkspaceOperations.IsDisposed || _execution.Rules?.FatalFailure == true || _execution.Skills?.FatalFailure == true) return Failure("unavailable");
            if (_sensitive) return Failure("sensitive_content");
            if (_active is not null || _resetting != 0) return Failure("mcp_busy");
            if (_plans.ReviewTurnActive) return Failure("mode_restricted", false);
            if (!IsCurrent(expected)) return Failure("mcp_changed");
            if (_status != McpStatus.Ready || _client is null) return Failure(_status == McpStatus.Stale ? "mcp_catalog_stale" : "mcp_not_connected");
            if (!_guard.IsSafe(arguments)) return Failure("sensitive_arguments");
            if (!ToolArguments.TryStrings(arguments, ["url"], out var values)) return Failure("invalid_arguments", false);
            try
            {
                var code = WebDocumentUrlPolicy.Validate(values[0], _guard, out _);
                return code == "none" ? null : Failure(code);
            }
            catch (McpFailure error) { return Failure(error.Code); }
        }
    }
    internal async Task<ToolExecutionResult> InvokeAsync(string localName, JsonElement arguments, McpOperationStamp expected, CancellationToken token)
    {
        try { arguments = arguments.Clone(); } catch { return Failure("invalid_arguments", false); }
        if (PreflightFetch(localName, arguments, expected) is { } rejected) return rejected;
        McpToolDescriptor? tool; IMcpReadClient client; TaskCompletionSource done; CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_active is not null || _resetting != 0) return Failure("mcp_busy");
            if (_plans.ReviewTurnActive) return Failure("mode_restricted", false);
            if (!IsCurrent(expected)) return Failure("mcp_changed");
            if (_status != McpStatus.Ready || _client is null) return Failure(_status == McpStatus.Stale ? "mcp_catalog_stale" : "mcp_not_connected");
            tool = _snapshot!.Catalog.Tools.SingleOrDefault(t => t.LocalName == localName);
            if (tool is null) return Failure("unknown_tool");
            if (!_guard.IsSafe(arguments)) { _sensitive = true; return Failure("sensitive_arguments"); }
            if (!McpSchema.ValidArguments(tool.Schema, arguments)) return Failure("invalid_arguments", false);
            if (!_execution.Allows(ToolCapability.ExternalRead)) return Failure("mode_restricted", false);
            if (_calls >= 2) return Failure("mcp_limit");
            client = _client; done = _active = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation = _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        }
        WorkspaceOperationLease? lease = null;
        var budget = new McpNetworkBudget(_clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        McpCallOutcome? outcome = null;
        var initialBytes = client.ReceivedBytes;

        try
        {
            if (!_execution.WorkspaceOperations.TryAcquire(linked.Token, out lease)) return Failure("mcp_busy");
            await ValidateSourcesAsync(lease!, linked.Token);
            var rules = _execution.Rules!.Capture(); var skills = _execution.Skills!.Capture();
            var plan = _plans.Snapshot; var generation = _plans.Generation; var mode = _plans.Mode; var recovery = _plans.Recovery.Revision;
            bool Current() => IsCurrent(expected) && _execution.Rules.IsCurrent(rules) && _execution.Skills.IsCurrent(skills) &&
                _execution.Allows(ToolCapability.ExternalRead) && ReferenceEquals(plan, _plans.Snapshot) && generation == _plans.Generation && mode == _plans.Mode && recovery == _plans.Recovery.Revision &&
                lease!.IsCurrent && !linked.IsCancellationRequested;
            async Task CatalogCheck()
            {
                try { if (!_snapshot!.Catalog.Same(await budget.RunAsync(client.ListToolsAsync, linked.Token))) throw new McpFailure("mcp_catalog_stale"); }
                catch { MarkStale(); throw; }
            }
            await CatalogCheck();
            if (!Current()) throw new McpFailure("mcp_changed");
            var explanation = tool.RemoteName == McpProfile.FetchTool
                ? "將 URL 交給 Learn 服務擷取，服務可能記錄，原站狀態與版次未由本機驗證。"
                : "外送公開搜尋；唯讀服務仍可能保存查詢。";
            var block = SafeDisplay($"{explanation}\n目的地：{McpProfile.Endpoint}\n來源：external_mcp／learn\n工具：{tool.LocalName}／{tool.RemoteName}\n目錄版本：{expected.CatalogRevision}；SHA-256：{tool.Fingerprint}\n全部參數：{Encoding.UTF8.GetString(McpJson.Write(arguments))}\n確認本次外送？[y/N] ", false);
            var decision = await _confirm(new(block), cancellation.Token);
            if (decision != McpApprovalDecision.Approved)
                return Failure(decision is McpApprovalDecision.Cancelled or McpApprovalDecision.EndOfInput ? "cancelled" : decision == McpApprovalDecision.Unavailable ? "unavailable" : "approval_declined");
            await ValidateSourcesAsync(lease!, linked.Token); await CatalogCheck();
            if (!Current()) throw new McpFailure("mcp_changed");
            McpCallPermit permit;
            lock (_gate)
            {
                if (!Current()) throw new McpFailure("mcp_changed");
                if (!_guard.IsSafe(arguments)) throw new McpFailure("sensitive_arguments");
                if (tool.RemoteName == McpProfile.FetchTool)
                {
                    var code = WebDocumentUrlPolicy.Validate(arguments.GetProperty("url").GetString()!, _guard, out _);
                    if (code != "none") throw new McpFailure(code);
                }
                permit = _issuedPermit = new(this, tool, arguments, expected.CatalogRevision);
                _permitValidity = Current; _permitConsumed = false;
                _calls = checked(_calls + 1); _callStarts = checked(_callStarts + 1);
            }
            outcome = await budget.RunAsync(async ct => outcome = await client.CallAsync(permit, ct), linked.Token);
            if (!Current()) return Failure(linked.IsCancellationRequested ? "cancelled" : "mcp_changed", outcome: outcome);
            if (outcome.ErrorCode != "none") return Failure(outcome.ErrorCode, outcome: outcome);
            await ValidateSourcesAsync(lease!, linked.Token);
            if (!Current()) return Failure("mcp_changed", outcome: outcome);
            return new(ToolStatus.Success, "none", outcome.Response, mcpOutcome: outcome);
        }
        catch (OperationCanceledException) { return Failure("cancelled", outcome: outcome); }
        catch (McpFailure error) { return Failure(error.Code, outcome: outcome); }
        catch (Exception) { return Failure("unavailable", outcome: outcome); }
        finally
        {
            try { RecordUsage(checked(client.ReceivedBytes - initialBytes), budget.Used); }
            finally
            {
                try { lease?.Dispose(); }
                finally { try { Settle(); } finally { CompleteOperation(done, cancellation); } }
            }
        }
    }
    internal bool TryConsumePermit(McpCallPermit permit, object client)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_issuedPermit, permit) || !ReferenceEquals(_client, client) || _permitConsumed ||
                _permitValidity?.Invoke() != true) return false;
            _permitConsumed = true; return true;
        }
    }
    internal bool IsPermitCurrent(McpCallPermit permit)
    { lock (_gate) return ReferenceEquals(_issuedPermit, permit) && _permitConsumed && _permitValidity?.Invoke() == true; }
    private void CompleteOperation(TaskCompletionSource done, CancellationTokenSource cancellation)
    {
        lock (_gate) { _active = null; _cancellation = null; _issuedPermit = null; _permitValidity = null; _permitConsumed = false; }
        try { cancellation.Dispose(); } finally { done.TrySetResult(); }
    }
    private ToolExecutionResult Failure(string code, bool stop = true, McpCallOutcome? outcome = null)
    {
        lock (_gate)
        {
            // A later stale/cancel/deadline observation cannot erase a stronger received failure.
            if (code is "sensitive_arguments" or "sensitive_content" || outcome?.ErrorCode is "sensitive_arguments" or "sensitive_content") _sensitive = true;
            if (code == "unavailable" || outcome?.ErrorCode == "unavailable") code = "unavailable";
            else if (outcome?.ErrorCode is "sensitive_arguments" or "sensitive_content") code = outcome.ErrorCode;
            _lastCode = code; if (code == "unavailable") _fatal = true;
            var result = ToolResponseWriter.Create(code == "cancelled" ? ToolStatus.Cancelled : ToolStatus.Rejected, code == "cancelled" ? "none" : code, _policy!);
            if (result.Response is { } response && !_guard.IsSafe(response)) { _sensitive = true; result = result.WithoutResponse(); }
            return new(result.Status, result.ErrorCode, result.Response, stopAfterOperation: stop, mcpOutcome: outcome is null ? new(code, McpTransmission.NotSent) : new(code, outcome.Transmission, bytes: outcome.Bytes));
        }
    }
    private void RecordUsage(long bytes, TimeSpan duration)
    {
        lock (_gate)
        {
            try { _receivedBytes = checked(_receivedBytes + bytes); _activeTicks = checked(_activeTicks + duration.Ticks); }
            catch (OverflowException) { _fatal = true; throw; }
        }
    }
    internal void MarkStale()
    {
        lock (_gate)
        {
            if (_snapshot is null || _status == McpStatus.Stale) return;
            _status = McpStatus.Stale; _pending = true;
            if (_generation == long.MaxValue) _fatal = true; else _generation++;
        }
    }
    internal void Settle()
    {
        lock (_gate)
        {
            if (!_pending || _plans.HasReservations) return;
            _plans.RevokeForMcp(); _pending = false;
        }
        _clearSnapshots();
    }
    internal void RulesPublished(string oldScope, string newScope)
    {
        if (oldScope == newScope) MarkStale();
        else lock (_gate)
            {
                if (_status != McpStatus.Disconnected)
                {
                    if (_revision == long.MaxValue) _fatal = true; else _revision++;
                    if (_generation == long.MaxValue) _fatal = true; else _generation++;
                }
                _status = McpStatus.Disconnected; _snapshot = null; _pending = true;
            }
    }
    internal async Task<McpOperationResult> DisconnectAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_active is not null || _resetting != 0) return new("mcp_busy");
            if (_status == McpStatus.Disconnected) return new("no_change", Display());
            _resetting++;
        }
        try { await ResetAsync(false); return new("none", Display()); }
        finally { lock (_gate) _resetting--; }
    }
    internal async Task ResetAsync(bool newSession = true)
    {
        Task? wait; IMcpReadClient? client; CancellationTokenSource? cancellation;
        lock (_gate)
        {
            _resetting++;
            if (_generation == long.MaxValue) _fatal = true; else _generation++;
            if (newSession) { if (_session == long.MaxValue) _fatal = true; else _session++; }
            if (_status != McpStatus.Disconnected) { if (_revision == long.MaxValue) _fatal = true; else _revision++; }
            _status = McpStatus.Disconnected; _snapshot = null; _pending = !newSession;
            cancellation = _cancellation; wait = _active?.Task;
        }
        try
        {
            var failed = false;
            try { cancellation?.Cancel(); } catch (ObjectDisposedException) { } catch { failed = true; }
            try { if (wait is not null) await wait; } catch { failed = true; }
            lock (_gate) { client = _client; _client = null; _sensitive = false; }
            try { if (client is not null) await CloseAsync(client); } catch { failed = true; }
            try { Settle(); } catch { failed = true; }
            if (failed)
            {
                lock (_gate) _fatal = true;
                throw new InvalidOperationException("MCP 無法完成安全收尾");
            }
        }
        finally { lock (_gate) _resetting--; }
    }
    internal void BindPolicy(AgentExecutionPolicy execution, ISensitiveDataPolicy policy, IMcpContentGuard guard)
    { lock (_gate) { _execution = execution; _policy = policy; _guard = guard; execution.Mcp = this; } }
    private async ValueTask CloseAsync(IMcpReadClient client)
    { try { await client.DisposeAsync(); } catch { lock (_gate) _fatal = true; throw new InvalidOperationException("MCP 資源無法安全關閉"); } }
    internal async Task DrainDisconnectedAsync()
    {
        IMcpReadClient? client;
        lock (_gate) { if (_status != McpStatus.Disconnected) return; client = _client; _client = null; }
        if (client is not null) await CloseAsync(client); Settle();
    }
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_disposal is not null) return new(_disposal);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _disposal = completion.Task; _disposed = true;
        }
        _ = DisposeCoreAsync(completion); return new(completion.Task);
    }
    private async Task DisposeCoreAsync(TaskCompletionSource completion)
    {
        try { await ResetAsync(); lock (_gate) _policy = null; completion.TrySetResult(); }
        catch { lock (_gate) _fatal = true; completion.TrySetException(new InvalidOperationException("MCP 無法完成釋放")); }
    }
    public override string ToString() => "[McpCoordinator]";
}