# Initial D Style Arcade Drift & Directional Drift Lines Implementation Summary

**Date:** 2026-08-09
**Status:** Completed

## Summary

Upgraded the Car system (`CarController` and `CarVfxEffectsController`) from basic deceleration-heavy turning to an authentic **Initial D Style Arcade Drift System**. The new system decouples the vehicle body heading (yaw slip angle) from its physics momentum trajectory, allowing the car to slide sideways (~40° oversteer pose) through curves while preserving forward kinetic velocity. It also introduces directional drift line rendering (`TrailRenderer`) per side (Left Drift vs Right Drift).

## Architecture & Decisions

### 1. Decoupled Yaw Angle (Sideways Initial D Pose)

- Added `DriftYawAngle` property to `ICarController` and `_currentDriftYawAngle` tracking in `CarController`.
- When entering drift (tapping/holding brake while turning at speed >= `_minSpeedToDrift`), the vehicle body rotates into a configurable slip angle (`_targetDriftAngle = 40.0f`).
- Implemented counter-steering support (`_counterSteerImpact = 0.5f`): steering into the corner deepens the sideways slide; steering away straightens the vehicle.

### 2. Forward Velocity Preservation & Drift Friction

- Replaced the emergency braking penalty (`_brakeDeceleration = 40f`) during drift with a gentle drift friction rate (`_driftDeceleration = 5f`).
- Pressing forward throttle (`W`) during drift maintains high cornering momentum through the apex.

### 3. Directional Drift Lines (2 Renderers per side)

- Updated `CarVfxEffectsController` with `TrailRenderer[]` references per side: `_leftDriftTrailRenderers` and `_rightDriftTrailRenderers` (allowing 2 drift trail renderers per side, e.g. front and rear wheels).
- Subscribed to `OnDriftDirectionChanged` and `OnDriftStop` events to enable only the relevant side's trails during active drifting (Left Drift = Left Trails, Right Drift = Right Trails) while muting straight-line speed trails.

## Key Changes

- Assets/Scripts/Player/Car/CarController.cs
  - Updated `ICarController` interface to expose `IsDrifting`, `DriftDirection`, `DriftYawAngle`, `IsGrounded`, `OnDriftStart`, `OnDriftStop`, and `OnDriftDirectionChanged`.
  - Added inspector fields under Arcade Grip & Drift and Initial D Sideways Drift (`_driftDeceleration`, `_targetDriftAngle`, `_driftYawResponseSpeed`, `_counterSteerImpact`, `_minSpeedToDrift = 8f`).
  - Updated `UpdateDriftState()` for drift entry threshold, brake-held drift duration, direction calculation (-1 Left, 1 Right, 0 None), counter-steer adjustment, event dispatch, and drift exit delta reset.
  - Updated `HandleArcadeMovement()` to apply `_driftDeceleration` instead of emergency brake when drifting.
  - Updated `HandleArcadeSteering()` to combine base path turning, steer intensity dynamic arc scaling (`Mathf.Lerp(0.35f, _driftTurnMultiplier, steerIntensity)`), and differential slip angle delta tracking (`_lastAppliedDriftYaw`) to prevent snap-back rotation on drift exit.

- Assets/Scripts/Player/Car/CarVfxEffectsController.cs
  - Added `_leftDriftTrailRenderers` and `_rightDriftTrailRenderers` array inspector fields (2 renderers per side for ground skid marks).
  - Subscribed to `OnDriftDirectionChanged` and `OnDriftStop` in `OnEnable`/`OnDisable`.
  - Updated `UpdateDriftTrails(int driftDirection)` and `ActivateSpeedTrailWhenSpeedExceedsThreshold()` to check `IsGrounded` and suppress trail emission when airborne.
  - Preserved Inspector `Time` setting for ground skid mark disappearance duration.

- .agents/context/game-systems/car-system.md
  - Documented new drift properties, events, and visual trail behavior in system docs.

## Standards & Compliance

- Followed project coding standards from .agents/context/project-coding-standards.md:
  - No LINQ (`System.Linq`) used anywhere.
  - Standard block syntax `{}` used for all methods.
  - Interface `ICarController` colocated above `CarController` in the same file.
  - Field ordering: `[Inject]`, `[SerializeField]`, private non-serialized.
  - Private serialized fields use `_camelCase`.
  - Event naming uses `OnX` pattern.

## Verification

- Automated compilation check:
  - Ran `dotnet build Assembly-CSharp-firstpass.csproj; dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Build completed successfully with **0 errors**.
