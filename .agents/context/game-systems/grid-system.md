# GridSystem Documentation

## Purpose

GridSystem owns the runtime 2D navigation grid data structures, player-centered grid chunk positioning, spatial coordinate mapping, and visibility/occupancy-filtered cell queries in Car Survivors.

It is responsible for:

- Creating the fixed-size `WorldGrid` based on inspector-authored `GridConfiguration` (dimensions and cell size).
- Managing a mobile `GridPlayerChunk` centered on the player car that reuses underlying `WorldGrid` cell references.
- Coordinating with `FlowField` to calculate cost fields, target-predicted integration fields, and vector flow fields over active grid regions.
- Providing dependency-injected grid access via `IGridManager`.
- Providing spatial query utilities for walkable cells, camera-hidden cells, edge boundaries, obstacle proximity, and world-position-to-cell conversions.
- Providing scene gizmos and debug visualizations for world and chunk grid bounds under `#if DEBUG`.

It is not responsible for:

- Moving enemies or collectibles directly (`FlowFieldMovementController`, `EnemyMovementController`, `ExpParticle` own their movement).
- Deciding enemy wave compositions, difficulty scaling, or drop chances (`WaveManager`, `EnemiesSpawner`, `EnemyDropHandler`).
- Defining physics layers or collision matrices.
- Persisting grid data between scenes.
- Editing scene or prefab references.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Navigation/Constants/GridConstants.cs
  - Assets/Scripts/Navigation/Constants/FlowFieldConstants.cs
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Assets/Scripts/Navigation/GridSystem/Grid.cs
  - Assets/Scripts/Navigation/GridSystem/Cell.cs
  - Assets/Scripts/Navigation/GridSystem/GridDirection.cs
  - Assets/Scripts/Navigation/GridSystem/WorldPosToCellConverter.cs
  - Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs
  - Assets/Scripts/Navigation/GridSystem/CellCameraVisibilityChecker.cs
  - Assets/Scripts/Navigation/GridSystem/CellStatusDescriber.cs
  - Assets/Scripts/Navigation/GridSystem/GridEdgeHelper.cs
  - Assets/Scripts/Navigation/GridSystem/GridDebug.cs
  - Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs
- Related systems:
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs
  - Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
  - Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/Editor/GUI/GridManagerEditor.cs
- Related docs:
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/interactables-system.md
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related skills:
  - .agents/skills/di-integration/SKILL.md when altering `IGridManager` or consumers.
  - .agents/skills/check-optimalization/SKILL.md when adjusting chunk update rates, spatial query loops, or physics overlap tests.
  - .agents/skills/unity-refactor-suggestions/SKILL.md for behavior-preserving cleanups and helper consolidations.

## Architecture and Data Flow

- `GridConfiguration` is a serializable data structure defining `Width`, `Height`, and `CellSize`.
- `Grid` is a runtime data container holding dimensions, cell size, and a 2D `Cell[,]` matrix.
- `Cell` represents an individual navigation cell with:
  - `WorldPos`: centered 3D position `(x * cellSize + 0.5f * cellSize, 0, z * cellSize + 0.5f * cellSize)`.
  - `WorldGridPos`: permanent `Vector2Int` index in `WorldGrid`.
  - `ChunkGridPos`: contextual `Vector2Int` index in `GridPlayerChunk` (`GridConstants.INVALID_CHUNK_GRID_POS` (-1, -1) when outside the chunk).
  - `Cost`: terrain difficulty byte (`DEFAULT_FIELD_COST` = 1, `ROUGH_TERRAIN_COST` = 3, `IMPASSABLE_COST` = 255).
  - `BestCost`: cumulative integration cost (ushort, initialized to `ushort.MaxValue`).
  - `BestDirection`: lowest-cost flow step (`GridDirection.None` by default).
- `GridManager` is the scene `MonoBehaviour` facade implementing `IGridManager`, registered as a singleton in `DefaultGameplaySceneInstaller`.

Runtime flow:

1. `GridManager.Awake`:
   - Allocates `WorldGrid = new Grid(_worldGridConfiguration)`.
   - Allocates `_playerChunkCells = new Cell[width, height]`.
   - Allocates `GridPlayerChunk = new Grid(_playerGridConfiguration, _playerChunkCells)`.
   - Instantiates `FlowField`.
2. `GridManager.OnEnable`:
   - Runs initial flow-field pass over `WorldGrid` targeting the center cell.
   - Schedules `UpdateFlowFieldWithNewPlayerChunkGrid` repeating every `_delayBetweenPlayerChunkGridUpdate` (default `0.32s`).
3. `UpdateFlowFieldWithNewPlayerChunkGrid`:
   - `ClearPlayerChunkCells`: sets all previous chunk cells' `ChunkGridPos` to `(-1, -1)`, `BestDirection` to `GridDirection.None`, and nulls the array entries.
   - `UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid`: finds the closest world cell to the player car, computes `minGridX` / `minGridY` clamped within `[0, WorldGrid.Width - chunkWidth]` and `[0, WorldGrid.Height - chunkHeight]`, and populates `_playerChunkCells` by referencing the corresponding `WorldGrid.Cells`.
   - Velocity prediction: gets car velocity from `_playerManager.CarController.GetMovementVelocity()`; if `speed > 0.1f`, offsets destination by `Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset)`.
   - `GetClampedChunkDestinationCell`: resolves destination cell within `GridPlayerChunk`, falling back to player current cell or chunk center.
   - Invokes `FlowField.CreateCostField`, `CreateIntegrationField`, and `CreateFlowField` over `GridPlayerChunk`.
4. Downstream spatial queries:
   - `EnemiesSpawner`: queries `GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk` to select off-camera spawn points outside the player chunk with enemy density limits.
   - `EnemiesOutsidePlayerChunkTeleporter`: checks enemies outside player chunk bounds and teleports them to hidden walkable cells within `GridPlayerChunk`.
   - `EnemyDropHandler`: calculates walkable landing spots for collectible scatter.
   - `MapInteractablesSpawner`: queries walkable world cells outside the initial player chunk, enforcing distance rules from impassable cells and other interactables.

## Rules and Invariants

- **Coordinate System**: Grid horizontal coordinates map to Unity world X (horizontal) and Z (depth), with Y fixed to 0. Cell world positions are centered at `(index * cellSize + cellSize * 0.5f)`.
- **Coordinate Conversion**: `WorldPosToCellConverter` normalizes coordinates relative to total grid extents (`percentX = worldPos.x / (width * cellSize)`), clamps to `[0, 1]`, and floors to grid indices `[0, width - 1]` and `[0, height - 1]`.
- **Shared Reference Model**: `GridPlayerChunk` does not clone `Cell` objects; it references `WorldGrid.Cells`. Modifying cell costs, best costs, directions, or chunk positions mutates the single shared instance.
- **Walkability Rule**: `CellStatusDescriber.IsWalkable(cell)` returns `true` if and only if `cell.Cost < byte.MaxValue` (i.e. cost < 255).
- **Camera Visibility Rule**: `CellCameraVisibilityChecker.IsCellVisibleFromCamera` uses `camera.WorldToViewportPoint(cellPosition)` and returns `true` when `0 <= x <= 1`, `0 <= y <= 1`, and `z > 0`.
- **Occupancy Querying**: `GridCellsNotVisibleByMainCamera` evaluates enemy occupancy per cell using `Physics.OverlapBoxNonAlloc` with `_occupancyBuffer` (size 32) on `EntityLayers.Enemies` with half-extents `(cellSize * 0.45f, 2f, cellSize * 0.45f)`.
- **Target Prediction**: Prediction lead offset is computed as `velocity.normalized * Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset)` when `speed > 0.1f`.

Preserve these constraints when editing:

