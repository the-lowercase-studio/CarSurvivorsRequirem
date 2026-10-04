# Player System Documentation

## Purpose

The Player system owns the player vehicle state, arcade driving physics, input handling, damage processing, visual/SFX feedback, and player death / UI transition flows.

In Car Survivors, the player entity is a vehicle. The Player system aggregates core player dependencies (`Health`, `LevelController`, `SkillsRegistry`, `CarController`, `AudioClipPlayer`), handles arcade vehicle acceleration, steering, raycast suspension, drift mechanics, wheel model animations, speed/drift trail VFX, processes incoming damage with visual scale shake and SFX, and orchestrates vehicle disablement and game-over UI transitions upon death.

It does not own enemy AI, wave pacing, scoreboard persistence, or individual skill execution logic. Those systems access vehicle and player data through the `IPlayerManager` and `ICarController` references exposed by `PlayerManager`.

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
  - Assets/Scripts/HealthSystem/RegenativeHealth.cs
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/Skills/SkillsRegistry.cs
  - Assets/InputSystem/InputSystem_Actions.inputactions
- Related docs:
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - `PlayerManager`: Aggregate root component attached to the player GameObject (`[RequireComponent(typeof(RegenativeHealth), typeof(LevelController))]`). Implements `IPlayerManager` (inheriting `IHealthy` and `IGameObjectProvider`) and exposes `Health`, `LevelController`, `SkillsRegistry`, `CarController`, and `AudioClipPlayer`. Registered as a scene-scoped dependency in Reflex DI (`DefaultGameplaySceneInstaller`).
  - `CarController`: `MonoBehaviour` requiring a `Rigidbody`. Implements `ICarController`. Reads global Input System actions (`Move` and `Brake`), performs down-facing raycast grounding checks across wheel origins with `Mathf.SmoothDamp` Y suspension positioning, applies forward/reverse acceleration and braking forces directly to `Rigidbody.linearVelocity` via `ForceMode.VelocityChange`, handles Initial D style arcade lateral grip, momentum preservation, decoupled sideways yaw drift angle (`_targetDriftAngle`, `_counterSteerImpact`), steer-intensity arc scaling, differential slip angle delta tracking (`_lastAppliedDriftYaw`), snap-back prevention on drift exit, rotates vehicle via `Rigidbody.MoveRotation`, animates visual wheel models, and manages dual top speed ceilings (`_maxForwardSpeed` = 16.0 m/s vs `_maxOverallSpeed` = 24.0 m/s).
  - `CarVfxEffectsController`: Requires `CarController` on the same GameObject. Brake events control backlights. Speed ribbons retain periodic forward/reverse velocity checks. Rear skid marks use independent cached authored contact anchors and local ground queries in `LateUpdate`. Supported points are inserted manually with authored spacing; bounded probes break continuity across detected gaps and oversized movement. Drift stop, disablement, and inactive visuals retire segments without removing unexpired marks.
  - `CarDriftTrailSegmentPool`: Prewarms eight renderers per emitter, including the authored renderer. Slots are Free, Active, or Fading. Active renderers enable emitting for native mesh generation; Fading renderers disable it after geometry initialization. Native transform sampling uses the finite DRIFT_NATIVE_SAMPLE_SUPPRESSION_DISTANCE, while authored vertex spacing is cached for manual filtering. Runtime renderer transforms are projected onto supported positions independently of the cached contact anchors. Reuse waits strictly beyond retirement time plus lifetime; expired stale native points are then cleared and Free renderers disabled. Saturation skips new drawing. Runtime clones share materials and are destroyed with the controller.
  - `CarDriftTrailSegmentAnchor`: One runtime helper per emitter pins occupied native renderer heads at their latest supported world positions, including during VFX disablement. It performs no queries or insertion and is removed during pool disposal.
  - `PlayerDamagedHandler`: Attached to the player (`[RequireComponent(typeof(PlayerManager))]`). Implements `IDamageable`. Handles incoming damage (`TakeDamage` and `TakeFullHpDamage`), reduces health via `_playerManager.Health`, plays damage SFX (`"Damaged"`), triggers `_damageVfxPlayer`, and executes a DOTween scale shake (`DOShakeScale`) on the car visual transform.
  - `PlayerDeathHandler`: Subscribes to `Health.OnNoHealth` and `_deathVfxPlayer.OnVFXFinished`. On death: hides car visual, disables non-wheel colliders (preserving `_wheelColliders` to maintain physics stability), and plays death VFX. Upon `OnVFXFinished`, it invokes `_playerDeathPresenter.EnableDeathScreen()`.
