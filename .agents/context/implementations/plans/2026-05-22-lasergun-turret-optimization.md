# Lasergun Turret Optimization Plan

## Purpose

Incrementally reduce CPU cost and managed allocations in:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

This plan is performance-focused. Preserve current Lasergun gameplay behavior, turret count behavior, targeting intent, laser VFX timing, line-renderer visuals, damage timing, audio timing, serialized fields, prefab references, scene references, and skill upgrade balance unless a phase explicitly calls out a user-approved behavior change.

## Source Documents

- `AGENTS.md`
- `.agents/README.md`
- `.agents/context/project-coding-standards.md`
- `.agents/context/technology-documentation.md`
- `.agents/skills/check-optimalization/SKILL.md`
- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`
- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunSkill.cs`
- `Assets/Scripts/Skills/Turret.cs`
- `Assets/Scripts/VFX/VFXPlayer.cs`
- `Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs`

Unity API references to verify during implementation:

- `Physics.OverlapSphereNonAlloc`
- `WaitForEndOfFrame`

## Current Pressure Points

### Target Acquisition Allocates Collider Arrays

`LasergunTurret.AssignNewTarget` uses `Physics.OverlapSphere`, which allocates a new collider array each time the turret needs a target. This can become visible when multiple turrets are active, when enemies frequently leave range, or when no valid target exists and target acquisition runs repeatedly from `FixedUpdate`.

Target method:

- `LasergunTurret.AssignNewTarget`

### Distance Checks Use Square Roots

The turret uses `Vector3.Distance` in target selection and range checks. These checks happen from `FixedUpdate`, `CanShoot`, `FireLaserBeam`, and target selection. Squared-distance comparisons can preserve behavior while avoiding repeated square-root work.

Target methods:

- `LasergunTurret.AssignNewTarget`
- `LasergunTurret.IsCurrentTargetInRange`

### Target Filtering Does Expensive Work Too Early

`AssignNewTarget` runs `ClosestPoint` and `Physics.Linecast` before checking whether a candidate can beat the current closest target. The loop can reject farther candidates earlier by distance before running obstacle checks.

Target method:

- `LasergunTurret.AssignNewTarget`

### Blocked First Candidate Can Skew Closest Target Selection

`AssignNewTarget` initializes `closestTarget` and `closestDistance` from the first overlap result before validating line of sight. If the first collider is blocked, a farther visible target can fail the `currentDistance <= closestDistance` check and the turret can end with no target. Fixing this is both a small correctness cleanup and a prerequisite for a simpler optimized loop.

Target method:

- `LasergunTurret.AssignNewTarget`

### Line Renderer State Is Reassigned Every Beam Frame

`ShootingLaserEffect` sets `_laserLineRenderer.positionCount = 2` on every coroutine iteration. The count only needs to be set once before the loop and reset once after the loop.

Target method:

- `LasergunTurret.ShootingLaserEffect`

### Optional Scan Cadence May Be Too Aggressive

When no target is available, every active turret can perform target acquisition every `FixedUpdate`. A small scan interval could reduce physics pressure, but this may introduce a visible delay before turrets acquire newly entered enemies.

Target methods:

- `LasergunTurret.FixedUpdate`
- `LasergunTurret.AssignNewTarget`

### Minor Per-Shot Allocations May Remain

`Shoot` creates a new `VFXPlayConfig` per shot. `ShootingLaserEffect` yields a new `WaitForEndOfFrame` inside the loop. These are smaller than target-acquisition allocations but can be reviewed after the main physics changes.

Target methods:

- `LasergunTurret.Shoot`
- `LasergunTurret.ShootingLaserEffect`

## Invariants

1. Preserve current turret initialization through `LasergunSkill` and `ItemsWithScriptableConfigsActivator`.
2. Preserve current firing gate semantics:
   - current target exists;
   - laser effect is not already showing;
   - turret is looking at target;
   - current target is in range.
