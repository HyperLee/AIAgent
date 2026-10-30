using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIAgent.Cli.Git;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed class DelegationIdentitySource
{
    internal long Parent;
    internal long Batch;
    internal long Agent;
    internal long Event;
    public override string ToString() => "[DelegationIdentitySource]";
}
[JsonConverter(typeof(DelegationOpaqueConverter))]
internal sealed class DelegationCoordinator(ContextCoordinator contexts, AgentExecutionPolicy execution, GenerationBudget budget,
    WorkspaceFiles files, ISensitiveDataPolicy policy, IDelegationRuntimeFactory? factory, Func<IGitReadWorkspace> gitFactory,
    DelegationIdentitySource identities, Action<DelegationEvent> onEvent, TimeProvider? timeProvider = null, IMcpContentGuard? reportGuard = null)
{
    private object Gate => contexts.DecisionGate;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private long _revision;
    private long _parentTurnId;
    private string _user = "";
    private bool _accepted;
    private bool _running;
    private ContextOperationStamp? _parentStamp;
    private CancellationTokenSource? _batchCancellation;
    private GenerationGateway? _gateway;
    private DelegationMode _mode;
    private DelegationEnableVersion? _enableStamp;
    private Task _projection = Task.CompletedTask;
    private Action<DelegationFailure>? _projectionFailure;
    private int _eventsQueued;
    private long _batchRevision;
    internal DelegationMode Mode { get { lock (Gate) { RefreshEnabled(); return _mode; } } }
    private void RefreshEnabled()
    {
        if (_mode != DelegationMode.Enabled || _enableStamp is not { } previous) return;
        var current = contexts.Capture();
        if (previous.Matches(current) && execution.Rules?.Capture().Status == ProjectRulesStatus.Valid) return;
        _mode = DelegationMode.Disabled; _revision = checked(_revision + 1); LastResult = new(ToolStatus.Failed, "delegation_source_changed", null);
    }
    internal DelegationBatchResult? LastResult { get; private set; }
    internal DelegationBudgetLease? AggregationLease { get; private set; }
    internal bool FatalFailure { get; private set; }
    internal bool Available => factory is not null;
    internal long Revision => _revision;
    internal long ParentTurnId => _parentTurnId;
    internal void BeginParentTurn(string user)
    {
        lock (Gate)
        {
            if (_running || FatalFailure) throw new DelegationFailure("delegation_cleanup_failed", fatal: true);
            files.ClearDelegationScopes(); _parentTurnId = checked(++identities.Parent); _user = user; _accepted = false; AggregationLease?.Dispose(); AggregationLease = null;
        }
    }
    internal void BindGateway(GenerationGateway gateway) { lock (Gate) _gateway = gateway; }
    internal void SetEnabled(bool enabled)
    {
        lock (Gate)
        {
            var next = enabled ? DelegationMode.Enabled : DelegationMode.Disabled;
            if (next == Mode) return;
            if (enabled && (!Available || FatalFailure)) throw new DelegationFailure("delegation_runtime_failed");
            _revision = checked(_revision + 1); _mode = next; _enableStamp = enabled ? new(contexts.Capture()) : null;
        }
    }
    internal void Invalidate(string code = "delegation_source_changed")
    {
        SetEnabled(false);
        lock (Gate) { LastResult = new(ToolStatus.Failed, code, null); _parentStamp = null; }
        _batchCancellation?.Cancel();
    }
    internal void EndParentTurn() { lock (Gate) { AggregationLease?.Dispose(); AggregationLease = null; _gateway = null; _user = ""; _parentStamp = null; _projectionFailure = null; _projection = Task.CompletedTask; } }
    internal async Task<string> ValidateAvailableAsync(CancellationToken token)
    {
        if (!Available || FatalFailure) return "delegation_runtime_failed";
        try { files.ClearDelegationScopes(); await ValidateOwnersAsync(token); return "none"; }
        catch (DelegationFailure ex) { return ex.Code; }
    }
    private async Task ValidateOwnersAsync(CancellationToken token)
    {
        try
        {
            await using var lease = await execution.WorkspaceOperations.AcquireAsync(token);
            var rules = execution.Rules; var skills = execution.Skills;
            if (rules is null || skills is null) throw new DelegationFailure("rules_unavailable");
            if (rules.FatalFailure || skills.FatalFailure) throw new DelegationFailure("delegation_cleanup_failed", fatal: true);
            var code = await rules.ValidateAsync(token, lease); if (code != "none") throw DelegationErrors.Source(code);
            code = await skills.ValidateAsync(token, lease); if (code != "none") throw DelegationErrors.Source(code);
            if (!files.CheckDelegationScopes(lease)) throw new DelegationFailure("delegation_source_changed");
            if (skills.RequiresContextReset) throw new DelegationFailure("sensitive_content", reset: true);
        }
        catch (SkillsSensitiveException) { throw new DelegationFailure("sensitive_content", reset: true); }
        catch (ProjectRulesSensitiveException) { throw new DelegationFailure("sensitive_content", reset: true); }
    }
    private async Task RequireCurrentAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (Gate) if (FatalFailure) throw new DelegationFailure("delegation_cleanup_failed", fatal: true);
        lock (Gate) if (_parentStamp is null || !contexts.IsCurrent(_parentStamp) || Mode != DelegationMode.Enabled || _batchRevision != _revision) throw new DelegationFailure("delegation_source_changed");
        await ValidateOwnersAsync(token);
        lock (Gate) if (FatalFailure) throw new DelegationFailure("delegation_cleanup_failed", fatal: true);
        lock (Gate) if (_parentStamp is null || !contexts.IsCurrent(_parentStamp) || Mode != DelegationMode.Enabled || _batchRevision != _revision) throw new DelegationFailure("delegation_source_changed");
    }
    internal async Task<DelegationBatchResult> RunBatchAsync(DelegationBatchRequest request, CancellationToken cancellationToken)
    {
        DelegationBatchResult Reject(string code) => new(ToolStatus.Rejected, code, null);
        lock (Gate)
        {
            if (Mode != DelegationMode.Enabled) return Reject("delegation_disabled");
            if (_running || _accepted || request.ParentTurnId != _parentTurnId) return Reject("delegation_limit");
            if (!Available || _gateway is null) return Reject("delegation_runtime_failed");
            _running = true; _parentStamp = contexts.Capture(); _batchRevision = _revision;
        }
        Work[] work = []; IGitReadWorkspace? git = null; Task? activity = null;
        CancellationTokenSource? deadline = null; CancellationTokenSource? cleanup = null; CancellationTokenSource? batchCancellation = null;
        DelegationBudgetLease? batchLease = null; Task? finalValidation = null; Task? projection = null; Task? sourceDisposal = null;
        var batchId = ""; DelegationFailure? global = null; var accepted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Tasks.Any(task => !policy.CanSendOrUse(task.Objective, ContentBoundary.ModelInput) || !policy.CanSendOrUse(task.Acceptance, ContentBoundary.ModelInput) || !policy.CanSendOrUse(task.ScopePath, ContentBoundary.ModelInput)))
                throw new DelegationFailure("sensitive_arguments", reset: true);
            await RequireCurrentAsync(cancellationToken);
            var tasks = new List<DelegationTask>();
            foreach (var task in request.Tasks)
            {
                if (!policy.CanSendOrUse(task.Objective, ContentBoundary.ModelInput) || !policy.CanSendOrUse(task.Acceptance, ContentBoundary.ModelInput) || !policy.CanSendOrUse(task.ScopePath, ContentBoundary.ModelInput)) throw new DelegationFailure("sensitive_arguments", reset: true);
                if (!WorkspacePathRules.TryNormalize(task.ScopePath, out var canonical, out _) || WorkspacePathRules.IsSensitive(canonical)) return Reject("invalid_path");
                var code = await files.ValidateDelegationScopeAsync(canonical, cancellationToken); if (code != "none") return Reject(code);
                tasks.Add(new(task.Role, task.Objective, canonical, task.Acceptance));
            }
            var transfers = tasks.Select(BuildTransfer).ToArray();
            // Reserve identity space and budget atomically, after all pure/local admission.
            lock (Gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!contexts.IsCurrent(_parentStamp!) || Mode != DelegationMode.Enabled) throw new DelegationFailure("delegation_source_changed");
                _ = checked(identities.Batch + 1); _ = checked(identities.Agent + tasks.Count);
                if (!budget.TryReserveDelegation(_parentTurnId, tasks.Count, cancellationToken, out var reservation)) return Reject("delegation_budget_insufficient");
                AggregationLease = batchLease = reservation; _accepted = accepted = true;
                batchId = "d" + checked(++identities.Batch).ToString(System.Globalization.CultureInfo.InvariantCulture);
                work = tasks.Select((task, index) => new Work("a" + checked(++identities.Agent).ToString(System.Globalization.CultureInfo.InvariantCulture), task)
                { Instruction = transfers[index].Instruction, Handoff = transfers[index].Handoff }).ToArray();
                LastResult = null; _eventsQueued = 0; _projection = Task.CompletedTask;
            }
            deadline = new(TimeSpan.FromSeconds(120), _clock);
            _batchCancellation = batchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            void Fail(DelegationFailure failure)
            {
                lock (Gate)
                {
                    if (global is null || failure.Fatal || failure.Reset && !global.Fatal) global = failure;
                    if (failure.Fatal) FatalFailure = true;
                }
                batchCancellation.Cancel();
            }
            _projectionFailure = Fail;
            async Task ExecuteAsync()
            {
                var token = batchCancellation.Token;
                try
                {
                    foreach (var item in work) Emit(item, batchId, DelegationEventKind.Queued);
                    if (work.Any(w => w.Task.Role == DelegationRole.Review))
                    {
                        git = gitFactory(); if (git is GitReadWorkspace native) native.Delegated = true;
                        var capture = await git.CaptureAsync(token);
                        if (capture.ErrorCode != "none" || capture.Snapshot is null)
                        {
                            if (IsGlobal(capture.ErrorCode)) throw new DelegationFailure(capture.ErrorCode, fatal: capture.ErrorCode is "process_cleanup_failed" or "process_unconfirmed", reset: capture.ErrorCode == "sensitive_content");
                            foreach (var item in work.Where(w => w.Task.Role == DelegationRole.Review)) item.Result = Failure(item, capture.ErrorCode);
                        }
                        else
                        {
                            var source = capture.Snapshot.WithId("g" + batchId[1..]);
                            foreach (var item in work.Where(w => w.Task.Role == DelegationRole.Review))
                            {
                                var view = new GitSnapshot(source.SnapshotId, item.Task.ScopePath, source.HeadOid, source.ObjectFormat,
                                    source.Changes.Where(c => WorkspaceFiles.DelegationContains(item.Task.ScopePath, c.Path)),
                                    source.Exclusions.Where(e => e.Path is null || WorkspaceFiles.DelegationContains(item.Task.ScopePath, e.Path)), source.Manifest, source.RulesStamp, source.EnumerationComplete);
                                item.Snapshots = new(new DelegationGitView(git, files.ExecutionPolicy.WorkspaceOperations), policy, () => view.SnapshotId);
                                item.Snapshots.BindDelegationView(view); item.Review = new(item.Snapshots, policy);
                                if (item.Snapshots.EligibleChangeCount == 0)
                                {
                                    var complete = view.Changes.Count == 0 && view.Exclusions.Count == 0 && view.EnumerationComplete;
                                    item.Result = new(item.Id, item.Task, DelegationTaskState.Completed, "none", null, DelegationJson.Coverage(DelegationRole.Review, complete: complete, limitations: [complete ? "no_changes" : "review_incomplete"]));
                                }
                            }
                        }
                    }
                    foreach (var item in work.Where(w => w.Result is not null))
                    { batchLease!.ReleaseChild(Array.IndexOf(work, item)); Emit(item, batchId, DelegationEventKind.Ended); }
                    try
                    {
                        foreach (var item in work.Where(w => w.Result is null))
                        {
                            token.ThrowIfCancellationRequested(); await RequireCurrentAsync(token);
                            if (item.Task.Role == DelegationRole.Explore) item.Reads = files.CreateDelegationReadSession(item.Task.ScopePath);
                            item.Spec = BuildSpec(item, batchId);
                            item.Runtime = await factory!.CreateAsync(item.Spec, token);
                            token.ThrowIfCancellationRequested();
                            ITool[] tools = item.Task.Role == DelegationRole.Explore ? [new ListFilesTool(item.Reads!.Files, policy), new ReadFileTool(item.Reads.Files, policy), new SearchFilesTool(item.Reads.Files, policy), new SearchTextTool(item.Reads.Files, policy), new ReadFileRangeTool(item.Reads.Files, policy)] :
                                [new GitStatusTool(item.Snapshots!, policy), new GitDiffTool(item.Snapshots!, policy), new ReadGitFileRangeTool(item.Snapshots!, policy)];
                            var index = Array.IndexOf(work, item);
                            item.Runner = new(item.Spec, item.Runtime, tools, item.Reads, item.Review, batchLease!, index, _gateway!, policy, () => !FatalFailure && _parentStamp is not null && contexts.IsCurrent(_parentStamp) && Mode == DelegationMode.Enabled && _batchRevision == _revision, RequireCurrentAsync,
                                (kind, count) => Emit(item, batchId, kind, count), reportGuard);
                            item.Runner.ValidateRuntime();
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException && ex is not DelegationFailure { Reset: true } && ex is not DelegationFailure { Fatal: true } && (ex is not DelegationFailure sourceFailure || !IsGlobal(sourceFailure.Code)))
                    {
                        var code = ex is DelegationFailure df && df.Code == "context_capacity" ? "context_capacity" : "delegation_runtime_failed";
                        foreach (var item in work.Where(w => w.Result is null))
                        { item.Result = Failure(item, code); batchLease!.ReleaseChild(Array.IndexOf(work, item)); Emit(item, batchId, DelegationEventKind.Ended); }
                        return;
                    }
                    await Task.WhenAll(work.Select(async item =>
                    {
                        if (item.Result is not null) return;
                        try { Emit(item, batchId, DelegationEventKind.Started); item.Result = await item.Runner!.RunAsync(token); }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                        catch (DelegationFailure failure)
                        {
                            if (failure.Reset || failure.Fatal || failure.AuthenticationFailed || IsGlobal(failure.Code)) Fail(failure);
                            else item.Result = item.Runner!.End(failure.Code);
                        }
                        catch (Exception) { item.Result = item.Runner!.End("delegation_model_failed"); }
                        finally
                        {
                            batchLease!.ReleaseChild(Array.IndexOf(work, item));
                            if (item.Result is not null) Emit(item, batchId, DelegationEventKind.Ended);
                        }
                    }));
                }
                catch (DelegationFailure failure) { Fail(failure); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception) { Fail(new("delegation_runtime_failed")); }
            }
            activity = ExecuteAsync();
            try { await activity.WaitAsync(batchCancellation.Token); }
            catch (OperationCanceledException) when (batchCancellation.IsCancellationRequested) { }
            cleanup = new(TimeSpan.FromSeconds(10), _clock);
            try
            {
                await activity.WaitAsync(cleanup.Token);
                lock (Gate) projection = _projection;
                await projection.WaitAsync(cleanup.Token);
                foreach (var item in work)
                    item.Result ??= item.Runner?.End(cancellationToken.IsCancellationRequested ? "turn_cancelled" : "delegation_timed_out", cancellationToken.IsCancellationRequested ? DelegationTaskState.Cancelled : DelegationTaskState.TimedOut) ??
                        Failure(item, cancellationToken.IsCancellationRequested ? "turn_cancelled" : "delegation_timed_out", cancellationToken.IsCancellationRequested ? DelegationTaskState.Cancelled : DelegationTaskState.TimedOut);
                if (global is null && !cancellationToken.IsCancellationRequested)
                {
                    using var validation = CancellationTokenSource.CreateLinkedTokenSource(cleanup.Token, cancellationToken);
                    async Task RevalidateAsync()
                    {
                        await RequireCurrentAsync(validation.Token);
                        foreach (var item in work.Where(w => w.Result!.State == DelegationTaskState.Completed))
                        {
                            var error = item.Reads is not null ? await item.Reads.ValidateAsync(validation.Token) : item.Snapshots is not null ? await item.Snapshots.ValidateAsync(validation.Token) : "none";
                            if (error != "none") throw DelegationErrors.Source(error);
                        }
                    }
                    finalValidation = RevalidateAsync(); await finalValidation.WaitAsync(cleanup.Token);
                }
            }
            catch (DelegationFailure failure) { global = failure; if (failure.Fatal) FatalFailure = true; }
            catch (OperationCanceledException) when (!cleanup.IsCancellationRequested && cancellationToken.IsCancellationRequested) { }
            catch (OperationCanceledException) when (cleanup.IsCancellationRequested) { if (activity.IsCompleted && (finalValidation is null || finalValidation.IsCompleted) && projection?.IsCompleted != false) global = new("delegation_source_changed"); else throw new DelegationFailure("delegation_cleanup_failed", fatal: true); }
            // All owners remain alive until every child has joined and relinquished its capability.
            foreach (var item in work)
            {
                if (item.Runtime is not null) await (item.RuntimeDisposal ??= DisposeOwned(item.Runtime)).WaitAsync(cleanup.Token);
                if (item.Reads is not null) await (item.ReadDisposal ??= DisposeOwned(item.Reads)).WaitAsync(cleanup.Token);
                if (item.Snapshots is not null) await (item.GitDisposal ??= DisposeOwned(item.Snapshots)).WaitAsync(cleanup.Token);
                batchLease!.ReleaseChild(Array.IndexOf(work, item));
            }
            if (git is not null) await (sourceDisposal = DisposeOwned(git)).WaitAsync(cleanup.Token);
            if (global is not null) throw global;
            cancellationToken.ThrowIfCancellationRequested();
            var children = work.Select(w => w.Result!).ToArray(); var data = DelegationReports.Batch(batchId, children);
            lock (Gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!contexts.IsCurrent(_parentStamp!) || Mode != DelegationMode.Enabled) throw new DelegationFailure("delegation_source_changed");
                return LastResult = new(ToolStatus.Success, "none", data, children);
            }
        }
        catch (Exception ex)
        {
            var unconfirmed = FatalFailure || global?.Fatal == true || cleanup?.IsCancellationRequested == true &&
                (activity?.IsCompleted != true || finalValidation?.IsCompleted == false || projection?.IsCompleted == false || sourceDisposal?.IsCompleted == false || work.Any(w => w.DisposalPending));
            if (!unconfirmed && ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                return LastResult = new(ToolStatus.Cancelled, "turn_cancelled", null);
            var failure = ex as DelegationFailure ?? new DelegationFailure(accepted ? "delegation_cleanup_failed" : "delegation_runtime_failed", fatal: accepted);
            FatalFailure |= unconfirmed || failure.Fatal;
            if (accepted) SetEnabled(false);
            if (FatalFailure) batchCancellation?.Cancel();
            return LastResult = new(!accepted && !failure.Reset && !failure.Fatal ? ToolStatus.Rejected : ToolStatus.Failed,
                FatalFailure ? "delegation_cleanup_failed" : failure.Code, null, fatal: FatalFailure, reset: failure.Reset, authenticationFailed: failure.AuthenticationFailed);
        }
        finally
        {
            deadline?.Dispose(); cleanup?.Dispose();
            if (FatalFailure)
                _ = ReleaseAbandonedAsync(activity, finalValidation, work, git, sourceDisposal, batchCancellation, batchLease);
            else batchCancellation?.Dispose();
            _batchCancellation = null;
            lock (Gate) _running = false;
        }
    }
    private static async Task ReleaseAbandonedAsync(Task? activity, Task? validation, Work[] work, IGitReadWorkspace? git, Task? sourceDisposal,
        CancellationTokenSource? cancellation, DelegationBudgetLease? lease)
    {
        try
        {
            try { if (activity is not null) await activity; } catch (Exception) { }
            try { if (validation is not null) await validation; } catch (Exception) { }
            foreach (var item in work)
            {
                try { if (item.Runtime is not null) await (item.RuntimeDisposal ??= DisposeOwned(item.Runtime)); } catch (Exception) { }
                try { if (item.Reads is not null) await (item.ReadDisposal ??= DisposeOwned(item.Reads)); } catch (Exception) { }
                try { if (item.Snapshots is not null) await (item.GitDisposal ??= DisposeOwned(item.Snapshots)); } catch (Exception) { }
            }
            try { if (git is not null) await (sourceDisposal ?? DisposeOwned(git)); } catch (Exception) { }
        }
        finally { cancellation?.Dispose(); lease?.Dispose(); }
    }
    private static Task DisposeOwned(IAsyncDisposable value)
    { try { return value.DisposeAsync().AsTask(); } catch (Exception ex) { return Task.FromException(ex); } }
    private DelegationChildSpec BuildSpec(Work item, string batch)
    {
        var identity = files.DelegationIdentity(item.Task.ScopePath);
        return new(item.Task, new(_parentTurnId, batch, item.Id, _revision, _parentStamp!, execution.Rules!.Capture().Scope, item.Task.ScopePath,
            identity.RootDevice, identity.RootIdentity, identity.ScopeDevice, identity.ScopeIdentity, execution.Plans.EvidenceEpoch), item.Instruction!, item.Handoff!);
    }
    private (string Instruction, string Handoff) BuildTransfer(DelegationTask task)
    {
        var handoff = DelegationJson.Write(w =>
        {
            w.WriteStartObject(); w.WriteNumber("version", 1); w.WriteString("objective", task.Objective); w.WriteString("acceptance", task.Acceptance);
            w.WriteString("scope_path", task.ScopePath); w.WriteString("user_text", _user); w.WriteEndObject();
        }, 32768).GetRawText();
        var pins = Encoding.UTF8.GetString(ContextDocuments.Pins(_parentStamp!.Context.Pins));
        var instruction = DelegationInstructions.Common +
            (task.Role == DelegationRole.Explore ? "僅輸出JSON：version=1、summary、observations（path/start_line/end_line/note）、limitations；僅存在性觀察的行號皆null；引用已提供來源。" : "僅輸出既有 review v1 JSON；selection=all；finding限已提供的變更行。") +
            execution.Rules!.BuildAppendix() + execution.Skills!.BuildAppendix() + execution.Plans.StateAppendix() + execution.Recovery.BuildAppendix() + "\n原文備註：" + pins;
        if (!policy.CanSendOrUse(instruction + handoff, ContentBoundary.ModelInput)) throw new DelegationFailure("sensitive_content", reset: true);
        return (instruction, handoff);
    }
    private void Emit(Work item, string batch, DelegationEventKind kind, int count = 0)
    {
        lock (Gate)
        {
            if (FatalFailure) return;
            if (++_eventsQueued > 64) throw new DelegationFailure("delegation_cleanup_failed", fatal: true);
            var value = new DelegationEvent(checked(++identities.Event), _parentTurnId, batch, item.Id, item.Task.Role, kind, count);
            var previous = _projection; var failed = _projectionFailure;
            _projection = Task.Run(async () =>
            {
                try { await previous; if (!FatalFailure) onEvent(value); }
                catch (Exception) { failed?.Invoke(new("delegation_cleanup_failed", fatal: true)); }
            });
        }
    }
    private static DelegationChildResult Failure(Work item, string code, DelegationTaskState state = DelegationTaskState.Failed) =>
        new(item.Id, item.Task, state, code, null, DelegationJson.Coverage(item.Task.Role, limitations: [code]));
    private static bool IsGlobal(string code) => code is "delegation_source_changed" or "git_snapshot_stale" or "sensitive_content" or "sensitive_arguments" or "process_cleanup_failed" or "process_unconfirmed" or "workspace_unavailable" or "mcp_changed" || code.StartsWith("rules_", StringComparison.Ordinal) || code.StartsWith("skills_", StringComparison.Ordinal);
    private sealed class Work(string id, DelegationTask task)
    {
        internal string Id { get; } = id; internal DelegationTask Task { get; } = task;
        internal string? Instruction; internal string? Handoff;
        internal Task? RuntimeDisposal; internal Task? ReadDisposal; internal Task? GitDisposal;
        internal bool DisposalPending => RuntimeDisposal?.IsCompleted == false || ReadDisposal?.IsCompleted == false || GitDisposal?.IsCompleted == false;
        internal DelegationChildSpec? Spec; internal DelegationRuntime? Runtime; internal DelegationReadSession? Reads;
        internal GitSnapshotCoordinator? Snapshots; internal CodeReviewCoordinator? Review; internal DelegatedReadRunner? Runner; internal DelegationChildResult? Result;
    }
    public override string ToString() => "[DelegationCoordinator]";
}
internal sealed class DelegationGitView(IGitReadWorkspace source, WorkspaceOperationCoordinator operations) : IGitReadWorkspace
{
    public Task<GitCaptureResult> CaptureAsync(CancellationToken token) => Task.FromResult(new GitCaptureResult("mode_restricted"));
    public Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken token) => ValidateAsync(snapshot.Manifest, token);
    public async Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken token)
    {
        if (source is GitReadWorkspace) return await source.ValidateAsync(manifest, token);
        await using var lease = await operations.AcquireAsync(token); return await source.ValidateAsync(manifest, token);
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}