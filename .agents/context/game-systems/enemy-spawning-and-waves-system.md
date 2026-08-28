# Enemy Spawning and Waves System Documentation

## Purpose

The Enemy Spawning and Waves system controls gameplay pacing, enemy generation, and spatial distribution for combat encounters in Car Survivors. It manages classic wave progression, high-density swarm event warnings and burst triggers, weighted enemy probability selection, progressive spawn chance redistribution, off-camera grid placement, object pooling for enemy entities, cell occupancy density limits, boss encounter swarm suppression, and off-chunk enemy teleporter reconciliation.

It divides enemy spawning into two pacing modes:
1. Standard Wave Pacing: Periodic batch spawning outside the active player chunk boundary with exponential scaling.
2. Swarm Event Pacing: Periodic high-density spawn events that temporarily freeze standard waves, play a UI warning countdown with screen gamma midtone dimming, and spawn targeted enemy types inside the player chunk on non-visible cells.

It does not own individual enemy combat AI, movement behaviors, or individual enemy damage/health logic. Those responsibilities are owned by the Enemy System (enemies-system.md). Boss encounter execution is owned by the Golem Boss System (golem-boss-system.md).

## Reading Map

- Primary code locations:
  - Assets/Scripts/Waves/WaveManager.cs
  - Assets/Scripts/Waves/WaveConfig.cs
  - Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Assets/Scripts/UI/HUD/SwarmNotificationPresenter.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
  - Assets/Scripts/Spawners/SpawnChanceInfo.cs
  - Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs
  - Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs
  - Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs
- Designer-authored data:
  - Assets/ScriptableObjects/Waves/WaveConfig.cs
  - Enemy prefabs and pool limits referenced by EnemiesSpawner._poolEnemiesInfo
  - Swarm spawn VFX prefab referenced by EnemiesSpawner._swarmSpawnVfxPrefab
- DI installer:
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related systems:
  - Boss management: Assets/Scripts/Enemies/Bosses/BossManager.cs
  - Enemy entities and drops: Assets/Scripts/Enemies/Base/Enemy.cs, Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Grid geometry and visibility: Assets/Scripts/Navigation/GridSystem/GridManager.cs, Assets/Scripts/Navigation/GridSystem/CellCameraVisibilityChecker.cs
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md

## Architecture and Data Flow

- Core components:
  - WaveManager: Scene MonoBehaviour driving standard wave pacing in Update. Evaluates wave delay countdown (_currentSpawnWaveDelay) and batch scaling multiplier (_maxEnemiesInWaveMultiplier = 1.2x). Injects IOnRandomGridPosSpawner<EnemiesSpawner>. Implements IWaveFreezer to pause progression during swarm events.
  - WaveConfig: ScriptableObject defining wave pacing parameters: _startSpawnWaveDelay (default 8s), _firstWaveDelay (default 1s), _initialMaxEnemiesInWave (default 4), and _maxEnemiesInWaveMultiplier (default 1.2x).
  - SwarmSpawner: Scene component managing timed high-density swarm events (_minSwarmInterval = 120s, _maxSwarmInterval = 180s). Freezes standard waves via IWaveFreezer, triggers warning and HUD presentation via ISwarmNotificationPresenter, selects target swarm enemy types sequentially based on _currentSwarmIndex, and drives incremental swarm spawning over ticks. Implements ISwarmFreezer to allow boss encounters to suppress swarms.
  - SwarmNotificationPresenter: Implements ISwarmNotificationPresenter. Animates HUD warning text via DOTween (DOScale, DOPunchScale) and dims screen midtones by lerping URP post-processing LiftGammaGain (_targetGamma = 0.7f).
  - EnemiesSpawner: The central DI-bound spawner component. Manages Unity ObjectPool<Enemy> per configured EnemySpawnInfo, processes standard and swarm spawn calls, filters candidate cells for visibility and occupancy, and maintains active entity count (CurrentlySpawnedObjectsCount).
  - EnemiesSpawnChanceRedistributionSystem: Pure C# system helper initialized by EnemiesSpawner. Dynamically redistributes spawn probability weights after each standard wave batch so harder enemies progressively appear.
  - EnemySpawnInfo & SpawnChanceInfo: Data structures holding enemy prefabs, max pool amounts, spawn probability weights, threshold flags, and fixed-probability overrides.
  - GridCellsNotVisibleByMainCamera: Static utility for camera-culled walkable cell sampling. Validates cell walkability, tests frustum visibility, checks occupancy caps using Physics.OverlapBoxNonAlloc against EntityLayers.Enemies, and expands search radii outside the player chunk.
  - EnemiesOutsidePlayerChunkTeleporter: Periodic maintenance component (_checkForEnemiesOutsidePlayerChunkDelay = 2s) that queries enemies straying outside the active player chunk and teleports them to random hidden walkable cells inside the chunk, resetting vertical velocity.
  - IncreaseDifficultyTotem: Interactive map totem that calls IEnemySpawnDifficultyController to add a flat difficulty boost (RedistributionFactorBonus) to spawn chance redistribution.
  - BossManager: Scene manager that triggers boss encounters and suppresses swarm events by toggling ISwarmFreezer.IsSuppressed = true while the boss is alive.
