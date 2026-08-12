# Pooling and Object Lifecycle System Documentation

## Purpose

The Pooling and Object Lifecycle system defines shared contracts and usage patterns for object pooling, release notifications, delayed disable sequences, setting-based functionality toggles, and cross-scene persistence.

It is responsible for:

- Standardizing pooled object lifecycle via Assets/Scripts/Pooling/IPoolable.cs.
- Exposing entity release notifications from spawners through Assets/Scripts/Pooling/IObjectReleaseNotifier.cs.
- Defining presentation completion contracts before disabling/releasing via Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs.
- Exposing generic feature enable/disable contracts via Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs.
- Marking scene objects that survive scene transitions via Assets/Scripts/ObjectLifecycle/DontDestroyOnSceneLoad.cs.

It is not responsible for:

- Concrete spawner placement logic or wave progression rules.
- Damage calculations, visual effect details, or sound playback logic.
- Direct scene wiring beyond attaching required component contracts.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Pooling/IPoolable.cs
  - Assets/Scripts/Pooling/IObjectReleaseNotifier.cs
  - Assets/Scripts/ObjectLifecycle/DontDestroyOnSceneLoad.cs
  - Assets/Scripts/ObjectLifecycle/Actions/INeedToCompleteBeforeDisable.cs
  - Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs
- Concrete implementations and consumers:
  - Assets/Scripts/Enemies/Base/Enemy.cs (IPoolable, waits for INeedToCompleteBeforeDisable)
  - Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs (INeedToCompleteBeforeDisable)
  - Assets/Scripts/LevelSystem/Exp/ExpParticle.cs (IPoolable)
  - Assets/Scripts/Projectiles/Projectile.cs (IPoolable)
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs (IPoolable)
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs (ObjectPool<Enemy>, IObjectReleaseNotifier via spawner interfaces)
  - Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs (IObjectPool<ExpParticle>, IObjectReleaseNotifier via spawner interfaces)
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs (IObjectPool<Projectile>)
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs (ObjectPool<GameObject> for drop prefabs, monitors IPoolable.OnCanBeReleased)
  - Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs (IObjectPool<DamageNumber>, IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>, IObjectReleaseNotifier via spawner interface)
  - Assets/Scripts/Settings/DamageNumbersSetting.cs (consumes IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>)
  - Assets/Scripts/Volumes/DeathVolume.cs (triggers IDamageable first, falls back to IPoolable.ReturnToPool)
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
  - IPoolable requires OnGet(), OnRelease(), ReturnToPool(), and OnCanBeReleased event handler.
  - IObjectReleaseNotifier exposes OnSpawnedEntityReleased event (inherited by IInWorldSpaceSpawner, IOnRandomGridPosSpawner, and IInGridSpaceSpawner interfaces).
  - INeedToCompleteBeforeDisable exposes OnCompleted event for components requiring async/presentation completion (such as EnemyDeathHandler visual sequence) before object release.
  - IEnableDisableFunctionalityTrigger<T> exposes EnableFunctionality() and DisableFunctionality() for feature toggles driven by settings.
  - DontDestroyOnSceneLoad calls DontDestroyOnLoad(gameObject) in Awake.
- Runtime flow:
  - Spawners create Unity ObjectPool<T> or IObjectPool<T> instances with creation, get, release, and destroy callbacks.
  - When spawning/getting, the spawner invokes OnGet(), subscribes to object release/death signals, activates the GameObject, and increments active entity counts.
  - During active play, the object raises OnCanBeReleased, OnCompleted, or a domain event when its life ends.
  - When releasing, the spawner invokes OnRelease(), unsubscribes event handlers, deactivates the GameObject, emits OnSpawnedEntityReleased, and decrements active counts.
  - DeathVolume applies full HP damage to IDamageable targets, or calls IPoolable.ReturnToPool() directly for non-damageable pooled entities.

## Rules and Invariants

- Critical behavior rules:
  - OnGet() must reset runtime state (velocity, health, timers, visual flags) for clean object reuse.
  - OnRelease() must halt active tweens/coroutines, unhook external listeners, and reset transient state.
  - ReturnToPool() is called by the object or external volumes and must raise OnCanBeReleased to signal the owning spawner.
  - Spawners must unsubscribe from object events upon release to prevent double-invocation on subsequent pool get calls.
  - Active object counts must increment exactly once on get and decrement exactly once on release.
- Ordering or sequencing guarantees:
  - Enemy release is delayed by EnemyDeathHandler's OnCompleted signal (implementing INeedToCompleteBeforeDisable).
  - DeathVolume prioritizes IDamageable damage application over direct ReturnToPool() calls.
- Constraints contributors must preserve:
  - Maintain exact event pairing (subscribe on get, unsubscribe on release).
  - Do not introduce Destroy() calls on pooled objects during standard gameplay release loops.

## Extension Points

- Safe extension areas:
  - Implement IPoolable on new entities managed by an ObjectPool<T> spawner.
  - Implement INeedToCompleteBeforeDisable on secondary components (death visuals, despawn animations) to defer pooling until presentation finishes.
  - Implement IEnableDisableFunctionalityTrigger<T> on systems controlled by toggleable settings.
  - Attach DontDestroyOnSceneLoad to scene objects required to survive scene transitions.
- Required dependencies and contracts:
  - Pooled objects must raise OnCanBeReleased or expose direct completion events to their owning spawner.
  - Components implementing INeedToCompleteBeforeDisable MUST fire OnCompleted; failing to do so will freeze the object in active state indefinitely.
- Testing implications:
  - C# compile check: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
  - Play Mode testing: verify active object counts, pool reuse without leftover state, clean unsubscriptions, and death volume cleanup.

## Integration Notes

- Upstream dependencies:
  - UnityEngine.Pool (ObjectPool<T>, IObjectPool<T>).
  - Reflex DI for binding lifecycle-enabled services (e.g. DamageNumbersSpawner as IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>).
- Downstream consumers:
  - Wave system monitors active enemy counts via spawner release events.
  - CollectibleDropNotifier monitors IPoolable.OnCanBeReleased on spawned drop objects.
  - Settings (DamageNumbersSetting) trigger EnableFunctionality() / DisableFunctionality().
- Cross-system coupling risks:
  - Double release calls return an already pooled object to the pool, triggering Unity ObjectPool exceptions.
  - Unsubscribing failures cause memory leaks and stale event handlers across reuse cycles.

## Known Risks and Open Questions

- Known limitations:
  - INeedToCompleteBeforeDisable lacks a timeout mechanism; broken animation states could permanently block pooling.
  - DontDestroyOnSceneLoad does not guard against duplicate instances if multiple scenes contain the component.
- Suggested follow-up tasks:
  - Add timeout safety to INeedToCompleteBeforeDisable implementations.

