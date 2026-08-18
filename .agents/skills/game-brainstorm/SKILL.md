---
name: game-brainstorm
description: "Use when: exploring gameplay ideas, new mechanics, combat balance changes, vehicle capabilities, weapon concepts, or architecture trade-offs before any code, spec, or asset is created. Triggers: brainstorm, game design idea, should we build this, let's think this through, weapon idea, mechanic concept."
---

# Game Brainstorm Skill

Use this skill for divergent gameplay, architecture, and design exploration before creating any technical spec, code change, or asset. It guides the conversation to question assumptions, evaluate gameplay feel, compare architectural options, and converge on a concrete handoff brief.

## Hard Gate (Read-Only)

During the brainstorm phase, do not edit repository files, create runtime scripts, modify prefabs, alter ScriptableObjects, or begin implementation.
The only file this skill produces is the final brainstorm brief when the user confirms the direction.

## Required Sources

Ground the exploration in the project context:

- AGENTS.md
- .agents/README.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/project-coding-standards.md
- .agents/context/project-scripts-folder-map.md
- Relevant game system docs under .agents/context/game-systems/

## Workflow

1. Frame the Topic
   - Restate the idea, question, or pain point clearly.
   - Classify the request: new vehicle mechanic, weapon/skill idea, enemy behavior, flow field/navigation update, balance adjustment, UI/HUD flow, or architecture simplification.
   - Read just enough of the codebase to speak concretely about existing systems and constraints (e.g. existing skills under Assets/Scripts/Skills/, pooling under Assets/Scripts/Pooling/, or Reflex installers under Assets/Scripts/ReflexDI/).

2. Explore Alternatives (Diverge)
   - Ask clarifying questions one at a time regarding motivation, player feel, game pacing, and constraints.
   - Always present at least two viable implementation alternatives plus the baseline option ("build nothing / keep simple / reuse existing system").
   - Compare alternatives across key Unity dimensions:
     - Player Feel & Juice: feedback, responsiveness, camera and sound integration.
     - Architecture & DI: ScriptableObject config vs runtime service vs component logic.
     - Performance & Allocations: update loops, physics costs, GC impact, object pooling.
     - Designer Usability: inspector tweakability vs hardcoded behavior.

3. Reality-Check Existing Systems
   - Verify if the feature overlaps with existing capabilities (e.g. CarController, FlowFieldSystem, Spawners, StatusEffects, DamageNumbers).
   - Check if an existing interface or ScriptableObject can be extended instead of building a separate parallel system.

4. Converge & Route (Decide)
   - Summarize the trade-offs and recommend the cleanest, most reversible path.
   - Clarify any non-obvious game balance or feel assumptions with the user.
   - Identify the appropriate next skill to execute the work:
     - gameplay-spec-writing: for complex mechanics requiring multi-step architecture.
     - unity-refactor-suggestions: for refactoring existing systems to accommodate the idea.
     - di-integration: for introducing new scene services or Reflex bindings.

5. Produce the Brainstorm Brief
   - Write a structured brief based on .agents/skills/game-brainstorm/templates/brainstorm-brief-template.md.
   - Store the brief under .agents/context/implementations/plans/ or present it directly in conversation.

## Output

Produce a completed brief following the template with:

1. Topic & Problem Statement.
2. Explored Alternatives & Trade-offs.
3. Selected Approach & Rationale.
4. Resolved Decisions & Remaining Open Questions.
5. Recommended Next Skill & Target Scope.
