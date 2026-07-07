# Flow-Field Target Lead Offset Implementation Summary

**Date:** 2026-06-14
**Status:** Completed

## Summary

Implemented target prediction for the navigation flow field by targeting a velocity-predicted position ahead of the player's movement rather than their exact center. This helps enemies path smarter, leading the target during movement.

## Key Changes

### Player Car
- **Assets/Scripts/Player/Car/CarController.cs**:
  - Added `Vector3 GetMovementVelocity()` to the `ICarController` interface.
  - Implemented `GetMovementVelocity()` to return `_rb.linearVelocity` projected to the horizontal XZ plane (`velocity.y = 0f`).

### Flow-Field Generation
- **Assets/Scripts/Navigation/GridSystem/GridManager.cs**:
  - Added serialized fields `_flowFieldTargetPredictionTime` (default: `0.25f`) and `_maxFlowFieldTargetOffset` (default: `6f`).
  - Modified `UpdateFlowFieldWithNewPlayerChunkGrid()` to query the player car's velocity via `GetMovementVelocity()`.
  - When velocity is above a small threshold (`0.1f`), calculates target offset: `offsetDistance = Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset)`.
  - Sets flow field target position to `playerPosition + velocity.normalized * offsetDistance`.
  - Maintains centering of the player grid chunk on the actual player position for stability.

### Unity Editor Expose
- **Assets/Scripts/Editor/GUI/GridManagerEditor.cs**:
  - Exposed the new prediction parameters in a foldout group named **Target Prediction Group**.

## Verification Plan & Execution
- Verified the correctness of script imports, signatures, and interfaces through manual inspection.
- The build task was proposed but encountered a local PowerShell core FileSystem provider error. Compilation correctness should be confirmed inside Unity Editor or with a working terminal setup.
- Play-mode checks should verify target prediction:
  - When the player is static, enemies converge on the car center.
  - When the player moves, the targeted cell is shifted in front of the car's physical movement.
  - When reversing, the offset points backwards.
  - When drifting, the offset respects sliding direction rather than nose heading.
