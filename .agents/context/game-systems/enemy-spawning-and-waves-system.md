# Enemy Spawning and Waves System Documentation

## Purpose

The Enemy Spawning and Waves system controls gameplay pacing for enemy appearance in Car Survivors. It manages classic wave progression, high-density swarm event warnings and triggers, weighted enemy probability selection, progressive spawn chance redistribution, off-camera grid placement, object pooling for enemy entities, and cell occupancy density limits.

It divides enemy spawning into two pacing modes:
1. **Standard Wave Pacing**: Periodic batch spawning outside the player's active chunk boundary.
2. **Swarm Event Pacing**: Periodic high-density spawn events that temporarily freeze standard waves, play a UI warning countdown with screen gamma dimming, and spawn targeted enemy types inside the player chunk on non-visible cells.

It does not own enemy combat AI, movement behaviors, or individual enemy damage/health logic. Those responsibilities are owned by the Enemy System (`enemies-system.md`).

## Reading Map

- Primary code locations:
  - Assets/Scripts/Waves/WaveManager.cs
  - Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Assets/Scripts/UI/HUD/SwarmNotificationPresenter.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
  - Assets/Scripts/Spawners/SpawnChanceInfo.cs
  - Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md

## Architecture and Data Flow

- Core components:
  - `WaveManager`: Scene MonoBehaviour driving standard wave pacing in `Update`. Manages wave index (`_wave`), delay countdown (`_currentSpawnWaveDelay`), and batch growth multiplier (`_maxEnemiesInWaveMultiplier` = 1.2x). Injects `IOnRandomGridPosSpawner<EnemiesSpawner>`. Implements `IWaveFreezer` to allow pausing during swarm events.
  - `SwarmSpawner`: Scene component managing timed swarm events (`_minSwarmInterval` = 120s, `_maxSwarmInterval` = 180s). Freezes standard waves via `IWaveFreezer`, triggers warning cues via `ISwarmNotificationPresenter`, selects target swarm enemy types sequentially based on `_currentSwarmIndex`, and drives incremental swarm spawning over ticks.
  - `SwarmNotificationPresenter`: Implements `ISwarmNotificationPresenter`. Animates warning text via DOTween (`DOScale`, `DOPunchScale`) and controls screen dimming by lerping URP post-processing `LiftGammaGain` midtones (`_targetGamma` = 0.7f).
  - `EnemiesSpawner`: The main DI-bound spawner component. Manages `ObjectPool<Enemy>` per configured enemy type, processes standard and swarm spawn calls, filters candidate cells for visibility/occupancy, and maintains active entity count (`CurrentlySpawnedObjectsCount`).
  - `EnemiesSpawnChanceRedistributionSystem`: Helper instantiated by `EnemiesSpawner`. Shifts spawn probability weights after each standard wave batch so harder enemies progressively appear.
  - `EnemySpawnInfo` & `SpawnChanceInfo`: Data structures holding enemy prefabs, max pool amounts, spawn probability weights, and threshold triggers.
  - `IncreaseDifficultyTotem`: Interactive map totem that increases enemy spawn chance redistribution bonus (`RedistributionFactorBonus`) when activated.
- Key interfaces:
  - `IWaveFreezer`: Implemented by `WaveManager`. Allows `SwarmSpawner` to pause (`IsFrozen = true`) and resume standard wave delay counting.
  - `IOnRandomGridPosSpawner<EnemiesSpawner>`: Injected into `WaveManager` for standard wave spawns and reading `CurrentlySpawnedObjectsCount`.
  - `ISwarmEnemySpawner`: Implemented by `EnemiesSpawner`. Injected into `SwarmSpawner` to retrieve enemy configurations and trigger spawns for specific enemy types.
  - `ISwarmNotificationPresenter`: Injected into `SwarmSpawner` to present incoming countdowns, ongoing messages, and screen dimming.
  - `IEnemySpawnDifficultyController`: Injected into map objects (e.g. difficulty totem) to boost enemy chance redistribution speed.
