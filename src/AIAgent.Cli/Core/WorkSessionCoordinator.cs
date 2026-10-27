using System.Globalization;
using System.Text;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal enum WorkApprovalKind { Save, Delete, Resume, Scope, Execute }
internal enum WorkApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }

internal sealed class WorkApprovalPreview(WorkApprovalKind kind, string displayBlock)
{
    internal WorkApprovalKind Kind { get; } = kind;
    internal string DisplayBlock { get; } = displayBlock;
    public override string ToString() => "[WorkApprovalPreview]";
}

internal sealed class WorkOperationResult(string errorCode, string? displayBlock = null, AgentTurnResult? generation = null,
    WorkStoreMutation? mutation = null, WorkApprovalDecision? decision = null, bool requiresContextReset = false, bool fatal = false, string? workId = null)
{
    internal string Status => ErrorCode == "none" ? "completed" : ErrorCode is "work_cancelled" or "approval_declined" ? "cancelled" : "rejected";
    internal string ErrorCode { get; } = errorCode;
    internal string? DisplayBlock { get; } = displayBlock;
    internal string? WorkId { get; } = workId;
    internal WorkPublicationState PublicationState { get; } = mutation?.PublicationState ?? WorkPublicationState.NotPublished;
    internal WorkDeleteState DeleteState { get; } = mutation?.DeleteState ?? WorkDeleteState.None;
    internal bool CleanupFailed { get; } = mutation?.CleanupFailed ?? false;
    internal bool StoreStopped { get; } = mutation?.StoreStopped ?? false;
    internal string? TemporaryName { get; } = mutation?.TemporaryName;
    internal int GenerationsStarted { get; } = generation?.GenerationsStarted ?? 0;
    internal UsageSnapshot? Usage { get; } = generation?.TurnUsage;
    internal LlmErrorCode? ModelErrorCode { get; } = generation?.ModelErrorCode;
    internal bool RequiresContextReset { get; } = requiresContextReset || generation?.RequiresContextReset == true;
    internal bool Fatal { get; } = fatal || generation?.ModelErrorCode == LlmErrorCode.ClientUnavailable;
    internal WorkApprovalDecision? Decision { get; } = decision;
    public override string ToString() => "[WorkOperationResult]";
}

