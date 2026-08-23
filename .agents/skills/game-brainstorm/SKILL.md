---
name: game-brainstorm
description: "Use when: exploring gameplay ideas, new mechanics, combat balance changes, vehicle capabilities, weapon concepts, or architecture trade-offs before any code, spec, or asset is created. Triggers: brainstorm, game design idea, should we build this, let's think this through, weapon idea, mechanic concept."
---

# Game Brainstorm Skill

Use this skill for divergent gameplay, architecture, and design exploration before creating any technical spec, code change, or asset. It guides the conversation to question assumptions, evaluate gameplay feel, compare architectural options, and converge on a concrete handoff brief.

## Hard Gate (Read-Only)

During the brainstorm phase, do not edit runtime repository code, modify prefabs, alter ScriptableObjects, or begin implementation.
The only persistent artifact this skill produces is the final, detail-rich brainstorm summary saved to `.agents/context/brainstorming-summaries/[feature-name]-brainstorm-summary.md` when the user confirms the direction or concludes the session.

## Required Sources

Ground the exploration in the project context:

- AGENTS.md
- .agents/README.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/project-coding-standards.md
- .agents/context/project-scripts-folder-map.md
- Relevant game system docs under .agents/context/game-systems/
- Existing brainstorm summaries under .agents/context/brainstorming-summaries/

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

5. Produce & Save the Brainstorm Summary
   - When the brainstorm session concludes or is confirmed by the user, produce a comprehensive, detail-rich brief based on `.agents/skills/game-brainstorm/templates/brainstorm-brief-template.md`.
   - Save the completed summary directly in the repository at `.agents/context/brainstorming-summaries/[feature-name]-brainstorm-summary.md` (use kebab-case without date prefix in the filename; specify the date inside the document body).
   - Invariant: The summary must be **rich with technical and gameplay details** (concrete parameters, state transitions, architecture choices, Unity lifecycle/memory considerations, phase progressions, and exact target files rather than brief high-level summaries).

## Output

Produce and save a completed summary in `.agents/context/brainstorming-summaries/` following the template with:

1. Context & Motivation: Detailed feature description, player-facing goals, and exhaustive breakdown of impacted systems.
2. Explored Alternatives & Trade-offs: Detailed comparison of options with pros, cons, and architectural risks.
3. Unity & Architecture Considerations: Specific data authoring, navigation/physics handling, VFX/audio/tweens, lifecycle & zero-GC allocations, UI presenters, and state flows.
4. Key Decisions & Detailed Specifications: Deep-dive into combat/movement patterns, formulas, timing parameters, priority triggers, and phase structures.
5. Next Steps & Target Scope: Recommended next skill and explicit list of target scripts, prefabs, ScriptableObjects, and scene assets.
