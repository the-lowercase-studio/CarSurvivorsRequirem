# VFX System Documentation

## Purpose

The VFX system manages the execution, lifetime, scale, and playback speed of particle-based visual effects, scene-pooled explosion effects, and vehicle-specific environmental effects (brake lights, speed ribbons, surface-grounded tire skid marks, and Fire Tails ribbons/flames).

It is responsible for:
- Providing a generic, play-and-forget interface for particle-based visual effects through Assets/Scripts/VFX/VFXPlayer.cs.
- Managing a prewarmed, bounded scene pool of explosion effects through Assets/Scripts/VFX/ExplosionVfxPool.cs and Assets/Scripts/VFX/PooledExplosionEffect.cs.
- Managing the lifecycle of instantiated visual effects (e.g. self-destruction via config options or watchdog-guarded pool release).
- Emitting events when the longest active particle duration in a VFX prefab finishes.
- Controlling vehicle-specific visual feedback (brake lights, material glow, motion speed trails, pooled tire skid mark ribbons, Fire Tails burning drift ribbons, and rear-wheel/world-space flame particles) via Assets/Scripts/Player/Car/CarVfxEffectsController.cs, Assets/Scripts/Player/Car/CarDriftTrailSegmentPool.cs, and Assets/Scripts/Player/Car/CarDriftTrailSegmentAnchor.cs.

It is not responsible for:
- Deciding when or where to trigger combat damage or power-up timers (handled by gameplay components like enemies, player handlers, totems, turrets, and power-up services).
- Managing damage numbers formatting or spawning (handled by the Damage Numbers system).

## Reading Map

- Primary code locations:
  - Assets/Scripts/VFX/VFXPlayer.cs
  - Assets/Scripts/VFX/ExplosionVfxPool.cs
  - Assets/Scripts/VFX/PooledExplosionEffect.cs
  - Assets/Scripts/Player/Car/CarVfxEffectsController.cs
  - Assets/Scripts/Player/Car/CarDriftTrailSegmentPool.cs
  - Assets/Scripts/Player/Car/CarDriftTrailSegmentAnchor.cs
  - Assets/Scripts/Player/Constants/CarVfxConstants.cs
- Shaders, materials, textures, and VFX prefabs:
  - Assets/Shaders/TireTrackMultiply.shader
  - Assets/Shaders/FireTailsTrail.shader
  - Assets/Materials/TireTrack.mat
  - Assets/Materials/FireTailsTrail.mat
  - Assets/Textures/TireTracks2.png
  - Assets/Textures/FlameNoise.png
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
  - .agents/context/game-systems/player-system.md
  - .agents/context/game-systems/power-ups-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md

## Architecture and Data Flow

- Core components:
  - VFXPlayer: MonoBehaviour attached to VFX prefabs. On Awake, it caches all child ParticleSystem components. In Play(VFXPlayConfig), it iterates through them, updates their local scale and simulation speed, triggers playback, and schedules a completion callback matching the longest particle's main duration. Also exposes StopPlayback() to cancel pending invokes and clear particles immediately.
  - VFXPlayConfig: Configuration class containing parameters for playing a VFX (Scale, SimulationSpeed, DestroyOnEnd).
  - ExplosionVfxPool and PooledExplosionEffect: Scene-scoped pool (IExplosionVfxPool) registered in DefaultGameplaySceneInstaller that prewarms PooledExplosionEffect instances (default 32, capacity limit 32) and recycles them in Update once all nested ParticleSystems finish or a scaled watchdog timeout (8s) elapses.
  - CarVfxEffectsController: Implements ICarVfxEffectsController. Listens to ICarController brake and drift events to toggle emission on the stop-light material (CarStopLights material name, setting IsGlowing float parameter) and backlights holder, polls forward/reverse speed every 0.1s for speed trail emission, manages parallel DriftEmitter pools (_driftEmitters for standard tire skid marks and _fireDriftEmitters for Fire Tails burning ribbons) in LateUpdate, and controls rear-wheel constant flame ParticleSystems (_rearWheelConstantFireParticles) and lingering world-space drift flame ParticleSystems (_rearDriftFireTrailParticles).
  - CarDriftTrailSegmentPool and CarDriftTrailSegmentAnchor: Prewarms 8 TrailRenderer segments per rear wheel emitter, suppressing native transform distance sampling (DRIFT_NATIVE_SAMPLE_SUPPRESSION_DISTANCE = 1,000,000f) while manually filtering appended ground-snapped vertices using the template's authored minVertexDistance (0.15m on Fire Tails trails) and pinning active segment heads via CarDriftTrailSegmentAnchor.
  - TireTrackMultiply.shader (Custom/TireTrackMultiply): Single-pass URP HLSL shader using Multiply blending (Blend DstColor Zero), ZWrite Off, ZTest LEqual, Cull Off, and hardware depth bias (Offset -1, -1). Multiplies ground pixels by tread darkness (_BaseColor, _DarknessIntensity, clamped by _MinDarkness = 0.05) and interpolates toward pure white (1, 1, 1) as TrailRenderer vertex alpha fades to 0 or fog increases, leaving underlying road details visible without Z-fighting.
  - FireTailsTrail.shader (Custom/FireTailsTrail): Single-pass URP HLSL shader using additive blending (Blend SrcAlpha One), ZWrite Off, ZTest LEqual, Cull Off, and Offset -1, -1. Computes a cubic polynomial molten core profile (1 - uDist^3, avoiding transcendental pow() calls), samples seamless 128x128 grayscale Perlin noise (Assets/Textures/FlameNoise.png) on hardware TMU with scrolling UV flow, and applies a smoothstep vertex-alpha dissolve so expiring fire trails erode into glowing embers.