3. Preserve current VFX completion flow where `VFXPlayer.OnVFXFinished` triggers `FireLaserBeam`.
4. Preserve damage timing: damage is applied when the beam fires, not when shoot preparation starts.
5. Preserve audio timing: `"Shoot"` plays when the beam fires.
6. Preserve terrain line-of-sight blocking with `TerrainLayers.All`.
7. Preserve enemy target filtering with `EntityLayers.Enemy`.
8. Preserve serialized field names and inspector setup.
9. Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly.
10. Compile after source changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.

## Phase 1: Remove Target Acquisition Allocations

Status: ready.

Scope:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

Implementation direction:

1. Add a private reusable `Collider[]` target buffer to `LasergunTurret`.
2. Add a local constant for buffer size using project constant naming rules.
3. Replace `Physics.OverlapSphere` with `Physics.OverlapSphereNonAlloc`.
4. Iterate only over the returned hit count.
5. Decide and document behavior if the buffer fills.
6. Keep target acquisition local to the turret; do not introduce singleton access, scene-wide lookup, or a shared global buffer.

Risk notes:

- `OverlapSphereNonAlloc` truncates results if the buffer is too small. If the returned hit count equals the buffer length, the closest target might be outside the buffer.
- A conservative first buffer size should be chosen based on expected enemy density around one turret.

Validation:

1. Compile.
2. In Unity, test Lasergun target acquisition with one enemy, many enemies, and enemies entering/leaving range.
3. Profile managed allocations while turrets repeatedly acquire targets.

## Phase 2: Use Squared-Distance Target Selection

Status: ready.

Scope:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

Implementation direction:

1. Cache `float rangeSqr = _config.Range * _config.Range` inside methods where needed.
2. Replace `Vector3.Distance(...) <= _config.Range` with squared-distance comparisons.
3. Compare candidates by squared distance in `AssignNewTarget`.
4. Preserve the same world positions currently used for distance checks.

Risk notes:

- Boundary behavior can differ by tiny floating-point amounts at exactly `_config.Range`.
- Do not switch to closest-point distance for range unless approved as a gameplay/targeting change.

Validation:

1. Compile.
2. In Unity, test firing at enemies near the exact edge of Lasergun range.
3. Confirm target switching still feels unchanged during dense enemy movement.

## Phase 3: Reorder And Correct Target Filtering

Status: ready after Phase 2.

Scope:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

Implementation direction:

1. Initialize `closestTarget` to `null` and `closestDistanceSqr` to `float.PositiveInfinity`.
2. For each candidate:
   - skip null entries defensively if the buffer can contain stale values beyond hit count;
   - compute squared distance first;
   - skip candidates outside range;
   - skip candidates farther than the current closest candidate;
   - then run `ClosestPoint` and `Physics.Linecast`;
   - assign the target only after it passes line-of-sight validation.
3. Set `_currentTarget` once at the end of the method.

Risk notes:

- This fixes the blocked-first-candidate issue and can make the turret acquire a valid farther target in cases where it currently fails to acquire one. That is a correctness improvement, but it can be player-visible.
- Candidate tie behavior can change if using `<` instead of `<=`. Match current tie preference intentionally.

Validation:

1. Compile.
2. In Unity, place one blocked enemy closer than one visible enemy and confirm the visible enemy can be targeted.
3. Test multiple visible enemies at similar distances.

## Phase 4: Reduce Laser Coroutine Work

Status: ready.

Scope:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

Implementation direction:

1. Move `_laserLineRenderer.positionCount = 2` before the coroutine loop.
2. Keep `SetPosition` calls inside the loop so the beam endpoint follows the moving target.
3. Keep `_laserLineRenderer.positionCount = 0` after the loop.
4. Leave `WaitForEndOfFrame` unchanged in this phase to avoid visual timing changes.

Risk notes:

- Low risk if only `positionCount` assignment is moved.

Validation:

1. Compile.
2. In Unity, fire the laser at moving enemies and confirm the beam remains visible and follows the current target point.

