# Player System Documentation

## Purpose

The Player system owns the player vehicle state, arcade driving physics, input handling, damage processing, visual/SFX feedback, drift skid mark and fire trail presentation, and player death / UI transition flows.

In Car Survivors, the player entity is a vehicle. The Player system aggregates core player dependencies (Health, LevelController, SkillsRegistry, CarController, CarVfxEffectsController, AudioClipPlayer), handles arcade vehicle acceleration, steering, raycast suspension, drift mechanics, wheel model animations, speed/drift/fire trail VFX, processes incoming damage with Bubble Shield immunity, Armor plate absorption, visual scale shake, and SFX, and orchestrates vehicle disablement and game-over UI transitions upon death.

It does not own enemy AI, wave pacing, scoreboard persistence, power-up timers/hazard overlap scans, or individual skill execution logic. Those systems access vehicle and player data through the IPlayerManager, ICarController, and ICarVfxEffectsController references exposed by PlayerManager.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Player/PlayerDeathHandler.cs
  - Assets/Scripts/Player/Car/CarController.cs
  - Assets/Scripts/Player/Car/CarVfxEffectsController.cs
  - Assets/Scripts/Player/Car/CarDriftTrailSegmentPool.cs
  - Assets/Scripts/Player/Car/CarDriftTrailSegmentAnchor.cs
  - Assets/Scripts/Player/Constants/CarVfxConstants.cs
  - Assets/Scripts/Player/Constants/PlayerAudioConstants.cs
  - Assets/Scripts/HealthSystem/RegenativeHealth.cs
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/Skills/SkillsRegistry.cs
  - Assets/InputSystem/InputSystem_Actions.inputactions
- Shaders, materials, textures, and prefabs:
  - Assets/Shaders/TireTrackMultiply.shader
  - Assets/Shaders/FireTailsTrail.shader
  - Assets/Materials/TireTrack.mat
  - Assets/Materials/FireTailsTrail.mat
  - Assets/Textures/TireTracks2.png
  - Assets/Textures/FlameNoise.png
  - Assets/Prefabs/Player/Player.prefab
- Related docs:
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/power-ups-system.md
  - .agents/context/game-systems/vfx-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - PlayerManager: Aggregate root component attached to the player GameObject ([RequireComponent(typeof(RegenativeHealth), typeof(LevelController))]). Implements IPlayerManager (inheriting IHealthy and IGameObjectProvider) and exposes Health, LevelController, SkillsRegistry, CarController, CarVfxEffectsController, and AudioClipPlayer. Registered as a scene-scoped dependency in Reflex DI (DefaultGameplaySceneInstaller).
  - CarController: MonoBehaviour requiring a Rigidbody. Implements ICarController. Reads global Input System actions (Move and Brake), performs down-facing raycast grounding checks across wheel origins with Mathf.SmoothDamp Y suspension positioning, applies forward/reverse acceleration and braking forces directly to Rigidbody.linearVelocity via ForceMode.VelocityChange, handles Initial D style arcade lateral grip, momentum preservation, decoupled sideways yaw drift angle (_targetDriftAngle, _counterSteerImpact), steer-intensity arc scaling, differential slip angle delta tracking (_lastAppliedDriftYaw), snap-back prevention on drift exit, rotates vehicle via Rigidbody.MoveRotation, animates visual wheel models, and manages dual top speed ceilings (_maxForwardSpeed = 16.0 m/s vs _maxOverallSpeed = 24.0 m/s).
  - CarVfxEffectsController: Requires CarController on the same GameObject and implements ICarVfxEffectsController. Brake events control backlights and the CarStopLights material IsGlowing property. Speed ribbons retain periodic (0.1s) forward/reverse velocity checks. Rear drift trails maintain two parallel sets of DriftEmitter pools in LateUpdate: _driftEmitters for standard black tire skid marks (rendered via Assets/Materials/TireTrack.mat using Custom/TireTrackMultiply) and _fireDriftEmitters for Fire Tails burning ribbons (rendered via Assets/Materials/FireTailsTrail.mat using Custom/FireTailsTrail). Both pools tick expiry every frame so existing skid marks fade out naturally when Fire Tails activates and fire ribbons fade out naturally after Fire Tails expires. Also controls 2 local-space constant rear-wheel flame ParticleSystems (_rearWheelConstantFireParticles, maxParticles = 50) active throughout the Fire Tails duration and 2 world-space drift flame ParticleSystems (_rearDriftFireTrailParticles, maxParticles = 120, startLifetime = 3.5s) active only while grounded and drifting with Fire Tails. Uses ps.isEmitting checks when starting/stopping particle emission so re-drifting while older world-space flames are still decaying resumes emission immediately.
  - CarDriftTrailSegmentPool: Prewarms eight TrailRenderer segments per emitter, including the authored template renderer. Slots are Free, Active, or Fading. Active renderers enable emitting for native mesh generation; Fading renderers disable it after geometry initialization. Native transform sampling uses the finite DRIFT_NATIVE_SAMPLE_SUPPRESSION_DISTANCE (1,000,000f), while authored vertex spacing (_minVertexDistance, 0.15m on fire trails) is cached for manual filtering in AppendPoint. Exposes UpdateTimingAndGradient(float timeSeconds, Gradient gradient) so FireTailsService can synchronize fire trail lifetimes and fade gradients dynamically. Reuse waits strictly beyond retirement time plus lifetime; expired stale native points are then cleared and Free renderers disabled.
  - CarDriftTrailSegmentAnchor: One runtime helper per emitter pins occupied native renderer heads at their latest supported world positions in LateUpdate, including during VFX disablement. It performs no queries or insertion and is removed during pool disposal.
  - PlayerDamagedHandler: Attached to the player ([RequireComponent(typeof(PlayerManager))]). Implements IDamageable and injects IPlayerManager, IBubbleShield, and IArmorService. In TakeDamage(float damage): (1) if _bubbleShield.IsActive is true, calls _bubbleShield.NotifyDamageAbsorbed() and returns; (2) if _armorService.HasArmor is true, calls damage = _armorService.AbsorbDamage(damage) and returns if damage <= 0f; (3) otherwise reduces base health via _playerManager.Health.DecreaseHealth(damage), plays damage SFX (PlayerAudioConstants.DAMAGED_AUDIO_KEY = "Damaged"), triggers _damageVfxPlayer, and executes a DOTween scale shake (DOShakeScale) on _carVisual, killing and resetting scale cleanly on OnDisable/OnDestroy. In TakeFullHpDamage(): clears active armor via _armorService.ClearArmorImmediate() and reduces base health by MaxHealth.
  - PlayerDeathHandler: Subscribes to Health.OnNoHealth and _deathVfxPlayer.OnVFXFinished. On death: hides car visual, disables non-wheel colliders (preserving _wheelColliders to maintain physics stability), and plays death VFX. Upon OnVFXFinished, it invokes _playerDeathPresenter.EnableDeathScreen().
