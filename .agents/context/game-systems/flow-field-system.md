# FlowFieldSystem Documentation

## Purpose

FlowFieldSystem owns flow-field generation and flow-field-based steering and movement helpers for runtime grid navigation in Car Survivors.

It is responsible for:

- Querying terrain colliders to generate per-cell movement costs (`CreateCostField`).
- Calculating cumulative integration pathfinding costs from a target destination cell across the grid using breadth-first search (`CreateIntegrationField`).
- Computing the lowest-cost flow vector for each grid cell (`CreateFlowField`).
- Providing a reusable movement component (`FlowFieldMovementController`) that translates cell flow vectors into world-space entity movement while applying dynamic local separation against other enemies.
- Providing debug overlays and text visualizers for cost, integration, and flow direction fields under `#if DEBUG`.

It is not responsible for:

- Allocating, sizing, or moving grid data structures (`GridManager` owns grids).
- Selecting or predicting target destination positions (managed by `GridManager` via player position and velocity).
- Spawning, teleporting, or pooling enemies, particles, or collectibles.
- Defining physics layers or collision matrices.
- Controlling enemy attack states, stun logic, death sequences, or animations.
- Persisting flow-field data across scene transitions.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Navigation/Constants/FlowFieldConstants.cs
  - Assets/Scripts/Navigation/Constants/GridConstants.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldDebug.cs
- Related systems:
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Assets/Scripts/Navigation/GridSystem/Grid.cs
  - Assets/Scripts/Navigation/GridSystem/Cell.cs
  - Assets/Scripts/Navigation/GridSystem/GridDirection.cs
  - Assets/Scripts/Navigation/GridSystem/WorldPosToCellConverter.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticle.cs
  - Assets/Scripts/LayerMasks/TerrainLayers.cs
  - Assets/Scripts/LayerMasks/EntityLayers.cs
- Related docs:
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/collisions-and-physics-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related skills:
  - .agents/skills/check-optimalization/SKILL.md when altering update intervals, physics query volumes, grid dimensions, or separation logic.
  - .agents/skills/di-integration/SKILL.md when modifying `IGridManager` or `IFlowFieldMovementController` bindings.
  - .agents/skills/unity-refactor-suggestions/SKILL.md for behavior-preserving optimizations and cleanups.

## Architecture and Data Flow

- `FlowField` is a pure C# service instantiated and invoked by `GridManager`.
- `GridManager` controls the timing and scope of flow-field generation, running updates primarily over `GridPlayerChunk` at runtime and `WorldGrid` during initialization.
- `Cell` stores navigation state: `Cost` (byte terrain difficulty), `BestCost` (ushort cumulative path cost), and `BestDirection` (`GridDirection` unit step).
- `GridDirection` defines cardinal and intercardinal direction objects and handles vector-to-direction lookups.
- `FlowFieldMovementController` is a `MonoBehaviour` adapter implementing `IFlowFieldMovementController`, attached to dynamic entities (`Enemy`, `ExpParticle`).
- `FlowFieldDebug` renders TextMeshPro world labels displaying cell costs, integration values, or direction vectors when enabled in debug builds.

Runtime flow:

1. `GridManager.Awake` initializes `WorldGrid`, allocates `_playerChunkCells`, creates `GridPlayerChunk`, and instantiates `FlowField`.
2. `GridManager.OnEnable` initializes the full `WorldGrid` flow field targeting the center cell.
3. `GridManager.UpdateFlowFieldWithNewPlayerChunkGrid` triggers periodically (default every `0.32s`):
   - Repositions `GridPlayerChunk` around the player car in `WorldGrid`.
   - Computes target destination using player car velocity and target prediction lead time.
   - Selects and clamps `DestinationCell` within `GridPlayerChunk`.
4. `FlowField.CreateCostField` resets each cell in the target grid (`cell.ResetCosts()`) and performs `Physics.OverlapBoxNonAlloc` against `TerrainLayers.All`:
   - Query box half-extents: `(cellSize * 0.49f, 1.0f, cellSize * 0.49f)`.
   - If colliders hit `TerrainLayers.Impassable` or if the 16-collider buffer is saturated, cost is increased by `IMPASSABLE_COST` (255).
   - If colliders hit `TerrainLayers.Rough` and no impassable layer is present, cost is increased by `ROUGH_TERRAIN_COST` (3).
   - If no `TerrainLayers.Ground` is detected (or 0 obstacles returned), the cell is treated as missing ground and assigned `IMPASSABLE_COST` (255).
