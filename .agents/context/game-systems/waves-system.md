# Waves System Documentation

## Purpose

The Waves system owns gameplay wave pacing for enemy spawn batches. It decides when the next batch should spawn, how many enemies to request for that batch, and when the next batch size should grow.

It does not own enemy prefab selection, spawn chance redistribution, off-camera cell selection, enemy pooling, enemy movement, enemy death, experience payout, grid generation, or scene DI setup. Those responsibilities live in the enemy, grid, pooling, and Reflex installer systems.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Waves/WaveManager.cs`
- Related systems:
  - Enemy spawning and active enemy count: `Assets/Scripts/Enemies/EnemiesSpawner.cs`
  - Spawn contract: `Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs`
  - Active spawned object count contract: `Assets/Scripts/Spawners/ISpawnedObjectsCounter.cs`
  - Gameplay scene DI binding: `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`
  - Grid-based spawn placement: `Assets/Scripts/Navigation/GridSystem/`
- Related docs:
  - `.agents/context/game-systems/enemies-system.md`
  - `.agents/context/game-systems/grid-system.md`
  - `.agents/context/project-coding-standards.md`
  - `.agents/context/ai-game-dev-best-practices.md`
- Related agents or instructions:
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/architecture-review/SKILL.md`
  - `.agents/skills/check-optimalization/SKILL.md`
  - `.agents/skills/di-integration/SKILL.md`

## Architecture and Data Flow

- Core components:
  - `WaveManager` is a gameplay scene `MonoBehaviour` that runs every `Update`.
  - `_startSpawnWaveDelay` is the inspector-authored delay used after each spawned wave.
  - `_firstWaveDelay` is the current hard-coded initial delay before wave 1.
  - `_currentSpawnWaveDelay` is runtime countdown state.
  - `_maxEnemiesInWave` is the current requested batch size.
  - `_maxEnemiesInWaveMultiplier` grows the requested batch size after each wave.
  - `_wave` tracks the current wave number and starts at 1.
- Key interfaces:
  - `IOnRandomGridPosSpawner<EnemiesSpawner>` is injected into `WaveManager` and is the only dependency used to spawn enemies.
  - `IOnRandomGridPosSpawner<TSelf>` inherits `ISpawnedObjectsCounter`, so `WaveManager` can read `CurrentlySpawnedObjectsCount` from the same dependency it uses for spawning.
