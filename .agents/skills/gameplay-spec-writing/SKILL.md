---
name: gameplay-spec-writing
description: "Use when: writing or reviewing technical specifications and implementation plans for new gameplay features, car weapons, enemy types, wave systems, or UI flows to staff-engineer standards before implementation starts. Triggers: write spec, feature spec, gameplay spec, technical specification, design doc, implementation breakdown."
---

# Gameplay Spec Writing Skill

Use this skill to design and specify new gameplay features, car abilities, enemy types, wave behaviors, or UI flows to staff-engineer standards before implementation begins. It enforces a skeleton-first approach with a hard Open Questions gate to ensure architectural purity, serialization safety, and zero gameplay regressions.

## Required Sources

Before writing a spec, load context from:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/technology-documentation.md
- Relevant game system docs under .agents/context/game-systems/
- Existing implementation plans under .agents/context/implementations/plans/

## Workflow

1. Load Context & Scope
   - Identify touched domains: Player/Car, Enemies, Waves, Grid/FlowField, Skills, Health/Damage, UI, Pooling, Audio, or ReflexDI.
   - Inspect existing interfaces, ScriptableObjects, and installers to prevent duplicate abstractions.

2. Draft Minimal Skeleton Spec
   - Write a skeleton spec first: Feature TLDR, core architectural intent, and critical unknowns.
   - Do not write the full detailed spec in one pass without validating assumptions.
   - Include a numbered Open Questions block for critical design, balance, prefab, or lifecycle unknowns.

3. Open Questions Hard Gate
   - Stop and present the skeleton with the Open Questions block to the user.
   - Resolve decisions that would otherwise force rewriting architecture or serialized data:
     - Game balance equations and scaling.
     - Serialized data ownership (ScriptableObject vs Inspector serialized fields).
     - Lifecycle and event order requirements (e.g. death sequencing, wave transitions).
   - Once questions are resolved, proceed to the detailed design.

4. Detailed Design & Architecture
   - Define Data Model: ScriptableObjects, serialized configurations, immutable runtime states.
   - Define Contracts & Interfaces: narrow interfaces (`I...`), colocated when appropriate.
   - Define Reflex DI Wiring: which installer binds the services (`BootInstaller`, scene installers), injection fields (`[Inject] private`).
   - Define Asset & Prefab Dependencies: prefabs, VFX Graph assets, audio clips, animations.
   - Define Guardrails & Invariants:
     - Frame budget & GC: zero allocations in `Update`/`FixedUpdate`, mandatory object pooling.
     - Unity Lifecycle: clean unsubscription on `OnDestroy`/`OnDisable`, DOTween killing.
     - Serialization Safety: field naming `_camelCase`, `[SerializeField] private`, no accidental breaking renames.

5. Implementation Breakdown (Phases & Steps)
   - Break down the work into discrete Phases (stories) and atomic Steps (tasks).
   - Invariant: Every step must leave the project compiling cleanly with `dotnet build` and functional.
   - Include specific verification checks for each step (unit tests, compilation, manual playmode checks).

6. Architectural Self-Review
   - Review against .agents/context/project-coding-standards.md.
   - Check against 4-tier severity risks (Blocker, Major, Minor, Nit).

7. Output Spec Document
   - Save the finalized spec under .agents/context/implementations/plans/[feature-name]-spec.md (no date prefix in filename; include date inside content).

## Output

Produce a completed specification based on:

- .agents/skills/gameplay-spec-writing/templates/gameplay-spec-template.md