- Key interfaces:
  - IVFXPlayer: Exposes Play(VFXPlayConfig config), GetLongestParticleDuration(), and OnVFXFinished. Colocated in Assets/Scripts/VFX/VFXPlayer.cs.
  - IExplosionVfxPool: Exposes Play(Vector3 position, float radius). Colocated in Assets/Scripts/VFX/ExplosionVfxPool.cs.
  - ICarVfxEffectsController: Exposes RearDriftEmitterCount, TryGetRearDriftEmitterWorldCandidate, ConfigureFireTailsTimings, SetFireTailsActive, and SetFireTailsFrozen. Colocated in Assets/Scripts/Player/Car/CarVfxEffectsController.cs.
- Runtime flow:
  - Generic Particle Playback:
    1. A gameplay system (e.g. Enemy, PlayerDeathHandler, ArmorService, or a turret) acquires a reference to a VFXPlayer.
    2. The caller calls Play with a VFXPlayConfig.
    3. VFXPlayer sets scale and speed overrides on all cached particle systems, plays them, and schedules OnAllParticlesFinished matching the longest particle's main duration.
    4. Upon completion, OnVFXFinished is invoked, and if DestroyOnEnd is set, the GameObject is destroyed.
  - Swarm Spawn VFX Pacing:
    1. EnemiesSpawner instantiates a swarm spawn VFX prefab at the target grid cell.
    2. It calls Play with destroyOnEnd: true and subscribes to OnVFXFinished.
    3. The enemy prefab is only retrieved from the pool and placed after the spawn VFX completes.
  - Laser Charge-up Pacing:
    1. LasergunTurret calls Play on the cumulating VFX when Shoot is triggered.
    2. It listens to OnVFXFinished to fire the actual laser beam, achieving a charging effect.
  - Car Drift Skid Marks & Fire Tails Loop:
    1. CarVfxEffectsController subscribes to brake and drift events in OnEnable and polls speed trails every 0.1s (suppressed when ungrounded or drifting).
    2. In LateUpdate, both _driftEmitters and _fireDriftEmitters tick segment expiry.
    3. When grounded and drifting without Fire Tails, _driftEmitters sample tire ground contact (offset by DRIFT_GROUND_SURFACE_OFFSET = 0.005f) and append points to TireTrack.mat segments.
    4. When Fire Tails is active (SetFireTailsActive(true)), _rearWheelConstantFireParticles play continuously (local space, maxParticles = 50), standard _driftEmitters end their active segments, and drifting appends points to _fireDriftEmitters (FireTailsTrail.mat) while enabling emission on _rearDriftFireTrailParticles (world space, maxParticles = 120, startLifetime = 3.5s).
    5. SetParticleSystemsPlaying checks ps.isEmitting (not ps.isPlaying) when starting/stopping particles so lingering world-space flames from an earlier drift do not prevent immediate re-emission on a subsequent drift.
    6. When Time Freeze activates, SetFireTailsFrozen(true) pauses both constant and drift fire particle systems and resumes them when unfrozen.

## Rules and Invariants

