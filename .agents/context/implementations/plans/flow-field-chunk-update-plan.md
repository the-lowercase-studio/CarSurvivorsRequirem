# Specification & Plan: Flow Field Navigation & Player Chunk Update Fixes

**Date:** 2026-08-25  
**Author:** Antigravity  
**Target Systems:** Assets/Scripts/Navigation/GridSystem/, Assets/Scripts/Navigation/FlowFieldSystem/, Assets/Scripts/Enemies/Base/, Assets/Scripts/LevelSystem/Exp/  

---

## 1. Overview & Player Experience

- **Summary:** Fixes the runtime defect causing enemies (and EXP particles) to suddenly freeze in place, vibrate/jitter without moving, or oscillate between adjacent grid tiles. The solution guarantees deterministic flow-field generation within the player chunk, eliminates target-prediction overshoots, clears stale direction vectors, and introduces a zero-cost direct-vector fallback for entities residing within the destination cell or outside the active chunk, while fully preserving the high-performance "player-chunk-only" update optimization.
- **Player-Facing Goals:**
  - **Aggressive & Continuous Enemy Swarming:** Enemies consistently and smoothly steer around obstacles and advance toward the player car from all angles without stalling or twitching in place.
  - **Fluid Close-Quarters Combat:** When reaching the player's car, enemies do not freeze at point-blank range; they transition seamlessly into attack collisions.
  - **Smooth EXP Magnetism:** EXP gems floating toward the player car do not get trapped in dead cells or freeze near chunk boundaries.
  - **Zero Frame Drops:** Retaining chunk-only physics queries (1,564 cells vs 46,225 world cells) maintains a smooth 60+ FPS without GC spikes or CPU stalls.
- **In-Scope vs. Out-of-Scope:**
  - **In-Scope:**
    - Assets/Scripts/Navigation/GridSystem/GridManager.cs: Safe destination clamping inside the active player chunk, boundary-safe chunk slicing, and stale ChunkGridPos cleanup.
    - Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs: Safe BFS integration check, explicit GridDirection.None assignment for destination/unreachable cells, and stale cost clearing.
    - Assets/Scripts/Navigation/GridSystem/Cell.cs: Resetting BestDirection in ResetCosts().
    - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs: Direct-vector fallback when inside the destination cell or on unintegrated tiles, preventing separation vector domination.
  - **Out-of-Scope:**
    - Updating all 46,225 cells of WorldGrid every frame (explicitly preserved as an optimization).
    - Changing enemy attack animations, stats, or spawn table logic.

---

## 2. Technical Evaluation of "Player Chunk Only" Optimization

### Optimization Assessment
- **Current Performance Baseline:**
  - WorldGrid is 215 x 215 = 46,225 cells.
  - GridPlayerChunk is 46 x 34 = 1,564 cells (~3.3% of the world grid).
  - Updating the full WorldGrid every 0.32s would require 46,225 Physics.OverlapBoxNonAlloc queries, a BFS queue of up to 46,225 nodes, and 46,225 direction calculations. This would cause noticeable frame stutter on mobile and mid-range PCs.
  - Updating only GridPlayerChunk (1,564 cells) takes under 0.8ms per update, making it essential for runtime performance.

### Identified Risks & Architectural Countermeasures
1. **Risk: Target Prediction Exceeding Chunk Boundaries**
   - *Issue:* High player car velocity projects destination beyond the 46x34 chunk bounds, causing CreateIntegrationField to abort because DestinationCell is outside grid.Cells.
   - *Countermeasure:* Clamp the predicted destination coordinate so that DestinationCell is guaranteed to be a valid cell within the current GridPlayerChunk.
2. **Risk: Enemies Residing Outside the Player Chunk**
   - *Issue:* Enemies spawned far away or left behind as the player drives encounter un-updated cells in WorldGrid.
   - *Countermeasure:* 
     - Existing EnemiesOutsidePlayerChunkTeleporter teleports distant enemies into camera-hidden chunk cells every 2.0s.
     - For the duration before teleportation, FlowFieldMovementController utilizes a cheap direct vector (playerPos - enemyPos).normalized fallback when BestDirection == GridDirection.None, keeping them marching toward the player.
3. **Risk: Stale Direction Vectors & 2-Cell Oscillation Loops**
   - *Issue:* When the chunk moves, cells leaving the chunk retain their old directions. If adjacent cells were written in different frames, they can point at each other (Cell A -> East, Cell B -> West).
   - *Countermeasure:* FlowField.CreateFlowField explicitly sets BestDirection = GridDirection.None for cells without a downhill neighbor, and Cell.ResetCosts() clears BestDirection.

---

## 3. Open Questions & Resolved Decisions

