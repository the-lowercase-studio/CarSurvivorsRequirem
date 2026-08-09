# Arcade Dual Top Speed System Implementation Summary

**Date:** 2026-08-09
**Status:** Completed

## Summary

Upgraded `CarController` and `ICarController` to support a **Dual Top Speed Ceiling System**. Normal straight-line throttle driving (`W`) caps top forward speed at `_maxForwardSpeed` (16.0 m/s). Sustained drifting past an anti-snaking duration threshold (`_minDriftTimeToBoost` = 0.25s) unlocks acceleration towards `_maxOverallSpeed` (24.0 m/s) at `_driftAcceleration` (18.0 m/s²). Upon exiting drift back onto a straightaway, excess momentum smoothly bleeds down to `_maxForwardSpeed` via `_driftSpeedDecayRate` (5.0 m/s²), providing a window of speed after completing a drift curve.

## Key Changes

### 1. `ICarController` & `CarController.cs`
- Exposed read-only properties `MaxForwardSpeed` and `MaxOverallSpeed` on `ICarController`.
- Added serialized fields:
  - `_maxForwardSpeed` = 16f (standard forward speed cap).
  - `_maxOverallSpeed` = 24f (absolute top speed limit reachable via drift).
  - `_driftAcceleration` = 18f (acceleration rate while drifting).
  - `_driftSpeedDecayRate` = 5f (decay rate when exiting drift above `_maxForwardSpeed`).
  - `_minDriftTimeToBoost` = 0.25f (anti-snaking threshold to prevent short tap abuse).
- Added `_currentDriftDuration` tracking in `UpdateDriftState()`.
- Refactored `HandleArcadeMovement()`:
  - In drift: Check if `_currentDriftDuration >= _minDriftTimeToBoost`. If true, target top speed is `_maxOverallSpeed`, accelerating via `_driftAcceleration`.
  - On exit / normal throttle: If `_currentForwardSpeed > _maxForwardSpeed`, apply `_driftSpeedDecayRate` to smoothly bleed excess speed back to `_maxForwardSpeed`.

### 2. Documentation
- Updated `.agents/context/game-systems/car-system.md` with dual top speed rules and invariants.

## Standards Compliance
- Standard block formatting used.
- Private serialized fields named with `_camelCase`.
- `ICarController` interface colocated above `CarController`.
- No LINQ introduced.

## Verification
- Automated compilation checked via `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` (Build succeeded with 0 errors).
