# FlowFieldSystem Documentation

## Purpose

FlowFieldSystem owns flow-field generation and flow-field-based movement helpers for runtime grid navigation.

It is responsible for:

- Reading terrain layers into per-cell movement costs.
- Building integration costs from a destination cell.
- Assigning each processed cell a best movement direction.
- Providing a reusable movement component that moves entities along the current cell direction.
- Providing debug text for cost, integration, and flow direction fields.

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
  - Assets/Scripts/Enemies/EnemyMovementController.cs
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

- `FlowField` is a plain runtime service object created by `GridManager`.
- `GridManager` owns when the flow field is updated and which grid is processed.
- `Cell` stores generated field data through `Cost`, `BestCost`, and `BestDirection`.
- `GridDirection` defines cardinal and intercardinal direction values used during integration and flow direction selection.
- `FlowFieldMovementController` is a `MonoBehaviour` movement adapter for pooled or scene entities.
- `IFlowFieldMovementController` exposes the movement call used by movement consumers.
- `FlowFieldDebug` creates or updates TextMeshPro labels for field diagnostics under debug builds.

Runtime flow:

1. `GridManager.Awake` creates `WorldGrid` and a `FlowField` instance.
2. `GridManager.OnEnable` initializes the world flow field toward the center of `WorldGrid`.
3. `GridManager` repeatedly creates a player chunk around the player and updates flow-field data for that chunk.
4. `FlowField.CreateCostField` resets each processed cell and assigns costs from terrain overlap checks.
5. `GridManager` computes the destination position using the player's position and speed-clamped velocity offset (Target Prediction), and resolves `DestinationCell` from `WorldGrid`.
6. `FlowField.CreateIntegrationField` starts at `DestinationCell` and propagates `BestCost` through cardinal neighbors.
7. `FlowField.CreateFlowField` compares all neighboring directions and writes each cell's `BestDirection`.
8. `FlowFieldMovementController.MoveOnFlowFieldGrid` finds the mover's current cell in `IGridManager.WorldGrid`, converts `BestDirection.Vector` into Unity X/Z movement, blends in separation, and updates `transform.position`.

Enemy movement and EXP particles both consume `FlowFieldMovementController`; they do not generate flow-field data themselves.

## Rules and Invariants

- `FlowField.CreateCostField` uses `Physics.OverlapBox` with `TerrainLayers.All`.
- Cells touched by cost generation are reset before terrain cost is applied.
- Cells overlapping `TerrainLayers.Impassable` receive the impassable cost.
- Cells overlapping `TerrainLayers.Rough` receive the rough terrain cost when no higher-priority impassable terrain is found.
- Cells with no terrain overlap are treated as impassable by current implementation.
- `CreateIntegrationField` uses `GridDirection.CardinalDirections` for propagation.
- `CreateFlowField` uses `GridDirection.AllDirections`, then writes a cardinal or intercardinal direction through `GridDirection.GetDirectionFromV2I`.
- Movement uses Unity X/Z axes from `Cell.BestDirection.Vector`; Y movement remains zero.
- **Target Prediction**: GridManager computes the target cell ahead of the player using `GetMovementVelocity()` from `ICarController`.
  - When the player speed is above `0.1f`, the destination is offset: `destination = playerPosition + velocity.normalized * Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset)`.
  - Prediction values default to `0.25s` lead time and `6.0f` maximum offset units, adjustable via the inspector foldout **Target Prediction Group**.
- `FlowFieldMovementController` blends grid movement with separation from nearby enemies found through `EntityLayers.Enemy`.
- `FlowFieldMovementController` reads `IGridManager.WorldGrid`, even though current field updates are usually performed on the player chunk after startup.
- Flow-field data lives on shared `Cell` instances. Updating a player chunk can mutate cells also visible through `WorldGrid`.
- `FlowFieldDebug.DisplayFlowFieldDebugTextOnGrid` assumes the configured grid and holder already represent the intended debug target.

Preserve these constraints when editing:

- Do not replace `IGridManager` injection in movement helpers with scene searches or singleton access.
- Do not change cost values, terrain layer interpretation, or field update cadence as a hidden balance change.
- Do not alter movement direction semantics without checking enemies, EXP particles, and grid debug output.
- Treat per-entity physics queries as performance-sensitive before increasing enemy or particle counts.

## Extension Points

- Add terrain-cost behavior in `FlowField.CreateCostField` when a new terrain layer needs to affect navigation.
- Add or adjust debug display modes in `FlowFieldDebug` when new generated cell state needs visualization.
- Add new flow-field movement consumers by using `FlowFieldMovementController` and depending on `IFlowFieldMovementController`.
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

- `GridManager` decides the update target grid and destination.
- `IGridManager` exposes `WorldGrid`, `GridPlayerChunk`, and `DestinationCell` to movement consumers.
- `TerrainLayers` defines terrain masks used for cost generation.
- `EntityLayers.Enemy` defines the separation query mask in `FlowFieldMovementController`.

Downstream consumers:

- `EnemyMovementController` calls `IFlowFieldMovementController.MoveOnFlowFieldGrid` when the enemy can move on the grid.
- `ExpParticle.FixedUpdate` calls `MoveOnFlowFieldGrid` every physics tick while the particle is active.
- `GridDebug` and `FlowFieldDebug` help inspect generated cell state during development.

Cross-system coupling risks:

- `GridManager` resolves `DestinationCell` from `WorldGrid` even when updating the player chunk. This relies on the player chunk sharing `Cell` references with `WorldGrid`.
- `FlowFieldMovementController` reads from `WorldGrid`; stale or incomplete `BestDirection` data can affect movement immediately.
- Separation uses `Physics.OverlapSphere` per movement controller, so enemy count and particle count can affect runtime cost.
- Terrain cost behavior affects enemy movement, EXP particle movement, spawn filtering, and collectible placement indirectly through shared cell state and walkability checks.

## Known Risks and Open Questions

- Known limitations:
  - Player chunks near world-grid edges can contain null cells; `FlowField` correctly handles this using null checks during cost and flow field updates.
  - `DestinationCell` is resolved from `WorldGrid` during chunk updates; if the destination is outside the processed chunk, integration behavior relies on shared references and should be reviewed before refactoring.
  - `FlowFieldMovementController` runs a separation `OverlapSphere` in `FixedUpdate` for every component instance.
  - `FlowFieldDebug` flow direction mode reads `cell.BestDirection.Vector`; this assumes `BestDirection` is never null.

Open design questions:

- Should flow-field generation become an injected service if more systems need to request updates?
- Should movement consumers read from `GridPlayerChunk` when available instead of always reading `WorldGrid`?
- Should separation behavior be enemy-specific instead of shared by all `FlowFieldMovementController` consumers?
- Should edge chunks be clamped, padded, or resized to avoid null cells?

Suggested follow-up tasks:

- Add focused validation for flow-field generation when the player is near each world-grid edge.
- Profile per-entity separation before increasing enemy counts, EXP particle counts, or separation radius.