- Key interfaces:
  - IPlayerManager: Aggregate contract exposing IHealth Health, GameObject GameObject, ICarController CarController, ICarVfxEffectsController CarVfxEffectsController, ILevelController LevelController, ISkillsRegistry SkillsRegistry, and IAudioClipPlayer AudioClipPlayer. Colocated in Assets/Scripts/Player/PlayerManager.cs.
  - ICarController: Colocated in Assets/Scripts/Player/Car/CarController.cs. Exposes OnBrakePress, OnBrakeRelease, OnDriftStart, OnDriftStop, OnDriftDirectionChanged, GetMovementSpeed(), GetMovementVelocity(), MaxForwardSpeed, MaxOverallSpeed, IsDrifting, DriftDirection (-1 Left, 1 Right, 0 None), DriftYawAngle, IsGrounded, and TryGetTireGroundContact(Vector3 worldCandidate, out RaycastHit groundHit).
  - ICarVfxEffectsController: Colocated in Assets/Scripts/Player/Car/CarVfxEffectsController.cs. Exposes RearDriftEmitterCount, TryGetRearDriftEmitterWorldCandidate(int emitterIndex, out Vector3 worldCandidate), ConfigureFireTailsTimings(float trailLifetime, float trailFadeTime), SetFireTailsActive(bool isActive), and SetFireTailsFrozen(bool isFrozen).
- Centralized constants:
  - CarVfxConstants (Assets/Scripts/Player/Constants/CarVfxConstants.cs):
    - SPEED_CHECK_FOR_TRAIL_DELAY = 0.1f
    - CAR_STOP_LIGHTS_MAT_NAME = "CarStopLights"
    - DRIFT_GROUND_PROBE_HEIGHT = 0.15f
    - DRIFT_GROUND_PROBE_DISTANCE = 0.30f
    - DRIFT_GROUND_SURFACE_OFFSET = 0.005f
    - DRIFT_SEGMENTS_PER_EMITTER = 8
    - DRIFT_CONTINUITY_SAMPLE_SPACING = 0.10f
    - DRIFT_MAX_CONTINUITY_SAMPLES = 8
    - DRIFT_NATIVE_SAMPLE_SUPPRESSION_DISTANCE = 1000000f
  - PlayerAudioConstants (Assets/Scripts/Player/Constants/PlayerAudioConstants.cs):
    - DAMAGED_AUDIO_KEY = "Damaged"