- Critical behavior rules:
  - VFXPlayer computes _longestParticleDuration from main.duration of the child particle systems.
  - VFXPlayConfig.Scale modifies transform.localScale of each individual child particle system rather than the root parent GameObject.
  - Custom trail shaders (Custom/TireTrackMultiply and Custom/FireTailsTrail) must declare all material properties inside CBUFFER_START(UnityPerMaterial) to preserve URP SRP Batcher compatibility and include Offset -1, -1 to prevent Z-fighting against road geometry.
  - CarVfxEffectsController.SetParticleSystemsPlaying must gate Play() and Stop(true, ParticleSystemStopBehavior.StopEmitting) on ps.isEmitting rather than ps.isPlaying so world-space emitters with decaying particles can restart emission immediately.
- Ordering or sequencing guarantees:
  - Swarm enemy spawning is strictly delayed by the duration of the spawn VFX.
  - Enemy pooling release is delayed by EnemyDeathHandler until both the death VFX (OnVFXFinished) and audio clip (OnAudioClipFinished) have completed.
- Constraints contributors must preserve:
  - Always clean up event subscriptions (e.g. unsubscribe from OnVFXFinished in OnDisable or upon completion) to avoid memory leaks.
  - Keep vehicle feedback visual states synchronized with ICarController inputs and IFireTailsService / ITimeFreezeState notifications.
  - Do not use direct Destroy calls on pooled VFX objects; let ExplosionVfxPool or the owning pool manager handle their lifecycle, or use DestroyOnEnd only for non-pooled VFX prefabs.

## Extension Points

- Safe extension areas:
  - Creating new visual effects by constructing a prefab with Unity ParticleSystem components and adding the VFXPlayer script.
  - Adding player feedback effects by creating a custom handler listening to event hooks (like damage, power-up pickup, or skill activation) and calling IVFXPlayer.Play.
  - Tuning Fire Tails particle limits, trail widths, and material properties via Assets/Scripts/Editor/Tools/FireTailsWiringTool.cs (Tools/Fire Tails/Wire All Fire Tails).
- Required dependencies and contracts:
  - Prefabs carrying the VFXPlayer component must contain one or more child ParticleSystem components.
  - Custom vehicle controllers must implement ICarController to support CarVfxEffectsController.
- Testing implications:
  - Ensure any new VFX prefab behaves correctly when scaled up/down (e.g. verify particle sizes scale proportionally).
  - Verify that OnVFXFinished fires exactly when expected and does not get cut off or trigger prematurely.

## Integration Notes

- Upstream dependencies:
  - ICarController supplies vehicle inputs, velocity, drift state, and tire ground contact raycasts.
  - FireTailsService drives ICarVfxEffectsController timing configuration, active state, and Time Freeze pause state.
- Downstream consumers:
  - EnemyDeathHandler and PlayerDeathHandler rely on VFX completion before transitioning state or releasing pooled enemies.
  - BarrelEnemyExplosionController, LandMine, and MortarShellProjectile trigger pooled explosions via IExplosionVfxPool.
  - LasergunTurret and EnemiesSpawner pace gameplay events (shooting beams, spawning enemies) based on VFX timing.
- Cross-system coupling risks:
  - VFXPlayer relies on Unity's standard InvokeRepeating which keeps firing if the object is not destroyed, disabled, or stopped via StopPlayback().
  - If a particle system loops infinitely, OnVFXFinished will still fire once after _longestParticleDuration, which may cause state transitions while particles continue emitting.

## Known Risks and Open Questions

- Known limitations:
  - VFXPlayer uses InvokeRepeating to trigger OnAllParticlesFinished. While _particlesStartedPlaying = false prevents duplicate OnVFXFinished invocations after the first callback, the repeating timer remains scheduled until Play(), StopPlayback(), OnDisable, or Destroy occurs.
  - main.duration does not account for startDelay or startLifetime of particles when computing the total effect duration, which might lead to OnVFXFinished firing before the last particles fully fade out (PooledExplosionEffect works around this by polling particle.IsAlive(true) directly).
- Open design questions:
  - Should VFXPlayer use a single-shot Invoke or CancelInvoke inside OnAllParticlesFinished instead of InvokeRepeating?
  - Should VFXPlayer implement IPoolable directly to automatically cancel active invocations and clear event handlers upon release?
- Suggested follow-up tasks:
  - Cancel the repeating invoke inside VFXPlayer.OnAllParticlesFinished once _particlesStartedPlaying is cleared.
  - Add validation to check if any child particle systems on one-shot VFXPlayer prefabs are set to loop infinitely.
