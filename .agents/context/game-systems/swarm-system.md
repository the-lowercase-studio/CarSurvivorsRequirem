# Swarm System Documentation

## Purpose

The Swarm System manages high-density enemy spawning events that occur periodically. During a swarm event, the standard wave spawning process is paused, and a targeted warning countdown is shown to the player before a large batch of a specific enemy type is spawned incrementally.

Swarms are allowed to spawn enemies inside the Player Chunk (which is disallowed for classic wave spawning). Swarm spawning respects the configured cell occupancy limit (`_maxEnemiesPerCell`) to keep enemies from piling up on the exact same cells upon spawn.

It does not own standard wave pacing or the low-level object pooling of the enemies, which are managed by the Wave Manager and the Enemies Spawn System.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Assets/Scripts/UI/HUD/SwarmNotificationPresenter.cs
- Related docs:
  - .agents/context/game-systems/enemies-spawn-system.md
  - .agents/context/game-systems/waves-system.md
  - .agents/context/game-systems/ui-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - `SwarmSpawner`: The main control component. It handles the timer between swarms, pauses standard waves during active swarms, selects the target enemy type, and drives the spawning phases.
  - `SwarmNotificationPresenter`: UI component that animates warning/ongoing text messages and alters the screen tint via post-processing to build tension.
- Key interfaces:
  - `ISwarmNotificationPresenter`: Controls notification presentation phases (`ShowIncoming`, `ShowOngoing`, `Hide`).
  - `ISwarmEnemySpawner` (Enemies Spawn System): Exposes the list of configured enemy types and allows spawning specific enemy types directly.
  - `IWaveFreezer` (Wave Manager): Allows pausing and resuming standard wave pacing.
- Runtime flow:
  - **Cooldown Phase**: `SwarmSpawner.Update` counts down the swarm timer (`_nextSwarmTime`) when no swarm is active.
  - **Swarm Trigger**: When the timer expires, `StartSwarm` freezes standard waves (`_waveFreezer.IsFrozen = true`), selects the target enemy type sequentially using `_currentSwarmIndex`, computes the target size, and starts `SwarmCoroutine`.
  - **Warning Phase**: The coroutine runs for `_swarmWarningDuration` seconds, informing the player of the countdown using `ISwarmNotificationPresenter.ShowIncoming`.
  - **Spawning Phase**: The coroutine enters the spawning loop, calculating and triggering fractional enemy spawns at each `_spawnTickInterval` using `_swarmEnemySpawner.SpawnSpecificEnemy`. The spawner uses `GridCellsNotVisibleByMainCamera.GetRandomWalkableCells` within `GridPlayerChunk` to select candidate spawning positions, respecting the `_maxEnemiesPerCell` limit.
  - **Restoration**: Once all spawns complete, `EndSwarm` hides the UI, unfreezes standard waves, increments `_currentSwarmIndex`, and resets the cooldown timer.

## Rules and Invariants

- Critical behavior rules:
  - Standard waves must remain frozen (`IsFrozen = true`) from the moment a swarm starts until all spawning ticks finish.
  - Swarms must progress sequentially through the spawner's enemy configuration list using `_currentSwarmIndex` (clamped to the list length).
  - Swarm sizes are bounded by the enemy config's `MaxAmount`.
  - Swarms are allowed to spawn enemies inside the Player Chunk (centered on the player), but must respect the cell occupancy limit `_maxEnemiesPerCell` (default: 2) to limit overlapping upon spawn.
- Ordering or sequencing guarantees:
  - The warning countdown ticks once per integer second.
  - Spawning tick intervals must wait for `_spawnTickInterval` seconds between batches.
- Constraints contributors must preserve:
  - Maintain the clean interface boundaries between `SwarmSpawner` and its injected dependencies (`ISwarmEnemySpawner`, `IWaveFreezer`, and `ISwarmNotificationPresenter`).
  - Ensure post-processing screen gamma changes clean up correctly when the script or object is disabled.

## UI and Screen Dimming Integration

The `SwarmNotificationPresenter` integrates with standard UI and rendering pipelines:
1. **Text Animation**: Uses DOTween to scale up text using `DOScale` with an `Ease.OutBack` ease, followed by a continuous scale punch (`DOPunchScale`) to draw attention.
2. **Post-Processing (Gamma Midtones)**: Interacts with a URP `Volume` component containing a `LiftGammaGain` block. When a swarm is incoming, it tweens the gamma midtone value down to a configured `_targetGamma` (lerping over `_gammaEnterDuration`) to darken the screen. When the swarm ends, it lerps back to the original gamma value over `_gammaExitDuration`.
3. **Tween Management**: All active scale and post-processing tweens are explicitly killed in `OnDisable` to avoid DOTween memory leaks.

## Extension Points

- **Adjusting Intervals and Counts**: Expose or modify `_minSwarmInterval`, `_maxSwarmInterval`, `_minSwarmSize`, `_maxSwarmSize`, `_swarmWarningDuration`, and `_swarmDuration` in `SwarmSpawner`.
- **UI Customization**: Update the text templates, font sizes, animation speeds, or colors inside `SwarmNotificationPresenter`.

## Integration Notes

- Upstream dependencies:
  - `Volume` and `LiftGammaGain` (URP) are required for screen dimming.
  - Reflex DI binds `ISwarmNotificationPresenter`, `IWaveFreezer`, and `ISwarmEnemySpawner`.
- Downstream consumers:
  - `EnemiesSpawner` handles low-level instantiation of swarm enemies, enforcing `_maxEnemiesPerCell` density inside the player chunk.
  - `WaveManager` halts and resumes wave pacing based on the swarm state.

## Known Risks and Open Questions

- **Sequential enemy config clamping**: When `_currentSwarmIndex` exceeds the length of the spawner's configurations, it clamps to the last entry. This means late-game swarms will continuously spawn the final enemy configuration.
- **URP Volume Dependency**: If the post-processing volume is not assigned or lacks a `LiftGammaGain` component, the screen dimming tweens will fail silently, though text notifications will continue to work.
- **Immediate deactivation safety**: If the presenter GameObject is disabled abruptly, `OnDisable` restores the screen gamma to its original value, which is safe, but any active tweens are aborted immediately.
