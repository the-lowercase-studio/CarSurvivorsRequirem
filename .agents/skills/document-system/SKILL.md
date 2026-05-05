---
name: document-system
description: "Use when: creating or updating technical documentation for a specific gameplay system, such as the Player Party system, cards, intentions, or combat flow."
---

# Document System Skill

Use this skill to create clear, implementation-grounded documentation for a pointed system.

## Inputs

- Target system name (example: Player Party system).
- Scope boundaries and key files.
- Audience (contributors, gameplay designers, or agents).
- Required sections or templates.

## Workflow

1. Search existing documentation to determine if this system or a similar one is already documented.
2. If existing documentation is found:
   - Review it against the current state of the project (source code, systems).
   - Update the existing documentation to make it up-to-date and accurate.
3. If no existing documentation is found:
   - Identify source files and authoritative docs for the target system.
   - Extract behavior, data flow, extension points, and invariants.
   - Write concise new documentation focused on architecture and practical usage.
4. For both updates and new documentation, ensure you:
   - Add references to related systems and ownership boundaries.
   - Include known risks, assumptions, and open questions.


## Output

Produce a filled system document based on:

- .agents/skills/document-system/templates/system-document-template.md
