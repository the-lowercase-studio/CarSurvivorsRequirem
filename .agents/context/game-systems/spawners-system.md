# Spawners System Documentation

## Purpose

The Spawners system defines shared contracts and data used by runtime systems that create gameplay objects in grid space or world space.

It does not own the complete behavior of every spawned object. Enemy behavior, collectible rewards, damage popup presentation, exp collection, and projectile movement remain owned by their domain systems. Spawner implementations are responsible for choosing or receiving spawn positions, creating or pooling objects, tracking active spawned counts, and raising release notifications when a spawned object leaves active play.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Spawners/`
  - `Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs`
  - `Assets/Scripts/Spawners/GridSpace/IInGridSpaceSpawner.cs`
  - `Assets/Scripts/Spawners/WorldSpace/IInWorldSpaceSpawner.cs`
  - `Assets/Scripts/Spawners/ISpawnedObjectsCounter.cs`
  - `Assets/Scripts/Spawners/SpawnChanceInfo.cs`
- Current concrete implementations:
  - `Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs`
  - `Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs`
  - `Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs`
  - `Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs`
  - `Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs`
- DI setup:
  - `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`
  - `Assets/Scripts/ReflexDI/BootLoader.cs`
- Related docs:
  - `.agents/context/game-systems/enemies-system.md`
  - `.agents/context/game-systems/collectibles-system.md`
  - `.agents/context/game-systems/damage-numbers-system.md`
  - `.agents/context/game-systems/level-system.md`
  - `.agents/context/game-systems/grid-system.md`
- Related agents or instructions:
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/di-integration/SKILL.md`
  - `.agents/context/project-coding-standards.md`

## Architecture and Data Flow

- Core components:
  - `ISpawnedObjectsCounter` exposes `CurrentlySpawnedObjectsCount` for systems that need active spawned object counts.
  - `IObjectReleaseNotifier` exposes `OnSpawnedEntityReleased` for release-driven consumers.
  - `SpawnChanceInfo` is a serializable chance payload used by enemy spawn data and mutated by enemy spawn chance redistribution at runtime.
  - `IOnRandomGridPosSpawner<TSelf>` is for systems that pick their own random grid cell before spawning.
  - `IInGridSpaceSpawner<TSelf, TSpecificConfig>` is for systems that receive an explicit `Cell` plus caller-specific spawn config.
  - `IInWorldSpaceSpawner<TSelf, TSpecificConfig>` is for systems that receive a world-space `Vector3` plus caller-specific spawn config.
