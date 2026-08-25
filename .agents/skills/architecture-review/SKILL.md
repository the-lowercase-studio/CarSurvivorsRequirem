---
name: architecture-review
description: "Use when: reviewing a system change for DI correctness, gameplay-flow safety, ownership boundaries, and architecture drift risks."
---

# Architecture Review Skill

Use this skill to audit a proposed or implemented system change against Car Survivors architecture constraints, Reflex DI conventions, and Unity engine safety.

## Required Sources

Always read these before reviewing:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/technology-documentation.md
- Assets/Scripts/ReflexDI/

## Inputs

- Target system or feature name.
- Changed files or planned touch points.
- Intended behavior change.

## Severity Classification

Group every finding into one of four severity tiers:

- Blocker: Must be resolved before merge/commit.
  - Broken C# compilation or compiler warnings.
  - Unbound `[Inject]` interface or missing Reflex installer registration.
  - Reintroduction of `FindAnyObjectByType`, static mutable service state, or singleton shortcuts.
  - Unmanaged DOTween sequences creating memory leaks (missing `Kill()` on recycle/disable/destroy).
  - Unsafe serialized field changes breaking scenes or prefabs without a migration plan.
- Major: Serious architecture or lifecycle flaws that need resolution.
  - Inverted dependency direction (high-level domain depending directly on low-level UI/concrete presenter).
  - Missing object pooling for high-frequency spawns (projectiles, damage numbers, enemy VFX).
  - Event subscriptions without unsubscription in `OnDisable`/`OnDestroy`.
  - Violating game loop invariants (event sequencing, death flow, wave transitions).
- Minor: Standards violations and non-blocking structural drift.
  - Field ordering mismatch (`[Inject]` -> `[SerializeField]` -> private).
  - Private field naming not matching `_camelCase`.
  - Constants defined outside the domain's `Constants/` subfolder or not using `UPPER_SNAKE_CASE`.
  - Non-colocated narrow interface owned by a single implementation.
- Nit: Stylistic suggestions and code clarity notes.
  - Minor variable renaming, formatting, or local comment clarity.

## Unity Breaking Change Matrix

Check changed files against the following breaking dimensions:

1. Serialized Data Breaking:
   - Renaming or changing types of `[SerializeField]` fields without `[FormerlySerializedAs]` or designer notification.
   - Deleting serialized fields still referenced by prefabs or ScriptableObjects.
2. Reflex DI & Interface Breaking:
   - Changing constructor or injected interface signatures without updating the corresponding installer under Assets/Scripts/ReflexDI/.
   - Changing service lifetime from Singleton to Transient in a way that breaks stateful assumptions.
3. Lifecycle & Event Order Breaking:
   - Altering event firing order (e.g. `Awake` vs `Start` vs `OnEnable`, wave start, death sequence).
   - Changing synchronous event flows into async/delayed flows without updating all subscribers.

## Review Workflow

1. Identify impacted domains and dependency direction.
2. Run compilation verification: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
3. Check Reflex DI registration, field order, and interface colocation.
4. Check Unity lifecycle, event unsubscriptions, and DOTween cleanup.
5. Apply the Breaking Change Matrix.
6. Classify findings into Blocker, Major, Minor, and Nit.
7. Issue a clear verdict:
   - Approve: 0 Blocker, 0 Major findings.
   - Request Changes: 1+ Blocker or Major findings.

## Output

Produce a filled review based on:

- .agents/skills/architecture-review/templates/architecture-review-checklist.md
