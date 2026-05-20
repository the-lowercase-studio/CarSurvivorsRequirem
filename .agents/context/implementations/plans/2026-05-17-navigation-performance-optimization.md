# Navigation Performance Optimization Plan

## Purpose

Incrementally reduce CPU cost and managed allocations in the current navigation stack:

- `Assets/Scripts/Navigation/GridSystem/`
- `Assets/Scripts/Navigation/FlowFieldSystem/`
- enemy spawn/teleport consumers that query grid cells
- enemy and exp-particle consumers that move through flow-field navigation

This plan is performance-focused. Preserve current gameplay behavior, movement direction semantics, terrain cost semantics, spawn rules, collectible occupancy, serialized fields, scene references, Reflex bindings, and debug behavior unless a phase explicitly calls out a user-approved behavior change.

## Source Documents

- `AGENTS.md`
- `.agents/README.md`
- `.agents/context/project-coding-standards.md`
- `.agents/context/grid-system.md`
- `.agents/context/flow-field-system.md`
- `.agents/context/enemies-system.md`
- `.agents/context/level-system.md`
- `.agents/context/technology-documentation.md`
- `.agents/skills/check-optimalization/SKILL.md`

Unity API references to verify during implementation:

- `Physics.OverlapBoxNonAlloc`
- `Physics.OverlapSphereNonAlloc`
- `Camera.main`

## Current Pressure Points

### Flow-Field Cost Generation Allocates And Performs Many Physics Queries

`FlowField.CreateCostField` currently runs `Physics.OverlapBox` for every processed cell. The repeated chunk update path means this can allocate collider arrays and perform many physics queries at the `_delayBetweenPlayerChunkGridUpdate` cadence.

Target file:

- `Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs`

### Player Chunk Rebuild Allocates On Every Update

`GridManager.UpdateFlowFieldWithNewPlayerChunkGrid` rebuilds `GridPlayerChunk` by creating a new `Cell[,]` and a new `Grid` every update.

Target file:

- `Assets/Scripts/Navigation/GridSystem/GridManager.cs`

### Integration And Flow Generation Allocate Neighbor Lists

`FlowField.GetNeighbourCells` creates a new `List<Cell>` for every processed cell during both integration field generation and flow direction generation.

Target file:

- `Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs`

### Per-Entity Separation Runs Physics Every FixedUpdate

`FlowFieldMovementController.FixedUpdate` runs `Physics.OverlapSphere` for every attached mover. This affects enemies and exp particles because both consume the same movement controller.

Target files:

- `Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs`
- `Assets/Scripts/Enemies/EnemyMovementController.cs`
- `Assets/Scripts/LevelSystem/Exp/ExpParticle.cs`

### Spawn Cell Queries Allocate Through LINQ And Shuffle

Enemy spawning and teleporting use grid helper methods that build lists, shuffle through LINQ ordering, and sometimes convert back to lists or arrays.

Target files:

- `Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs`
- `Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs`
- `Assets/Scripts/Enemies/EnemiesSpawner.cs`
- `Assets/Scripts/Enemies/EnemiesOutsidePlayerChunkTeleporter.cs`

### Minor Hot-Path LINQ And Lookup Costs

`GridDirection.GetDirectionFromV2I` uses LINQ over a small static direction list during flow-field generation. This is small but easy to remove in the same focused pass.

Target file:

- `Assets/Scripts/Navigation/GridSystem/GridDirection.cs`

## Invariants

1. Preserve `IGridManager` as the access boundary for grid data.
2. Do not introduce singleton access, global mutable services, or scene-wide lookup shortcuts.
3. Preserve current terrain semantics:
   - impassable terrain uses impassable cost;
   - rough terrain uses rough cost if no higher-priority impassable terrain is found;
   - no terrain overlap currently makes the cell impassable.
4. Preserve current flow-field generation order:
   - cost field;
   - destination cell resolution from `WorldGrid`;
   - integration field;
   - flow field.
5. Preserve current movement semantics:
   - movers read directions from `IGridManager.WorldGrid`;
   - movement uses Unity X/Z axes;
   - Y movement remains zero.
6. Preserve current player chunk size, update delay, and inspector fields unless the user separately approves balance or scene-setup changes.
7. Preserve collectible occupancy on `Cell.IsOccupiedByCollectible`.
8. Keep debug-only behavior under existing debug conditions.
9. Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly.
10. Compile after source changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.

## Phase 1: Remove Allocations From Flow-Field Cell Processing

Status: ready.

