# Car System Documentation

## Purpose

The Car system owns player vehicle movement input, arcade velocity and raycast grounding control, brake and drift state handling, visual wheel model synchronization, and car-specific movement VFX.

It does not own player health, leveling, skills, spawning, camera behavior, enemy movement, or general gameplay state. Those systems access car movement data through the player-facing ICarController reference exposed by PlayerManager.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Player/Car/CarController.cs
  - Assets/Scripts/Player/Car/CarVfxEffectsController.cs
- Related systems:
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs
  - Assets/InputSystem/InputSystem_Actions.inputactions
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
  - .agents/context/game-systems/player-system.md

## Architecture and Data Flow

- Core components:
  - ICarController is colocated with CarController and exposes brake events, drift events (OnDriftStart, OnDriftStop, OnDriftDirectionChanged), current movement speed, Y-flattened movement velocity, IsDrifting, DriftDirection (-1 Left, 1 Right, 0 None), and DriftYawAngle.
  - CarController is a MonoBehaviour requiring a Rigidbody. It reads global Input System actions named Move and Brake, performs raycast grounding checks across wheel origins, applies forward acceleration/braking forces directly to Rigidbody linear velocity via ForceMode.VelocityChange, handles Initial D style arcade lateral grip, momentum preservation, and decoupled sideways yaw drift angle (_targetDriftAngle, _counterSteerImpact), rotates the vehicle via Rigidbody.MoveRotation, and animates visual wheel model transforms.
  - CarVfxEffectsController requires CarController, reads ICarController from the same GameObject, reacts to brake and drift events, controls per-side drift trails (_leftDriftTrailRenderers, _rightDriftTrailRenderers - 2 renderers per side for front and rear wheels), and periodically toggles speed trails based on GetMovementSpeed().
- Runtime flow:
  - Awake: CarController caches the Rigidbody, resolves Move and Brake actions from InputSystem.actions, and configures ground layer mask defaults; CarVfxEffectsController caches ICarController.
  - OnEnable/OnDisable: CarController subscribes and unsubscribes named brake action callbacks that raise OnBrakePress and OnBrakeRelease; resets drift state and drift direction on disable. CarVfxEffectsController subscribes in OnEnable and unsubscribes in OnDisable to brake, drift direction change, and drift stop events.
  - Update: CarController reads normalized movement input, brake action status, updates drift state, calculates target drift yaw angle with counter-steer offset, and animates visual wheel models.
  - FixedUpdate: CarController clears angular velocity, executes raycast grounding and Y-position snapping, calculates target linear velocity change based on acceleration/grip/drift and applies force via VelocityChange, updates Y-rotation steering and dynamic drift yaw offset via MoveRotation.
  - Start in VFX: CarVfxEffectsController finds the stop-light material by name prefix, configures trail lifetime, disables trail emission for speed and drift trails, and starts repeated speed threshold checks.

## Rules and Invariants

- The Move action must exist and return a Vector2; the vertical axis drives forward/reverse acceleration and deceleration, and the horizontal axis drives arcade steering.
- The Brake action must exist as a button action; pressing and releasing it drive deceleration, drift state eligibility, and brake VFX events.
- CarController assumes _wheels contains valid wheel model GameObject references and Axel designations (Front/Rear).
- Grounding uses down-facing raycasts from wheel positions with configured check distance and target Y offset, grounding the car on the highest detected terrain point.
- Drift requires brake input, minimum movement speed (_minSpeedToDrift), forward movement input, and minimum horizontal steer input. While drifting, lateral grip drops to _driftGrip (0.25), forward speed is governed by _driftDeceleration (5m/s²), turn speed is scaled by _driftTurnMultiplier (1.3), and the car body rotates into a sideways posture (_targetDriftAngle = 40°).
- DriftDirection returns -1 for Left drift, 1 for Right drift, and 0 for None.
- GetMovementSpeed() returns Rigidbody.linearVelocity with y forced to 0f magnitude; consumers treat it as horizontal physics movement speed.
- GetMovementVelocity() returns Rigidbody.linearVelocity with y forced to 0f.
- PlayerManager.CarController is the DI-facing access path for gameplay systems. DefaultGameplaySceneInstaller binds PlayerManager as IPlayerManager, not ICarController directly.
- VFX behavior is local to the car object. Brake light and trail changes should not become authoritative gameplay state.

## Extension Points

- Safe extension areas:
  - Add new read-only values to ICarController when another system needs car state and the value is owned by CarController.
  - Add car-only visual reactions in CarVfxEffectsController when driven by existing car events, drift direction, or speed data.
  - Tune max speed, acceleration, reverse speed, braking, turn speed, steer response speed (_steerResponseSpeed), grip values, drift thresholds, target drift angle (_targetDriftAngle), drift yaw response speed (_driftYawResponseSpeed), counter-steer impact (_counterSteerImpact), raycast origins/distances, and visual wheel radius via serialized inspector fields.
- Required dependencies and contracts:
  - Keep CarController on the same GameObject as a Rigidbody.
  - Keep CarVfxEffectsController on a GameObject that can resolve ICarController.
  - Preserve the Move and Brake action names unless all code and scene input references are updated together.
  - Preserve PlayerManager as the gameplay-facing aggregate for systems that already depend on IPlayerManager.
- Testing implications:
  - Compile after C# changes.
  - Play-test movement responsiveness, ground snapping, drift entry/exit, steering feel, visual wheel animation, brake lights, and speed trails in the Unity Editor.
  - Regression-check saw blade knockback when changing GetMovementSpeed() semantics.

## Integration Notes

- Upstream dependencies:
  - Unity Input System global actions from Assets/InputSystem/InputSystem_Actions.inputactions.
  - Unity physics through Rigidbody and Physics.Raycast.
  - Serialized car setup for wheel model transforms, center of mass, ground layer mask, mesh renderer, light holder, and trail renderers.
- Downstream consumers:
  - PlayerManager exposes ICarController through IPlayerManager.
  - SawBlade scales knockback using IPlayerManager.CarController.GetMovementSpeed().
  - CarVfxEffectsController uses brake events and movement speed for local visual feedback.
- Cross-system coupling risks:
  - Changing GetMovementSpeed() from rigidbody linear velocity to input-derived speed would alter skill behavior.
  - Ground raycast layer mask must cover all navigable terrain layers to prevent false airborne state.
  - Renaming Input System actions without updating CarController would break movement or braking at runtime.
  - Moving car access out of PlayerManager would require coordinated DI changes and consumer updates.

## Known Risks and Open Questions

- Known limitations:
  - Hard-snapping Rigidbody position Y to terrain height in FixedUpdate prevents vehicle tipping/flipping on slopes, providing maximum arcade stability, but requires terrain elevation changes to be smooth to avoid visual snapping.
  - Per-frame raycast list creation in GetWheelRaycastOrigins was refactored to use a pre-allocated cache field to eliminate GC overhead.
  - Material name matching in CarVfxEffectsController uses prefix matching to support Unity's (Instance) material instantiation.
- Open design questions:
  - Should ICarController be bound directly in Reflex, or should car access continue to be intentionally routed through IPlayerManager?
  - Should brake and speed VFX use event-driven state changes instead of InvokeRepeating polling?
- Suggested follow-up tasks:
  - Verify ground raycast offsets on sloped terrain during play-testing.
