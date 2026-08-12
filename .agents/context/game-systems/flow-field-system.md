# FlowFieldSystem Documentation

## Purpose

FlowFieldSystem owns flow-field generation and flow-field-based movement helpers for runtime grid navigation.

It is responsible for:

- Reading terrain layers into per-cell movement costs.
- Building integration costs from a destination cell.
- Assigning each processed cell a best movement direction.
- Providing a reusable movement component (`FlowFieldMovementController`) that moves entities along the current cell direction with local enemy separation.
- Providing debug text for cost, integration, and flow direction fields under `#if DEBUG`.

It is not responsible for:

- Creating or resizing grids.
- Choosing the destination position for grid updates.
- Spawning, teleporting, or pooling enemies and collectibles.
- Defining terrain layers.
- Applying enemy attack, death, stun, or animation behavior.
- Persisting generated field data between scenes.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldDebug.cs
- Related systems:
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
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
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related skills:
  - .agents/skills/check-optimalization/SKILL.md when changing field update cadence, physics queries, grid size, or per-entity separation.
  - .agents/skills/di-integration/SKILL.md when changing `IGridManager` or injected movement dependencies.
  - .agents/skills/unity-refactor-suggestions/SKILL.md for behavior-preserving cleanup proposals.

## Architecture and Data Flow

- `FlowField` is a plain C# service instantiated by `GridManager`.
- `GridManager` owns when the flow field is updated and which grid (`WorldGrid` or `GridPlayerChunk`) is processed.
- `Cell` stores generated field data through `Cost`, `BestCost`, and `BestDirection`.
- `GridDirection` defines cardinal and intercardinal direction values used during integration and flow direction selection.
- `FlowFieldMovementController` is a `MonoBehaviour` movement adapter implementing `IFlowFieldMovementController`.
- `FlowFieldDebug` manages TextMeshPro labels for field diagnostics under debug builds (`#if DEBUG`).

Runtime flow:

1. `GridManager.Awake` creates `WorldGrid` and a `FlowField` instance.
2. `GridManager.OnEnable` initializes the world flow field toward the center of `WorldGrid`.
3. `GridManager` periodically updates `GridPlayerChunk` around the player and updates flow-field data for that chunk.
4. `FlowField.CreateCostField` resets each processed cell in the target grid and assigns costs from `Physics.OverlapBoxNonAlloc` queries.
5. `GridManager` computes the destination position using the player's position and velocity offset (Target Prediction), and resolves `DestinationCell` from `WorldGrid`.
6. `FlowField.CreateIntegrationField` starts at `DestinationCell` (cost = 0, bestCost = 0) and propagates `BestCost` via BFS using cardinal neighbors (`GridDirection.CardinalDirections`).
7. `FlowField.CreateFlowField` compares all neighboring directions (`GridDirection.AllDirections`) and writes each cell's `BestDirection`.
8. `FlowFieldMovementController.MoveOnFlowFieldGrid` looks up the entity's current cell in `IGridManager.WorldGrid`, extracts `BestDirection.Vector` into Unity X/Z movement, blends with local separation calculated in `FixedUpdate`, updates `transform.position`, and returns the movement vector.

Enemy movement (`EnemyMovementController`) and EXP particles (`ExpParticle`) consume `FlowFieldMovementController`; they do not generate flow-field data themselves.

## Rules and Invariants

- `FlowField.CreateCostField` uses `Physics.OverlapBoxNonAlloc` with a 16-element collider buffer (`_terrainColliderBuffer`), `halfExtents = Vector3.one * (grid.CellSize / 2 - 0.05f)`, and `TerrainLayers.All`.
- Cells touched by cost generation are reset (`Cost = 1`, `BestCost = ushort.MaxValue`) before terrain cost is applied.
- If the overlap buffer reaches capacity (16 colliders), cell cost is conservatively set to `IMPASSABLE_COST` (255).
- Cells overlapping `TerrainLayers.Impassable` receive `IMPASSABLE_COST` (255).
- Cells overlapping `TerrainLayers.Rough` receive `ROUGH_TERRAIN_COST` (3) when no impassable layer is present.
- Cells with no terrain overlap (`obstacleCount == 0`) receive `IMPASSABLE_COST` (255).
- `CreateIntegrationField` propagates costs only through cardinal neighbors (`GridDirection.CardinalDirections`).
- `CreateFlowField` evaluates all 8 directions (`GridDirection.AllDirections`) and resolves `GridDirection` via `GridDirection.GetDirectionFromV2I`.
- Movement uses Unity X/Z axes derived from `Cell.BestDirection.Vector`; Y movement remains zero.
- **Target Prediction**: `GridManager` computes target cell lead time using velocity from `ICarController.GetMovementVelocity()`.
  - Offset calculation: `destination = playerPosition + velocity.normalized * Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset)` when `speed > 0.1f`.