- Runtime flow:
  - Setup: PlayerManager.Awake caches core components (Health, LevelController, SkillsRegistry, CarController, CarVfxEffectsController, AudioClipPlayer) and registers as scene-scoped IPlayerManager via DefaultGameplaySceneInstaller. CarController.Awake configures Rigidbody center of mass (_centerOfMass = (0, -0.5, 0)), validates _groundLayerMask, and caches InputSystem actions. CarVfxEffectsController.Start initializes both standard _driftEmitters and _fireDriftEmitters pools.
  - Driving & Drift Processing: CarController.Update reads Move (Vector2) and Brake inputs and animates wheel models. CarController.FixedUpdate clears angular velocity, executes wheel raycasts to snap Y position with smooth dampening, updates drift state, calculates target linear velocity with smooth lateral grip interpolation (_currentLateralGrip), enforces top speed ceilings, and rotates vehicle via MoveRotation.
  - Drift Trail & Fire Tails VFX Processing: In CarVfxEffectsController.LateUpdate, both _driftEmitters and _fireDriftEmitters tick segment expiry. When grounded and drifting:
    - If _isFireTailsActive is false, _fireDriftEmitters and _rearDriftFireTrailParticles are stopped, and standard _driftEmitters append surface-snapped points (hit.point + Vector3.up * 0.005f) rendered with Custom/TireTrackMultiply (Multiply blend DstColor Zero with Offset -1, -1 that darkens the underlying asphalt while preserving road lines and cracks, fading toward white (1, 1, 1) as vertex alpha decays).
    - If _isFireTailsActive is true, standard _driftEmitters are ended, _fireDriftEmitters append surface-snapped points rendered with Custom/FireTailsTrail (Additive blend SrcAlpha One with Offset -1, -1, cubic polynomial molten core 1 - uDist^3, hardware TMU sampling of Assets/Textures/FlameNoise.png, and vertex-alpha smoothstep ember dissolve), and _rearDriftFireTrailParticles emit lingering world-space flames (unless frozen by Time Freeze).
  - Damage Processing: When TakeDamage is called on PlayerDamagedHandler, it checks IBubbleShield.IsActive (blocking 100% of damage) and then IArmorService.AbsorbDamage (depleting armor plates with overflow). Any remaining positive damage decreases health on IPlayerManager.Health, plays damage SFX "Damaged", spawns damage VFX, and shakes car visual scale using DOTween.
  - Death Processing: When health reaches 0, OnNoHealth fires. PlayerDeathHandler hides car visuals, disables colliders except wheels, and plays death VFX. Upon OnVFXFinished, it displays the game-over screen via IPlayerDeathPresenter.

## Rules and Invariants

- Critical behavior rules:
  - PlayerManager requires RegenativeHealth and LevelController on the same GameObject.
  - Normal throttle (W) caps top speed at _maxForwardSpeed (16.0 m/s).
  - Drifting past the duration threshold (_minDriftTimeToBoost = 0.25s) unlocks acceleration towards _maxOverallSpeed (24.0 m/s) at _driftAcceleration (18.0 m/s²).
  - Upon exiting drift above _maxForwardSpeed, excess speed smoothly decays back to _maxForwardSpeed at _driftSpeedDecayRate (5.0 m/s²).
  - Drift requires brake input, minimum speed (_minSpeedToDrift = 8.0 m/s), forward movement input, and horizontal steer input. Lateral grip drops to _driftGrip (0.25) and car posture turns sideways (_targetDriftAngle = 40°).
  - Upon exiting drift, slip angle delta tracking (_lastAppliedDriftYaw) is zeroed so vehicle orientation remains pointing in exit heading without snap-back.
  - Speed ribbon emission is suppressed whenever IsGrounded is false or IsDrifting is true. Skid and fire ribbon insertion additionally requires each rear anchor's own nearby ground contact and an active visual hierarchy; front-wheel support cannot authorize unsupported rear marks.
  - Two authored rear standard emitters and two authored rear fire emitters each own at most eight pooled TrailRenderer segments per wheel (16 standard + 16 fire total), with at most one Active segment per emitter at a time. No new renderers, materials, or managed arrays are allocated during driving or rollover. Each emitter uses one current query and at most eight continuity probes per rendered frame.
  - SetParticleSystemsPlaying must check ps.isEmitting (not ps.isPlaying) and stop with ParticleSystemStopBehavior.StopEmitting so lingering world-space drift flame particles do not block immediate re-emission when the player stops and re-initiates a drift within the 3.5s particle lifetime window.
  - GetMovementSpeed() and GetMovementVelocity() return Rigidbody velocity with Y forced to 0f magnitude.
  - Main colliders must be disabled upon death to prevent further enemy collisions, while wheel colliders remain interactive/decoupled to avoid physics instability.
