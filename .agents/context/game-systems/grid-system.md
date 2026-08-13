# GridSystem Documentation

## Purpose

GridSystem owns the runtime grid data used for movement, spawning, and camera-visibility-filtered cell queries in gameplay scenes.

It is responsible for:

- Creating the world grid from inspector-authored dimensions and cell size.
- Maintaining a smaller player-centered grid chunk.
- Updating cell terrain cost, integration cost, and best flow direction through the FlowField system.
- Providing grid access through `IGridManager`.
- Offering helper queries for walkable cells, camera-hidden cells, edge cells, and world-position-to-cell conversion.

It is not responsible for:

- Moving enemies or collectibles directly.
- Choosing spawn waves or spawn chance distributions.
- Owning terrain layer definitions.
- Persisting grid data between scenes.
- Editing scene or prefab references.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Assets/Scripts/Navigation/GridSystem/Grid.cs
  - Assets/Scripts/Navigation/GridSystem/Cell.cs
  - Assets/Scripts/Navigation/GridSystem/GridDirection.cs
  - Assets/Scripts/Navigation/GridSystem/WorldPosToCellConverter.cs
  - Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs
  - Assets/Scripts/Navigation/GridSystem/CellCameraVisibilityChecker.cs
  - Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs
  - Assets/Scripts/Navigation/GridSystem/GridEdgeHelper.cs
  - Assets/Scripts/Navigation/GridSystem/CellStatusDescriber.cs
  - Assets/Scripts/Navigation/GridSystem/GridDebug.cs
- Related systems:
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/Editor/GUI/GridManagerEditor.cs
- Related docs:
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
  - .agents/context/technology-documentation.md
- Related skills:
  - .agents/skills/di-integration/SKILL.md when changing grid bindings or injected consumers.
  - .agents/skills/check-optimalization/SKILL.md when changing update cadence, physics queries, or cell scan behavior.
  - .agents/skills/unity-refactor-suggestions/SKILL.md for behavior-preserving cleanup proposals.

## Architecture and Data Flow

- `GridConfiguration` is a serializable data holder with `Width`, `Height`, and `CellSize`.
- `Grid` is a runtime data container with `Width`, `Height`, `CellSize`, and a `Cell[,]` array.
- `Cell` stores world position, world-grid index, chunk-grid index, terrain cost (`Cost`), integration cost (`BestCost`), and flow direction (`BestDirection`).
- `GridDirection` defines cardinal and intercardinal direction objects used by flow-field navigation.
- `GridManager` is the scene `MonoBehaviour` facade and implements `IGridManager`.
- `DefaultGameplaySceneInstaller` registers the scene `GridManager` instance as `IGridManager`.

Runtime flow:

1. `GridManager.Awake` creates `WorldGrid` from `_worldGridConfiguration`, constructs `GridPlayerChunk` with a `Cell[,]` buffer from `_playerGridConfiguration`, and instantiates `FlowField`.
2. `GridManager.OnEnable` initializes the world flow field toward the world-grid center cell `[WorldGrid.Width / 2, WorldGrid.Height / 2]`.
3. `GridManager.OnEnable` schedules `UpdateFlowFieldWithNewPlayerChunkGrid` via `InvokeRepeating` using `_delayBetweenPlayerChunkGridUpdate` (default `0.32s`).
4. Each player-chunk update finds the cell closest to the player in `WorldGrid` using `WorldPosToCellConverter`.
5. `UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid` populates `GridPlayerChunk` around that world cell by reusing references to cells from `WorldGrid`.
6. `UpdateFlowField` runs cost-field (`CreateCostField`), target prediction for destination cell, integration-field (`CreateIntegrationField`), and flow-field (`CreateFlowField`) generation for the selected grid.
7. Consumers read `IGridManager.WorldGrid`, `IGridManager.GridPlayerChunk`, or `IGridManager.DestinationCell`.

The world grid and player chunk are not independent cell stores. The player chunk contains references to cells owned by `WorldGrid`; updating cost, best cost, best direction, or chunk index through one grid affects the same `Cell` instance seen through the other.

## Rules and Invariants

