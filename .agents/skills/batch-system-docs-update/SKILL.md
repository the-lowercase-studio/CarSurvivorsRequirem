---
name: batch-system-docs-update
description: "Use when: refreshing agent-facing documentation of multiple existing Car Survivors systems through size-aware batches and delegated updates. Triggers: batch system docs update, batch gameplay documentation update, batch gameplay spec update when referring to existing system documentation, multi-agent system documentation refresh."
---

# Batch System Docs Update

Refresh existing system documentation against current implementation. Inventory and size the systems first, partition them into balanced batches of one to three systems, then assign each batch to a worker. Coordinate document-system and agent-docs-review rather than duplicating their authoring guidance.

Use gameplay-spec-writing for new feature specifications and implementation plans. This workflow covers current system documentation under .agents/context/game-systems/.

## Required Sources

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/skills/document-system/SKILL.md
- .agents/skills/agent-docs-review/SKILL.md

Workers also read their assigned system documents, relevant ADRs, and source files. Consult .agents/context/technology-documentation.md and its official sources when a claim depends on Unity or package behavior.

## Scope and Authority

- Use the systems named by the user. For a project-wide refresh without a list, inventory existing documents under .agents/context/game-systems/ and map them to current code.
- Derive system boundaries from responsibilities and ownership, not just folders. A combined document can cover multiple tightly coupled areas; record the boundary explicitly. Do not merge unrelated systems or split one large system into artificial systems to satisfy the batch limit.
- Update existing canonical documents in place. Report undocumented systems discovered outside the requested scope; create additional system documents only when the request includes that work.
- Code and inspected authored configuration establish current behavior. Plans and brainstorms provide intent, not proof that a feature exists. Mark contradictions, missing evidence, and proposed behavior explicitly.
- Change assigned documentation only. Runtime code, scenes, assets, balance, serialized data, and editor workflows remain outside this task. Report defects for separate work rather than fixing them during documentation updates.
- Preserve user changes in a dirty worktree. Resolve overlapping in-progress edits before assigning that document, while proceeding with independent batches.

## Inventory and Effort Estimation

Before dispatching workers, inspect each target document and its primary source locations. Build a system inventory containing:

- Canonical document and primary source paths.
- First-party source file count and approximate code line count; identify overlapping source areas instead of silently double-counting them.
- Documentation size and visible drift, including renamed files or unsupported behavior claims.
- Integration burden: interfaces and DI wiring, event/lifecycle sequencing, serialized configuration, pooling, and shared consumers.
- Estimated documentation effort: Small, Medium, Large, or Very Large, with a short justification and confidence level.

Read representative entry points to check the estimates; file count alone is insufficient. Keep uncertain systems conservatively sized until their scope is understood. Choose and record a practical batch workload target for the current agent context and tool limits, such as relative effort units and a source-reading budget; these are planning estimates, not runtime performance measurements. Record the source state used for the inventory and recheck it before dispatch if concurrent edits occurred.

## Batch Partitioning

1. Allocate the largest systems first. Before isolating a Large or Very Large system, evaluate pairing it with one or two smaller related systems or placing it in a batch containing smaller systems.
2. Accept a combination only when its total estimated work fits the recorded workload target and leaves room to verify dependencies. Prefer pairings that share integration context. Record rejected pairings and the reason when assigning a large system alone.
3. Give a system a dedicated worker when the evaluated pairings would overload the batch. Do not force a batch to reach three systems.
4. Pack the remaining smaller systems into balanced batches, using integration affinity after workload fit.
5. Enforce a hard maximum of three distinct systems per batch and per delegated agent during the run. Give each delegated agent one batch; use a fresh worker for another batch instead of accumulating more systems in the same context. Follow-ups stay within that worker's assigned batch.
6. Assign every selected system exactly once. Give each writable document exactly one owner. Shared code may be read by multiple workers; reserve shared indexes and cross-batch edits for the coordinator.

If inspection reveals substantially greater effort, have the current owner stop and checkpoint its edits, then return unfinished systems to the pending queue and repartition. Obtain acknowledged release or cancellation of its write ownership; a paused or silent worker still owns the documents. Review preserved partial edits and coverage before assigning a replacement, and update both the plan and handoff. Move superseded allocations into handoff history with replacement batch IDs; keep only current allocations in the active batch table and preserve exactly-once system coverage. Keep a single extensive system with one owner who can inspect it in bounded passes; report incomplete coverage if it cannot be verified in the available context.