- Ordering or sequencing guarantees:
  - Game over screen is enabled only after death VFX completes its animation and raises OnVFXFinished.
  - Physics calculations, drift state transitions, and steer interpolations run synchronously in FixedUpdate using Time.fixedDeltaTime.
  - Drift trail continuity sampling and segment pool updates run in LateUpdate.
- Constraints contributors must preserve:
  - Do not introduce static singleton accessors to the Player. Always inject IPlayerManager via Reflex.
  - Preserve Move and Brake Input System action names.
  - Keep player damage routing unified through IDamageable interface on PlayerDamagedHandler.

## Extension Points

- Safe extension areas:
  - Expose additional player stats or vehicle controllers by expanding IPlayerManager and caching in PlayerManager.Awake.
  - Add visual cues or overlays by subscribing to OnHealthChanged, OnLvlUp, OnBrakePress, or OnDriftDirectionChanged.
  - Tune max speed, acceleration, braking, turn rate, grip values, drift thresholds, and raycast suspension via serialized Inspector fields on CarController.
  - Tune tire skid mark darkness in Assets/Materials/TireTrack.mat (_BaseColor, _DarknessIntensity, _MinDarkness) or Fire Tails ribbon glow and noise flow in Assets/Materials/FireTailsTrail.mat (_CoreColor, _OuterColor, _FlowSpeed, _NoiseTiling, _EmissionBoost, _DissolveSoftness).
  - Re-wire Fire Tails trail renderers and particle systems on Assets/Prefabs/Player/Player.prefab via Tools/Fire Tails/Wire All Fire Tails in Assets/Scripts/Editor/Tools/FireTailsWiringTool.cs.
- Required dependencies and contracts:
  - PlayerManager acts as the aggregate root, providing direct access to nested dependencies.
  - CarController requires a Rigidbody component and an assigned _groundLayerMask.
  - DOTween is used for visual effects like scale shake on damage.
- Testing implications:
  - Compile after C# changes via dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
  - Play-test driving feel, ground snapping, drift entry/exit, multiply-blended skid marks on asphalt, Fire Tails ribbon/particle switching during drift, damage absorption (Bubble Shield -> Armor -> Health), scale shake, and death UI transitions in the Unity Editor.

## Integration Notes

- Upstream dependencies:
  - Reflex DI framework for injection of IPlayerManager, IPlayerDeathPresenter, IBubbleShield, and IArmorService.
  - Unity Input System global actions from Assets/InputSystem/InputSystem_Actions.inputactions.
  - DOTween for scale animations.
- Downstream consumers:
  - GridManager reads player position and velocity for flow field calculations and target prediction.
  - IncreaseDifficultyTotem and CapturePoint check player distance for interaction.
  - ExpParticle rewards XP to player's LevelController on collection.
  - SawBlade queries GetMovementSpeed() to scale knockback force.
  - BubbleShieldService, ArmorService, and FireTailsService consume IPlayerManager (Health, CarController, CarVfxEffectsController).
  - UI presenters (PlayerLevelPresenter, PlayerDeathPresenter, SkillUpgradePresenter, PlayerSkillsHUDPresenter, SkillsStatsPresenter) monitor player state.
- Cross-system coupling risks:
  - Changing GetMovementSpeed() semantics from Rigidbody linear velocity to input speed alters skill mechanics like SawBlade.
  - Ensure references are cleared or handled gracefully if the player is destroyed to avoid downstream NullReferenceException.

## Known Risks and Open Questions

- Known limitations:
  - PlayerDeathHandler uses GetComponentsInChildren<Collider> on Awake. Dynamically attached colliders created after initialization (e.g. from skills) will not be cached in _allColliders and thus won't be disabled on death.
  - Raycast ground snapping in FixedUpdate provides arcade stability but requires terrain elevation changes to be smooth to avoid visual snapping.
  - Skid and fire trail probes sample the centerline at up to 0.10 m intervals. Sub-resolution holes, moving surfaces, exact ribbon-width clipping, visual tolerances, and renderer cost need separate Editor verification; the bounded sampling is not an exact footprint test.
- Open design questions:
  - Should ICarController be bound directly in Reflex DI, or should car access continue to be intentionally routed through IPlayerManager?