5. `FlowField.CreateIntegrationField` initializes `DestinationCell` (`Cost = 0`, `BestCost = 0`) and propagates path costs via breadth-first search across cardinal neighbors (`GridDirection.CardinalDirections`):
   - Neighbor candidate cost: `currentNeighbour.Cost + currentCell.BestCost`.
   - If candidate cost is strictly less than `currentNeighbour.BestCost`, `BestCost` is updated and the neighbor is enqueued in `_cellsToCheck`.
6. `FlowField.CreateFlowField` iterates all cells in the grid, inspects all 8 neighbors (`GridDirection.AllDirections`), finds the neighbor with the minimum `BestCost`, and sets `currentCell.BestDirection` to point toward that neighbor via `GridDirection.GetDirectionFromV2I(bestCostCell.WorldGridPos - currentCell.WorldGridPos)`. If no neighbor has a lower cost, `BestDirection` is set to `GridDirection.None`.
7. Dynamic entity movement:
   - In `FixedUpdate`, `FlowFieldMovementController` runs `PreventEntitiesFromStackingOnEachOther` using `Physics.OverlapSphereNonAlloc` (radius `1.2f`, buffer 32) against `EntityLayers.Enemies` to calculate `_separationVector`.
   - `EnemyMovementController.MovementHandler` or `ExpParticle.FixedUpdate` calls `IFlowFieldMovementController.MoveOnFlowFieldGrid(speed)`.
   - `MoveOnFlowFieldGrid` looks up the entity's current cell in `IGridManager.WorldGrid`, reads `BestDirection`, blends the normalized grid vector with `_separationVector`, updates `transform.position`, and returns the frame displacement.

## Rules and Invariants

- **Physics Query Sizing**: `FlowField.CreateCostField` uses `Vector3(grid.CellSize * 0.49f, FlowFieldConstants.QUERY_BOX_VERTICAL_HALF_EXTENT, grid.CellSize * 0.49f)` with `QUERY_BOX_VERTICAL_HALF_EXTENT = 1.0f`.
- **Collider Buffer Limit**: `_terrainColliderBuffer` has fixed capacity `FlowFieldConstants.TERRAIN_COLLIDER_BUFFER_SIZE` (16). Saturated queries conservatively default to impassable.
- **Cost Constants**:
  - `DEFAULT_FIELD_COST` = 1 (normal ground).
  - `ROUGH_TERRAIN_COST` = 3 (slow/rough terrain).
  - `IMPASSABLE_COST` = 255 (walls, obstacles, or voids with no ground collider).
- **Integration Topology**: Integration strictly expands across cardinal neighbors (North, East, South, West). Diagonal expansion during integration is disallowed to prevent path clipping through diagonal obstacle corners.
- **Flow Direction Topology**: Flow vector evaluation evaluates all 8 cardinal and intercardinal directions (`GridDirection.AllDirections`) to permit smooth 8-way diagonal steering.
- **Coordinate System**: Grid horizontal axes map to Unity world X (horizontal) and Z (depth). The Y component in flow vectors is strictly zero (`Vector3(gridDirection.x, 0, gridDirection.y)`).
- **Movement Fallback Hierarchy**:
  1. If current cell has a valid `BestDirection` (`!= GridDirection.None`), combine grid direction with `_separationVector`.
  2. If cell direction is `None` but `_separationVector != Vector3.zero`, apply dampened separation (`_separationVector * 0.1f`) to prevent oscillations when resting directly on destination.
  3. If cell direction is `None` and outside chunk/destination, direct straight toward `_gridManager.DestinationCell.WorldPos`.
  4. Otherwise, zero movement.
- **Entity Separation**: Separation queries evaluate `EntityLayers.Enemies` using `Physics.OverlapSphereNonAlloc` with `FlowFieldConstants.SEPARATION_COLLIDER_BUFFER_SIZE` (32), excluding the mover's own `_selfCollider`.

