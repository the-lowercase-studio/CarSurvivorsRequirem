# Gemini & Antigravity Agent Directives

## Purpose

This document defines high-priority operational rules and constraints specifically for Gemini and Antigravity agents operating in this repository. These rules take precedence over default IDE prompt behaviors.

## Implementation Lifecycle & In-Repo Storage Invariant

1. In-Repository Storage Mandate:
   - All brainstorming session summaries MUST be saved directly to the project repository under .agents/context/brainstorming-summaries/[feature-name]-brainstorm-summary.md.
   - All implementation plans MUST be saved directly to the project repository under .agents/context/implementations/plans/[feature-name]-plan.md (or [feature-name]-spec.md).
   - All implementation summaries MUST be saved directly to the project repository under .agents/context/implementations/summaries/[feature-name]-summary.md (or [feature-name].md).
   - NEVER save brainstorm summaries, implementation plans, or summaries exclusively to external IDE directories (e.g. brain/, AppData, /tmp, or user profile directories).

2. Antigravity UI Artifact Handling:
   - When Antigravity requests creating an artifact (e.g. implementation_plan.md or walkthrough.md in the brain artifact directory for UI display), the agent MUST ALWAYS write the primary authoritative markdown document to the corresponding path under .agents/context/implementations/ in the repository.
   - The repository file in .agents/context/implementations/ (or .agents/context/brainstorming-summaries/ for brainstorm sessions) is the single source of truth for version control.

3. Mandatory Implementation Lifecycle Flow:
   - Step 1 (Planning): For any non-trivial change, refactor, or new feature, create the implementation plan under .agents/context/implementations/plans/ before modifying source code. Use .agents/context/implementations/templates/plan-template.md as reference.
   - Step 2 (Execution): Execute the approved code changes according to project coding standards.
   - Step 3 (Verification & Summary): Verify compilation and gameplay safety, then create an implementation summary under .agents/context/implementations/summaries/ documenting changes made, validation performed, and manual check instructions. Use .agents/context/implementations/templates/summary-template.md as reference.

## Documentation Formatting Constraints

- Filenames for brainstorm summaries, plans, and summaries must use kebab-case without any date prefix (e.g. feature-name-brainstorm-summary.md or feature-name-plan.md). The date must be placed inside the document body.
- File and directory paths in agent documentation must be written relative to the project root in plain text without markdown links and without backticks (e.g. - Assets/Scripts/Player/Car/CarController.cs).
- Human-facing documentation belongs in .user-docs/ and must ONLY be created or modified upon explicit user request.
- English Language Invariant: All files created or modified in the repository (including code, comments, inspector metadata, plans, summaries, and templates) must be in English. Multilingual responses are restricted to chat communication with the user.
