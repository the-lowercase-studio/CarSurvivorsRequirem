# Enemies Spawn System Documentation

## Purpose

The Enemies Spawn System is responsible for pooling, instantiating, selecting, and placing enemy entities in grid space. It handles weighted random selection based on progressive spawn chances, manages individual object pools for configured enemy types, determines valid off-camera positions, and redistributes spawn chances to progressively introduce more challenging enemies.

It separates classic wave spawning (which is restricted to spawning outside the player's immediate chunk boundaries) from swarm spawning (which is allowed to spawn inside the player chunk). It also enforces a cell occupancy density limit to prevent too many enemies from overlapping on the same cell on spawn.

It does not own enemy combat behavior, wave pacing, or swarm warning cues. Those are delegated to the enemies, waves, and swarm systems.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
  - Assets/Scripts/Spawners/SpawnChanceInfo.cs
  - Assets/Scripts/Enemies/IncreaseDifficultyTotem.cs
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/waves-system.md
  - .agents/context/game-systems/swarm-system.md
  - .agents/context/game-systems/spawners-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - `EnemiesSpawner`: The main MonoBehaviour component bound in Reflex. It creates and manages an `ObjectPool<Enemy>` for each configured enemy type, processes spawning calls, and updates the active spawned count.
  - `EnemiesSpawnChanceRedistributionSystem`: A non-MonoBehaviour helper class instantiated by the spawner. It shifts spawn probability weights as enemies are spawned, letting harder enemies appear progressively.
  - `EnemySpawnInfo`: A serializable data structure defining the enemy prefab, pool constraints (`MaxAmount`), and spawn chance configuration.
  - `SpawnChanceInfo`: Holds the current probability, threshold triggers, and behavior flags for weighted random selection.
  - `IncreaseDifficultyTotem`: A one-use interactive totem component placed in the map. It monitors player distance, displays an interaction canvas prompt, hides itself when used, plays a VFX, and increases difficulty.
- Key interfaces:
  - `IOnRandomGridPosSpawner<EnemiesSpawner>`: Injected into `WaveManager` for standard wave spawning and reading `CurrentlySpawnedObjectsCount`.
  - `ISwarmEnemySpawner`: Injected into `SwarmSpawner` to retrieve enemy configurations and trigger spawns for specific enemy types.
  - `IObjectReleaseNotifier`: Emits `OnSpawnedEntityReleased` events when an enemy is returned to the pool, allowing other systems to react.
  - `IEnemySpawnDifficultyController`: Injected into interactive map objects (like the difficulty totem) to allow increasing the enemy spawn chance redistribution speed.
- Runtime flow:
  - **Setup**: During `Awake`, `EnemiesSpawner` pools configured enemy types. During `Start`, it initializes the redistribution system and pre-warms the pools.
  - **Standard Wave Spawning**: `WaveManager` calls `SpawnAtRandomGridPos`. The spawner uses `GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk` to select random walkable cells in `WorldGrid` that are outside the player chunk but within `_outerSpawnBufferCells` bounds. It filters cells to ensure they have fewer than `_maxEnemiesPerCell` enemies, and adds cells multiple times based on available slots to ensure the requested count is met. Finally, it pulls prefabs weight-proportionally, gets them from the pool, activates them, and invokes chance redistribution.
  - **Swarm Spawning**: `SwarmSpawner` calls `SpawnSpecificEnemy`. The spawner uses `GridCellsNotVisibleByMainCamera.GetRandomWalkableCells` to select random walkable cells within `GridPlayerChunk` that are not visible. It also respects the `_maxEnemiesPerCell` occupancy limit.
  - **Difficulty Increase**: Interactive elements trigger difficulty adjustments by calling `IncreaseSpawnChanceRedistributionFactor` on the injected `IEnemySpawnDifficultyController`.
  - **Release**: When an enemy dies or is removed, it invokes its `OnCanBeReleased` event. The spawner catches this, deactivates the enemy, decrements `CurrentlySpawnedObjectsCount`, and fires `OnSpawnedEntityReleased`.

## Rules and Invariants

- Critical behavior rules:
  - Standard waves must spawn via `IOnRandomGridPosSpawner<EnemiesSpawner>` using weighted chances, and only outside the Player Chunk boundary.
  - Swarms must spawn via `ISwarmEnemySpawner.SpawnSpecificEnemy` and can spawn inside the Player Chunk.
  - Both spawning systems must respect the cell occupancy limit `_maxEnemiesPerCell` (default: 2), preventing spawning on cells that are already full.
  - If spawning slots are scarce for standard waves, the search area expands dynamically (`_outerSpawnBufferCells` increases by 4 per iteration) until the requested number of enemies can be safely accommodated or no new cells are discovered.
  - Newly spawned or teleported enemies must be positioned on walkable cells out of the main camera's viewport.
  - Spawn chance redistribution must execute exactly once per standard spawn batch.
  - The difficulty totem can only be activated once. On activation, it disables its own script component, deactivates its UI prompt canvas, and hides its visuals.
  - Draw the totem's interaction radius gizmo exclusively in Unity editor edit mode (when not playing) to prevent runtime clutter.
- Ordering or sequencing guarantees:
  - `EnemiesSpawner.Awake` initializes the pools before `Start` sets up the redistribution system and pre-warms.
  - When an enemy is fetched, its health is reset to `EnemyConfigSO.MaxHealth` in `Enemy.OnGet` before it is activated.
- Constraints contributors must preserve:
  - Maintain the Reflex bindings and do not use singleton patterns or scene lookups for spawning.
  - Event subscriptions must be safely paired (e.g., unsubscribed upon release) to avoid leaks in pooled reuse.

## Spawn Chance Redistribution Algorithm

The `EnemiesSpawnChanceRedistributionSystem` operates on configured `EnemySpawnInfo` list entries that do not have the `SpawnChanceWillNotChange` flag set:
1. **Threshold Assessment**: On redistribution, entries are marked as having reached their threshold (`HasEverReachedThreshold = true`) if their spawn chance equals or exceeds `TresholdToStartAddingSpawnChanceToOtherInfos`.
2. **Subtraction**: The current active source entry (starting with the first entry in the list) has its `SpawnChance` decreased by a value computed as `_spawnChanceDecreaseFactor.GetRandomValueInRange() + _redistributionFactorBonus`.
3. **Shift**: If the source entry's chance drops to 0, the active source shifts to the next configured entry in the list.
4. **Geometric Distribution**: The subtracted value is redistributed to downstream eligible entries. An entry is eligible if its predecessor has met its threshold. The value is distributed geometrically (the first eligible gets half, the next a quarter, and so on, with the last eligible getting the remainder). If no entries are eligible, the entire value goes to the very last entry in the system.

## Extension Points

- **Adding Enemies**: Create a prefab with `Enemy` and config, and add it to the `_poolEnemiesInfo` list on `EnemiesSpawner` in the Unity Inspector.
- **Redistribution Adjustments**: Tune `_spawnChanceDecreaseFactor` or configure thresholds in `SpawnChanceInfo` to alter the pace at which harder enemies start to dominate standard waves.
- **Spawning Limits**: Adjust `_outerSpawnBufferCells` (default: 8) to change how far classic waves spawn, or `_maxEnemiesPerCell` (default: 2) to change maximum spawning density.

## Integration Notes

- Upstream dependencies:
  - `IGridManager` supplies chunk grid boundaries (via `GridPlayerChunk` and `WorldGrid`).
  - `Camera _mainCamera` is used to filter out visible cells.
  - Reflex scene installer binds the spawner interface.
- Downstream consumers:
  - `WaveManager` pacing depends on standard waves and active counts.
  - `SwarmSpawner` queries configs and spawns specific waves during swarms.
  - `EnemiesOutsidePlayerChunkTeleporter` teleports stray enemies back to valid cells inside the player chunk.

## Known Risks and Open Questions

- **Spelling Invariants**: The field `TresholdToStartAddingSpawnChanceToOtherInfos` and method `HasReachedTresholdToStartAddingSpawnChanceToOtherInfos` contain a spelling mistake (missing the first 'h' in threshold). This spelling must be preserved at code call-sites.
- **Direct Release Bypass (Potential Memory leak)**: In `EnemiesSpawner.cs`, the runtime release event handler `Enemy_OnRelease` calls `OnEnemyRelease(enemy)` directly. It does not invoke `pool.Release(enemy)` during regular gameplay (unlike `PreWarmPools`, which uses it correctly). While the enemy is set inactive and count is decremented, this bypasses the pool's internal bookkeeping, which could lead to redundant allocations under load.
- **Redistribution Config Constraint**: If all configured enemies have `SpawnChanceWillNotChange` enabled, the list of eligible redistribution targets will be empty, which could lead to an index out of range exception during initialization.
