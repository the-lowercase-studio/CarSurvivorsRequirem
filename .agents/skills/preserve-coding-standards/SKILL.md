---
name: preserve-coding-standards
description: "Use when: auditing and fixing a provided Car Survivors scope for practices that compile or work but drift from .agents/context/project-coding-standards.md. Triggers: preserve coding standards, coding standards cleanup, style drift, naming/order cleanup, fix standards violations, align scope with Car Survivors standards."
---

# Preserve Coding Standards Skill

Use this skill to inspect a user-provided scope, identify code that drifts from Car Survivors coding standards, apply safe incremental fixes, and verify them with an automated compilation and self-correction loop.

## Required Sources

Always read these before editing:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md

Use .agents/context/technology-documentation.md and official Unity or Reflex documentation only when a standards fix depends on framework behavior.

## Scope Rules

1. Work strictly inside the user-provided scope: files, folders, classes, or systems.
2. If no scope is provided, infer the narrowest active or mentioned scope; otherwise ask for clarification.
3. Exclude generated and transient directories:
   - Library/
   - Temp/
   - Obj/
   - Logs/
   - Builds/
   - Packages/
   - UserSettings/
4. Never edit .prefab or .unity files directly unless explicitly requested.
5. Treat legacy style incrementally: fix touched and scoped violations without broadening into unrequested rewrites.

## Standards Checklist & Rules

Apply the core Car Survivors coding rules:

1. Field Ordering in MonoBehaviours and runtime classes:
   - `[Inject]` private fields first.
   - `[SerializeField] private` fields second.
   - Other private / protected fields third.
2. Naming Conventions:
   - Private and serialized fields: `_camelCase`.
   - Constants: `UPPER_SNAKE_CASE`, placed inside a `Constants/` subfolder under the owning domain.
   - Public properties, methods, types, and events: `PascalCase`.
   - Events: `OnX` naming.
   - Interfaces: prefixed with `I`, colocated above primary implementation when tightly owned.
3. Encapsulation:
   - Convert public mutable fields meant for inspector editing to `[SerializeField] private`.
4. Clean Diagnostics:
   - Remove temporary or noisy `Debug.Log` calls that are not intentional runtime diagnostics.

## Audit & Automated Self-Correction Loop

Follow this verified loop inspired by automated fix workflows:

1. Inventory Scope
   - Search candidate files with ripgrep (`rg --files <scope> -g '*.cs'`).
   - Read the relevant code context before modifying.

2. Classify & Apply Safe Fixes
   - Safe to fix now: field reordering, private field `_camelCase` renaming, constant relocation to `Constants/`, `[SerializeField] private` encapsulation, `OnX` event naming.
   - Needs user confirmation: renaming serialized fields that hold inspector data in prefabs/scenes, altering public API shapes, or changing DI lifetimes.

3. Automated Verification Gate (Mandatory)
   - Run targeted C# compilation:
     ```powershell
     dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
     ```
   - Treat warnings as errors: exit code must be 0 with 0 warnings.
   - If any compiler error or warning occurs, immediately analyze the diagnostic, apply a minimal correction, and re-run compilation until completely green.

4. Serialized Data Safety Check
   - Confirm that no serialized fields were renamed without explicit user approval or without verifying inspector safety.

## Output

After completing the audit and verification loop, report:

1. Audited Scope.
2. Files Modified.
3. Standards Violations Fixed (by rule category: Field Order, Naming, Constants, Visibility).
4. Items Intentionally Preserved / Deferred.
5. Automated Compilation Gate Result (`dotnet build` exit code and warnings count).