Scope:

- `Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs`
- `Assets/Scripts/Navigation/GridSystem/GridDirection.cs`

Implementation direction:

1. Replace `Physics.OverlapBox` with `Physics.OverlapBoxNonAlloc`.
2. Add a reusable collider buffer owned by `FlowField`.
3. Pick a conservative fixed buffer size first and document truncation risk if the buffer fills.
4. Move repeated half-extents calculation outside the inner cell loop where possible.
5. Replace per-cell neighbor `List<Cell>` creation with direct neighbor iteration or a reusable small buffer.
6. Replace `GridDirection.GetDirectionFromV2I` LINQ lookup with a direct branch or switch-style lookup.
7. Keep all cost values and direction outputs behavior-identical.

Risk notes:

- NonAlloc physics calls truncate results when the buffer is too small. If a cell can overlap many terrain colliders, detect a full buffer and treat it conservatively or expose the buffer size through a safe constant.
- Changing neighbor iteration must preserve cardinal-only integration and all-direction flow selection.
- `CreateFlowField` currently includes `GridDirection.None` in the neighbor direction set. Preserve the resulting behavior unless intentionally cleaned up after validation.

Validation:

1. Compile.
2. In Unity, test enemy movement toward the player from open ground, rough terrain, and near impassable terrain.
3. Validate flow-field debug modes for cost, integration, and flow direction.
4. Profile GC allocations during player chunk updates.

## Phase 2: Reuse Player Chunk Grid Storage

Status: ready after Phase 1.

Scope:

- `Assets/Scripts/Navigation/GridSystem/GridManager.cs`
- `Assets/Scripts/Navigation/GridSystem/Grid.cs` only if a small API addition is useful.

Implementation direction:

1. Allocate the player chunk `Cell[,]` once during `Awake` or first initialization.
2. Allocate `GridPlayerChunk` once and update its cell references during each chunk rebuild.
3. Clear stale cells before repopulating when the player is near world-grid edges.
4. Preserve `ChunkGridPos` assignment for included world cells.
5. Avoid changing public `Grid` shape unless a narrow method such as replacing cells is cleaner and behavior-neutral.

Risk notes:

- Current edge behavior can leave null cells. Reusing storage makes stale references a risk unless the array is explicitly cleared or fully overwritten.
- Flow-field code currently assumes cells are non-null. If nulls remain possible near edges, either preserve current behavior knowingly or add null handling as a separate correctness fix after approval.
- `GridPlayerChunk` consumers use chunk dimensions and center cell assumptions; do not alter dimensions.

Validation:

1. Compile.
2. In Unity, move the player near all four world-grid edges and corners.
3. Confirm enemies still spawn and teleport inside the active chunk.
4. Profile managed allocations during repeated chunk updates.

## Phase 3: Optimize Grid Cell Selection For Spawning And Teleporting

Status: ready after Phase 2.

Scope:

- `Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs`
- `Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs`
- `Assets/Scripts/Navigation/GridSystem/CellCameraVisibilityChecker.cs`
- `Assets/Scripts/Enemies/EnemiesSpawner.cs`
- `Assets/Scripts/Enemies/EnemiesOutsidePlayerChunkTeleporter.cs`

Implementation direction:

1. Replace LINQ-heavy random cell selection with indexed loops.
2. Add a helper that returns one random hidden walkable cell without shuffling the whole candidate set.
3. For multiple teleport targets, fill a reusable or caller-owned list with hidden walkable cells, then select by index.
4. Reuse `CellCameraVisibilityChecker.IsCellVisibleFromCamera` instead of duplicate private visibility logic.
5. Cache the camera per query or pass a camera from the caller to avoid repeated `Camera.main` lookup in loops.
6. Preserve current hidden-cell and walkable-cell filtering.

Risk notes:

- Random selection distribution can change if replacing full shuffle with reservoir sampling or random indexed candidates. This is probably acceptable for spawn placement but should be called out before implementation.
- Camera caching must still handle scene startup where the main camera may not be ready.
- Do not make camera access a broad DI change in this phase unless the user approves it.

Validation:

1. Compile.
2. In Unity, spawn enemies with the camera near the grid center and near grid edges.
3. Confirm enemies do not spawn on visible cells.
4. Confirm teleporting enemies move to hidden walkable cells.
5. Profile allocations during enemy spawn bursts and teleport checks.

## Phase 4: Reduce Per-Mover Separation Cost

Status: requires design confirmation before implementation.

Scope:

- `Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs`
- `Assets/Scripts/Enemies/EnemyMovementController.cs`
- `Assets/Scripts/LevelSystem/Exp/ExpParticle.cs`

Implementation options:

1. Low-risk option:
   - Replace `Physics.OverlapSphere` with `Physics.OverlapSphereNonAlloc`.
   - Add a reusable collider buffer per movement controller.
   - Keep current `FixedUpdate` cadence.
2. Medium-risk option:
   - Add a serialized or initialization-driven flag to disable separation for consumers that do not need it.
   - Disable separation on exp particles if the desired behavior is only enemy separation.
3. Higher-impact option:
   - Move separation into an enemy-owned system or update it at a lower cadence.
   - Keep `FlowFieldMovementController` focused on grid direction movement.

Recommended first implementation:

1. Implement the low-risk NonAlloc change.
2. Separately ask whether exp particles should participate in enemy separation.
3. Only after approval, make separation optional or enemy-specific.

Risk notes:

- Separation affects enemy clumping and visual movement feel.
- Removing separation from exp particles may alter pickup movement if they currently steer around enemies.
- Centralized separation could become a larger architecture change and should not be mixed with allocation cleanup.

Validation:

1. Compile.
2. In Unity, test dense enemy groups moving toward the player.
3. Test exp particle collection while many enemies are nearby.
4. Profile `FixedUpdate` time and GC allocations with high enemy and exp-particle counts.

## Phase 5: Optional Static Terrain Cost Cache

Status: deferred until profiling confirms Phase 1 is insufficient.

Scope:

- `Assets/Scripts/Navigation/GridSystem/Cell.cs`
- `Assets/Scripts/Navigation/GridSystem/GridManager.cs`
- `Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs`

Implementation direction:

1. Determine whether terrain colliders affecting navigation are static during gameplay.
2. If static, compute terrain cost once for `WorldGrid`.
3. During chunk updates, reset integration and direction fields without re-running terrain overlap checks for unchanged cells.
4. Preserve current cost semantics and collectible occupancy.
5. Add explicit invalidation only if dynamic obstacles are introduced.

Risk notes:

- This is the highest behavior-risk optimization because it assumes terrain cost does not change at runtime.
- If terrain can move, spawn, despawn, or change layer, a cache can make navigation stale.
- Keep this separate from low-risk allocation work.

Validation:

1. Compile.
2. In Unity, validate all navigation-affecting terrain types.
3. If dynamic obstacles exist or are planned, test cache invalidation before enabling this.
4. Profile physics query count during chunk updates.

## Recommended Execution Order

1. Phase 1: remove flow-field allocations and hot-path LINQ.
2. Phase 2: reuse player chunk grid storage.
3. Phase 3: optimize spawn and teleport grid cell selection.
4. Phase 4 low-risk option: NonAlloc separation query.
5. Phase 4 optional behavior change: disable or relocate separation only after user approval.
6. Phase 5: static terrain cost cache only after profiling shows terrain overlap checks remain a bottleneck.

## Pre-Implementation Checklist

1. Check `git status` and protect unrelated user changes.
2. Search direct references with `rg` before changing public helper signatures.
3. Review `GridManager`, `FlowField`, `FlowFieldMovementController`, enemy spawn, enemy teleport, and exp particle consumers together.
4. Keep each phase as a separate reviewable change when possible.
5. Avoid scene, prefab, asset, and meta edits.

## Post-Implementation Checklist

1. Run:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

2. Open Unity and check Console for compile, missing script, or missing reference errors.
3. Run manual play checks for:
   - enemy movement toward the player;
   - enemy movement near impassable and rough terrain;
   - exp particle movement and collection;
   - enemy spawning outside camera visibility;
   - enemy teleporting back into the player chunk;
   - player movement near world-grid edges and corners;
   - grid and flow-field debug visualization.
4. Profile:
   - GC allocations during player chunk updates;
   - `FixedUpdate` cost with many enemies and exp particles;
   - physics query cost during flow-field updates;
   - spawn burst allocations.
5. Create an implementation summary under `.agents/context/implementations/summaries/` after approved changes are implemented.

## Open Questions

1. Should exp particles use enemy separation at all, or should separation be enemy-only?
2. Are terrain colliders that influence navigation guaranteed to be static during gameplay?
3. Should player chunk edge behavior be fixed as part of optimization work, or kept behavior-identical and handled in a separate correctness pass?
4. What enemy and exp-particle counts should be treated as the target profiling scenario?
