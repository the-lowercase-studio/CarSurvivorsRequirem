# Batch System Docs Update Handoff

Date: YYYY-MM-DD

## Run Context

- Run key:
- Durable plan path:
- Coordinator:
- Current execution mode and worker capacity:
- Last checkpoint:
- Workspace changes since the previous checkpoint:

## Batch State

The coordinator owns this table of active batches. Workers return their results or write only their own report. Status values: PENDING, IN_PROGRESS, REVIEW, DONE, BLOCKED. DONE requires coordinator acceptance, including accepted no-change reviews. Move superseded allocations to the history below and preserve exactly-once coverage in the active allocation.

| Batch ID | Systems | Writable Documents | Worker / Session | Status | Report Path | Coordinator Verification | Open Issues / Next Action |
| --- | --- | --- | --- | --- | --- | --- | --- |

## Worker Report Contract

For each completed or interrupted batch, report:

- Batch ID and assigned systems.
- Documents changed and documents verified as already current.
- Source files and integration paths inspected; coverage gaps.
- Observed source state and relevant concurrent changes requiring re-verification.
- Corrected stale claims, with supporting source paths and symbols.
- Path/reference and document checks performed.
- Unresolved evidence, contradictory intent, and editor-dependent claims.
- Cross-batch corrections requested without editing another owner's documents.

## Repartitioning and Ownership Changes

- Batch or system affected:
- Reason and remaining work:
- Superseded allocation and replacement batch IDs:
- Previous owner's acknowledged release or cancellation of write ownership:
- Preserved partial edits and coverage reviewed before reassignment:
- New assignment and exclusive writable documents:

## Cross-Batch Reconciliation

| Shared Contract / Boundary | Affected Systems | Evidence and Resolution | Status |
| --- | --- | --- | --- |

## Resume Next

- First unfinished batch and its next action:
- Existing ownership and confirmed releases to check before redispatching:
- Previously accepted batches whose source changed and need re-verification:
- Blocked systems and their specific dependencies:

## Completion

- Selected systems all accepted exactly once in the current allocation, including no-change reviews:
- All active batch rows DONE and cross-batch review complete:
- Durable summary path:
- Remaining questions or manual follow-ups:

Report partial completion when a batch or required evidence remains unresolved. Keep this temporary state under .agents/context/tmp/; the durable plan and summary belong under .agents/context/implementations/.
