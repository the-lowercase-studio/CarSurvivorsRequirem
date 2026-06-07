# Flow-Field Target Lead Offset Plan

## Summary

Change the flow-field destination from the player center to a speed-scaled point ahead of the player's actual car velocity. This should make enemies path toward where the car is moving, reducing cases where they chase a cell the player has already left or cannot occupy cleanly.

The current setup supports this approach: `RuinedBloodCity` uses 1-unit navigation cells, so offsetting the destination can change the destination cell. Because the offset uses car velocity, the behavior will follow actual physics motion, including drift and sliding, rather than raw input intent.

## Key Changes

- Extend `ICarController` with a read-only velocity-facing API, for example `Vector3 GetMovementVelocity()` or `Vector3 MovementVelocity { get; }`.
- Implement it in `CarController` using `_rb.linearVelocity`, projected onto the XZ plane.
- In `GridManager`, compute the flow-field destination as:
  - player position when horizontal velocity magnitude is below a small threshold, for example `0.1f`;
  - otherwise `playerPosition + velocity.normalized * offsetDistance`.
- Use clamped prediction for offset distance:
  - `offsetDistance = Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset)`;
  - defaults: `_flowFieldTargetPredictionTime = 0.25f`, `_maxFlowFieldTargetOffset = 6f`.
- Keep the player chunk centered on the player, not the predicted target, so enemy-grid scope remains stable and existing teleport/spawn assumptions stay intact.
- Add serialized fields to `GridManager` for prediction tuning and expose them in `GridManagerEditor`.

## Edge Behavior

- No movement or near-zero velocity: destination remains player center.
- Reversing: target offsets behind the car if the car is physically moving backward.
- Drifting/sliding: target follows slide direction, not car nose direction.
- World/grid edges: rely on existing `WorldPosToCellConverter` clamping.
- Player-facing balance: expose prediction time and max offset for Unity play-mode tuning.

## Test Plan

- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Manual Unity checks in `RuinedBloodCity`:
  - idle player: enemies still converge on player center;
  - moving straight: destination/debug flow points ahead of movement;
  - reversing: enemies path toward reverse movement direction;
  - drifting/sliding: enemies lead the actual velocity direction;
  - high speed: offset never exceeds 6 cells/units;
  - near world bounds: no null destination or broken flow-field behavior.

## Assumptions

- The desired behavior is to predict actual car motion, not raw input intent.
- A 0.25 second prediction time and 6-unit cap are first-pass tuning defaults, not final balance.
- No scene, prefab, asset, or meta files should be edited directly for this change unless later requested.