- Runtime flow:
  - **Initialization**: `EnemiesSpawner.Awake` creates enemy object pools. `WaveManager.Start` sets `_currentSpawnWaveDelay` to `_firstWaveDelay` (1.0s). `EnemiesSpawner.Start` initializes redistribution and pre-warms pools. `SwarmSpawner.Start` picks initial `_nextSwarmTime` in range [120s, 180s].
  - **Standard Wave Flow**: `WaveManager.Update` counts down `_currentSpawnWaveDelay`. When the timer expires (or when `CurrentlySpawnedObjectsCount` drops to 0 after wave 1), `WaveManager` calls `SpawnAtRandomGridPos(_maxEnemiesInWave)`. `EnemiesSpawner` selects random walkable cells outside the player chunk using `GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk`, enforces cell occupancy limit `_maxEnemiesPerCell` (default: 2), retrieves enemies from pools, activates them, and triggers one step of chance redistribution. Wave size multiplies by 1.2x up to `ushort.MaxValue`.
  - **Swarm Flow**: `SwarmSpawner.Update` counts down swarm interval (`_nextSwarmTime`). Upon trigger, it freezes standard waves (`_waveFreezer.IsFrozen = true`), selects the next enemy type from `EnemyConfigs[clampedIndex]`, determines swarm size (`_minSwarmSize` 80 to `_maxSwarmSize` 100), and starts `SwarmCoroutine`. During the warning phase (`_swarmWarningDuration` = 10s), it displays HUD countdown text and dims screen gamma via `SwarmNotificationPresenter`. During the spawning phase (`_swarmDuration` = 5s, `_spawnTickInterval` = 1s), it calls `ISwarmEnemySpawner.SpawnSpecificEnemy` on tick intervals, placing enemies inside the player chunk on non-visible cells with optional spawn VFX (`_swarmSpawnVfxPrefab`). Upon completion, it unfreezes standard waves, restores screen gamma, and resets the cooldown timer.
  - **Release Flow**: Upon enemy death, `Enemy.OnRelease` returns the enemy to its pool, decrements `CurrentlySpawnedObjectsCount`, and fires `OnSpawnedEntityReleased`.

## Rules and Invariants

- Critical behavior rules:
  - Standard wave spawning occurs strictly outside the Player Chunk boundary.
  - Swarm spawning is permitted inside the Player Chunk boundary on non-visible cells.
  - Both standard and swarm spawning must respect cell occupancy limit `_maxEnemiesPerCell` (default: 2) to prevent overlapping on spawn.
  - Standard waves must remain frozen (`IsFrozen = true`) throughout the entire duration of a swarm event.
  - After wave 1, if active enemy count (`CurrentlySpawnedObjectsCount`) reaches 0, standard wave pacing bypasses remaining delay and triggers the next wave immediately.
  - Spawn chance redistribution runs exactly once per standard wave batch.
  - Newly spawned enemies must be placed out of the main camera's viewport on walkable grid cells.
- Ordering or sequencing guarantees:
  - Pools are created in `Awake` before redistribution and pre-warming in `Start`.
  - Swarm warning countdown ticks once per integer second before enemy spawning ticks begin.
- Constraints contributors must preserve:
  - Keep wave and spawner dependencies explicit through Reflex DI.
  - Preserve string spelling invariants on redistribution members (e.g. `TresholdToStartAddingSpawnChanceToOtherInfos`).

## Spawn Chance Redistribution Algorithm

The `EnemiesSpawnChanceRedistributionSystem` operates on configured `EnemySpawnInfo` entries:
1. **Threshold Assessment**: Entries update `HasEverReachedThreshold` when their chance reaches `TresholdToStartAddingSpawnChanceToOtherInfos`.
2. **Subtraction**: The current active source entry's `SpawnChance` is decreased by `_spawnChanceDecreaseFactor.GetRandomValueInRange() + RedistributionFactorBonus`.
3. **Shift**: If chance reaches 0, the active source shifts to the next list entry.
4. **Geometric Distribution**: The subtracted value is redistributed geometrically (50% to first eligible downstream entry, 25% to next, etc.) to progressively increase harder enemy weights.

## Extension Points

- **Tuning Waves & Swarms**: Adjust `_startSpawnWaveDelay`, `_maxEnemiesInWave`, `_minSwarmInterval`, `_maxSwarmInterval`, `_swarmWarningDuration`, `_swarmDuration`, and `_spawnTickInterval` via Inspector fields.
- **Adding Enemies**: Add prefabs and pool limits to `_poolEnemiesInfo` in `EnemiesSpawner`.
- **UI & Post-Processing**: Customize warning text animations or gamma dimming values (`_targetGamma`, `_gammaEnterDuration`, `_gammaExitDuration`) in `SwarmNotificationPresenter`.

## Integration Notes

- Upstream dependencies:
  - `IGridManager`: Supplies grid geometry and player chunk boundaries.
  - Reflex DI: Binds spawner, presenter, volume, and wave freezer interfaces.
  - URP `Volume` & `LiftGammaGain`: Handles screen gamma dimming during swarms.
- Downstream consumers:
  - `WaveManager` and `SwarmSpawner` drive enemy generation.
  - `EnemiesOutsidePlayerChunkTeleporter` teleports stray enemies back toward the player chunk.

## Known Risks and Open Questions

- **Clear-Triggered Gating Risk**: If an enemy gets stuck or fails to release, `CurrentlySpawnedObjectsCount` remains > 0, preventing immediate wave spawns and forcing full delay timers.
- **Spelling Invariants**: Preserved misspelling `TresholdToStartAddingSpawnChanceToOtherInfos` must be maintained across call-sites.