## Phase 5: Optional Target Scan Cadence

Status: requires design confirmation before implementation.

Scope:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`

Implementation direction:

1. Add a small scan interval used only when `_currentTarget` is null or out of range.
2. Track next allowed scan time using `Time.time` or an accumulated timer.
3. Keep immediate scan behavior during `FireLaserBeam` when the current target becomes invalid.
4. Consider a serialized value only if designers need to tune responsiveness; otherwise prefer a local constant.

Risk notes:

- This can delay target pickup and may make Lasergun feel less responsive.
- Any scan cadence is a gameplay-feel change and should be approved separately.

Validation:

1. Compile.
2. In Unity, test enemies entering range while the Lasergun is ready to shoot.
3. Compare target pickup responsiveness before and after the change.
4. Profile physics query count with no valid enemies in range.

## Phase 6: Optional Per-Shot Allocation Cleanup

Status: deferred until profiling confirms the main physics changes are insufficient.

Scope:

- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`
- `Assets/Scripts/VFX/VFXPlayer.cs` only if a broader VFX API change is approved.

Implementation options:

1. Low-risk option:
   - Cache a `WaitForEndOfFrame` instance as a private readonly/static readonly field and yield it during the laser effect.
2. Medium-risk option:
   - Replace `WaitForEndOfFrame` with `yield return null` if visual timing remains acceptable.
3. Broader option:
   - Avoid per-shot `VFXPlayConfig` allocation by reusing a config instance or changing `VFXPlayer.Play` to accept value parameters.

Risk notes:

- `WaitForEndOfFrame` has specific render-end timing. Replacing it with `yield return null` may alter when line-renderer positions update relative to rendering.
- Reusing `VFXPlayConfig` is safe only if `VFXPlayer` does not retain and mutate it in a way that overlaps with another play request.
- Changing `VFXPlayer` affects all VFX consumers and should not be bundled with the turret-only optimization pass unless separately approved.

Validation:

1. Compile.
2. In Unity, compare laser visual timing frame-by-frame if possible.
3. Test other VFX consumers if `VFXPlayer` is changed.
4. Profile per-shot managed allocations.

## Recommended Execution Order

1. Phase 1: replace target acquisition with `OverlapSphereNonAlloc`.
2. Phase 2: use squared-distance range and closest-target comparisons.
3. Phase 3: reorder target filtering and fix blocked-first-candidate behavior.
4. Phase 4: move line-renderer count assignment out of the beam loop.
5. Phase 5: add target scan cadence only after user approval.
6. Phase 6: address minor per-shot allocations only after profiling.

## Pre-Implementation Checklist

1. Check `git status` and protect unrelated user changes.
2. Search references with `rg` before changing shared APIs.
3. Keep changes scoped to `LasergunTurret.cs` for Phases 1-5.
4. Avoid scene, prefab, asset, and meta edits.
5. Do not change Lasergun balance values or serialized config shapes.

## Post-Implementation Checklist

1. Run:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

2. Open Unity and check Console for compile, missing script, or missing reference errors.
3. Run manual play checks for:
   - Lasergun target acquisition;
   - target switching when enemies leave range;
   - blocked line-of-sight targets;
   - multiple active Lasergun turrets;
   - laser VFX preparation;
   - line-renderer beam visibility;
   - beam damage timing;
   - `"Shoot"` audio timing.
4. Profile:
   - GC allocations during target acquisition;
   - `FixedUpdate` cost with many enemies and active turrets;
   - per-shot allocations during repeated Lasergun firing.
5. Create an implementation summary under `.agents/context/implementations/summaries/` after approved changes are implemented.

## Open Questions

1. What maximum enemy density near one turret should the NonAlloc collider buffer support?
2. Should target acquisition be allowed to become slightly delayed when no target is available?
3. Should the blocked-first-candidate fix be treated as part of optimization work or separated as a correctness change?
4. Is the current `WaitForEndOfFrame` laser visual timing intentional, or can it be changed after visual comparison?
