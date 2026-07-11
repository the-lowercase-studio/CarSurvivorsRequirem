# VFX System Documentation

## Purpose

The VFX system manages the execution, lifetime, scale, and playback speed of visual effects (particle systems) and vehicle-specific environmental effects (brake lights, speed trails).

It is responsible for:
- Providing a generic, play-and-forget interface for particle-based visual effects through Assets/Scripts/VFX/VFXPlayer.cs.
- Managing the lifecycle of instantiated visual effects (e.g. self-destruction via config options).
- Emitting events when the longest active particle duration in a VFX prefab finishes.
- Controlling vehicle-specific visual feedback (brake lights, material glow, and motion trails) via Assets/Scripts/Player/Car/CarVfxEffectsController.cs responding to vehicle inputs.

It is not responsible for:
- Deciding when or where to spawn visual effects (handled by gameplay components like enemies, player handlers, totems, and turrets).
- Managing particle asset design, textures, shaders, or rendering pipelines (handled by Unity and URP).
- Managing damage numbers formatting or spawning (handled by the Damage Numbers system).

## Reading Map

- Primary code locations:
  - Assets/Scripts/VFX/VFXPlayer.cs
  - Assets/Scripts/Player/Car/CarVfxEffectsController.cs
- Related assets:
  - Assets/VFX/Vfx_LandmineExplosion.prefab
  - Assets/VFX/Vfx_LaserCumulating.prefab
  - Assets/VFX/Vfx_MuzzleFlesh.prefab
  - Assets/VFX/Player/PlayerDamagedVfx.prefab
  - Assets/VFX/Player/PlayerDeathVfx.prefab
  - Assets/VFX/Spawning/Vfx_SingleEnemySpawnDuringSwarm.prefab
  - Assets/VFX/Interactables/Vfx_DifficultyTotemUssage.prefab
  - Assets/VFX/Zombies/Vfx_BloodHit.prefab
  - Assets/VFX/Zombies/Vfx_CellsExplosion.prefab
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/game-systems/pooling-and-object-lifecycle-system.md
  - .agents/context/game-systems/car-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md

## Architecture and Data Flow

- Core components:
  - **VFXPlayer**: A MonoBehaviour script attached to VFX prefabs. On `Awake`, it caches all child `ParticleSystem` components. In `Play`, it iterates through them, updates their local scale and simulation speed based on `VFXPlayConfig`, triggers playback, and starts a repeating invoke to monitor completion.
  - **VFXPlayConfig**: A configuration class containing parameters for playing a VFX (`Scale`, `SimulationSpeed`, `DestroyOnEnd`).
  - **CarVfxEffectsController**: Listens to the `ICarController`'s brake events to toggle emission on the stop-light material ("CarStopLights" material name, setting "IsGlowing" float parameter) and backlights holder. It also monitors car velocity, enabling `TrailRenderer` emission when the speed exceeds a threshold.
- Key interfaces:
  - **IVFXPlayer**: Exposes the `Play(VFXPlayConfig config)` method, `GetLongestParticleDuration()`, and the `OnVFXFinished` event.
- Runtime flow:
  - **Generic Particle Playback**:
    1. A gameplay system (e.g. `Enemy`, `PlayerDeathHandler`, or a turret) acquires a reference to a `VFXPlayer` (either serialized or instantiated).
    2. The caller calls `Play` with a new `VFXPlayConfig` (e.g. passing scaling factors or specifying `DestroyOnEnd`).
    3. `VFXPlayer` sets up scale and speed overrides on all caching particle systems, plays them, and schedules a completion callback via `InvokeRepeating` matching the longest particle's main duration.
    4. Upon completion, `OnVFXFinished` is invoked, and if `DestroyOnEnd` is set, the GameObject is destroyed.
  - **Swarm Spawn VFX Pacing**:
    1. `EnemiesSpawner` instantiates a swarm spawn VFX prefab at the target grid cell.
    2. It calls `Play` with `destroyOnEnd: true` and subscribes to `OnVFXFinished`.
    3. The enemy prefab is only retrieved from the pool and placed *after* the spawn VFX completes.
  - **Laser Charge-up Pacing**:
    1. `LasergunTurret` calls `Play` on the cumulating VFX when `Shoot` is triggered.
    2. It listens to `OnVFXFinished` to fire the actual laser beam, achieving a charging effect.
  - **Car Effects Loop**:
    1. `CarVfxEffectsController` subscribes to brake events on `Awake`/`OnEnable` and starts a repeat invoke (`ActivateSpeedTrailWhenSpeedExceedsThreshold`) every 0.1s.
    2. When the player brakes, the stop light material parameter `IsGlowing` is set to `1.0f` and the backlight GameObject is enabled.
    3. If the car's speed is above `_thresholdToStartSpeedTrail`, `emitting` is enabled on the tail renderers.

