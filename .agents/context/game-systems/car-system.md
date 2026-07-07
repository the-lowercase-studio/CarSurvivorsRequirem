# Car System Documentation

## Purpose

The Car system owns player vehicle movement input, wheel physics control, brake and drift state handling, wheel model synchronization, and car-specific movement VFX.

It does not own player health, leveling, skills, spawning, camera behavior, enemy movement, or general gameplay state. Those systems access car movement data through the player-facing `ICarController` reference exposed by `PlayerManager`.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Player/Car/CarController.cs`
  - `Assets/Scripts/Player/Car/CarVfxEffectsController.cs`
- Related systems:
  - `Assets/Scripts/Player/PlayerManager.cs`
  - `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`
  - `Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs`
  - `Assets/InputSystem/InputSystem_Actions.inputactions`
- Related docs:
  - `.agents/context/project-coding-standards.md`
  - `.agents/context/ai-game-dev-best-practices.md`

## Architecture and Data Flow

- Core components:
  - `ICarController` is colocated with `CarController` and exposes brake events, current movement speed, and Y-flattened movement velocity.
  - `CarController` is a `MonoBehaviour` requiring a `Rigidbody`. It reads global Input System actions named `Move` and `Brake`, applies torque and steering to configured `WheelCollider` entries, applies brake torque, applies drift friction to rear wheels when drift requirements are met, and copies wheel collider poses to visible wheel model transforms.
  - `CarVfxEffectsController` requires `CarController`, reads `ICarController` from the same GameObject, reacts to brake events, and periodically toggles speed trails based on `GetMovementSpeed()`.
- Runtime flow:
  - `Awake`: `CarController` caches the `Rigidbody`, resolves `Move` and `Brake` actions from `InputSystem.actions`, assigns the configured center of mass, and caches original rear-wheel sideways friction; `CarVfxEffectsController` caches `ICarController`.
  - `OnEnable`/`OnDisable`: `CarController` subscribes and unsubscribes named brake action callbacks that raise `OnBrakePress` and `OnBrakeRelease`; `OnDisable` also restores drift friction when needed. `CarVfxEffectsController` subscribes in `OnEnable` and unsubscribes in `OnDisable` to the car controller's brake events.
  - `Update`: `CarController` reads normalized movement input and current brake state.
  - `FixedUpdate`: `CarController` updates motor torque, front-axle steering, drift state, brake torque, and wheel model poses.
  - `Start` in VFX: `CarVfxEffectsController` finds the stop-light material, configures trail lifetime, disables trail emission, and starts repeated speed threshold checks.

## Rules and Invariants

- The `Move` action must exist and return a `Vector2`; the vertical axis drives wheel motor torque and the horizontal axis drives front-axle steering.
- The `Brake` action must exist as a button action; pressing and releasing it drive both brake torque and brake VFX events.
- `CarController` assumes `_wheels` contains valid `WheelCollider` and wheel model pairs. Front axle entries are the only wheels that receive steering angle changes.
- Drift requires brake input, minimum rigidbody speed, minimum forward input, and minimum steering input. While drifting, rear sideways friction stiffness is multiplied by `_driftRearSidewaysStiffnessMultiplier`, and brake torque uses `_driftBrakeTorqueMultiplier` instead of the normal brake multiplier.
- `GetMovementSpeed()` returns `Rigidbody.linearVelocity.magnitude`; consumers should treat it as physics-frame movement speed, not input strength.
- `GetMovementVelocity()` returns `Rigidbody.linearVelocity` with the vertical component `y` forced to `0f`.
- `PlayerManager.CarController` is the DI-facing access path for gameplay systems. `DefaultGameplaySceneInstaller` binds `PlayerManager` as `IPlayerManager`, not `ICarController` directly.
- VFX behavior is local to the car object. Brake light and trail changes should not become authoritative gameplay state.

## Extension Points

- Safe extension areas:
  - Add new read-only values to `ICarController` when another system needs car state and the value is owned by `CarController`.
  - Add car-only visual reactions in `CarVfxEffectsController` when they can be driven by existing car events or speed data.
  - Tune movement, steering, center of mass, wheel references, drift thresholds, drift friction/brake multipliers, trail threshold, and trail lifetime through serialized fields or scene/prefab setup.
- Required dependencies and contracts:
  - Keep `CarController` on the same GameObject as a `Rigidbody`.
  - Keep `CarVfxEffectsController` on a GameObject that can resolve `ICarController`.
  - Preserve the `Move` and `Brake` action names unless all code and scene input references are updated together.
  - Preserve `PlayerManager` as the gameplay-facing aggregate for systems that already depend on `IPlayerManager`.
- Testing implications:
  - Compile after C# changes.
  - Play-test movement feel, braking, drift entry/exit, rear-wheel friction restoration, wheel animation, brake lights, and speed trails in the Unity Editor because these depend on physics, serialized scene references, materials, and VFX timing.
  - Regression-check saw blade knockback when changing `GetMovementSpeed()` semantics.

## Integration Notes

- Upstream dependencies:
  - Unity Input System global actions from `Assets/InputSystem/InputSystem_Actions.inputactions`.
  - Unity physics through `Rigidbody` and `WheelCollider`.
  - Serialized car setup for wheel colliders, wheel model transforms, center of mass, mesh renderer, light holder, and trail renderers.
- Downstream consumers:
  - `PlayerManager` exposes `ICarController` through `IPlayerManager`.
  - `SawBlade` scales knockback using `IPlayerManager.CarController.GetMovementSpeed()`.
  - `CarVfxEffectsController` uses brake events and movement speed for local visual feedback.
- Cross-system coupling risks:
  - Changing `GetMovementSpeed()` from rigidbody speed to input-derived speed would alter skill behavior.
  - Changing rear-wheel friction caching or restore timing can leave scene wheel colliders in drift friction after disable/re-enable.
  - Renaming Input System actions without updating `CarController` would break movement or braking at runtime.
  - Moving car access out of `PlayerManager` would require coordinated DI changes and consumer updates.
  - Changing serialized wheel data shape can break scene or prefab references.

## Known Risks and Open Questions

- Known limitations:
  - `CarVfxEffectsController` correctly unsubscribes in `OnDisable`, but if the component or GameObject is destroyed without `OnDisable` running (rare in Unity), reference leakage might occur.
  - The stop-light material lookup matches the exact material name `CarStopLights`; Unity material instancing can append suffixes such as `(Instance)` depending on runtime access.
  - The code relies on global `InputSystem.actions`, so tests or alternate input setups need that action asset configured.
  - Normal brake torque multiplier is a private hard-coded field, while drift brake behavior is serialized.
- Open design questions:
  - Should `ICarController` be bound directly in Reflex, or should car access continue to be intentionally routed through `IPlayerManager`?
  - Should brake and speed VFX use event-driven state changes instead of `InvokeRepeating` polling?
  - Should movement input resolution be injected or wrapped for easier testing and input rebinding support?
- Suggested follow-up tasks:
  - Add safe null-conditional event invocation or ensure default subscribers exist before brake actions can fire.
  - Consider a focused coding-standards cleanup for field ordering and constant placement in the Car files.
