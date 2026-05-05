# ProjectLizard Agent Operations (.agents)

This folder is the vendor-neutral operational source of truth for agent customization files in this repository.

## Structure

- docs/: long-form architecture, technology, system, and coding guidance.
- skills/: reusable multi-step workflows with templates and optional nested vendor descriptors.

## Discovery Order

1. Root AGENTS.md
2. .agents/README.md
3. .agents/docs/
4. .agents/skills/
5. Optional vendor-specific descriptors nested under skills.

## Source of Truth Policy

- Edit agent operational files in .agents first.
- .github content is compatibility-only and should point to .agents.
- Keep long-form architecture and coding guidance in .agents/docs/.

## Maintenance

- When introducing a new specialist domain, add/update the corresponding skill under .agents/skills/ and any optional vendor descriptor nested under that skill.
- Keep skill descriptions explicit with "Use when:" trigger phrases for better discovery.
