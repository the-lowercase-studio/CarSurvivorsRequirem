# Navigation Performance Optimization Implementation Summary

## Implemented Scope

- Phase 1:
  - Replaced per-cell `Physics.OverlapBox` allocation with a reusable `Physics.OverlapBoxNonAlloc` collider buffer in `FlowField`.
  - Moved flow-field half-extents calculation outside the inner cell loop.
  - Replaced per-cell neighbor `List<Cell>` allocations with direct neighbor lookup.
  - Reused the integration queue across field builds.
  - Replaced `GridDirection.GetDirectionFromV2I` LINQ lookup with direct vector checks.
- Phase 2:
  - Reused the player chunk `Cell[,]` storage and `GridPlayerChunk` instance across chunk rebuilds.
  - Cleared player chunk slots before repopulating to avoid stale references near world-grid edges.
  - Added null-slot guards in flow-field and cell-query loops because reused edge chunks can contain empty cells.
- Phase 3:
  - Replaced spawn-cell LINQ shuffle selection with indexed scans and reservoir-style single-cell selection.
  - Added caller-owned hidden walkable cell filling for teleporting.
  - Reused `CellCameraVisibilityChecker` for visibility checks.
  - Removed LINQ sum usage from enemy spawn chance selection.
- Phase 4 low-risk option:
  - Replaced per-mover `Physics.OverlapSphere` allocation with `Physics.OverlapSphereNonAlloc`.
  - Added a reusable separation collider buffer per `FlowFieldMovementController`.
  - Preserved existing enemy/exp-particle separation behavior.

## Files Changed

- `Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs`
- `Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs`
- `Assets/Scripts/Navigation/GridSystem/GridDirection.cs`
- `Assets/Scripts/Navigation/GridSystem/GridManager.cs`
- `Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs`
- `Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs`
- `Assets/Scripts/Enemies/EnemiesSpawner.cs`
- `Assets/Scripts/Enemies/EnemiesOutsidePlayerChunkTeleporter.cs`

## Behavior Preserved

- Terrain cost priority remains impassable over rough.
- Cells with no terrain overlap still become impassable.
- Flow-field generation order remains cost field, destination resolution, integration field, then flow field.
- Movement still reads directions from `IGridManager.WorldGrid` and maps grid X/Y to Unity X/Z with zero Y movement.
- Player chunk dimensions, update delay, and inspector-authored fields remain unchanged.
- Enemy spawn and teleport filtering still require hidden walkable cells.
- Exp particles still use the same shared flow-field separation path as enemies.

## Notes

- `FlowField` uses a 16-collider terrain buffer. If a cost-cell query fills the buffer, the cell is treated as impassable to avoid accidentally missing a higher-priority impassable terrain collider.
- `FlowFieldMovementController` uses a 32-collider separation buffer. If more enemy colliders overlap the separation query, Unity returns only the buffered hits.
- Serialized separation field names were updated to project naming standards with `FormerlySerializedAs` to preserve existing inspector values.
- Unity API usage was checked against official `Physics.OverlapBoxNonAlloc`, `Physics.OverlapSphereNonAlloc`, and `Camera.main` documentation.

## Validation

- Built `Assembly-CSharp-firstpass.csproj` first because `Assembly-CSharp.csproj` initially failed due to missing `Temp/bin/Debug/Assembly-CSharp-firstpass.dll`.
- Ran `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Latest result on 2026-05-22: both builds succeeded. `Assembly-CSharp.csproj` emitted existing `CS0649` warnings for injected or serialized fields and 0 errors.
- Unity Editor `6000.4.5f1` was already running with the project open; the editor log showed the gameplay scene and changed scripts importing, but no batch-mode play/profiling validation was completed.

## Current Worktree Notes

- The navigation optimization source changes are in the files listed above.
- `Assets/Scenes/RuinedBloodCity.unity` contains Unity-side changes, including the `_mainCamera` scene reference for `DefaultGameplaySceneInstaller`.
- `Assets/Textures/Skills/SkillItemRenderTexture.renderTexture` is dirty from Unity import/editor activity.
- Skill files under `Assets/Scripts/Skills/` are also dirty in the current worktree, but they are outside this navigation optimization scope and were not part of the implementation pass.

## Remaining Manual Checks

- Unity Editor console check after script reload.
- Enemy movement toward the player on open ground, rough terrain, and near impassable terrain.
- Exp particle movement and collection with enemies nearby.
- Enemy spawning and teleporting outside camera visibility near grid center and grid edges.
- Player movement near all world-grid edges and corners.
- Flow-field debug visualization for cost, integration, and direction modes.
- Profiler checks for GC allocations during chunk updates, spawn bursts, physics query cost, and high enemy/exp-particle counts.

## Deferred Items

- Phase 4 behavior change: disabling or relocating separation for exp particles still needs design confirmation.
- Phase 5 static terrain cost cache remains deferred until profiling proves Phase 1 is insufficient and terrain collider stability is confirmed.
