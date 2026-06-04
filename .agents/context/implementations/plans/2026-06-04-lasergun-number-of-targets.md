# Lasergun Number Of Targets Upgrade Plan

## Summary

Add a new lasergun upgrade stat named `NumberOfTargets`, default `1`, max `5`, upgrading by `+1`. Each initialized lasergun turret will be able to hit the closest visible enemies in range up to its current target count, with one visible laser beam per target.

## Key Changes

- Add `IntUpgradeableStat _numberOfTargets` and public `NumberOfTargets` to `LasergunSkillSO`.
- Deep-copy `NumberOfTargets` on `OnEnable` so it participates in the existing reflection-based upgrade flow.
- Update `LasergunUpgradeableSkillConfig.asset` and the lasergun preset with:
  - value/min `1`
  - max `5`
  - upgrade range `1..1`
  - `CanBeUpgraded = true`
  - non-subtract mode, no unit
- Update `LasergunSkill` to apply `_config.NumberOfTargets.Value` to every initialized turret.
- Subscribe to `NumberOfTargets.OnUpgrade` and `NumberOfTurrets.OnUpgrade` with named handlers, and unsubscribe in `OnDestroy`.

## Turret Behavior

- Replace single-target state in `LasergunTurret` with a small tracked target set plus a primary target.
- Select targets as the closest visible enemy colliders in turret range, using the existing enemy layer and terrain line-of-sight checks.
- Rotate toward the primary closest target; firing still requires the turret to be aligned with that primary target.
- On shot completion, damage every currently valid tracked target once using existing projectile damage.
- Keep one shoot sound per turret shot, not one sound per target.
- Show one laser line per hit target by reusing the existing serialized `LineRenderer` as the first beam and creating runtime clones only when needed.
- Clear all active laser lines when the shoot effect ends.
- Avoid per-frame allocations in target selection and laser updates.

## Tests And Validation

- Run:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

- Manual Unity checks:
  - With `NumberOfTargets = 1`, lasergun behaves like the current single-target turret.
  - Upgrade UI can show `Increase Number Of Targets by 1`.
  - At values `2..5`, one turret damages up to that many closest visible enemies.
  - Enemies outside range or behind terrain are not selected.
  - Multiple beams render during the laser effect and clear afterward.
  - Multiple turrets can each target up to their own configured target count.
  - Existing damage, fire delay, turret count, VFX charge, and audio timing remain unchanged.

## Assumptions

- Target rule: closest visible enemies in range.
- Visual rule: one beam per target.
- Upgrade range: default `1`, max `5`, increment `1`.
- If multiple turrets choose the same enemy, damage stacks as separate turret shots.
- No DI, scene, prefab, or balance changes beyond the lasergun upgrade data are required.