Save the durable execution plan under .agents/context/implementations/plans/ with a descriptive kebab-case filename and date inside the document. Use .agents/skills/batch-system-docs-update/templates/batch-docs-plan-template.md for inventory and ownership. Follow the project requirements gate: reuse existing user authorization, and resolve missing requirements when they affect scope. Routine documentation corrections do not require gameplay design decisions.

## Execution and Ownership

Use delegation capabilities supplied by the current environment; no vendor-specific API, model, plugin, or descriptor is required. Respect available concurrency, retaining coordinator capacity and queuing remaining batches.

When delegation is unavailable, perform the same batches sequentially with identical ownership, reporting, and verification. For external agent sessions, write self-contained batch prompts and accept their reported results through the same coordinator checks.

Create run-specific state under .agents/context/tmp/batch-system-docs-update/<run-key>/ using .agents/skills/batch-system-docs-update/templates/batch-docs-handoff-template.md. Keep all scratch data, worker prompts, and reports there. The coordinator owns the shared handoff state; workers return their report or write an exclusively owned <batch-id>-report.md.

Each worker assignment must include:

- Batch ID, one to three named systems, and estimated effort.
- Exact writable documents and primary source paths, plus relevant related documents available for reading.
- Required guidance paths, this skill, and the document-system and agent-docs-review workflows.
- Scope restrictions: edit only owned documents, keep paths relative and plain, write in English, and do not delegate the batch further or expand it independently.
- Expected report: changed documents, inspected source coverage and observed source state, corrected stale claims, verification performed, unresolved evidence, and requested cross-batch corrections.

Workers inspect implementation before correcting claims and retain concise agent guidance: reading maps, ownership and DI contracts, lifecycle/event order, extension points, configuration, invariants, risks, and meaningful validation steps. Avoid turning system docs into code transcripts. Record editor-dependent wiring as unverified unless inspected; do not claim gameplay or play-mode validation from source inspection alone.

## Checkpoints and Resumption

Track batches as PENDING, IN_PROGRESS, REVIEW, DONE, or BLOCKED:

- PENDING: partitioned and not started.
- IN_PROGRESS: an assigned worker is inspecting or updating owned documents.
- REVIEW: worker output is ready for coordinator verification.
- DONE: coordinator accepted the documents and report.
- BLOCKED: a named scope, ownership, or evidence dependency prevents completion; record the dependency and next action while continuing independent batches.

Record worker identity, writable documents, verified source coverage, open issues, and any repartitioning. On resumption, read the durable plan and handoff first, verify the workspace and worker state, and resume the first unfinished active batch. Do not redispatch documents until the previous owner's write ownership has been explicitly released or cancelled. Preserve accepted batches unless code or documents changed since verification.

## Coordinator Verification and Completion

Before accepting a batch:

1. Inspect its diff and report for scope compliance and preservation of user work.
2. Check referenced files, types, contracts, and methods against current source, especially corrected claims and critical lifecycle or DI statements. If relevant source changed during the review, reverify affected claims and sizing before acceptance.
3. Check path accuracy, English language, concise guidance, and explicit uncertainty. Require a worker follow-up when evidence or coverage is insufficient.

After workers finish, reconcile shared contracts, ownership boundaries, event sequences, and references across batches. The coordinator applies necessary corrections to shared documentation after worker ownership is released.

Documentation-only validation consists of source checks, paths/references, coverage, and diff review. Compilation and Unity play-mode execution are not completion requirements for this workflow. Describe any recommended editor follow-ups as pending checks.

Complete the run only when all selected systems have an accepted update or an accepted no-change review, each is covered exactly once by the current allocation, every active batch is DONE, and cross-batch verification is finished. If evidence or scope remains blocked, report the run as partial with the precise unresolved systems and continue unaffected work; do not mark missing coverage as verified.

Write the durable implementation summary under .agents/context/implementations/summaries/ using .agents/context/implementations/templates/summary-template.md. Report selected systems, actual batches, documents changed or already current, validation, unresolved questions, and manual follow-ups. Keep the temporary handoff separate from the durable plan and summary.

## Optional Vendor Metadata

.agents/skills/batch-system-docs-update/agents/openai.yaml supplies optional Codex UI metadata. SKILL.md and its templates define the portable workflow; other agents can use them without this descriptor.
