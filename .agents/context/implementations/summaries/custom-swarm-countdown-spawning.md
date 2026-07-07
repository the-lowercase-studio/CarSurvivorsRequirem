# Swarm Warning Countdown, Spawning Distribution, and Post-Processing Volume Summary

Date: 2026-07-05

This summary details the modifications made to support custom swarm warning countdowns, rate-divided enemy spawning distribution, URP post-processing integration, and scaling optimizations for high enemy counts.

---

## 1. Goals Achieved
* **Warning Countdown**: Changed the swarm notification text to display `"Swarm Incoming in Xs"` and count down dynamically every second. Each countdown tick executes a rhythmic DOTween scale pulse.
* **Ongoing Indicator**: Replaced the continuous pulsing loop with a single scale punch on start showing `"Swarm ongoing"`, which shrinks down and disables upon swarm completion.
* **Rate-Divided Spawning**: Replaced the random batch spawner delays with a duration-based progressive spawner that divides and distributes the spawning evenly over a configurable duration.
* **Increased Swarm Size**: Scaled the default swarm size range from `40-60` to **`80-100`** enemies.
* **Post-Processing Integration & Corrective Fix**: Integrated URP Lift Gamma Gain control. The screen gamma transitions slowly to a darker look (using a target offset of `-0.7f`) over the duration of the warning countdown, stays there during the swarm, and smoothly fades back to original. Added auto-negation code to enforce negative offset values (which URP requires to darken the screen, rather than positive values that wash the screen out).
* **Batched Cell Gathering (Optimization)**: Eliminated the expensive $O(W \times H \times N)$ camera visibility check in the pool get callback. Instead, cell visibility is queried **once** as a single batch per spawning frame using `GetRandomWalkableCells(..., count)`, bringing complexity down to $O(W \times H + N)$ and eliminating CPU spikes during large spawns.
* **Object Pool Pre-Warming (Optimization)**: Added a pool pre-warming stage to instantiate and cache all enemy prefabs up to their `MaxAmount` limit at scene start, preventing runtime instantiation lags.
* **Reflex Dependency Injection**: Registered the `Volume` component in the Reflex gameplay installer to enforce explicit ownership boundaries and avoid direct scene lookups.

---

## 2. Implementation Details

### UI HUD Notification Component
#### Assets/Scripts/UI/HUD/SwarmNotificationPresenter.cs
* Replaced `Show()` with `ShowIncoming(int secondsRemaining)` and `ShowOngoing()`.
* Changed default `_targetGamma` to `-0.7f` (added tooltip describing the negative direction for URP).
* Added check in `Start()` to automatically negate `_targetGamma` if set to a positive value in the inspector (safeguarding existing serialized components).
* Cached starting `LiftGammaGain` color offset at `Start()` and implemented `TweenGamma(float targetVal, float duration)`.

### DI Container Installer
#### Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
* Bound the `Volume _postProcessVolume` field as a singleton.

### Swarm Spawner Component
#### Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
* Updated defaults: `_minSwarmSize = 80`, `_maxSwarmSize = 100`.
* Refactored `SwarmCoroutine` to count down warning times via `Time.deltaTime` and distribute spawned counts progressively over ticks.

### Enemies Spawner Component
#### Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
* Added `PreWarmPools()` in `Start()` to pre-instantiate and return `MaxAmount` instances of each enemy pool configuration.
* Removed cell search/position settings from `OnEnemyGet()`.
* Refactored `SpawnAtRandomGridPos()`, `SpawnSpecificEnemy()`, and `SpawnRandomEnemiesBasedOnSpawnChance()` to batch-gather off-screen cells via `GridCellsNotVisibleByMainCamera.GetRandomWalkableCells()` and assign positions sequentially to pooled instances.

---

## 3. Editor Setup Checklist
1. Open the **RuinedBloodCity** scene in the Unity Editor.
2. In the inspector for the **DefaultGameplaySceneInstaller** GameObject, assign the scene's **Global Volume** component into the **Post Process Volume** reference slot.
3. Verify that the **SwarmSpawner** has its values set (e.g., Min Swarm Size = `80`, Max Swarm Size = `100`).
4. Ensure the **EnemiesSpawner** pool configurations (`EnemyConfigs`) have their `MaxAmount` values set appropriately (pre-warming will pre-instantiate these quantities).

