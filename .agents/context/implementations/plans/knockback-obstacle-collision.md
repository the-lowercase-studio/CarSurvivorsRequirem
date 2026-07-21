# Knockback Obstacle Collision Plan

Date: 2026-07-21

## Summary

Prevent enemies from clipping through impassable environment obstacles (`TerrainLayers.Impassable`) during knockback effects (e.g. Chainsaw / SawBlade skill, Landmine traps).

Currently, knockback moves enemies using `transform.DOMove` in `EnemyMovementController.MoveToPositionInTimeIgnoringSpeed`, which interpolates `transform.position` linearly without checking environment collisions. To resolve this without introducing high PhysX overhead, we perform a `Physics.SphereCast` sweep along the knockback trajectory against `TerrainLayers.Impassable` before starting the tween. If a wall or obstacle is detected on the trajectory, the target knockback position is clamped to stop just short of the obstacle.

## Key Changes

### Assets/Scripts/Enemies/Base/EnemyMovementController.cs

- Add obstacle check parameters:
  - `_obstacleCheckRadius = 0.4f` (matches enemy bounds)
  - `_obstacleCheckCenterOffset = new Vector3(0, 0.5f, 0)` (casts at torso level rather than ground pivot)
  - `_obstacleSafetyBuffer = 0.1f` (prevents overlapping inside wall geometry)
- In `MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time)`:
  - Calculate direction and distance from `transform.position` to target `pos`.
  - Cast a `Physics.SphereCast` using origin `transform.position + _obstacleCheckCenterOffset`, radius `_obstacleCheckRadius`, direction `direction`, and distance `distance` against `TerrainLayers.Impassable`.
  - If a hit is detected, clamp the target position:
    ```csharp
    float safeDistance = Mathf.Max(0f, hit.distance - _obstacleSafetyBuffer);
    pos = transform.position + (direction * safeDistance);
    ```
  - Pass the clamped `pos` to `transform.DOMove(pos, time)`.

## Edge Behavior

- **Knockback directly into a wall**: The enemy moves up to the wall minus the safety buffer and stops cleanly.
- **Zero distance / near target**: No SphereCast performed if distance is below threshold (e.g. `0.01f`).
- **No obstacle hit**: Full knockback distance is preserved as before.
- **Dynamic ease**: DOTween ease curve remains intact for the reduced distance.

## Proposed Changes

### Enemies System

#### [MODIFY] Assets/Scripts/Enemies/Base/EnemyMovementController.cs
- Add `Physics.SphereCast` obstacle check in `MoveToPositionInTimeIgnoringSpeed`.
- Clamp target position when colliding with `TerrainLayers.Impassable`.

## Verification Plan

### Automated Tests
- Build project targeting C# assembly to confirm no syntax or compile errors:
  ```powershell
  dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  ```

### Manual Verification
- In Unity Play Mode (`RuinedBloodCity` scene):
  - Test Chainsaw (SawBlade) skill against enemies near walls / buildings.
  - Verify enemies are pushed back towards walls but stop cleanly without passing through or getting stuck inside wall colliders.
  - Test Landmine traps near impassable terrain to ensure explosion knockback respects terrain boundaries.
