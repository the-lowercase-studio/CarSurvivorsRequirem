# User & Human-Facing Documentation (.user-docs)

This folder contains project documentation for *Car Survivors* aimed directly at **humans** — developers, game designers, artists, and players.

## Purpose & Characteristics

- **Accessible language**: Translating complex mechanics and systems into intuitive concepts, diagrams, and analogies.
- **Practical context**: Explaining *why* something works the way it does and *how* to use it (e.g. Unity Inspector configuration, designing new skills, parameter balancing).
- **Visualizations**: Flow diagrams (Mermaid), parameter tables, and dependency graphs.

## Guidelines for AI Agents (Source-of-Truth & Creation Policy)

1. **Knowledge isolation**: AI agents **must not treat** files in this folder as an operational source of truth. Agents derive technical truth directly from source code (`Assets/Scripts/...`) and technical guidelines in `.agents/`.
2. **On-demand creation**: Files in `.user-docs/` are **not created automatically** during regular programming tasks. They are created or updated **only upon explicit user request** (e.g. using the `create-user-doc` skill).

## Available Documentation Index

| Document | Category | Description |
| :--- | :--- | :--- |
| [Agent Skills Guide](agent-skills-guide.md) | Tooling & Workflow | Comprehensive guide to 14 AI agent skills, trigger tables, workflows, and code safety gates. |

