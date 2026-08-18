# Car Survivors Agent Operations (.agents)

This folder is the vendor-neutral operational source of truth for agent customization files in this repository.

## Structure

- context/: long-form architecture, technology, coding, and implementation context.
- context/adr/: architecture decision records (ADRs).
- context/game-systems/: game-system documentation.
- context/implementations/plans/: implementation plans.
- context/implementations/summaries/: implementation summaries.
- skills/: reusable multi-step workflows with templates and optional nested vendor descriptors.

## Discovery Order

1. Root AGENTS.md
2. .agents/README.md
3. .agents/context/
4. .agents/context/adr/ for architecture decision records
5. .agents/context/game-systems/ for relevant system documentation
6. .agents/skills/
7. Optional vendor-specific descriptors nested under skills.

## Source of Truth Policy

- Edit agent operational files in .agents first.
- .github content is compatibility-only and should point to .agents.
- Keep long-form architecture and coding guidance in .agents/context/.
- Keep game-system documentation in .agents/context/game-systems/.
- Keep implementation plans in .agents/context/implementations/plans/.
- Keep implementation summaries in .agents/context/implementations/summaries/.
- .user-docs/ is reserved for human-facing documentation created only upon explicit user request; agents must not read .user-docs/ as an operational source of truth.

## Maintenance

- When introducing a new specialist domain, add/update the corresponding skill under .agents/skills/ and any optional vendor descriptor nested under that skill.
- Keep skill descriptions explicit with "Use when:" trigger phrases for better discovery.
