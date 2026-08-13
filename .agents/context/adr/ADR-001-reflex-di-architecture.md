# ADR-001: Explicit Dependency Injection via Reflex DI

- Status: Accepted
- Date: 2026-08-12
- Decision Makers: Game Development Team & AI Agents

## Context

In Unity survivor prototypes, components often resort to static singletons (`Instance`), `FindObjectOfType`, or direct scene references to access central systems (Score, Spawners, LevelManager, Player). Over time, this leads to tight coupling, race conditions during scene loading/unloading, untestable code, and hidden execution dependencies.

## Decision

We standardize dependency injection across `Car Survivors` using **Reflex DI**.

1. **Explicit Binding:** All runtime services (e.g., audio, score, spawner, navigation, status managers) must be bound in installer contexts (`ProjectInstaller`, `SceneInstaller`, or custom Reflex installers under `Assets/Scripts/ReflexDI/`).
2. **Explicit Injection:** Runtime components receive dependencies via `[Inject]` fields or constructor injection.
3. **No Singletons or Global Lookups:** Static mutable singletons and `FindObjectOfType`/`FindAnyObjectByType`/`GameObject.Find` calls are strictly forbidden in runtime code.

## Consequences

### Positive
- Predictable scene setup and explicit component initialization sequence.
- Easy mocking and isolation for EditMode and PlayMode tests.
- Agents and developers can inspect installer files under `Assets/Scripts/ReflexDI/` to see all active dependencies.

### Negative / Trade-offs
- Components instantiated dynamically must be resolved via Reflex container or spawned by Reflex-aware factories/spawners.
- Requires registering new runtime services in installer assets/scripts before consumption.
