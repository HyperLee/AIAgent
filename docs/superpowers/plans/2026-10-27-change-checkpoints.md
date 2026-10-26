# Change Checkpoints and Recovery Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for integration and superpowers:dispatching-parallel-agents for the independent Git guard, state integration and terminal surfaces. Track progress here. The supplied specification and explicit implementation request authorize the selected design.

**Goal:** Implement FUT-09 according to specs/16-change-checkpoints-and-recovery.md.

**Architecture:** Capture independent, bounded before/after bytes inside controlled writes. Restore only through the local ID-based workspace entrance under the existing operation lease, safe publishing and fresh approval. Git metadata guards, recovery/evidence revisions, mandatory context projection and terminal commands have separate owners.

**Tech Stack:** Existing .NET 10, Google.GenAI, MSTest/MTP and macOS filesystem/process isolation. No new packages.

**Spec:** specs/16-change-checkpoints-and-recovery.md (all product requirements CKPT-003–076; CKPT-001/002/077–080 describe the prior document-only delivery).

## Global Constraints

- MaxCheckpointCount=32; MaxCheckpointBytes=8388608; MaxCheckpointSideBytes=131072; MaxCheckpointDisplayBytes=65536; MaxCheckpointAppendixBytes=32768.
- Internal sealed defensive contracts; unchanged public LLM/tool input/success schemas; no new model tools, credentials or configuration.
- Independent in-memory source bytes, no Git/index writes, deletion, redo, force, persistent backups or extra model generation.
- Existing workspace lease, current rules/scope, sensitive checks, exact bytes/identity/mode/parents, native atomic publishing and post-publication facts remain mandatory.
- Preserve CRLF/no-final-newline C# style; no recursive or bulk deletion. Work in the user-selected checkout; do not commit or push.

## Review Focus

- Same bytes with a changed inode, mode, parent or Git tracking must refuse restoration.
- Publication followed by cancellation, rules changes, clear or cleanup failure must preserve known facts without reviving cleared data.
- A forgotten middle link must prevent earlier restoration; consuming a restored record must preserve a proven predecessor anchor.
- Every model request, summary and measure after recovery must carry the mandatory bounded appendix and version.
- Approval preinput, malformed IDs, sensitive fixed/dynamic fields and busy commands must never authorize publication.

### Task 1: Checkpoint contracts, bounded store and controlled-write capture

Files: new Workspace/CheckpointContracts.cs, CheckpointStore.cs, WorkspaceCheckpoints.cs; modify WorkspaceFiles.cs, WorkspaceFileWrites.cs, WorkspaceContracts.cs, Tool error projections; tests CheckpointWorkspaceTests.cs.

Interfaces: ICheckpointWorkspace ListAsync/ShowAsync/RestoreAsync/ForgetAsync; CheckpointOperationResult with error/display/write facts; CheckpointApprovalPreview and ICheckpointApproval; immutable history and separate mutable anchors.

- [x] Add and observe failing store/capture/restore tests (including count/bytes boundaries, NoChange, rejected approval and chains).
- [x] Implement atomic reservation, ownership, monotonic ID/store/lifetime, manual Create/Unconfirmed and exact original source capture.
- [x] Implement fresh restore candidate, three source/guard checks, native staged publication/readback/cleanup, chain transfer, forget and invalidation.
- [x] Verify focused tests and tool-chain resource-limit behavior.

### Task 2: Target-only Git guard

Files: new Workspace/WorkspaceCheckpointGit.cs and Git/CheckpointGitGuard.cs; modify GitReadAdapter admission only if needed; new CheckpointGitTests.cs.

Interfaces: private CaptureCheckpointGitGuardAsync(path, operation, token) returns stable CheckpointGitGuard or throws a fixed GitFailure; constructor-injected optional IGitReadAdapter for offline tests. Guard equality covers admitted root/gitdir/executable/head/object-format and exact target metadata.

- [x] Add failing absent-root and fake-adapter metadata guard tests.
- [x] Implement two complete bounded observations using the existing lease and fixed recipes; literal target-only manifests/ITA, no blobs/global index digest.
- [x] Verify conflicts, manual unsupported layouts, cancellation and strict cleanup/sensitive failures.

### Task 3: Recovery facts, evidence epoch and mandatory context

Files: Core/CheckpointRecoveryState.cs; modify PlanStore/Contracts/Projection, ContextCoordinator/Contracts, AgentContext/AgentWorkSummary and WorkSessionCoordinator; tests CheckpointStateTests.cs.

Interfaces: AgentExecutionPolicy.Recovery tied to Plans; Recovery.CanPublish, Recovery.Revision and Recovery.Record(id,path,WorkspaceWriteResult,lifetimeCurrent); Plans.EvidenceEpoch. Record after publication/cleanup, before rules settle. Clear retains monotonic revision/epoch while discarding the last result.

