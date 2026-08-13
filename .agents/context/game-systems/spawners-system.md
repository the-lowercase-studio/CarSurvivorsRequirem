# Spawners System Documentation

## Purpose

The Spawners system defines shared generic contracts and concrete implementations for creating gameplay objects in grid space or world space.

It does not own the complete behavior of every spawned object. Enemy behavior, collectible rewards, damage popup presentation, experience particle collection, and projectile movement remain owned by their respective domain systems. Spawner implementations are responsible for choosing or receiving spawn positions, creating or pooling objects, tracking active spawned counts, and raising release notifications when a spawned object leaves active play.

Collectible drops from enemies are triggered directly by death events via `EnemyDropHandler` and managed/pooled by `CollectibleDropNotifier`.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Spawners/
  - Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs
  - Assets/Scripts/Spawners/GridSpace/IInGridSpaceSpawner.cs
  - Assets/Scripts/Spawners/WorldSpace/IInWorldSpaceSpawner.cs
  - Assets/Scripts/Spawners/ISpawnedObjectsCounter.cs
  - Assets/Scripts/Spawners/SpawnChanceInfo.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
- Concrete implementations:
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
- DI setup:
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/ReflexDI/BootLoader.cs
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/damage-numbers-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/interactables-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - `ISpawnedObjectsCounter`: Exposes `uint CurrentlySpawnedObjectsCount { get; }` for systems that track active entity counts.
  - `IObjectReleaseNotifier`: Exposes `event EventHandler OnSpawnedEntityReleased;` for release-driven consumers.
  - `IOnRandomGridPosSpawner<TSelf>`: Contract for spawners that select random walkable grid cells before spawning. Inherits `ISpawnedObjectsCounter` and `IObjectReleaseNotifier`.
  - `IInGridSpaceSpawner<TSelf, TSpecificConfig>`: Contract for spawners that receive an explicit `Cell` plus config. Inherits `ISpawnedObjectsCounter` and `IObjectReleaseNotifier`.
  - `IInWorldSpaceSpawner<TSelf, TSpecificConfig>`: Contract for spawners that receive a world-space `Vector3` plus config. Inherits `ISpawnedObjectsCounter` and `IObjectReleaseNotifier`.
  - `SpawnChanceInfo`: Serializable chance payload used by enemy spawn entries and updated by spawn chance redistribution.
  - `MapInteractablesSpawner`: Scene component that places interactive map objects (e.g. difficulty totems) onto walkable grid cells in `Start()` before standard gameplay waves begin.
- Key interfaces:
  - `IOnRandomGridPosSpawner<TSelf>`: Method: `void SpawnAtRandomGridPos(int count = 1);`
  - `IInWorldSpaceSpawner<TSelf, TSpecificConfig>`: Method: `void Spawn(Vector3 pos, TSpecificConfig specificConfig, int count = 1);`
  - `IInGridSpaceSpawner<TSelf, TSpecificConfig>`: Method: `void Spawn(Cell cell, TSpecificConfig specificConfig, int count = 1);`
  - All generic spawner contracts enforce `TSelf` constraints to bind DI uniquely per concrete spawner type.
- Concrete Implementations Inventory:
  - `EnemiesSpawner`: Implements `IOnRandomGridPosSpawner<EnemiesSpawner>`, `ISwarmEnemySpawner`, `IEnemySpawnDifficultyController`.
  - `DamageNumbersSpawner`: Implements `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`.
  - `ExpParticleSpawner`: Implements `IInWorldSpaceSpawner<ExpParticleSpawner, float>`.
  - `CollectibleDropNotifier`: Implements `ICollectibleDropNotifier`. Pools and spawns item drops.
  - `MapInteractablesSpawner`: Config driven grid placement component.
