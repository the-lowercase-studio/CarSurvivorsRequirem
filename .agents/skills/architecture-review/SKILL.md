---
name: architecture-review
description: "Use when: reviewing a system change for DI correctness, turn-flow safety, ownership boundaries, and architecture drift risks."
---

# Architecture Review Skill

Use this skill to audit a proposed or implemented system change against ProjectLizard architecture constraints.

## Inputs

- Target system or feature name.
- Changed files or planned touch points.
- Intended behavior change.

## Workflow

1. Identify impacted domains and owners.
2. Check dependency direction and interface usage.
3. Validate turn-flow and combat invariants.
4. Flag ownership conflicts and integration risks.
5. Produce a concise action list for remediation.

## Output

Produce a filled review based on:

- .agents/skills/architecture-review/templates/architecture-review-checklist.md