- Key interfaces:
  - IWaveFreezer: Implemented by WaveManager. Allows SwarmSpawner to pause (IsFrozen = true) and resume standard wave delay counting.
  - ISwarmFreezer: Implemented by SwarmSpawner. Allows BossManager to suppress (IsSuppressed = true) swarm event timers during boss fights.
  - IOnRandomGridPosSpawner<EnemiesSpawner>: Injected into WaveManager for standard wave spawns and reading CurrentlySpawnedObjectsCount.
  - ISwarmEnemySpawner: Implemented by EnemiesSpawner. Injected into SwarmSpawner to retrieve enemy configurations (EnemyConfigs) and trigger spawns for specific enemy types.
  - ISwarmNotificationPresenter: Injected into SwarmSpawner to present incoming countdowns, ongoing burst notices, and screen dimming.
  - IEnemySpawnDifficultyController: Injected into map interactables (e.g. IncreaseDifficultyTotem) to boost enemy chance redistribution speed.
- Runtime flow:
  - Initialization:
    1. EnemiesSpawner.Awake creates an ObjectPool<Enemy> for each configured EnemySpawnInfo with defaultCapacity and maxSize matching MaxAmount.
    2. WaveManager.Start loads parameters from WaveConfig (or defaults), setting _currentSpawnWaveDelay to _firstWaveDelay (1.0s) and _maxEnemiesInWave to _initialMaxEnemiesInWave (4).
    3. EnemiesSpawner.Start initializes EnemiesSpawnChanceRedistributionSystem and pre-warms all pools by retrieving and immediately releasing MaxAmount instances for each enemy type.
    4. SwarmSpawner.Start chooses the initial _nextSwarmTime within range [_minSwarmInterval, _maxSwarmInterval] (120s to 180s).
  - Standard Wave Flow:
    1. WaveManager.Update monitors _currentSpawnWaveDelay when not frozen.
    2. Delay countdown: If _wave == 1 or CurrentlySpawnedObjectsCount > 0, _currentSpawnWaveDelay decrements by Time.deltaTime. If CurrentlySpawnedObjectsCount drops to 0 after wave 1, the timer is bypassed and the next wave spawns immediately.
    3. WaveManager calls _enemiesSpawner.SpawnAtRandomGridPos(_maxEnemiesInWave).
    4. GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk samples cells outside the player chunk, expanding buffer radius if needed and filtering for cell occupancy (_maxEnemiesPerCell = 2).
    5. For each cell, EnemiesSpawner selects an enemy type using weighted roulette sampling based on SpawnChanceInfo.SpawnChance, retrieves an instance from the corresponding pool, activates it, positions it at the cell world position, and increments CurrentlySpawnedObjectsCount.
    6. EnemiesSpawner triggers _enemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance(), advancing spawn chances for subsequent waves.
    7. WaveManager multiplies _maxEnemiesInWave by _maxEnemiesInWaveMultiplier (1.2x), clamped to ushort.MaxValue, and increments _wave.
  - Swarm Event Flow:
    1. SwarmSpawner.Update decrements _nextSwarmTime when no swarm is active and IsSuppressed is false.
    2. When _nextSwarmTime <= 0, SwarmSpawner freezes standard waves (_waveFreezer.IsFrozen = true), selects the target enemy type from EnemyConfigs[clampedIndex], picks swarm size (_minSwarmSize = 80 to _maxSwarmSize = 100, clamped to MaxAmount), and launches SwarmCoroutine.
    3. Warning Phase (_swarmWarningDuration = 10s): SwarmNotificationPresenter displays countdown text and tweens URP LiftGammaGain midtones toward _targetGamma (0.7f).
    4. Spawning Phase (_swarmDuration = 5s, _spawnTickInterval = 1s): SwarmNotificationPresenter switches to ongoing text. Over ticks, SwarmSpawner calls _swarmEnemySpawner.SpawnSpecificEnemy, which samples hidden walkable cells inside the player chunk and spawns enemies (instantiating _swarmSpawnVfxPrefab and spawning upon VFX completion if configured).
    5. Completion: SwarmNotificationPresenter.Hide restores screen gamma and scales text to zero, _waveFreezer.IsFrozen is set to false, _currentSwarmIndex is incremented, and _nextSwarmTime is reset.
  - Teleportation Reconciliation Flow:
    1. EnemiesOutsidePlayerChunkTeleporter periodically inspects all active enemies in _enemiesHolder.
    2. Any enemy whose position lies beyond the player chunk dimensions is identified.
    3. The teleporter samples hidden walkable cells inside the player chunk via GridCellsNotVisibleByMainCamera.FillWalkableCells, shuffles them, repositions the stray enemies, and calls ResetVerticalVelocity() on their movement controllers.
  - Release Flow:
    1. Upon enemy death and presentation completion, Enemy raises OnCanBeReleased.
    2. EnemiesSpawner receives the event, releases the enemy back to its mapped ObjectPool<Enemy>, deactivates the GameObject, decrements CurrentlySpawnedObjectsCount, and raises OnSpawnedEntityReleased.

