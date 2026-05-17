---
name: check-optimalization
description: "Use when: checking current optimizations or performance risks for pointed ProjectLizard systems, scripts, methods, code parts, Unity components, combat flows, UI flows, allocations, update loops, events, DI usage, or data access before proposing and requesting approval to implement possible optimization changes. Also trigger for optimization/optimisation/optimalization review requests."
---

# Check Optimalization Skill

Use this skill to inspect a targeted ProjectLizard system, script, or code section for existing optimization choices, performance risks, and safe improvement opportunities before making changes.

## Required Sources

Before reviewing code, ground the work in:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/technology-documentation.md
- Relevant system summary under .agents/context/, if one exists.
- The exact target files, systems, or code excerpts named by the user.

Use official Unity, Reflex, and DOTween documentation from `.agents/context/technology-documentation.md` when performance behavior depends on engine or package details.

## Inputs

- Target system, script, method, scene behavior, or code excerpt to inspect.
- Performance concern, if the user named one.
- Target runtime context when available, such as combat turn flow, UI feedback, card usage, enemy intention execution, editor tooling, or scene startup.
- Whether the user wants a review only or is open to implementation after approval.

## Workflow

1. Identify the review boundary.
   - Confirm which files and runtime paths are in scope.
   - Follow call sites only far enough to understand performance behavior.
   - Do not broaden into unrelated refactors.
2. Establish current optimization state.
   - Note existing caching, pooling, event-driven behavior, DI boundaries, serialized references, precomputed data, allocation avoidance, and batching.
   - Identify intentional tradeoffs that should not be undone without evidence.
3. Inspect likely Unity performance risks.
   - Check frequent `Update`, `LateUpdate`, coroutine, tween, event, and UI rebuild paths.
   - Look for repeated scene searches, component lookups, LINQ/enumerator allocations, per-frame string formatting, unnecessary allocations, repeated list resizing, expensive logging, and repeated ScriptableObject/data traversal in hot paths.
   - Check object creation/destruction in combat feedback, VFX, damage numbers, cards, enemies, and UI.
4. Inspect architecture-sensitive optimization risks.
   - Do not propose singleton or global lookup shortcuts where DI interfaces already exist.
   - Preserve turn-event ordering, shield-first damage behavior, effect execution semantics, and inspector workflows.
   - Prefer narrow caching, pooling, data precomputation, or event-driven updates over broad rewrites.
5. Rank possible optimizations.
   - Label each item as `High`, `Medium`, or `Low` impact.
   - Label confidence as `Observed`, `Likely`, or `Needs profiling`.
   - Separate behavior-preserving changes from changes that could alter timing, ordering, balance, visuals, or designer workflow.
6. Ask before implementation.
   - After the review, present the proposed optimizations and explicitly ask the user which changes they approve.
   - Do not implement optimization changes in the same pass unless the user has already clearly approved that specific implementation work.
   - If an issue is a correctness bug rather than a pure optimization, call it out separately and still ask before changing behavior.
7. Implement only approved changes.
   - Keep edits scoped to approved items.
   - Preserve project coding standards.
   - Avoid prefab edits; any prefab setup must be left to the user in Unity Editor.
   - Add or update focused validation only when it fits the changed area.

## Output

For review-only work, produce:

- Target reviewed.
- Existing optimizations already present.
- Proposed optimizations ranked by impact and confidence.
- Risks or behavior-sensitive areas.
- Recommended validation steps.
- A direct approval question before implementation.

For approved implementation work, summarize:

- Approved optimizations implemented.
- Files changed.
- Behavior intentionally preserved.
- Validation performed.
- Remaining profiling or Unity Editor checks.

## Approval Prompt Pattern

End the review phase with a concrete question:

```text
I can implement these optimization changes next: [short list]. Do you approve all of them, or only specific items?
```

