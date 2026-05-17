---
name: unity-refactor-suggestions
description: "Use when: suggesting behavior-safe refactoring for a selected Unity system, script, or code block in ProjectLizard. Triggers: refactor, cleanup, architecture review, C# best practices, Unity best practices, reduce complexity, code quality."
argument-hint: "Target (system/script/code block) + goal + constraints"
---

# Unity Refactor Suggestions Skill

Use this skill to produce focused, reviewable refactor suggestions for ProjectLizard while preserving gameplay semantics.

## Scope

Apply to one selected target at a time:

- A gameplay system (multi-file, example: turns, cards, enemies, shield)
- A single script
- A specific code block

If the request spans multiple systems, split into separate passes.

## Required Sources

Always ground suggestions in:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/technology-documentation.md

Use official Unity and Reflex documentation first when API behavior is uncertain.

## Inputs

- Target scope: system, script, or code block
- Exact target files and current pain points
- Desired outcome: readability, maintainability, safety, performance, testability
- Hard constraints (example: keep behavior identical, no public API break)

## Decision Flow

1. Classify the target.
   - System: map ownership boundaries, events, and DI edges before suggesting edits.
   - Script: audit class responsibilities, field ordering, naming, and dependency usage.
   - Code block: constrain changes to local behavior and nearby invariants.
2. Determine risk category.
   - Low risk: style, naming, extraction, dead code cleanup.
   - Medium risk: method decomposition, dependency reshaping without semantic change.
   - High risk: anything touching turn sequencing, damage resolution, intention selection, or inspector serialization.
3. Branch on mechanics impact.
   - If mechanics may change, stop and request explicit user confirmation before suggesting implementation edits.
   - If mechanics are preserved, proceed with behavior-safe refactor suggestions.

## Refactor Workflow

1. Define the invariant set before proposing changes.
   - Preserve DI via interfaces where established.
   - Do not reintroduce singleton access.
   - Preserve shield-first damage semantics.
   - Preserve turn-event sequencing.
   - Preserve inspector workflows and serialized compatibility.
2. Identify refactor opportunities (small diffs first).
   - Long methods around combat or turn transitions.
   - Mixed responsibilities in MonoBehaviours.
   - Hidden side effects in event handlers.
   - Duplicate logic that should be reused via existing interfaces/systems.
   - Naming/order issues violating coding standards.
3. Produce 3 tiers of suggestions.
   - Tier 1: quick wins (safe, local, low regression risk).
   - Tier 2: structural cleanup (moderate effort, still behavior-safe).
   - Tier 3: deferred items requiring design confirmation.
4. For each suggestion, include:
   - Why this change is needed.
   - Expected benefit.
   - Risk/regression notes.
   - Minimal validation steps.
5. Keep recommendations implementation-ready.
   - Prefer additive or incremental migration.

## C# and Unity Best-Practice Lens

Apply these checks while suggesting refactors:

- Keep MonoBehaviour classes thin and focused on orchestration.
- Extract pure logic to testable methods/classes when safe.
- Prefer explicit dependencies over runtime lookups.
- Keep required inspector references serialized and private.
- Use clear naming and short methods near state transitions.
- Subscribe and unsubscribe events in matching lifecycle methods.
- Avoid hidden allocations in hot paths when performance-sensitive code is touched.
- Preserve serialized field names or include migration guidance when renaming is unavoidable.

## Completion Criteria

A good output must:

1. Stay within the selected scope (system/script/code block).
2. Keep gameplay semantics deterministic unless explicitly approved to change.
3. Respect ProjectLizard coding standards for touched areas.
4. Present small, reviewable recommendations.
5. Include compile, logic, and manual play validation checks.

## Output

Produce a filled report in `.agents/context/refactor-suggestions/<report-name>.md` using:

- `.agents/skills/unity-refactor-suggestions/templates/refactor-suggestion-report.md`