## Rules and Invariants

- Critical behavior rules:
  - Standard wave spawning occurs strictly outside the active player chunk boundary on walkable grid cells hidden from camera view.
  - Swarm spawning occurs inside the active player chunk boundary on walkable grid cells hidden from camera view.
  - Both spawning modes enforce cell occupancy limits (_maxEnemiesPerCell = 2) via non-allocating physics overlap boxes to prevent enemy overlap on spawn.
  - Standard waves must remain frozen (IsFrozen = true) throughout the entire duration (warning + spawning) of a swarm event.
  - Active boss encounters suppress swarm event progression (IsSuppressed = true) until the boss is defeated.
  - Clear-Triggered Pacing: After wave 1, if active enemy count (CurrentlySpawnedObjectsCount) reaches 0, standard wave pacing bypasses the remaining delay and spawns the next wave immediately.
  - Spawn chance redistribution runs exactly once per standard wave batch.
  - Pool instances must be pre-warmed to MaxAmount during Start() to eliminate runtime instantiation hitches.
- Ordering or sequencing guarantees:
  - Object pools are created in Awake() before redistribution initialization and pool pre-warming in Start().
  - Swarm warning countdown ticks once per integer second before burst spawning ticks begin.
  - Swarm completion unfreezes standard waves and restores post-processing gamma before scheduling the next swarm interval.
- Constraints contributors must preserve:
  - Keep wave, swarm, and spawner dependencies explicit through Reflex DI bindings.
  - Preserve field naming and spelling on serialized members (e.g. TresholdToStartAddingSpawnChanceToOtherInfos, SpawnChanceWillNotChange) to prevent asset data loss.
  - Ensure all cell sampling checks CellStatusDescriber.IsWalkable and CellCameraVisibilityChecker.IsCellVisibleFromCamera.

## Spawn Chance Redistribution Algorithm

