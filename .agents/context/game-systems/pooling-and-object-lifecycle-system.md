# Pooling and Object Lifecycle System Documentation

## Purpose

The Pooling and Object Lifecycle system defines shared contracts for pooled objects, release notifications, enable/disable toggles, delayed disable completion, and objects that persist across scene loads.

It is responsible for:

- Standardizing pooled object get/release behavior through Assets/Scripts/Pooling/IPoolable.cs.
- Exposing release events from spawners and spawned objects.
- Defining lifecycle completion contracts for objects that need presentation to finish before disable.
- Defining generic functionality enable/disable contracts used by settings.
- Marking scene objects that should survive scene loads.

It is not responsible for:

- Concrete pool creation or spawn placement.
- Enemy death, projectile movement, exp collection, or damage-number animation details.
- Unity scene/prefab wiring beyond requiring the correct components to implement the contracts.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Pooling/IPoolable.cs
  - Assets/Scripts/Pooling/IObjectReleaseNotifier.cs
  - Assets/Scripts/ObjectLifecycle/DontDestroyOnSceneLoad.cs
  - Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs
  - Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs
- Current concrete users:
  - Assets/Scripts/Enemies/Enemy.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Enemies/EnemyDeathHandler.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticle.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
  - Assets/Scripts/Projectiles/Projectile.cs
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs
  - Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Assets/Scripts/Volumes/DeathVolume.cs
  - Assets/Scripts/Settings/DamageNumbersSetting.cs
- Related docs:
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/projectiles-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/damage-numbers-system.md
  - .agents/context/game-systems/settings-system.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core contracts:
  - Assets/Scripts/Pooling/IPoolable.cs requires `OnGet`, `OnRelease`, `ReturnToPool`, and `OnCanBeReleased` event handler.
  - Assets/Scripts/Pooling/IObjectReleaseNotifier.cs exposes `OnSpawnedEntityReleased` for spawners or spawner-like components.
  - Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs exposes `OnCompleted` for presentation or async cleanup that must finish before disable.
  - Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs exposes `EnableFunctionality` and `DisableFunctionality` for feature toggles.
  - Assets/Scripts/ObjectLifecycle/DontDestroyOnSceneLoad.cs calls `DontDestroyOnLoad(gameObject)` in `Awake`.
- Runtime flow:
  - Pool-backed spawners create `ObjectPool<T>` instances and provide get/release callbacks.
  - On get, the spawner calls the object's `OnGet`, subscribes to release/life-end events, activates the GameObject, and increments active count.
  - During active play, the object raises `OnCanBeReleased`, `OnLifeEnd`, or a domain-specific completion signal.
  - On release, the spawner calls `OnRelease`, unsubscribes events, deactivates the GameObject, raises `OnSpawnedEntityReleased`, and decrements active count.
  - Assets/Scripts/Volumes/DeathVolume.cs first applies full HP damage to `IDamageable` objects; if no damageable capability exists, it calls `ReturnToPool` on Assets/Scripts/Pooling/IPoolable.cs.
  - Settings can enable or disable feature behavior through Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs, currently used for damage numbers.

## Rules and Invariants

- Critical behavior rules:
  - `OnGet` should reset runtime state needed for reuse.
  - `OnRelease` should stop active animations/tweens, clear subscriptions owned by the object, and restore reusable state.
  - `ReturnToPool` should be reserved for object-initiated or external forced release paths and must raise `OnCanBeReleased`.
  - Spawner release paths must unsubscribe from object events before or during release to avoid duplicate callbacks across reuse.
  - `OnSpawnedEntityReleased` means an active spawned object left active play; do not use it as a spawn-created event.
  - Active object counts must increment and decrement exactly once per successful active spawn.
- Ordering or sequencing guarantees:
  - Enemy pool release is delayed by Assets/Scripts/Enemies/EnemyDeathHandler.cs's `OnCompleted` through Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs.
  - Projectile and exp particle release is event-driven by life/completion events.
  - Assets/Scripts/Volumes/DeathVolume.cs prioritizes full HP damage over direct pool return when an object is damageable.
- Constraints contributors must preserve:
  - Keep pooled lifecycle events paired with spawner subscriptions and unsubscriptions.
  - Preserve required scene/prefab components that implement lifecycle contracts.
  - Do not introduce direct `Destroy` calls into pooled object normal release paths unless replacing pooling is intentional.
  - Do not edit prefabs/scenes directly unless explicitly requested.

## Extension Points

- Safe extension areas:
  - Add a new pooled object by implementing Assets/Scripts/Pooling/IPoolable.cs and using an owning spawner's `ObjectPool<T>` get/release callbacks.
  - Add delayed disable behavior by implementing Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs on a sibling component and raising `OnCompleted` when all required presentation finishes.
  - Add a setting-controlled feature by implementing Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs and binding it through Reflex.
  - Add persistent scene objects with Assets/Scripts/ObjectLifecycle/DontDestroyOnSceneLoad.cs when their lifetime is intentionally global.
- Required dependencies and contracts:
  - Pooled objects must expose a reliable release signal or be released only by their owning spawner.
  - Delayed-disable components must always raise `OnCompleted`; otherwise pooled objects can remain active forever.
  - Feature toggles consumed by settings must be registered in the active Reflex container.
- Testing implications:
  - Compile after contract changes.
  - In Unity, validate pool reuse, active counts, event unsubscription, forced release through death volume, and scene reload persistence.
  - For delayed disable paths, test all completion branches, including missing/short audio or VFX references.

## Integration Notes

- Upstream dependencies:
  - Unity `ObjectPool<T>` is used by enemy, exp particle, damage number, and projectile owner systems.
  - Reflex binds some lifecycle-capable services such as damage-number functionality toggles.
  - Domain systems decide when life ends; pooling contracts only standardize release handoff.
- Downstream consumers:
  - Wave pacing depends on enemy spawned count.
  - Skill upgrade UI listens to collectible spawner release notifications.
  - Level progression consumes exp particle release behavior indirectly through collection flow.
  - Settings consume damage-number enable/disable functionality.
- Cross-system coupling risks:
  - Release notification semantics are shared by spawners, UI, and wave logic; changing event timing can cause visible gameplay changes.
  - Pooled objects often cache component references in `Awake`; prefab composition is part of the lifecycle contract.
  - Persistent boot objects can create duplicate-service bugs if scenes also instantiate equivalent services.

## Known Risks and Open Questions

- Known limitations:
  - Assets/Scripts/Pooling/IPoolable.cs does not define whether `OnCanBeReleased` should be raised before or after internal cleanup; current implementations vary by path.
  - Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs has only an event and no cancellation or timeout contract.
  - Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs uses a self-referential generic constraint that makes bindings specific but verbose.
  - Assets/Scripts/ObjectLifecycle/DontDestroyOnSceneLoad.cs does not prevent duplicates if multiple scenes contain the same persistent object.
- Open design questions:
  - Should pooled lifecycle have a stricter state machine to prevent double release?
  - Should delayed-disable contracts include failure/timeout behavior?
  - Should persistent boot services include duplicate guards?
- Suggested follow-up tasks:
  - Audit pooled release paths for double-release and missing-unsubscribe risks.
  - Add lightweight play-mode checks for active count stability on enemies, exp particles, damage numbers, and projectiles.