## Rules and Invariants

- Critical behavior rules:
  - `VFXPlayer` computes `_longestParticleDuration` from `main.duration` of the child particle systems. Note that if particle systems have looping enabled or custom start lifespans, `main.duration` represents the emission cycle, not necessarily the active lifetime of all particles.
  - `VFXPlayConfig.Scale` modifies `transform.localScale` of each individual child particle system rather than the root parent GameObject.
- Ordering or sequencing guarantees:
  - Swarm enemy spawning is strictly delayed by the duration of the spawn VFX.
  - Enemy pooling release is delayed by `EnemyDeathHandler` until both the death VFX (`OnVFXFinished`) and audio clip (`OnAudioClipFinished`) have completed.
- Constraints contributors must preserve:
  - Always clean up event subscriptions (e.g. unsubscribe from `OnVFXFinished` in `OnDisable` or upon completion) to avoid memory leaks.
  - Keep vehicle feedback visual states synchronized with `ICarController` inputs.
  - Do not use direct `Destroy` calls on pooled VFX objects; instead, let the pool manager handle their lifecycle, or use `DestroyOnEnd` only for non-pooled, instantiated-on-the-fly VFX prefabs (like swarm spawning).

## Extension Points

- Safe extension areas:
  - Creating new visual effects by constructing a prefab with Unity `ParticleSystem` components and adding the `VFXPlayer` script.
  - Adding player feedback effects by creating a custom handler listening to event hooks (like damage or skill activation) and calling `IVFXPlayer.Play`.
- Required dependencies and contracts:
  - Prefabs carrying the `VFXPlayer` component must contain one or more child `ParticleSystem` components.
  - Custom vehicle controllers must implement `ICarController` to support `CarVfxEffectsController`.
- Testing implications:
  - Ensure any new VFX prefab behaves correctly when scaled up/down (e.g. verify particle sizes scale proportionally).
  - Verify that `OnVFXFinished` fires exactly when expected and doesn't get cut off or trigger prematurely.

## Integration Notes

- Upstream dependencies:
  - Unity Physics and Input System trigger vehicle changes consumed by `CarVfxEffectsController`.
  - `ICarController` handles vehicle inputs and velocity.
- Downstream consumers:
  - `EnemyDeathHandler` and `PlayerDeathHandler` rely on VFX completion before transitioning state or releasing pooled enemies.
  - `LasergunTurret` and `EnemiesSpawner` pace actual gameplay events (shooting beams, spawning enemies) based on VFX timing.
- Cross-system coupling risks:
  - `VFXPlayer` relies on Unity's standard `InvokeRepeating` which keeps firing if the object is not destroyed or disabled. This can waste CPU cycles if non-destroyed players are left idle.
  - If a particle system loops infinitely, `OnVFXFinished` will still fire once after `_longestParticleDuration`, which may cause state transitions (like enemy spawning or laser firing) while particles continue emitting.

## Known Risks and Open Questions

- Known limitations:
  - `VFXPlayer` uses `InvokeRepeating` to trigger `OnAllParticlesFinished`. The repeating invoke continues to run in the background until the GameObject is destroyed, disabled, or `Play()` is called again. For pooled VFX, if a prefab is returned to a pool but not immediately deactivated or destroyed, the timer will fire repeatedly.
  - `main.duration` does not account for `startDelay` or `startLifetime` of particles when computing the total effect duration, which might lead to `OnVFXFinished` firing before the last particles fully fade out.
- Open design questions:
  - Should `VFXPlayer` use a single-shot Coroutine or `DOVirtual.DelayedCall` from DOTween instead of `InvokeRepeating` to avoid infinite background invocations?
  - Should `VFXPlayer` implement `IPoolable` directly to automatically cancel active invocations and clear event handlers upon release?
- Suggested follow-up tasks:
  - Refactor `VFXPlayer.CallParticlesFinishAfterDelayOrWithout` to use a non-repeating invoke/coroutine or explicitly cancel the repeat invoke in `OnAllParticlesFinished` if the object is not destroyed.
  - Add validation to check if any child particle systems are set to loop infinitely, which could cause discrepancies with `OnVFXFinished`.