Preserve these constraints when editing:

- Do not replace Reflex injection of `IGridManager` with singleton or scene lookup patterns.
- Do not modify cell cost values, terrain layer evaluations, or grid update frequencies without checking enemy movement feel and performance budgets.
- Do not alter `GridDirection` vector semantics without auditing movement controllers, EXP particles, and debug renderers.
- Keep per-entity physics queries in `FixedUpdate` lightweight; monitor total active enemy count and separation buffer allocations.

## Extension Points

- **New Terrain Types**: Extend `FlowField.CreateCostField` to recognize additional layer masks (e.g. mud, hazards) and assign custom cell costs.
- **Movement Steering Flavors**: Extend `FlowFieldMovementController` with optional steering behaviors (such as obstacle avoidance or flocking) or expose tunable separation parameters per entity archetype.
- **New Navigation Consumers**: Attach `FlowFieldMovementController` and inject `IFlowFieldMovementController` into any new dynamic entity that needs to navigate toward the player.
- **Debug Modes**: Add new visualization modes to `FlowFieldDebug.DisplayMode` (e.g. occupancy heatmap, velocity vectors) under `#if DEBUG`.

Testing implications:

- Compile C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- In Unity, verify entity navigation across terrain boundaries (open ground, rough terrain, impassable walls, and grid corners).
- Test enemy navigation and EXP particle collection separately to ensure separation dynamics do not adversely affect particle convergence.
- Validate flow field behavior when the player car drives along world grid boundaries and reverses direction rapidly.
- Run `check-optimalization` skill before increasing grid chunk sizes or decreasing update delay intervals.

## Integration Notes

Upstream dependencies:

- `GridManager` schedules updates, repositions chunks, and sets the active destination cell.
- `IGridManager` provides read access to `WorldGrid`, `GridPlayerChunk`, and `DestinationCell`.
- `TerrainLayers` provides layer masks (`Impassable`, `Rough`, `Ground`, `All`) for physics cost queries.
- `EntityLayers.Enemies` provides layer masks for neighbor separation queries.
- `IPlayerManager` provides player car transform and velocity for target prediction.

Downstream consumers:

- `EnemyMovementController` calls `IFlowFieldMovementController.MoveOnFlowFieldGrid` for enemy movement and rotation towards flow direction.
- `ExpParticle` calls `MoveOnFlowFieldGrid` in `FixedUpdate` to home in on the player.
- `FlowFieldDebug` renders cost, integration, and direction values in the editor scene view.

Cross-system coupling risks:

- `GridPlayerChunk` contains direct references to `Cell` instances owned by `WorldGrid`. Resetting costs or directions during chunk updates immediately affects the shared cell objects.
- `FlowFieldMovementController` queries `IGridManager.WorldGrid` for cell lookup; incomplete or out-of-date flow vectors cause fallback straight-line movement.
- High enemy counts multiply `Physics.OverlapSphereNonAlloc` calls in `FixedUpdate`, creating physics query overhead if separation radius or enemy density increases significantly.

## Known Risks and Open Questions

Known limitations:

- `FlowField.CreateCostField` uses a fixed 16-element collider buffer per cell. Dense clusters of overlapping decorative colliders may saturate the buffer and cause false impassable markings.
- If the predicted destination cell falls outside `GridPlayerChunk` boundaries, `GridManager` clamps the destination to the chunk boundary, which can momentarily orient edge enemies toward the boundary instead of the player's true position.
- `FlowFieldMovementController.MoveOnFlowFieldGrid` scales displacement using `Time.deltaTime` even when invoked from `FixedUpdate`.

Open design questions:

- Should flow field generation be refactored into an independent injected service (`IFlowFieldGenerator`) to decouple calculation logic from `GridManager`?
- Should `FlowFieldMovementController` use `Time.fixedDeltaTime` when driven by physics loops?
- Should dynamic separation be moved to a centralized separation manager or spatial hash grid to eliminate per-entity `OverlapSphereNonAlloc` physics queries?

Suggested follow-up tasks:

- Validate flow-field navigation smoothness when the player car moves at maximum boosted speed across chunk borders.
- Profile `Physics.OverlapSphereNonAlloc` overhead during 300+ enemy swarm events.
