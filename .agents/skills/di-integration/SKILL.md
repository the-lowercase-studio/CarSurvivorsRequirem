---
name: di-integration
description: "Use when: adding, changing, or reviewing ProjectLizard runtime dependencies, Reflex bindings, injected services, interfaces, installers, or replacing direct scene/global lookups with DI-aligned access."
---

# DI Integration Skill

Use this skill to add or review dependency injection integration in ProjectLizard while preserving explicit ownership boundaries and Reflex-based service wiring.

## Required Sources

Before changing DI-related code, ground the work in:

- AGENTS.md
- .agents/README.md
- .agents/docs/project-coding-standards.md
- .agents/docs/technology-documentation.md
- Assets/Scripts/ReflexDI/

Use official Reflex documentation from `.agents/docs/technology-documentation.md` when binding or injection behavior is uncertain.

## Inputs

- Target feature, system, or dependency being introduced.
- Runtime consumer files that need access to the dependency.
- Owning system for the dependency.
- Whether the dependency is scene-bound, prefab-local, asset-authored, or pure runtime logic.

## Workflow

1. Identify the dependency owner.
   - Keep services in the domain that owns their state or behavior.
   - Do not place cross-system behavior in consumers just because they need access to it.
2. Choose the access pattern.
   - Use Reflex injection for scene/runtime services already managed by DI.
   - Use prefab-local component references for tightly owned child components.
   - Use ScriptableObject fields for designer-authored configuration.
   - Avoid `FindAnyObjectByType`, singleton access, static mutable service state, or broad scene searches unless explicitly accepted as a temporary bridge.
   - Choose if it should be a readonly field.
3. Define or reuse the narrowest interface.
   - Reuse existing interfaces before adding a new one.
   - If adding an interface primarily used by one implementation, colocate it above the implementation class.
   - Prefix interface names with `I`.
4. Add or update Reflex binding.
   - Register scene services in the appropriate installer under `Assets/Scripts/ReflexDI/`.
   - Bind concrete implementations to interfaces consumed by runtime systems.
   - Keep binding names and lifetimes consistent with nearby registrations.
5. Update consumers.
   - Add `[Inject] private` fields before `[SerializeField]` fields.
   - Keep dependencies explicit and narrow.
   - Do not add fallback global lookups that hide missing bindings.
6. Validate integration boundaries.
   - Confirm consumers depend on interfaces, not concrete scene implementations, when an interface exists.
   - Confirm turn-flow, combat, shield, and effect semantics are unchanged unless the task explicitly changes them.
   - Confirm inspector workflows and serialized fields remain compatible.

## Common ProjectLizard Cases

- New manager or service:
  - Create or reuse an `I...` interface.
  - Implement it in the owning system.
  - Bind it in `SceneInstaller`.
  - Inject the interface into consumers.
- Replacing direct lookup:
  - Identify why the consumer currently uses `FindAnyObjectByType` or similar access.
  - Move access to an injected interface when the consumer is a scene runtime object.
  - If the consumer is a `ScriptableObject`, prefer passing dependencies through an execution context or runtime adapter instead of injecting the asset directly.
- Effect needing a service:
  - Do not make the `EffectSO` find scene services directly.
  - Prefer extending `CardEffectContext` or routing through a runtime effect executor when the effect needs scene-owned services.
- UI presenter needing gameplay state:
  - Inject the state interface when the presenter is scene-owned.
  - Subscribe and unsubscribe events in matching lifecycle methods.

## Review Checklist

- Dependency owner is clear.
- Existing interface was reused when available.
- New interface is narrow and colocated when appropriate.
- `SceneInstaller` binding exists for injected scene services.
- `[Inject]` fields are ordered before serialized fields.
- No new singleton, static mutable service, or scene search was introduced.
- No hidden fallback path masks missing DI setup.
- Inspector-configured references remain inspector-configured.
- Turn-event subscriptions still unsubscribe correctly.
- Combat invariants such as shield-first damage and card energy payment remain unchanged.

## Output

When implementing, summarize:

- Files changed.
- Interfaces and bindings added or reused.
- Consumers updated.
- Any direct lookup removed or intentionally left in place.
- Validation performed and remaining Unity Editor setup, if any.