- Grid coordinates use Unity X/Z as horizontal axes; cell world positions are centered at `(cellSize * 0.5, 0, cellSize * 0.5)` and advance by `CellSize`.
- `WorldGridPos` is the cell's stable index in `WorldGrid`.
- `ChunkGridPos` is mutable and is rewritten when a world cell is included in the current player chunk.
- `WorldPosToCellConverter` clamps positions outside grid bounds to the nearest valid cell.
- `CellStatusDescriber.IsWalkable` treats any cell with `Cost < byte.MaxValue` as walkable.
- `FlowField.CreateCostField` resets each cell in the target grid before evaluating terrain layer overlaps.
- `FlowField.CreateIntegrationField` uses cardinal neighbors for integration cost propagation.
- `FlowField.CreateFlowField` uses all directions when choosing each cell's best direction.
- **Target Prediction**: `GridManager` calculates the flow-field destination cell using velocity-based prediction (multiplying player car velocity from `ICarController.GetMovementVelocity()` by `_flowFieldTargetPredictionTime`, clamped to `_maxFlowFieldTargetOffset`) to path enemies ahead of player movement when speed is above `0.1f`.
- `DestinationCell` is resolved from `WorldGrid` using the target prediction destination, even when the flow field update is running over `GridPlayerChunk`.
- Enemy spawning and enemy teleporting use `GridPlayerChunk` / `WorldGrid` and avoid cells visible to the main camera via `CellCameraVisibilityChecker` and `GridCellsNotVisibleByMainCamera`.
- Debug grid and flow-field drawing are compiled under `#if DEBUG`.

Preserve these constraints when editing:

- Do not replace `IGridManager` consumers with direct scene lookups.
- Do not change cell cost semantics without checking flow-field movement, enemy spawn filtering, and collectible spawn placement.
- Do not alter player-chunk sizing or update cadence as a hidden balance change.
- Preserve inspector fields on `GridManager`; they are part of scene setup.

## Extension Points

- Add new grid consumers by injecting `IGridManager` through Reflex where DI is already active.
- Add new cell query helpers as small static utilities in Assets/Scripts/Navigation/GridSystem/ when they are pure queries over `Grid` or `Cell`.
- Add new terrain cost behavior in `FlowField.CreateCostField` only after checking Assets/Scripts/LayerMasks/ and all movement/spawn consumers.
- Add debug-only visualization through `GridDebug` or `FlowFieldDebug` under the existing debug flow.
- Extend the custom inspector in `GridManagerEditor` if new serialized `GridManager` settings need designer access.

Testing implications:

- Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- In-editor validation is required for scene wiring, layer-mask behavior, camera visibility filtering, debug drawing, and spawn feel.
- Movement changes should be checked with enemies moving from several positions, including near world-grid edges.
- Spawn changes should be checked with the camera near center and near grid edges.

## Integration Notes

Upstream dependencies:

- `GridManager` depends on `IPlayerManager` for player transform and car controller velocity.
- `FlowField.CreateCostField` depends on Unity physics and `TerrainLayers`.
- `GridCellsNotVisibleByMainCamera` and `CellCameraVisibilityChecker` depend on `Camera`.
- `DefaultGameplaySceneInstaller` must reference the scene `GridManager` and bind it to `IGridManager`.

Downstream consumers:

- `FlowFieldMovementController` reads `IGridManager.WorldGrid` and moves along the current cell's `BestDirection`.
- `EnemiesSpawner` gets hidden walkable cells from `IGridManager.GridPlayerChunk` or `IGridManager.WorldGrid`.
- `EnemiesOutsidePlayerChunkTeleporter` uses `GridPlayerChunk` bounds and hidden walkable cells to recycle enemies back into the active area.
- `EnemyDropHandler` finds walkable cells in `IGridManager.WorldGrid` for collectible drop placement.
- `GridManagerEditor` exposes grouped inspector controls for world grid, player chunk, and debug settings.

Cross-system coupling risks:

- Flow-field data is stored directly on `Cell`, so any code that resets costs or best directions affects movement immediately.
- Player chunk cells reuse world cells, so `ChunkGridPos` is contextual and should not be treated as a stable world coordinate.
- Visibility-based spawning requires a valid `Camera` instance passed to query methods.
- Cell scans and physics overlap checks scale with grid size and update cadence.

## Known Risks and Open Questions

Known limitations:

- `UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid` can leave null slots in `GridPlayerChunk.Cells` near world-grid edges because out-of-bounds world cells are cleared to null.
- The current flow-field update passes a `DestinationCell` from `WorldGrid` into integration over `GridPlayerChunk`. This relies on shared cell references and may be fragile if the destination cell falls outside the player chunk bounds.
- `GridDirection.GetDirectionFromV2I` compares `GridDirection` instances to a `Vector2Int` through the implicit conversion path; keep this behavior in mind before refactoring equality or direction lookup.
- `GridCellsNotVisibleByMainCamera.Shuffle` uses `UnityEngine.Random`, so cell ordering is non-deterministic unless Unity's random state is explicitly controlled.

Open design questions:

- Should player chunk edge behavior clamp, pad, or resize instead of leaving null cell slots?
- Should `IGridManager` expose read-only grid views to prevent accidental mutation by consumers?
- Should camera visibility checks use an injected camera provider instead of expecting camera parameters in static helpers?
- Should `RandomWalkableCellsFinder` be removed or implemented if it remains an empty class placeholder?

Suggested follow-up tasks:

- Add a focused test or debug validation for player chunk creation at all four world-grid edges.
- Review flow-field update frequency and physics overlap cost with the `check-optimalization` skill before changing grid sizes or update delays.

