---
name: document-system
description: "Use when: creating or updating agent-facing technical documentation for a specific Car Survivors gameplay system under .agents/context/game-systems/, such as Car Controller, FlowField Navigation, Waves/Spawners, Skills, Health, or UI flow. Route explicit human-facing deliverables through create-user-doc."
---

# Document System Skill

Use this skill to create clear, implementation-grounded documentation for a pointed system.

## Inputs

- Target system name (example: FlowField Navigation System, Car Controller, Wave Spawner).
- Scope boundaries and key files.
- Agent-facing operational purpose. Route an explicit human-document request to create-user-doc and its index workflow; do not create .user-docs/ during ordinary system documentation work.
- Required sections or templates.

## Workflow

1. Read AGENTS.md, .agents/README.md, coding standards, relevant ADRs, and game-system documents. Consult official sources through .agents/context/technology-documentation.md for framework claims. Search agent documentation to determine whether this system is already documented; do not read .user-docs/ as operational truth.
2. If existing documentation is found:
   - Review it against the current state of the project (source code, systems).
   - Update the existing documentation to make it up-to-date and accurate.
3. If no existing documentation is found:
   - Identify source files and authoritative docs for the target system.
   - Extract behavior, data flow, extension points, and invariants.
   - Write concise new documentation under .agents/context/game-systems/ focused on architecture and practical usage.
4. For both updates and new documentation, ensure you:
   - Add references to related systems and ownership boundaries.
   - Include known risks, assumptions, and open questions.
   - Verify concrete claims in source and distinguish current implementation from planned behavior. Record editor-dependent wiring and gameplay checks as unverified unless performed.

## Output

Produce a filled system document based on:

- .agents/skills/document-system/templates/system-document-template.md