- Do not replace `IGridManager` dependency injection with `FindAnyObjectByType<GridManager>()` or singleton access.
- Do not create detached `Cell` instances for chunk updates; preserve the shared reference architecture between `WorldGrid` and `GridPlayerChunk`.
- Do not modify cell cost thresholds or camera visibility margins without auditing spawning, teleporting, and drop mechanics.
- Preserve serialized fields and custom inspector groupings in `GridManagerEditor`.

## Extension Points

- **Spatial Query Helpers**: Add pure query functions in `Assets/Scripts/Navigation/GridSystem/` (e.g. radius scans, line-of-sight checks, path distance queries) operating over `Grid` and `Cell`.
- **Dynamic Grid Layers**: Extend `Cell` with additional runtime metadata (e.g. hazard zones, player scent trails, occupancy flags) when new gameplay systems require grid-based data.
- **Custom Inspector**: Extend `GridManagerEditor` when adding new serialized tuning parameters or debug modes to `GridManager`.
- **Custom Spawner Filters**: Utilize `GridCellsNotVisibleByMainCamera` methods to add specialized spawning or placement filters for bosses, shrines, or environmental hazards.

Testing implications:

- Compile C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- In Unity, verify chunk alignment when driving the car to all four extreme world borders (North, South, East, West corners).
- Verify off-camera enemy spawning near screen edges at multiple camera zoom levels and aspect ratios.
- Verify enemy recycling (`EnemiesOutsidePlayerChunkTeleporter`) does not spawn enemies within the player's view cone.
- Run `check-optimalization` skill before increasing chunk dimensions or decreasing player chunk update delays.

## Integration Notes

Upstream dependencies:

- `IPlayerManager` provides player car transform and velocity via `ICarController.GetMovementVelocity()`.
- `Camera` (injected or passed) provides viewport projection for off-screen visibility checks.
- `TerrainLayers` defines physics masks used during flow field cost evaluation.
- `DefaultGameplaySceneInstaller` binds `GridManager` to `IGridManager`.

Downstream consumers:

- `FlowFieldMovementController` queries `IGridManager.WorldGrid` for current cell flow vectors.
- `EnemiesSpawner` queries `GridPlayerChunk` and `WorldGrid` for off-camera, non-crowded walkable spawn positions.
- `EnemiesOutsidePlayerChunkTeleporter` reads chunk bounds and hidden walkable cells to teleport distant enemies.
- `EnemyDropHandler` validates collectible bounce landing positions on walkable cells.
- `MapInteractablesSpawner` queries walkable world cells with clearance checks to distribute chests, repair stations, and barrels.
- `GolemBoss` accesses `IGridManager.WorldGrid` for boss arena spatial queries.

Cross-system coupling risks:

- Player chunk clearing sets `cell.ChunkGridPos` to `INVALID_CHUNK_GRID_POS` and `cell.BestDirection` to `GridDirection.None` on departing cells; code reading outside the active chunk must handle `GridDirection.None`.
- Off-screen spawning queries perform multiple `Physics.OverlapBoxNonAlloc` tests per candidate cell when `maxEnemiesPerCell > 0`. Large candidate sets or high spawn counts will scale physics query overhead.
- Spatial converters assume a flat Y plane at Y = 0 for 2D cell indexing.

## Known Risks and Open Questions

Known limitations:

- `WorldPosToCellConverter` clamps positions outside grid boundaries to edge cells rather than returning null or out-of-bounds indicators.
- `GridCellsNotVisibleByMainCamera.Shuffle` uses `UnityEngine.Random` rather than a seeded generator, resulting in non-deterministic cell selection order.
- `RandomWalkableCellsFinder.cs` is currently an empty placeholder class in the repository.

Open design questions:

- Should `RandomWalkableCellsFinder` be removed or refactored into a consolidated spatial query utility?
- Should `IGridManager` expose a read-only interface or snapshot view for consumer safety?
- Should camera visibility checks be backed by an injected `ICameraProvider` rather than requiring `Camera` parameters in static helpers?

Suggested follow-up tasks:

- Clean up unused `RandomWalkableCellsFinder.cs` placeholder if no longer needed.
- Profile memory allocations and physics overlap overhead during heavy wave spawning phases.