- `FlowFieldMovementController` calculates separation in `FixedUpdate` using `Physics.OverlapSphereNonAlloc` (32-element buffer) against `EntityLayers.Enemy`, ignoring its own cached `_selfCollider`.
- `FlowFieldMovementController` reads `IGridManager.WorldGrid` for cell lookup during movement.

Preserve these constraints when editing:

- Do not replace `IGridManager` injection in movement helpers with scene searches or singleton access.
- Do not change cost values, terrain layer interpretation, or field update cadence as a hidden balance change.
- Do not alter movement direction semantics without checking enemies, EXP particles, and grid debug output.
- Treat per-entity physics queries (`OverlapSphereNonAlloc`) as performance-sensitive before increasing enemy or particle counts.

## Extension Points

- Add terrain-cost behavior in `FlowField.CreateCostField` when a new terrain layer needs to affect navigation.
- Add or adjust debug display modes in `FlowFieldDebug` when new generated cell state needs visualization.
- Add new flow-field movement consumers by attaching `FlowFieldMovementController` and injecting `IFlowFieldMovementController`.
- Add new movement filtering or steering behavior in `FlowFieldMovementController` when it should apply to every flow-field mover.
- Keep movement-consumer-specific pauses, rotations, attacks, collection behavior, and pool lifecycle in the owning system rather than in `FlowField`.

Testing implications:

- Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- In Unity, validate movement from several grid positions, including near world-grid edges and near impassable or rough terrain.
- Validate enemy movement and EXP particle collection separately because both consume the same movement component with different gameplay contexts.
- Validate debug display modes for cost, integration, and flow direction when changing field generation.
- Use the `check-optimalization` skill before changing grid dimensions, update intervals, overlap sizes, or separation logic.

## Integration Notes

Upstream dependencies:

- `GridManager` decides the update target grid and destination position.
- `IGridManager` exposes `WorldGrid`, `GridPlayerChunk`, and `DestinationCell` to movement consumers.
- `TerrainLayers` defines terrain masks (`Impassable`, `Rough`, `Ground`, `All`) used for cost generation.
- `EntityLayers.Enemy` defines the separation query mask in `FlowFieldMovementController`.

Downstream consumers:

- `EnemyMovementController` calls `IFlowFieldMovementController.MoveOnFlowFieldGrid` when the enemy moves on the grid.
- `ExpParticle.FixedUpdate` calls `MoveOnFlowFieldGrid` while moving toward the player.
- `GridDebug` and `FlowFieldDebug` draw field state in debug builds.

Cross-system coupling risks:

- `GridManager` resolves `DestinationCell` from `WorldGrid` even when updating `GridPlayerChunk`. This relies on chunk cells sharing `Cell` references with `WorldGrid`.
- `FlowFieldMovementController` reads from `WorldGrid`; stale or incomplete `BestDirection` data can affect movement immediately.
- Separation uses `Physics.OverlapSphereNonAlloc` per movement controller in `FixedUpdate`, so entity count directly impacts physics query volume.
- Terrain cost behavior affects enemy movement, EXP particle movement, spawn filtering, and collectible placement indirectly through shared cell state and walkability checks.

## Known Risks and Open Questions

Known limitations:

- `FlowField.CreateCostField` uses a fixed 16-element collider buffer per cell; cells with >16 colliders default conservatively to impassable cost.
- `DestinationCell` is resolved from `WorldGrid` during chunk updates; if the destination is outside the processed chunk, integration behavior relies on shared references.
- `FlowFieldMovementController` runs separation `OverlapSphereNonAlloc` in `FixedUpdate` for every active component instance.
- `FlowFieldDebug` flow direction display assumes `cell.BestDirection` is not null.

Open design questions:

- Should flow-field generation become an injected service if more systems need to request updates?
- Should movement consumers read from `GridPlayerChunk` when available instead of always reading `WorldGrid`?
- Should separation behavior be enemy-specific instead of shared by all `FlowFieldMovementController` consumers?
- Should edge chunks be clamped, padded, or resized to avoid null cell slots?

Suggested follow-up tasks:

- Add focused validation for flow-field generation when the player is near each world-grid edge.
- Profile per-entity separation before increasing enemy counts, EXP particle counts, or separation radius.

