# AI Game Development Best Practices for Car Survivors

## Goal

Use AI to speed up delivery while preserving game feel, balance, and architecture quality.

## Core Principles

1. Keep humans responsible for final design decisions.
2. Prefer deterministic gameplay logic over vague behavior.
3. Make AI-generated changes small, reviewable, and testable.
4. Preserve architecture constraints already established in this project.

## Project-Specific Guardrails

1. Do not reintroduce singleton access where DI interfaces exist.
2. Keep gameplay event sequencing intact when changing combat, wave, or spawner logic.
3. Preserve damage, health, and status effect semantics unless intentionally rebalanced.
4. Keep inspector-driven workflows usable for designers.
5. Reuse existing interfaces and systems before creating new abstractions.

## High-Value AI Use Cases

### 1. Content and Data Authoring

- Generate enemy wave configurations and spawner parameters.
- Propose weapon/skill upgrades and projectile balance profiles.
- Draft status effect variants and damage curve presets.

Review checklist:

1. Are parameters normalized and intentional?
2. Are skill targets and layers configured correctly?
3. Does the configuration avoid degenerate balance spikes or unkillable states?

### 2. Boilerplate and Integration Code

- Scaffold new enemy types and movement behaviors utilizing the FlowField system.
- Draft ScriptableObject data classes and editor inspectors.
- Produce adapter code that binds to existing systems via interfaces.

Review checklist:

1. Uses existing interface contracts.
2. Avoids duplicate logic that already exists elsewhere.
3. Handles nulls and missing optional components safely.

### 3. Test Case Drafting

- Generate scenario tables for enemy spawning, wave progression, and combat resolution.
- Propose edge-case tests for projectile collisions, damage numbers, and status effects.
- Draft regression tests for score tracking, leveling, and death flow sequencing.

Review checklist:

1. Includes normal, edge, and failure-path cases.
2. Verifies event order, not just final values.
3. Protects against previous regressions.

## Practices for Reliable AI-Assisted Coding

### Scope Before Prompting

1. Define one task per prompt.
2. Include exact target files and expected behavior changes.
3. State constraints explicitly (DI only, zero GC allocations in update loops, inspector compatibility).

### Require Explainable Changes

1. Ask for why each changed block is necessary.
2. Require explicit assumptions.
3. Require a short risk list and regression list.

### Keep Diffs Small

1. One mechanic or one subsystem per change.
2. Avoid broad refactors mixed with gameplay tuning.
3. Prefer additive changes with backward compatibility when possible.

### Validate in Layers

1. Compile validation (`dotnet build`).
2. Unit or scenario checks for logic correctness.
3. In-editor play validation for vehicle feel, camera responsiveness, and UX.
4. Balance sanity check with a small metric set.

## Gameplay Quality Controls

### Determinism and State Safety

1. Ensure combat state changes happen in predictable lifecycle phases.
2. Avoid hidden side effects in event handlers.
3. Ensure actions and pooled resets are idempotent.

### Balance and Tuning

1. Keep AI-generated values as starting points, not final balance.
2. Tune with bounded ranges and compare against baseline enemy waves.
3. Track time-to-kill, swarm density, and player survivability.

### Readability and Maintainability

1. Prefer descriptive names over clever compact code.
2. Keep methods short around combat or wave state transitions.
3. Add concise comments only for non-obvious intent.

## Common Failure Modes and Mitigations

1. Failure mode: Architecture drift toward direct references.
   Mitigation: Enforce interface-based dependencies and installer bindings.
2. Failure mode: Wave/combat sequence regressions.
   Mitigation: Add event-order checks for wave start, wave end, unit death, and score updates.
3. Failure mode: Hidden balance spikes.
   Mitigation: Cap parameters and compare against known baseline waves.
4. Failure mode: Designer-unfriendly workflows.
   Mitigation: Keep key values inspector-exposed with clear defaults.

## Suggested Prompt Pattern for This Repo

1. Context: Mention the exact system and target files.
2. Constraints: DI via interfaces, no singleton reintroduction, zero GC allocations in hot paths.
3. Requested output: Minimal diff plus reasoning and test plan.
4. Validation ask: Compile check, regression risks, and manual test steps.

## Pull Request Review Checklist (AI-Related)

1. Architecture: DI and interfaces respected.
2. Mechanics: Combat flow, wave transitions, and damage logic still correct.
3. Data: Inspector setup remains clear and editable.
4. Testing: Includes edge cases and regression checks.
5. Risk: Notes assumptions and unresolved uncertainties.

## When to Ask the User Instead of Guessing

1. Ambiguous mechanic intent (vehicle handling, weapon firing behavior).
2. Conflicting design goals (difficulty vs swarm density vs frame rate).
3. Missing constraints for balancing or visual feedback.
4. Any change that might alter player-facing combat semantics.