- Key interfaces:
  - All generic spawner interfaces include `ISpawnedObjectsCounter` and `IObjectReleaseNotifier`.
  - Generic `TSelf` constraints keep DI bindings specific to one concrete spawner contract, for example `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - World-space spawners use `Spawn(Vector3 pos, TSpecificConfig specificConfig, int count = 1)`.
  - Random-grid spawners use `SpawnAtRandomGridPos(int count = 1)`.
  - Grid-cell spawners use `Spawn(Cell cell, TSpecificConfig specificConfig, int count = 1)`.
- Runtime flow:
  - Scene-level spawners are registered through Reflex installers.
  - Consumers inject the narrow generic spawner interface instead of finding scene objects directly.
  - Spawn requests create or retrieve objects, initialize object-specific state, subscribe to release/life-end events when needed, activate or instantiate the object, and increment `CurrentlySpawnedObjectsCount`.
  - Release paths undo per-object subscriptions, deactivate or stop tracking the object, raise `OnSpawnedEntityReleased`, and decrement `CurrentlySpawnedObjectsCount`.

## Rules and Invariants

- Critical behavior rules:
  - Keep spawner consumers on interface contracts such as `IOnRandomGridPosSpawner<EnemiesSpawner>` or `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`.
  - Register scene spawners through Reflex in the appropriate installer. Do not add singleton access or scene searches as a shortcut.
  - Preserve `CurrentlySpawnedObjectsCount` semantics: increment only for successful active spawns and decrement exactly once for each completed release path.
  - Preserve `OnSpawnedEntityReleased` as a release signal, not as a generic spawn-completed signal.
  - Treat changes to spawn chance data and redistribution as gameplay balance changes.
- Ordering or sequencing guarantees:
  - `EnemiesSpawner.Awake` creates object pools before `Start` initializes spawn chance redistribution.
  - `WaveManager` relies on enemy spawned counts when scheduling waves.
  - `CollectibleItemsSpawner` clears collectible cell occupancy before raising `OnSpawnedEntityReleased`.
  - `DamageNumbersSpawner` releases a popup after `DamageNumber.OnLifeEnd`.
  - `ExpParticleSpawner` queues spawn requests, then drains the queue during repeating checks.
- Constraints contributors must preserve:
  - Preserve inspector-authored scene references, prefab references, parent transforms, spawn delays, and chance data.
  - Keep object lifecycle subscriptions paired with unsubscriptions in release paths.
  - Keep grid occupancy rules consistent with `GridSystem` when adding grid-based spawners.
  - Do not edit scene, prefab, asset, or meta files directly unless explicitly requested.

## Extension Points

- Safe extension areas:
  - Add a new spawner by implementing the narrowest existing contract that matches the requested coordinates and config shape.
  - Add a new world-space consumer by depending on `IInWorldSpaceSpawner<TSpawner, TConfig>`.
  - Add a new random-grid consumer by depending on `IOnRandomGridPosSpawner<TSpawner>`.
  - Add new spawn chance data only when serialized data migration and balance review are intentional.
- Required dependencies and contracts:
  - Grid-space random spawners that need world/grid state should use injected `IGridManager`.
  - Pool-backed spawners should route object release through the spawned object's existing release or life-end event.
  - DI bindings must be added to `DefaultGameplaySceneInstaller` for gameplay-scene spawners or `BootLoader.InstallExtra` for cross-scene extras.
  - New spawner interfaces should be avoided unless existing world-space, explicit-grid, or random-grid contracts cannot express the use case.
- Testing implications:
  - Compile after C# contract or DI changes.
  - In Unity, validate the scene installer has the required serialized spawner reference.
  - For count-sensitive systems, verify active count increments and decrements across spawn, normal release, and edge release paths.
  - For pooled spawners, verify event subscriptions do not accumulate across pool reuse.

## Integration Notes

- Upstream dependencies:
  - `IGridManager` supplies grid data for enemy and collectible placement.
  - Unity serialized fields supply prefabs, parent transforms, timing, pool size, visual thresholds, and spawn chance configuration.
  - Unity `ObjectPool<T>` is used by enemies, damage numbers, exp particles, and minigun projectiles.
- Downstream consumers:
  - `WaveManager` drives enemy waves through `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - `SkillUpgradePresenter` listens to collectible release through `IOnRandomGridPosSpawner<CollectibleItemsSpawner>` and delegates reward selection to `ISkillUpgradeFlow`.
  - `Enemy` spawns damage numbers through `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`.
  - `EnemyDeathHandler` spawns exp particles through `IInWorldSpaceSpawner<ExpParticleSpawner, float>`.
  - Turret/projectile skill code can use world-space spawner semantics for projectile spawning.
- Cross-system coupling risks:
  - Wave pacing depends on `EnemiesSpawner.CurrentlySpawnedObjectsCount`.
  - Skill upgrade UI behavior depends on collectible release notifications.
  - Grid occupancy bugs in collectible spawning can block future collectible placement.
  - Moving damage number bindings out of `BootLoader` can break both enemy injection and settings integration.
  - Changing generic interface signatures affects every DI binding and consumer.

## Known Risks and Open Questions

- Known limitations:
  - `IInGridSpaceSpawner<TSelf, TSpecificConfig>` currently appears to be a contract with no runtime implementation in `Assets/Scripts`.
  - `SpawnChanceInfo` uses misspelled field/method names such as `TresholdToStartAddingSpawnChanceToOtherInfos` and `HasReachedTresholdToStartAddingSpawnChanceToOtherInfos`; renaming affects serialized data and call sites.
  - Some concrete spawner docs record implementation-specific edge cases, including collectible count behavior for prefabs without `ICollectible`, exp divider behavior, and enemy spawn chance redistribution assumptions.
  - `MinigunTurret` implements `IInWorldSpaceSpawner<MinigunTurret, ProjectileSpawnConfig>` but is not registered as a scene-level DI spawner in the inspected installers.
- Open design questions:
  - Should the generic spawner contracts remain self-typed per concrete spawner, or should future shared systems use capability-specific non-self generic interfaces?
  - Should spawn chance state be copied at runtime so inspector-authored values remain unchanged during play sessions?
  - Should currently instantiated collectible spawns move to pooling if spawn frequency or collectible variety increases?
- Suggested follow-up tasks:
  - Add focused tests or debug validation for count/release behavior on enemy, collectible, damage number, and exp spawners.
  - Review whether `IInGridSpaceSpawner<TSelf, TSpecificConfig>` is planned API or dead contract.
  - Document any new spawner in the owning domain doc and link back to this shared contract document.
