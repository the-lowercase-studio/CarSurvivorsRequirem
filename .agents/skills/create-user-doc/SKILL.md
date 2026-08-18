---
name: create-user-doc
description: "Use when: creating or updating human-facing, user-oriented project documentation in .user-docs/ upon explicit user request (e.g., explaining gameplay concepts, systems, mechanics, architecture, or workflows in plain, intuitive language for human readers)."
---

# Create User Doc Skill

Use this skill when the user explicitly requests documentation intended for humans (game designers, developers, artists, or players) in the `.user-docs/` folder.

## Core Rules

1. **Explicit Request Only**: Do not create or update files in `.user-docs/` during normal feature development, bug fixes, or refactoring tasks unless the user explicitly requests a user-facing document.
2. **Grounding in Code**: Base all explanations on actual code (`Assets/Scripts/...`), ScriptableObjects, and `.agents/context/` to ensure technical accuracy.
3. **Human-Friendly Style**: Write in clear, accessible language. Focus on concepts, mental models, workflows, visual diagrams (Mermaid), and inspector configuration rather than raw code dumps.
4. **Target Location**: Place all generated documents in `.user-docs/<topic-name>.md` (kebab-case).
5. **Update Index**: Always update `.user-docs/README.md` to list the newly created or updated document.
6. **Isolation Guarantee**: Agents must not use `.user-docs/` as an operational source of truth for implementation logic. Technical decisions must rely on source code and `.agents/context/`.

## Inputs

- **Target Topic / System**: (e.g., FlowField Navigation, Car Health & Armor, Skill Upgrade System, Wave Director).
- **Target Audience**: (e.g., Game Designer configuring prefabs, New Team Member onboarding, Player/Community guide).
- **Scope & Focus**: Specific aspects the user wants explained (e.g., how to add a new weapon, how damage calculation works).

## Workflow

1. **Research the Codebase**:
   - Inspect authoritative runtime scripts in `Assets/Scripts/`.
   - Inspect relevant ScriptableObjects or prefab setups if applicable.
   - Review relevant `.agents/context/game-systems/` documentation for architectural context.

2. **Structure the Human Document**:
   - Follow the structure defined in `.agents/skills/create-user-doc/templates/user-doc-template.md`.
   - Include:
     - **Overview / What is this?**: Clear high-level explanation with analogies if helpful.
     - **Core Concepts**: Glossary and mental model breakdown.
     - **How It Works (Visual Flow)**: Mermaid diagram showing how data or gameplay events flow.
     - **Designer / User Guide**: Step-by-step instructions (e.g. how to tweak inspector values, create assets, or use the feature).
     - **Key Parameters & Balance**: Tables explaining serialized fields and balance knobs.
     - **FAQ / Troubleshooting**: Common questions and pitfalls.

3. **Write Document**:
   - Save to `.user-docs/<topic-name>.md`.

4. **Update Directory Index**:
   - Add entry to the table/list in `.user-docs/README.md`.

## Output

- A clean, well-formatted Markdown document in `.user-docs/<topic-name>.md`.
- Updated `.user-docs/README.md`.
