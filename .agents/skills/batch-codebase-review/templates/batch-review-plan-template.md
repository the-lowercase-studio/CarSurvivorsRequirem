# Batch Codebase Review Plan

Date: [YYYY-MM-DD]
Action mode: [Audit only | Authorized fixes]
Execution mode: [Delegated | Sequential | Prompt roadmap]
Durable plan: .agents/context/implementations/plans/[task-name]-plan.md
Authority and approved decisions: [User request / existing plan / unresolved requirements]

## Scope and Baseline

- Requested scope and exclusions:
- Pre-existing user changes:
- Baseline compilation: [Executed result | Pending | Not required for roadmap]
- Shared build owner and verified isolation, if any:
- Coordinator-owned shared interfaces/installers:

## Inventory and Ownership

Replace example groupings with the actual inventory. Assign each file once and reserve shared edits for the coordinator.

| Batch | Domain | Full project-relative scope paths | Actual files / size | Review owner | Writable files |
| --- | --- | --- | --- | --- | --- |
| [ID] | [Domain] | Assets/Scripts/[Domain]/ | [Count / lines] | [Owner] | [None for audit / explicit files] |

## Acceptance and Reporting

- Review coverage, authorized repair completion, and verification status are separate.
- Introduced diagnostics can be repaired only within authorized scope; report unrelated baseline failures.
- Coordinator serializes shared builds and reconciles cross-batch contracts and findings.
- Durable summary: .agents/context/implementations/summaries/[task-name]-summary.md
- Scratch handoff: .agents/context/tmp/batch_review_handoff.md
