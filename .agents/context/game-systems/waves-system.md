# Waves System Documentation

## Purpose

The Waves System owns standard gameplay wave pacing for enemy spawn batches. It decides when the next batch should spawn, how many enemies to request for that batch, and how the next batch size should grow.

It does not own enemy pooling, weighted probability selection, off-camera placement, or swarm events. Those responsibilities live in the enemies spawn system and the swarm system.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Waves/WaveManager.cs
- Related systems:
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs
  - Assets/Scripts/Spawners/ISpawnedObjectsCounter.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/enemies-spawn-system.md
  - .agents/context/game-systems/swarm-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/spawners-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - `WaveManager`: A gameplay scene MonoBehaviour that runs every `Update`. It maintains the state for wave indexing, spawn delays, and enemy counts.
- Key interfaces:
  - `IWaveFreezer`: Exposes the `IsFrozen` property, allowing other systems (like the Swarm System) to temporarily pause standard wave pacing.
  - `IOnRandomGridPosSpawner<EnemiesSpawner>`: Injected dependency used to request wave spawns and read `CurrentlySpawnedObjectsCount` (inherited via `ISpawnedObjectsCounter`).
- Runtime flow:
  - **Setup**: Reflex injects the spawner dependency. `WaveManager.Start` sets `_currentSpawnWaveDelay` to `_firstWaveDelay`.
  - **Pacing**: `WaveManager.Update` runs `WavesProcess` which counts down `_currentSpawnWaveDelay` if the gating condition is met.
  - **Clear-Triggered Immediate Spawning**: After wave 1, if `CurrentlySpawnedObjectsCount` drops to 0, standard wave pacing bypasses the remaining delay timer and immediately spawns the next wave.
  - **Spawn batch & growth**: When spawning a wave, `WaveManager` calls `SpawnAtRandomGridPos`, grows the requested batch size (`_maxEnemiesInWave` multiplied by `_maxEnemiesInWaveMultiplier`, capped at `ushort.MaxValue`), increments `_wave`, and resets the delay to `_startSpawnWaveDelay`.

## Rules and Invariants

- Critical behavior rules:
  - Standard waves must spawn enemies through `IOnRandomGridPosSpawner<EnemiesSpawner>` rather than directly instantiating prefabs or querying active scenes.
  - Standard wave pacing can be frozen by setting `IsFrozen = true` via `IWaveFreezer`.
  - The first wave is allowed to count down regardless of active enemy count.
  - The next wave triggers immediately if the active enemy count drops to 0 (after wave 1).
- Ordering or sequencing guarantees:
  - `_currentSpawnWaveDelay` is initialized in `Start` before normal `Update` wave processing.
  - Wave size growth casting to `ushort` truncates fractional values.
- Constraints contributors must preserve:
  - Keep wave dependencies explicit through Reflex.
  - Do not move enemy selection, enemy configuration, or grid placement logic into `WaveManager`.

## Extension Points

- **Tuning standard waves**: Adjust `_startSpawnWaveDelay` in the Unity Inspector. `_firstWaveDelay`, `_maxEnemiesInWave`, and `_maxEnemiesInWaveMultiplier` can be exposed to the Inspector if designers require tuning control.
- **Difficulty Curve**: Transition the wave size growth from a hardcoded multiplier to a designer-authored ScriptableObject curve.

## Integration Notes

- Upstream dependencies:
  - Reflex provides the bound spawner instance.
  - `EnemiesSpawner` updates `CurrentlySpawnedObjectsCount` when enemies are retrieved or released.
- Downstream consumers:
  - `EnemiesSpawner` processes standard wave spawn requests.
  - `SwarmSpawner` freezes the wave pacing using `IWaveFreezer` during swarm events.

## Known Risks and Open Questions

- **Immediate Spawn Gating**: If an enemy gets stuck (e.g., falls out of bounds or fails to release properly), `CurrentlySpawnedObjectsCount` remains greater than 0, preventing the clear-triggered immediate spawning and forcing waves to wait out the full `_startSpawnWaveDelay`.
- **Private Tuning Constants**: Critical balance parameters (`_firstWaveDelay`, `_maxEnemiesInWave`, and `_maxEnemiesInWaveMultiplier`) are hardcoded private fields and cannot be modified without recompiling.
- **Batch Size Truncation**: High wave counts can cause wave growth calculations to overflow/truncate at `ushort.MaxValue`.