- [x] Add failing old-fact exclusion, retained completed history, mandatory appendix and stale-candidate tests.
- [x] Implement epoch reservation/fact selection and local recovery transition to Plan/workspace_restored.
- [x] Add recovery appendix to all conversation/review/summary/save requests and complete metrics/body checks; include revision in stamps.
- [x] Verify clear/resume/save schema boundaries and original regression tests.

### Task 4: Local commands, fresh hidden confirmation and terminal verification

Files: Core/CheckpointCommands.cs, ChatCheckpointInteraction.cs; modify ChatApplication/Contracts/InputValidation and Console interaction/display; new CheckpointChatTests.cs and PTY scenarios.

Interfaces: workspace.Checkpoints default-null compatibility; direct operation-local ICheckpointApproval bound by active ChatApplication and current operation. Parse exact /checkpoint list|show cN|restore cN|forget cN (long positive canonical IDs).

- [x] Add failing parser, no-model, Plan gate and approval tests.
- [x] Implement commands with existing busy-reader and hidden single-line approval boundary, safe results and lifecycle clearing.
- [x] Add terminal fixture/PTY coverage for new prompts and local flows.
- [x] Verify focused chat and PTY cases.

### Task 5: Integration, documentation and acceptance

- [x] Review all requirements and independent changes; fix integration defects with focused regressions.
- [x] Synchronize README, SPEC, ROADMAP and upstream product continuation notes while preserving historical scope.
- [x] Sequential restore/build/MSTest/help; Python scanner tests; macOS PTY; format verify and git diff --check. Record actual evidence separately from human/Gemini/platform/Release gaps.
- [x] Perform independent code review and resolve material findings; leave changes uncommitted for review.

## Progress

- 2026-10-27: Specification and relevant ownership read; clean main at 7a9925f. Baseline verification running. The explicit implementation request supersedes the specification's original document-only task limits; product constraints remain binding.
- Baseline: restore/build succeeded; restricted run 825/838 passed, 13 preexisting native sandbox failures (`sandbox_apply` denied). Native testing will be repeated outside the outer execution sandbox without weakening product isolation.
- Task 1: contracts/store/capture/restore/forget/chain/lifecycle implemented; first interface test RED (missing property), initial 14 workspace tests GREEN. Expanded native fault, rules, rekey and resource-limit regressions pending final run.
- Task 2: target-only guard implemented; fake-adapter RED→GREEN cycle in progress report `/private/tmp/aiagent-ckpt-git-report.md`; real Git isolated fixtures running under approved elevated test execution.
- Task 3: state implementation complete; 16/16 dedicated cases GREEN. See `/private/tmp/aiagent-ckpt-state-report.md`.
- Task 4: CLI implementation complete; 12/12 dedicated cases and 10/10 focused PTY groups GREEN. Invalid-ID/overflow error classification refinement pending. See `/private/tmp/aiagent-ckpt-cli-report.md`.
- Integration ruling: postpublication guard settlement skips rule-source validation only after a verified owned write; retains lease/root/metadata/sensitive gates, then completes checkpoint before AGENTS Stale. This prevents the application's own rules write from losing its checkpoint.
- Integration ruling: ID sequence belongs to persistent PlanStore/application, so workspace recreation on new Key cannot reuse IDs. Store revisions/lifetime and candidates remain bound to their owning service.
- Capacity ruling resolved: production uses an optional internal exact SDK serialization preflight stopped before HTTP dispatch or budget use. Actual 262144 accepted / 262145 zero generation, with 9 dedicated cases; no conservative near-limit limitation in the production inspector. Public contracts unchanged.
- Task 5: README/SPEC/ROADMAP and upstream continuation notes added. Independent read-only review dispatched; full final verification and result counts pending. No commits, pushes, real Key or Gemini calls.

- Final integration: all three independent P2 findings reproduced RED and fixed GREEN; root also fixed late source approval after checkpoint clear and adapter cancellation classification. No deferred material finding remains; new capacity path independently reviewed with no material finding.
- Verification: full944/944 + supplementary3 offline +1 native end-to-end case passed with unchanged product source (948 distinct cases). Python36/36, PTY122/122, help/build/restore/format passed. Native dirty-before restore preserves exact BOM/CRLF/Unicode and index/HEAD bytes. Source307-file staged fixture scan0findings. Human/Gemini/other platforms/Release and product MTP-test gap remain separate.
- Final delivery is the uncommitted checkout; no recursive/bulk deletion, original-index staging, commits, pushes or real credentials/service calls.