- Key interfaces:
  - `IPlayerManager`: Aggregate contract exposing `IHealth Health`, `GameObject GameObject`, `ICarController CarController`, `ILevelController LevelController`, `ISkillsRegistry SkillsRegistry`, and `IAudioClipPlayer AudioClipPlayer`.
  - `ICarController`: Colocated with `CarController`. Exposes `OnBrakePress`, `OnBrakeRelease`, `OnDriftStart`, `OnDriftStop`, `OnDriftDirectionChanged`, `GetMovementSpeed()`, `GetMovementVelocity()`, `MaxForwardSpeed`, `MaxOverallSpeed`, `IsDrifting`, `DriftDirection` (-1 Left, 1 Right, 0 None), `DriftYawAngle`, and `IsGrounded`. `TryGetTireGroundContact` is a read-only visual query using the existing ground mask, a short downward ray, and ignored triggers; it rejects non-upward normals and non-finite hit points without changing suspension or vehicle-wide grounding.
- Runtime flow:
  - **Setup**: `PlayerManager.Awake` caches core components (`Health`, `LevelController`, `SkillsRegistry`, `CarController`, `AudioClipPlayer`) and registers as scene-scoped `IPlayerManager` via Reflex installer (`DefaultGameplaySceneInstaller`). `CarController.Awake` configures Rigidbody center of mass (`_centerOfMass` = (0, -0.5, 0)) and caches InputSystem actions. `PlayerDeathHandler.Awake` caches all child colliders.
  - **Driving & Drift Processing**: `CarController.Update` reads `Move` (Vector2) and `Brake` inputs and animates wheel models. `CarController.FixedUpdate` clears angular velocity, executes wheel raycasts to snap Y position with smooth dampening, updates drift state, calculates target linear velocity with smooth lateral grip interpolation (`_currentLateralGrip`), enforces top speed ceilings, and rotates vehicle via `MoveRotation`.
  - **Damage Processing**: When `TakeDamage` is called on `PlayerDamagedHandler`, it decreases health on `IPlayerManager.Health`, plays damage SFX `"Damaged"`, spawns damage VFX, and shakes car visual scale using DOTween.
  - **Death Processing**: When health reaches 0, `OnNoHealth` fires. `PlayerDeathHandler` hides car visuals, disables colliders except wheels, and plays death VFX. Upon `OnVFXFinished`, it displays the game-over screen via `IPlayerDeathPresenter`.

## Rules and Invariants

- Critical behavior rules:
  - `PlayerManager` requires `RegenativeHealth` and `LevelController` on the same GameObject.
  - Normal throttle (`W`) caps top speed at `_maxForwardSpeed` (16.0 m/s).
  - Drifting past the duration threshold (`_minDriftTimeToBoost` = 0.25s) unlocks acceleration towards `_maxOverallSpeed` (24.0 m/s) at `_driftAcceleration` (18.0 m/s²).
  - Upon exiting drift above `_maxForwardSpeed`, excess speed smoothly decays back to `_maxForwardSpeed` at `_driftSpeedDecayRate` (5.0 m/s²).
  - Drift requires brake input, minimum speed (`_minSpeedToDrift` = 8.0 m/s), forward movement input, and horizontal steer input. Lateral grip drops to `_driftGrip` (0.25) and car posture turns sideways (`_targetDriftAngle` = 40°).
  - Upon exiting drift, slip angle delta tracking (`_lastAppliedDriftYaw`) is zeroed so vehicle orientation remains pointing in exit heading without snap-back.
  - Speed ribbon emission is suppressed whenever `IsGrounded` is false. Skid insertion additionally requires each rear anchor's own nearby ground contact and an active visual hierarchy; front-wheel support cannot authorize unsupported rear marks.
  - Two authored rear emitters own at most sixteen drift renderers, with at most two Active segments. No new renderers, materials, or managed arrays are allocated during driving or rollover. Each emitter uses one current query and at most eight continuity probes per rendered frame.
  - Paused scaled time prevents skid insertion and expiry bookkeeping. Re-enable starts a new strip and reuses the existing pools. Keep native TrailRenderer lifetime/gradient behavior; the gradient is not an independent animation of point age.
  - `GetMovementSpeed()` and `GetMovementVelocity()` return Rigidbody velocity with Y forced to 0f magnitude.
  - Main colliders must be disabled upon death to prevent further enemy collisions, while wheel colliders remain interactive/decoupled to avoid physics instability.
