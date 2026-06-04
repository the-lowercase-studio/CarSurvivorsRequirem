# Lasergun Turret Optimization Summary

## Source Plan

- `.agents/context/implementations/plans/2026-05-22-lasergun-turret-optimization.md`

## Implemented

- Replaced `Physics.OverlapSphere` target scans in `LasergunTurret` with a reusable `Collider[]` buffer and `Physics.OverlapSphereNonAlloc`.
- Added a fixed `TARGET_BUFFER_SIZE` of 64. If more enemies overlap the turret range than the buffer can store, Unity truncates results to the buffer length and the turret chooses from those returned candidates.
- Replaced range and closest-target comparisons with squared-distance checks.
- Reordered target filtering so distance and closest-candidate checks happen before `ClosestPoint` and `Physics.Linecast`.
- Fixed the blocked-first-candidate target selection path by only assigning a closest target after line-of-sight validation succeeds.
- Moved `LineRenderer.positionCount = 2` out of the laser effect loop while keeping per-frame endpoint updates.

## Intentionally Deferred

- Target scan cadence changes from Phase 5 were not implemented because they can alter Lasergun responsiveness and require design confirmation.
- Per-shot allocation cleanup from Phase 6 was not implemented because it was deferred until profiling shows the main physics changes are insufficient.

## Files Changed

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

## Validation

- Ran `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Build succeeded with existing CS0649 warnings for inspector/DI-assigned fields in unrelated files.

## Remaining Manual Checks

- Test Lasergun targeting with one enemy, dense enemies, enemies entering/leaving range, and a blocked close enemy plus a visible farther enemy.
- Confirm laser VFX preparation, beam visibility, damage timing, and `"Shoot"` audio timing in Play Mode.
- Profile GC allocations during repeated target acquisition and FixedUpdate cost with multiple active turrets.