internal sealed class WorkSessionCoordinator(ContextCoordinator contexts, PlanStore plans, ProjectRulesCoordinator rules,
    IWorkReferenceWorkspace references, IWorkSaveStore store, ISensitiveDataPolicy sensitivePolicy, AgentRunner? runner,
    ILlmContextInspector? inspector, GenerationBudget budget, Func<DateTimeOffset>? clock = null, Func<string>? newId = null)
{
    private readonly WorkPersistencePolicy _policy = new(sensitivePolicy);
    private int _running;
    private bool _resumeExecutionConfirmationRequired;
    private long _confirmedRecoveryRevision;
    internal bool ResumeExecutionConfirmationRequired => _resumeExecutionConfirmationRequired || plans.Recovery.Last is not null && plans.Recovery.Revision != _confirmedRecoveryRevision;
    internal void BindRunner(AgentRunner value, ILlmContextInspector? inspection) { runner = value; inspector = inspection; }

    internal Task<WorkOperationResult> SaveAsync(string title, Func<WorkApprovalPreview, CancellationToken, ValueTask<WorkApprovalDecision>> confirm, CancellationToken token) =>
        RunAsync(async () =>
        {
            AgentTurnResult? generation = null;
            try
            {
                RequireSafe(title);
                await RequireRulesAsync(token);
                var stamp = contexts.Capture();
                if (plans.HasReservations || plans.HasInProgress) throw new WorkDocumentException("work_revision_conflict");
                var plan = WorkSaveCodec.FromPlan(stamp.Plan) ?? contexts.LoadedWorkPlan;
                var currentText = stamp.Context.Pins.Select(p => p.Text)
                    .Concat(stamp.Context.Summary is { } currentSummary ? currentSummary.Goals.Concat(currentSummary.Decisions).Concat(currentSummary.OpenQuestions).Concat(currentSummary.Background) : []);
                if (currentText.Any(text => !_policy.IsSafe(text)) || plan is not null && !SafeCurrentPlan(plan))
                    return new("work_sensitive_content", requiresContextReset: true);
                if (stamp.Context.Summary is null && stamp.Context.CompletedTurns.Count == 0 && stamp.Context.Pins.Count == 0 && plan is null)
                    return new("nothing_to_save");
                var preflight = await store.PreflightSaveAsync(token);
                if (preflight != "none") return new(preflight);
                var root = await references.CaptureWorkRootAsync(token);
                var id = (newId ?? (() => Guid.NewGuid().ToString("N")))(); RequireSafe(id);
                var created = (clock ?? (() => DateTimeOffset.UtcNow))().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                RequireSafe(created);
                var rulesStamp = rules.Capture();
                var ruleRefs = rulesStamp.Snapshot!.Sources.Select(s => new WorkRuleReference(s.Path, s.Sha256)).ToArray();
                var pins = stamp.Context.Pins.Select(p => p.Text).ToArray();
                var paths = Paths(plan);
                var preliminary = new WorkSaveDocument(id, title, created, root, rulesStamp.Scope, ruleRefs, stamp.Context.Summary, pins, plan,
                    paths.Select(p => new WorkFileReference(p, "unavailable", null, null)).ToArray());
                _ = WorkSaveCodec.Write(preliminary, _policy);
                var summary = stamp.Context.Summary;
                if (stamp.Context.CompletedTurns.Count > 0)
                {
                    if (runner is null || inspector is null) return new("context_measure_unavailable");
                    if (budget.Snapshot().ProgramRemaining == 0) return new("program_generation_limit");
                    var source = contexts.PrepareWorkSource(stamp, inspector);
                    generation = await runner.RunWorkSaveSummaryAsync(stamp, source, token);
                    if (generation.Status != AgentTurnStatus.Completed)
                        return new(GenerationError(generation), generation: generation,
                            fatal: generation.StopReason is AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable);
                    summary = ContextDocuments.ParseSummary(generation.SafeText, 0, 0, sensitivePolicy);
                    if (summary is null) return new("work_summary_failed", generation: generation);
                }
                await RequireCurrentAsync(stamp, token);
                var observed = await references.ReadWorkReferencesAsync(paths, token);
                if (!SameRoot(root, observed.Root)) throw new WorkDocumentException("work_revision_conflict");
                var document = new WorkSaveDocument(id, title, created, root, rulesStamp.Scope, ruleRefs, summary, pins, plan, observed.References);
                var bytes = WorkSaveCodec.Write(document, _policy);
                var preview = Preview(document, bytes, WorkApprovalKind.Save,
                    "明文存檔受本機權限保護；同 UID、管理員、備份或實體磁碟仍可能取得。SHA-256 只供變動比較，不是認證。\n" +
                    "模型摘要可能遺失資訊；必留原文請使用 pin。摘要來源可能已送至 Gemini；本次生成：" + (generation?.GenerationsStarted ?? 0) +
                    "。\n" + UsageText(generation) + "尚未保存；保存不恢復授權、快照或驗證。\n");
                var decision = await confirm(preview, token);
                if (decision != WorkApprovalDecision.Approved) return DecisionResult(decision, generation);
                await RequireCurrentAsync(stamp, token);
                var final = await references.ReadWorkReferencesAsync(paths, token);
                if (!observed.Same(final)) throw new WorkDocumentException("work_revision_conflict");
                await RequireCurrentAsync(stamp, token);
                var published = await store.PublishAsync(id, bytes, token);
                return new(published.ErrorCode, generation: generation, mutation: published, workId: id);
            }
            catch (OperationCanceledException) { return new("work_cancelled", generation: generation, decision: WorkApprovalDecision.Cancelled); }
            catch (WorkDocumentException ex) { return new(ex.Code, generation: generation, requiresContextReset: generation is not null && ex.Code == "work_sensitive_content"); }
            catch (WorkStoreException ex) { return new(ex.Code, generation: generation); }
            catch (ContextCapacityException) { return new("work_capacity", generation: generation); }
            catch (ContextInspectionException) { return new("work_sensitive_content", generation: generation, requiresContextReset: true); }
            catch (WorkSensitiveSourceException) { return new("work_sensitive_content", generation: generation, requiresContextReset: true); }
        });

    internal Task<WorkOperationResult> ListAsync(CancellationToken token) => RunAsync(async () =>
    {
        var files = await store.ListAsync(token);
        var documents = files.Select(f => WorkSaveCodec.Parse(f.Bytes, f.WorkId, _policy)).OrderByDescending(d => d.CreatedUtc, StringComparer.Ordinal).ThenBy(d => d.WorkId, StringComparer.Ordinal).ToArray();
        var display = new StringBuilder("[歷史工作存檔]\n明文歷史資料；不載入、不核准、不驗證目前檔案。\n");
        foreach (var d in documents) display.Append(d.WorkId).Append(" | ").Append(d.Title).Append(" | ").Append(d.CreatedUtc).Append(" | ").Append(d.Workspace.Root).Append(" | Scope ").Append(d.ScopeHint).Append('\n');
        if (documents.Length == 0) display.Append("沒有工作存檔。\n");
        return new("none", SafeDisplay(display.ToString()));
    });

    internal Task<WorkOperationResult> ShowAsync(string id, CancellationToken token) => RunAsync(async () =>
    {
        var file = await store.ReadAsync(id, token); var document = WorkSaveCodec.Parse(file.Bytes, id, _policy);
        return new("none", SafeDisplay("[未驗證歷史工作]\n不載入、不核准、不驗證目前檔案；舊狀態與 hash 不代表目前事實。\n" + Encoding.UTF8.GetString(WorkSaveCodec.Write(document, _policy)) + "\n"), workId: id);
    });

    internal Task<WorkOperationResult> DeleteAsync(string id, Func<WorkApprovalPreview, CancellationToken, ValueTask<WorkApprovalDecision>> confirm, CancellationToken token) => RunAsync(async () =>
    {
        var file = await store.ReadAsync(id, token); var document = WorkSaveCodec.Parse(file.Bytes, id, _policy);
        var display = "[單一存檔刪除]\nID：" + id + "\n標題：" + document.Title + "\n保存時間：" + document.CreatedUtc +
            "\n只刪除此正式檔；不清除目前工作記憶體，不清理其他檔案。\n" + Prompt(WorkApprovalKind.Delete);
        var decision = await confirm(new(WorkApprovalKind.Delete, SafeDisplay(display)), token);
        if (decision != WorkApprovalDecision.Approved) return DecisionResult(decision);
        var result = await store.DeleteAsync(file, token);
        return new(result.ErrorCode, mutation: result, workId: id);
    });

    internal Task<WorkOperationResult> ResumeAsync(string id, Func<WorkApprovalPreview, CancellationToken, ValueTask<WorkApprovalDecision>> confirm, CancellationToken token) => RunAsync(async () =>
    {
        var file = await store.ReadAsync(id, token); var document = WorkSaveCodec.Parse(file.Bytes, id, _policy);
        var root = await references.CaptureWorkRootAsync(token);
        if (!SameRoot(root, document.Workspace)) return new("work_workspace_mismatch");
        var error = await rules.EnsureInitializedAsync(token);
        if (error is not ("none" or "no_change")) return new(error);
        if (document.ScopeHint != ".")
        {
            var selection = SafeDisplay("[歷史 Scope 提示]\nScope：" + document.ScopeHint + "\n" + Prompt(WorkApprovalKind.Scope));
            var chosen = await confirm(new(WorkApprovalKind.Scope, selection), token);
            if (chosen != WorkApprovalDecision.Approved) return DecisionResult(chosen);
            error = await rules.TryChangeScopeAsync(document.ScopeHint, token);
            if (error is not ("none" or "no_change")) return new(error);
        }
        await RequireRulesAsync(token); var stamp = contexts.Capture(); var rulesStamp = rules.Capture();
        var observed = await references.ReadWorkReferencesAsync(Paths(document.Plan), token);
        if (!SameRoot(root, observed.Root)) return new("work_revision_conflict");
        var changedRules = !document.RulesRefs.Select(r => (r.Path, r.Sha256)).SequenceEqual(rulesStamp.Snapshot!.Sources.Select(r => (r.Path, r.Sha256)));
        var differences = new StringBuilder(changedRules ? "規則已變動，舊記錄不代表符合目前規則。\n" : "目前規則來源與保存參照相符；仍不恢復規則身分或授權。\n");
        foreach (var old in document.FileRefs)
        {
            var now = observed.References.Single(r => r.Path == old.Path);
            var status = now.Observation is "unavailable" or "outside_scope" || old.Observation is "unavailable" or "outside_scope" ? "unverified" :
                now.Observation == "missing" ? "missing" : old.Observation == now.Observation && old.ByteCount == now.ByteCount && old.Sha256 == now.Sha256 ? "unchanged" : "changed";
            differences.Append(old.Path).Append("：").Append(status).Append("（目前 ").Append(now.Observation).Append("）\n");
        }
        WorkMemoryCandidate candidate;
        try { candidate = contexts.PrepareWorkImport(document); }
        catch (ContextCapacityException) { return new("work_capacity"); }
        var pending = candidate.Plan.Plan is { } plan ? PlanProjection.Display(AgentMode.Plan, plan, 0) : "沒有未完成任務；不建立活動計畫。\n";
        var preview = Preview(document, WorkSaveCodec.Write(document, _policy), WorkApprovalKind.Resume,
            "歷史 basis／Completed 皆為未驗證轉述，不建立目前完成事實。摘要與備註只是歷史背景。\n" +
            differences + "預計重建的待辦：\n" + pending + "目前工作區已匹配；目前 Scope：" + rulesStamp.Scope +
            "。未核對範圍為上列 unverified；不執行、不恢復檔案、無有效核准。載入後進入 Plan。\n");
        var decision = await confirm(preview, token);
        if (decision != WorkApprovalDecision.Approved) return DecisionResult(decision);
        var reread = await store.ReadAsync(id, token);
        if (!file.Matches(reread)) return new("work_revision_conflict");
        await RequireCurrentAsync(stamp, token);
        var final = await references.ReadWorkReferencesAsync(Paths(document.Plan), token);
        if (!observed.Same(final)) return new("work_revision_conflict");
        await RequireCurrentAsync(stamp, token);
        if (!contexts.TryPublishWorkImport(candidate, token)) return new("work_revision_conflict");
        _resumeExecutionConfirmationRequired = true;
        return new("none", "已載入未驗證歷史背景並進入 Plan；等待新指示，/execute 仍須獨立確認。", workId: id);
    });

    internal void Clear() { _resumeExecutionConfirmationRequired = false; _confirmedRecoveryRevision = plans.Recovery.Revision; }
    internal void ConfirmedExecute() { _resumeExecutionConfirmationRequired = false; _confirmedRecoveryRevision = plans.Recovery.Revision; }
    internal Task<WorkOperationResult> ConfirmExecuteAsync(Func<WorkApprovalPreview, CancellationToken, ValueTask<WorkApprovalDecision>> confirm, CancellationToken token) => RunAsync(async () =>
    {
        await RequireRulesAsync(token); var stamp = contexts.Capture();
        var preview = new WorkApprovalPreview(WorkApprovalKind.Execute, SafeDisplay("[執行模式確認]\n目前沒有活動計畫；續接或復原前的完成狀態僅供歷史參考，須重讀目前來源並重新驗證。切換後仍須新訊息，每次寫入／程序另行核准。\n" + Prompt(WorkApprovalKind.Execute)));
        var decision = await confirm(preview, token);
        if (decision != WorkApprovalDecision.Approved) return DecisionResult(decision);
        await RequireCurrentAsync(stamp, token);
        lock (contexts.DecisionGate)
        {
            if (token.IsCancellationRequested || !contexts.IsCurrent(stamp)) return new("work_revision_conflict");
            plans.SetMode(AgentMode.Execute); ConfirmedExecute();
        }
        return new("none", "已切換 Execute；等待下一則新訊息，尚未執行任何操作。");
    });
    private async Task<WorkOperationResult> RunAsync(Func<Task<WorkOperationResult>> action)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return new("store_busy");
        try
        {
            if (store.Stopped) return new("store_unavailable");
            return await action();
        }
        catch (OperationCanceledException) { return new("work_cancelled", decision: WorkApprovalDecision.Cancelled); }
        catch (WorkDocumentException ex) { return new(ex.Code); }
        catch (WorkStoreException ex) { return new(ex.Code); }
        catch (ContextCapacityException) { return new("work_capacity"); }
        catch (Exception) { return new("store_unavailable", fatal: true); }
        finally { Volatile.Write(ref _running, 0); }
    }
    private async Task RequireRulesAsync(CancellationToken token)
    {
        var error = await rules.ValidateAsync(token); if (error != "none") throw new WorkDocumentException(error);
        try
        {
            var skillsError = plans.Skills is { } skills ? await skills.ValidateAsync(token) : "skills_unavailable";
            if (skillsError != "none") throw new WorkDocumentException(skillsError);
        }
        catch (SkillsSensitiveException) { throw new ContextInspectionException(); }
    }
    private async Task RequireCurrentAsync(ContextOperationStamp stamp, CancellationToken token)
    { await RequireRulesAsync(token); token.ThrowIfCancellationRequested(); if (!contexts.IsCurrent(stamp)) throw new WorkDocumentException("work_revision_conflict"); }
    private void RequireSafe(string text) { if (!_policy.IsSafe(text)) throw new WorkDocumentException("work_sensitive_content"); }
    private bool SafeCurrentPlan(WorkSavedPlan plan) => _policy.IsSafe(plan.Objective) && plan.Tasks.All(task =>
        _policy.IsSafe(task.Title) && _policy.IsSafe(task.Acceptance) && (task.Summary is null || _policy.IsSafe(task.Summary)) &&
        task.Requirements.All(requirement => _policy.IsSafe(requirement.Path)));
    private string SafeDisplay(string text)
    {
        if (new UTF8Encoding(false, true).GetByteCount(text) > 65536) throw new WorkDocumentException("work_capacity");
        RequireSafe(text); return text;
    }
    private WorkApprovalPreview Preview(WorkSaveDocument document, byte[] bytes, WorkApprovalKind kind, string explanation) =>
        new(kind, SafeDisplay("[" + (kind == WorkApprovalKind.Save ? "尚未保存的候選" : "待續接的未驗證歷史工作") + "]\n" + explanation +
            "完整保存資料：\n" + Encoding.UTF8.GetString(bytes) + "\n" + Prompt(kind)));
    internal static string Prompt(WorkApprovalKind kind) => kind switch
    {
        WorkApprovalKind.Save => "保存此工作摘要？[y/N] ",
        WorkApprovalKind.Delete => "刪除此單一工作存檔？[y/N] ",
        WorkApprovalKind.Resume => "載入此工作並進入 Plan？[y/N] ",
        WorkApprovalKind.Scope => "選擇此 Scope？[y/N] ",
        _ => "切換 Execute 並等待新指示？[y/N] "
    };
    private static string UsageText(AgentTurnResult? generation)
    {
        if (generation is null) return "傳輸嘗試：0；API 用量：不可得。\n";
        var usage = generation.TurnUsage;
        static string Part(string label, UsageFieldSnapshot value) => label + "=" + (value.HasKnownValue ? value.KnownTotal.ToString(CultureInfo.InvariantCulture) : "不可得") + "（缺值次數 " + value.MissingRequestCount + (value.Overflowed ? "；溢位" : "") + "）";
        return "傳輸嘗試：" + usage.DispatchedRequests + "；" + Part("Prompt", usage.Prompt) + "；" + Part("Output", usage.Output) + "；" +
            Part("Thought", usage.Thought) + "；" + Part("Cached", usage.Cached) + "；" + Part("Total", usage.Total) + "。非正式帳單。\n";
    }
    private static string[] Paths(WorkSavedPlan? plan) => plan?.Tasks.SelectMany(t => t.Requirements).Select(r => r.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
    private static bool SameRoot(WorkWorkspaceIdentity a, WorkWorkspaceIdentity b) => a.Root == b.Root && a.Device == b.Device && a.Identity == b.Identity;
    private static WorkOperationResult DecisionResult(WorkApprovalDecision decision, AgentTurnResult? generation = null) => new(
        decision == WorkApprovalDecision.Declined ? "approval_declined" : "work_cancelled", generation: generation, decision: decision,
        fatal: !Enum.IsDefined(decision) || decision == WorkApprovalDecision.Unavailable);
    private static string GenerationError(AgentTurnResult result) => result.StopReason switch
    {
        AgentStopReason.ProgramGenerationLimit => "program_generation_limit",
        AgentStopReason.GenerationLimit => "generation_limit",
        AgentStopReason.ContextCapacity => "context_measure_unavailable",
        AgentStopReason.TurnCancelled => "work_cancelled",
        AgentStopReason.SensitiveData or AgentStopReason.InvalidProtocol => "work_sensitive_content",
        AgentStopReason.SkillsChanged => "skills_changed",
        AgentStopReason.SkillsUnavailable => "skills_unavailable",
        AgentStopReason.SkillsLimit => "skills_limit",
        AgentStopReason.RulesChanged => "rules_changed",
        AgentStopReason.RulesUnavailable => "rules_unavailable",
        AgentStopReason.RulesScopeMismatch => "rules_scope_mismatch",
        AgentStopReason.RulesLimit => "rules_limit",
        AgentStopReason.ContextCompactionFailed => "work_revision_conflict",
        _ => "work_summary_failed"
    };
    public override string ToString() => "[WorkSessionCoordinator]";
}