- Ordering or sequencing guarantees:
  - Game over screen is enabled only after death VFX completes its animation and raises `OnVFXFinished`.
  - Physics calculations, drift state transitions, and steer interpolations run synchronously in `FixedUpdate` using `Time.fixedDeltaTime`.
- Constraints contributors must preserve:
  - Do not introduce static singleton accessors to the Player. Always inject `IPlayerManager` via Reflex.
  - Preserve `Move` and `Brake` Input System action names.
  - Keep player damage routing unified through `IDamageable` interface on `PlayerDamagedHandler`.

## Extension Points

- Safe extension areas:
  - Expose additional player stats by expanding `IPlayerManager` and delegates in `PlayerManager`.
  - Add visual cues or overlays by subscribing to `OnHealthChange`, `OnLvlUp`, `OnBrakePress`, or `OnDriftDirectionChanged`.
  - Tune max speed, acceleration, braking, turn rate, grip values, drift thresholds, and raycast suspension via serialized Inspector fields on `CarController`.
- Required dependencies and contracts:
  - `PlayerManager` acts as the aggregate root, providing direct access to nested dependencies.
  - `CarController` requires a `Rigidbody` component.
  - DOTween is used for visual effects like scale shake on damage.
- Testing implications:
  - Compile after C# changes via `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Play-test driving feel, ground snapping, drift entry/exit, damage scale shake, and death UI transitions in the Unity Editor.

## Integration Notes

- Upstream dependencies:
  - Reflex DI framework for injection of `IPlayerManager` and `IPlayerDeathPresenter`.
  - Unity Input System global actions from `Assets/InputSystem/InputSystem_Actions.inputactions`.
  - DOTween for scale animations.
- Downstream consumers:
  - `GridManager` reads player position and velocity for flow field calculations and target prediction.
  - `IncreaseDifficultyTotem` and `CapturePoint` check player distance for interaction.
  - `ExpParticle` rewards XP to player's `LevelController` on collection.
  - `SawBlade` queries `GetMovementSpeed()` to scale knockback force.
  - UI presenters (`PlayerLevelPresenter`, `PlayerDeathPresenter`, `SkillUpgradePresenter`) monitor player state.
- Cross-system coupling risks:
  - Changing `GetMovementSpeed()` semantics from Rigidbody linear velocity to input speed alters skill mechanics like `SawBlade`.
  - Ensure references are cleared or handled gracefully if the player is destroyed to avoid downstream `NullReferenceException`.

## Known Risks and Open Questions

- Known limitations:
  - `PlayerDeathHandler` uses `GetComponentsInChildren<Collider>` on `Awake`. Dynamically attached colliders created after initialization (e.g. from skills) will not be cached in `_allColliders` and thus won't be disabled on death.
  - Raycast ground snapping in `FixedUpdate` provides arcade stability but requires terrain elevation changes to be smooth to avoid visual snapping.
  - Skid probes sample the centerline at up to 0.10 m intervals. Sub-resolution holes, moving surfaces, exact ribbon-width clipping, visual tolerances, and renderer cost need separate Editor verification; the bounded sampling is not an exact footprint test.
- Open design questions:
  - Should `ICarController` be bound directly in Reflex DI, or should car access continue to be intentionally routed through `IPlayerManager`?