### Resolved Decisions
- [x] **Chunk-Only Cadence:** Keep high-frequency flow-field updates restricted to GridPlayerChunk (1,564 cells).
- [x] **Destination Cell Movement Fallback:** When an entity is located in DestinationCell (where BestCost == 0 and no downhill neighbor exists), FlowFieldMovementController directs the entity straight toward the target world position.
- [x] **Separation Vector Safety:** Prevent _separationVector from dictating movement when gridDir == Vector3.zero; if no directional intent exists and fallback is inactive, velocity drops to zero rather than jittering erratically.
- [x] **Stale Cell Cleanup:** Reset ChunkGridPos and BestDirection whenever cells are recycled or cleared.

### Open Questions (Hard Gate)
- [ ] **Q1:** Should the player chunk automatically lead (offset) toward the car's driving direction so the car has more navigation headroom in front of it when driving at top speed?
  - *Recommendation:* Clamping DestinationCell to the chunk perimeter with an inner margin of 1 cell is sufficient for current top speeds and requires zero extra allocations. If speeds increase in future updates, shifting chunk center by velocity * 0.1s can be introduced.
- [ ] **Q2:** For EXP particles (Assets/Scripts/LevelSystem/Exp/ExpParticle.cs), should they continue using the same FlowFieldMovementController component?
  - *Recommendation:* Yes. The direct-vector destination fallback will also fix the rare issue where EXP particles slow down or fail to enter the car collider when the player stops moving.

---

## 4. Proposed Changes & Implementation Breakdown

### Component 1: Grid System & Chunk Management

#### [MODIFY] Assets/Scripts/Navigation/GridSystem/GridManager.cs
- **Responsibilities to Add/Update:**
  1. `UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid()`:
     - Robustly calculate chunk cell mapping even when player is near WorldGrid boundaries (minGridX < 0 or maxGridX >= WorldGrid.Width).
     - Clear stale ChunkGridPos on previously occupied cells.
  2. `UpdateFlowFieldWithNewPlayerChunkGrid()`:
     - Compute target prediction with safe clamping to ensure DestinationCell is always an active cell inside GridPlayerChunk.
  3. `GetClampedChunkDestinationCell(Vector3 destination)`:
     - Helper to find the closest valid cell within GridPlayerChunk if predicted destination falls outside.

#### [MODIFY] Assets/Scripts/Navigation/GridSystem/Cell.cs
- **Responsibilities to Add/Update:**
  1. `ResetCosts()`: Reset `BestDirection = GridDirection.None;` in addition to Cost and BestCost.

---

### Component 2: Flow Field System

#### [MODIFY] Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs
- **Responsibilities to Add/Update:**
  1. `CreateIntegrationField(NavigationGrid grid, Cell destinationCell)`:
     - Verify destinationCell is non-null and exists in grid.Cells.
     - Protect BFS against invalid chunk indices.
  2. `CreateFlowField(NavigationGrid grid)`:
     - Explicitly assign `currentCell.BestDirection = GridDirection.None;` when `bestCostCell == currentCell` (destination cell or disconnected/unreachable cell).

#### [MODIFY] Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
- **Responsibilities to Add/Update:**
  1. `GetMoveDirectionBasedOnCurrentCell()`:
     - If currentCell.BestDirection is valid and not None, return flow direction vector.
     - **Destination / Unintegrated Fallback:** If currentCell == _gridManager.DestinationCell or BestDirection == GridDirection.None:
       - Calculate direct direction toward _gridManager.DestinationCell.WorldPos (or player position).
       - Return normalized vector.
  2. `MoveOnFlowFieldGrid(float movementSpeed)`:
     - Blend gridDir and _separationVector. If gridDir is zero and direct target is reached, dampen separation to avoid random jitter.

---

## 5. Verification Plan

### Automated Checks
- Solution compilation with zero warnings:
  ```powershell
  dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  ```

### Manual Verification in Unity Editor (RuinedBloodCity scene)
1. **Stationary Player Test:**
   - Stand still in the car surrounded by spawning enemies.
   - Verify that enemies swarm and collide directly with the car rather than stopping 1 meter away or shaking in place.
2. **High-Speed Driving & Swerve Test:**
   - Drive at maximum speed across the map and execute sharp U-turns.
   - Observe enemy behavior at the edges of the camera view: verify that no enemies freeze or oscillate between adjacent cells.
3. **Map Edge Test:**
   - Drive the car to the extreme corners of the map (near boundary colliders).
   - Verify that chunk extraction does not throw index out of range exceptions and that enemies navigate cleanly toward the player.
4. **EXP Particle Collection Test:**
   - Defeat a dense pack of enemies and stop inside the dropped EXP cluster.
   - Verify all EXP particles smoothly converge into the car.