- Runtime flow:
  - Scene-level spawners are bound through Reflex DI installers (`DefaultGameplaySceneInstaller`).
  - Consumers inject generic spawner interfaces (e.g. `IOnRandomGridPosSpawner<EnemiesSpawner>` or `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`).
  - Spawn calls retrieve or instantiate objects from pools, initialize per-object data, activate GameObjects, and increment `CurrentlySpawnedObjectsCount`.
  - Object release paths deactivate objects, raise `OnSpawnedEntityReleased`, and decrement `CurrentlySpawnedObjectsCount`.
  - **Map Interactables Initialization**: `MapInteractablesSpawner.Start` collects walkable cells outside the initial player chunk, shuffles candidates, and iterates over `InteractableSpawnRule` entries. For each candidate cell, it checks `MinDistanceToImpassable`, `MinDistanceToOtherInteractable`, and `MinDistanceToSameType`. When valid, it instantiates the prefab with a random Y rotation and injects dependencies via `Reflex.Injectors.GameObjectInjector.InjectRecursive(spawnedObject, _container)`.

## Rules and Invariants

- Critical behavior rules:
  - Systems consuming spawners must depend on generic spawner interfaces rather than direct scene lookups or singletons.
  - Scene-scoped spawners are bound via Reflex in `DefaultGameplaySceneInstaller`.
  - Increment `CurrentlySpawnedObjectsCount` only on successful object activation; decrement exactly once per completed release.
  - `OnSpawnedEntityReleased` signifies pool/world release, not initial spawn completion.
  - `MapInteractablesSpawner` runs in `Start()` (not `Awake()` or `OnEnable()`) to guarantee `IGridManager` grid cost fields are fully computed and the DI container is available for recursive injection.
  - Map interactables are excluded from spawning inside the initial player chunk or close to impassable walls/other interactables.
- Ordering or sequencing guarantees:
  - `EnemiesSpawner.Awake` creates pools before `Start` pre-warms pools and initializes redistribution.
  - `MapInteractablesSpawner` runs during `Start()`, placing map objects before wave spawning begins.
  - `DamageNumbersSpawner` returns floating text objects to pool on `DamageNumber.OnLifeEnd`.
- Constraints contributors must preserve:
  - Preserve inspector-authored prefabs, parent transforms, pool limits, and distance constraints.
  - Keep generic spawner interface signatures aligned across call sites.
  - Keep grid cell validation consistent with `GridSystem`.

## Extension Points

- Safe extension areas:
  - Create a new spawner by implementing `IInWorldSpaceSpawner<TSpawner, TConfig>` or `IOnRandomGridPosSpawner<TSpawner>`.
  - Add new interactable map object types by appending an `InteractableSpawnRule` to `MapInteractablesSpawner`.
- Required dependencies and contracts:
  - Grid-based spawners require injected `IGridManager`.
  - `MapInteractablesSpawner` requires Reflex container injection (`_container`) for recursive dependency resolution on spawned prefabs.
  - DI bindings must be registered in `DefaultGameplaySceneInstaller`.
- Testing implications:
  - Compile after spawner interface or DI changes using `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Validate pool capacities, active entity count tracking, and off-camera placement in Unity Play Mode.

## Integration Notes

- Upstream dependencies:
  - `IGridManager`: Supplies grid geometry and player chunk boundaries.
  - Reflex DI: Injects spawners into consumers and supplies `Container` for recursive instantiation.
  - Unity `ObjectPool<T>`: Used by enemy, damage number, exp particle, and drop spawners.
- Downstream consumers:
  - `WaveManager` drives enemy waves via `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - `Enemy` spawns damage numbers via `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`.
  - `EnemyDeathHandler` spawns EXP via `IInWorldSpaceSpawner<ExpParticleSpawner, float>`.
  - `EnemyDropHandler` delegates drops via `ICollectibleDropNotifier`.

## Known Risks and Open Questions

- Known limitations:
  - `IInGridSpaceSpawner<TSelf, TSpecificConfig>` is currently a defined interface without an active runtime implementation in `Assets/Scripts/`.
  - Misspelled fields in `SpawnChanceInfo` (e.g. `TresholdToStartAddingSpawnChanceToOtherInfos`) are preserved to prevent serialized asset data loss.