- Runtime flow:
  - `DefaultGameplaySceneInstaller` binds the scene `EnemiesSpawner` instance as `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - Reflex injects that spawner contract into `WaveManager`.
  - `WaveManager.Start` initializes `_currentSpawnWaveDelay` to `_firstWaveDelay`.
  - `WaveManager.Update` calls `WavesProcess`.
  - `WavesProcess` decrements `_currentSpawnWaveDelay` while the current gating rule allows time to pass.
  - When the countdown is not positive, `SpawnWave` requests `_maxEnemiesInWave` enemies through `SpawnAtRandomGridPos`, grows the next requested batch size, increments `_wave`, and resets `_currentSpawnWaveDelay` to `_startSpawnWaveDelay`.
  - `EnemiesSpawner` owns weighted enemy selection, pool retrieval, off-camera grid placement, spawn chance redistribution, and active enemy count updates.

## Rules and Invariants

- Critical behavior rules:
  - Waves must spawn enemies through `IOnRandomGridPosSpawner<EnemiesSpawner>` instead of directly instantiating enemies or querying scene objects.
  - The first wave is allowed to count down regardless of active enemy count.
  - After wave 1, the countdown only advances while `CurrentlySpawnedObjectsCount > 0`.
  - Wave spawn size grows after each spawn by `_maxEnemiesInWaveMultiplier`.
  - Requested wave size is capped at `ushort.MaxValue`.
  - Enemy type choice and placement are delegated to `EnemiesSpawner`; `WaveManager` only supplies the requested count.
- Ordering or sequencing guarantees:
  - `_currentSpawnWaveDelay` is initialized in `Start` before normal `Update` wave processing.
  - Spawn chance redistribution happens inside `EnemiesSpawner.SpawnAtRandomGridPos` after the batch spawn loop, not in `WaveManager`.
  - Active enemy count is incremented and decremented by `EnemiesSpawner` get/release lifecycle, so wave pacing depends on pooled release correctness.
- Constraints contributors must preserve:
  - Keep wave dependencies explicit through Reflex where DI is already established.
  - Treat delay, batch size, and multiplier changes as player-facing balance changes.
  - Do not move enemy selection, enemy configuration, or grid placement logic into `WaveManager`.
  - Preserve inspector-driven tuning for `_startSpawnWaveDelay` unless a deliberate data migration exposes more wave settings.

## Extension Points

- Safe extension areas:
  - Expose initial wave delay, starting enemy count, or growth multiplier as serialized fields if designers need tuning control.
  - Add wave-complete events if UI, audio, or analytics need to observe wave transitions.
  - Add difficulty curves or ScriptableObject wave definitions if the design moves from formula-based growth to authored wave progression.
  - Add pause/game-state checks around countdown progress if gameplay gains a central runtime state service.
- Required dependencies and contracts:
  - Any replacement enemy wave target must implement `IOnRandomGridPosSpawner<TSelf>` or provide an equivalent narrow injected contract.
  - `CurrentlySpawnedObjectsCount` must stay accurate for every enemy get/release path, because wave timing reads it directly.
  - The gameplay scene installer must bind the concrete spawner instance used by the scene.
- Testing implications:
  - Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, validate the first wave delay, subsequent wave delay behavior, wave size growth, off-camera spawning, and behavior when all enemies are killed before the next delay expires.
  - For balance changes, compare early survival pressure, spawn density, and time-to-overwhelm against the previous baseline.

## Integration Notes

- Upstream dependencies:
  - Reflex scene installation provides the spawner contract.
  - Unity `Time.deltaTime` drives countdown progress.
  - Enemy pool release updates `CurrentlySpawnedObjectsCount`, which controls whether later wave countdowns advance.
- Downstream consumers:
  - `EnemiesSpawner` consumes wave spawn requests and performs actual spawning.
  - Enemy movement, health, death, and experience systems react to the enemies spawned by each wave.
  - UI timer/score systems may indirectly reflect wave pressure but are not currently driven by wave events.
- Cross-system coupling risks:
  - Wave pacing currently depends on active enemy count, so release bugs in enemy death or pooling can stall or accelerate wave timing.
  - `WaveManager` has hard-coded initial delay, initial batch size, and growth multiplier values, so balance tuning currently requires code changes except for `_startSpawnWaveDelay`.

## Known Risks and Open Questions

- Known limitations:
  - After wave 1, if all enemies are released before `_currentSpawnWaveDelay` reaches zero, the countdown stops because `CurrentlySpawnedObjectsCount` is no longer greater than zero.
  - `_firstWaveDelay`, `_maxEnemiesInWave`, `_maxEnemiesInWaveMultiplier`, and `_wave` are private runtime fields, so designers cannot tune or inspect them in the Unity Inspector.
  - `_wave` is only used for first-wave gating and is not exposed to UI, score, analytics, or debugging.
  - Batch size growth truncates fractional results when casting back to `ushort`.
- Open design questions:
  - Should later waves count down while no enemies are alive, or is the current active-enemy gate intentional breathing room?
  - Should wave progression be formula-based, curve-based, or authored through ScriptableObjects?
  - Should wave count and next-wave countdown be exposed to UI or diagnostics?
- Suggested follow-up tasks:
  - Confirm the intended post-wave countdown behavior with gameplay design.
  - Consider exposing wave tuning fields or moving them into a ScriptableObject after balance requirements are clearer.
  - Add a focused play-mode test or debug harness for first-wave timing, active-count gating, and wave size growth.
