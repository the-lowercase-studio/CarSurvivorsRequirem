# Implementation Plan - [System Documentation Refresh Scope]

Date: YYYY-MM-DD

## Goal and Scope

- Requested systems:
- Existing documentation to refresh:
- Exclusions and undocumented systems discovered:
- Execution mode: delegated workers, sequential batches, or external agent prompts.

## User Review Required

- Existing user authorization:
- Missing requirements that affect scope, or None:

## Open Questions

- Scope or evidence questions, or None:

## System Inventory

Count first-party source files and approximate code lines. Explain overlaps and account for integration complexity when sizing documentation work.

| System | Canonical Document | Primary Source Paths | Source Files / Code Lines | Documentation Size / Drift | Integration Burden | Effort / Confidence / Reason |
| --- | --- | --- | --- | --- | --- | --- |

## Batch Workload Target

- Practical workload target and rationale for this environment:
- Available worker concurrency and coordinator capacity:
- Shared source areas and how overlap affects the estimates:
- Source state used for inventory and concurrent changes to recheck before dispatch:

## Batch Allocation and Write Ownership

Assign each selected system once. Each batch contains one to three systems, and each delegated agent owns one batch during the run. Each document has exactly one writer.

| Batch ID | Systems (Maximum 3) | Combined Effort / Fit | Pairing Rationale or Rejected Pairings | Writable Documents | Read-Only Integration Context | Owner |
| --- | --- | --- | --- | --- | --- | --- |

## Coordinator-Owned Shared Documents

- Shared indexes or other documents requiring final reconciliation:
- User edits or concurrent work requiring ownership coordination:

## Execution State

- Run key:
- Temporary handoff location under .agents/context/tmp/batch-system-docs-update/:
- Exclusive worker report locations in the same run directory:
- Durable summary location under .agents/context/implementations/summaries/:

## Verification Plan

- Worker source inspection and document review:
- Coordinator checks for paths, critical claims, and exclusive write scope:
- Cross-batch checks for ownership, contracts, event order, and references:
- Evidence gaps and any pending editor checks:

Documentation-only validation does not require compilation or play-mode execution. Mark unverified runtime or editor claims explicitly.
