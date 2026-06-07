# Car Controller Brake-Assisted Drift Plan

## Summary

Add drift to `CarController` as a physics state layered on top of the existing Brake action.
Drift starts when the Brake action is held, car speed is above a serialized threshold, forward input is held, and left or right steering input is held.
Pressing order does not matter.

Brake remains active during drift. Keep `Assets/InputSystem/InputSystem_Actions.inputactions` unchanged, so Space continues to brake and the gamepad Brake binding can also trigger drift when paired with forward and steering input.

## Key Changes

- Update `Assets/Scripts/Player/Car/CarController.cs` with serialized drift tuning fields:
  - `_minSpeedToDrift`
  - `_minForwardInputToDrift`
  - `_minSteerInputToDrift`
  - `_driftRearSidewaysStiffnessMultiplier`
  - optional `_driftFrictionRestoreSpeed`
- Add private drift state:
  - `_isDrifting`
  - cached original rear-wheel `WheelFrictionCurve.sidewaysFriction` values
- Detect drift in `FixedUpdate` from current input and physics state:
  - Brake is held.
  - `_rb.linearVelocity.magnitude >= _minSpeedToDrift`.
  - `_moveInput.y >= _minForwardInputToDrift`.
  - `Mathf.Abs(_moveInput.x) >= _minSteerInputToDrift`.
- Apply drift by reducing rear wheel sideways friction stiffness while the drift condition is true.
- Restore original rear wheel sideways friction when drift ends.
- Keep `HandleBrake()` behavior intact so brake lights and existing VFX events remain tied to Brake press and release.

## Public Interfaces

- Do not add a new input action.
- Do not directly edit prefabs, scenes, `.asset`, or `.meta` files for the first implementation.
- Avoid adding drift events unless VFX or audio needs them immediately.
- If another system later needs drift state, extend `ICarController` with:
  - `bool IsDrifting { get; }`
  - `event EventHandler OnDriftStart`
  - `event EventHandler OnDriftEnd`

## Test Plan

- Run compile validation:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

- Manual Unity play-mode checks:
  - Space alone still brakes and slows the car.
  - Space + W without A/D brakes but does not drift.
  - Space + W + A or Space + W + D above threshold starts drift.
  - The same combo below threshold does not drift.
  - Releasing Space, W, or steering restores normal grip.
  - Brake lights still turn on and off exactly on Brake press and release.
  - Car remains controllable and does not spin uncontrollably after repeated drift start and stop.

## Assumptions

- Drift should use WheelCollider friction, not direct yaw force.
- Drift applies to the current Brake action, so gamepad Brake plus forward and side stick can also drift.
- Initial tuning should use conservative serialized defaults derived from current controller behavior, then be adjusted in Unity play mode.
- Speed threshold uses `Rigidbody.linearVelocity.magnitude`, matching the current `GetMovementSpeed()` implementation.
- Drift grip changes should use `WheelCollider.sidewaysFriction` and `WheelFrictionCurve.stiffness`, matching Unity's wheel friction model.
