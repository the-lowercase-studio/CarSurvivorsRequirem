---
name: unity-refactor-suggestions
description: "Use when: suggesting behavior-safe refactoring for a selected Unity system, script, or code block in Car Survivors. Triggers: refactor, cleanup, architecture review, C# best practices, Unity best practices, reduce complexity, code quality."
argument-hint: "Target (system/script/code block) + goal + constraints"
---

# Unity Refactor Suggestions Skill

Use this skill to produce focused, reviewable refactor suggestions for Car Survivors while preserving gameplay semantics, Unity inspector compatibility, and Reflex DI boundaries.

## Scope

Apply to one selected target at a time:

- A gameplay system (multi-file, example: Car Controller, FlowField Navigation, Waves/Spawners, Skills/Projectiles, Health/DamageNumbers)
- A single script
- A specific code block

If the request spans multiple systems, split into separate passes or coordinate via batch-codebase-review.

## Required Sources

Always ground suggestions in:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/technology-documentation.md
- Assets/Scripts/ReflexDI/

## Finding & Suggestion Severity Tiers

Classify all suggestions using the 4-tier scale:

- Blocker: Critical architectural, lifecycle, or compilation risks that must be addressed immediately.
- Major: Significant structural improvements (e.g. decoupling large MonoBehaviours, extracting services to Reflex DI, fixing unpooled high-frequency objects).
- Minor: Standards compliance (field order `[Inject]` -> `[SerializeField]` -> private, `_camelCase` naming, `Constants/` subfolder placement).
- Nit: Readability enhancements, micro-optimizations, and comment clarity.

## Decision Flow

1. Classify the Target
   - System: map ownership boundaries, events, and DI edges before suggesting edits.
   - Script: audit class responsibilities, field ordering, naming, and dependency usage.
   - Code block: constrain changes to local behavior and nearby invariants.

2. Audit Against Unity Breaking Change Matrix
   - Serialized Data: preserve serialized field names or explicitly note Inspector re-assignment requirements.
   - Reflex DI: ensure all extracted services have corresponding bindings in Assets/Scripts/ReflexDI/.
   - Lifecycle & Events: ensure event subscriptions unsubscribe in `OnDisable`/`OnDestroy` and DOTween sequences are killed.

3. Refactor Workflow
   - Define the invariant set before proposing changes (gameplay flow, DI interfaces, inspector workflows).
   - Identify refactor opportunities with smallest diffs first.
   - For each suggestion, provide:
     - Finding severity (Blocker / Major / Minor / Nit).
     - Rationale and expected benefit.
     - Exact code diff / replacement snippet.
     - Verification command: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.

4. Completion Criteria
   - Stays strictly within the selected scope.
   - Preserves deterministic gameplay semantics.
   - Produces clean compilation with zero warnings.

## Output

Produce a filled report based on:

- .agents/skills/unity-refactor-suggestions/templates/refactor-suggestion-report.md