The EnemiesSpawnChanceRedistributionSystem executes after each standard wave batch:
1. Threshold Assessment: Evaluates configured EnemySpawnInfo entries and sets HasEverReachedThreshold = true when SpawnChance >= TresholdToStartAddingSpawnChanceToOtherInfos.
2. Active Source Selection: Operates on entries where SpawnChanceWillNotChange is false, starting at index 0.
3. Subtraction: Calculates spawnChanceScalar = _spawnChanceDecreaseFactor.GetRandomValueInRange() + RedistributionFactorBonus.
4. Redistribution:
   - If the current source's SpawnChance > spawnChanceScalar, it is decremented by spawnChanceScalar, and that amount is distributed to eligible downstream entries.
   - If the current source's SpawnChance <= spawnChanceScalar, its remaining chance is zeroed and distributed, and the active source advances to the next entry in the list.
5. Geometric Cascade: The distributed amount is split geometrically across eligible downstream entries where HasEverReachedThreshold is true (50% to first eligible entry, 25% to second, etc., with the remainder assigned to the final eligible entry). If no downstream entries are eligible, the full amount is assigned to the last entry in the list.
6. Dynamic Difficulty Boost: When the player activates an IncreaseDifficultyTotem, RedistributionFactorBonus is incremented, accelerating the rate at which harder enemies replace easier ones.

## Extension Points

- Wave Configuration: Create or edit WaveConfig ScriptableObjects to tune initial wave delays, standard delays, initial wave sizes, and exponential multipliers.
- New Enemy Types:
  1. Author an enemy prefab with required components and EnemyConfigSO.
  2. Add an EnemySpawnInfo entry to EnemiesSpawner._poolEnemiesInfo with desired prefab, MaxAmount, initial SpawnChance, and threshold values.
- Swarm Tuning: Adjust _minSwarmInterval, _maxSwarmInterval, _minSwarmSize, _maxSwarmSize, _swarmWarningDuration, _swarmDuration, and _spawnTickInterval in SwarmSpawner.
- Post-Processing & Presentation: Customize warning text animations, gamma midtone targets (_targetGamma), and transition durations in SwarmNotificationPresenter.
- Interactable Difficulty Modifiers: Implement new map interactables calling IEnemySpawnDifficultyController.IncreaseSpawnChanceRedistributionFactor.

## Integration Notes

- Upstream dependencies:
  - IGridManager: Supplies WorldGrid and GridPlayerChunk for cell sampling, walkability queries, and chunk bounds.
  - Camera: Injected main camera used for viewport visibility culling.
  - Reflex DI: Injects spawner, presenter, volume, and wave freezer interfaces via DefaultGameplaySceneInstaller.
  - URP Volume & LiftGammaGain: Controls screen midtone gamma dimming during swarm warnings.
  - DOTween: Controls HUD notification scale animations and post-processing gamma tweening.
- Downstream consumers:
  - WaveManager and SwarmSpawner drive enemy generation through EnemiesSpawner.
  - BossManager suppresses swarms via ISwarmFreezer.
  - IncreaseDifficultyTotem modifies spawn progression via IEnemySpawnDifficultyController.
  - EnemiesOutsidePlayerChunkTeleporter maintains enemy proximity to the player.
- Cross-system coupling risks:
  - Clear-Triggered Gating Deadlock: If an enemy entity gets stuck or fails to trigger its release callback upon death, CurrentlySpawnedObjectsCount will remain > 0, preventing immediate wave spawns and forcing full wave countdown delays.
  - Serialized Asset Mutation: Runtime mutation of SpawnChanceInfo modifies scriptable data in-memory; ensure runtime chance reset is handled between Play Mode sessions if serialized assets are modified directly.
  - Post-Processing Requirement: SwarmNotificationPresenter expects a Volume profile containing a LiftGammaGain component; if missing, screen dimming is safely bypassed but warning text remains active.

## Known Risks and Open Questions

- Known limitations:
  - ObjectPool.Release vs Manual Event Flow: In EnemiesSpawner, Enemy_OnRelease looks up the instance in _instancePoolMap to call pool.Release(enemy); if not found, it falls back to OnEnemyRelease(enemy). Care should be taken when modifying pool lifecycle events.
  - Spelling Invariants: Preserved misspelling TresholdToStartAddingSpawnChanceToOtherInfos must be maintained across all call sites to ensure inspector data compatibility.
- Open design questions:
  - Should spawn chance redistribution operate on cloned runtime data structures to isolate inspector defaults completely from runtime state?
  - Should stray enemy teleportation include an arrival VFX/SFX telegraph similar to swarm spawns?
