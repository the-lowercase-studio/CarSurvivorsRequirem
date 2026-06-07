# Lasergun Number Of Targets Implementation Summary

## Source Plan

- `.agents/context/implementations/plans/2026-06-04-lasergun-number-of-targets.md`

## Summary

Implemented a new lasergun upgrade stat, `NumberOfTargets`, with default value `1`, max value `5`, and fixed `+1` upgrade increments. Each initialized lasergun turret now tracks the closest visible enemies in range up to its configured target count, rotates toward the closest primary target, damages every captured target once per shot, and renders one laser beam per hit target.

## Files Changed

- `Assets/ScriptableObjects/Skills/PlayerSkills/LasergunSkill/LasergunSkillUpgradeableConfigSO.cs`
  - Added serialized `_numberOfTargets`.
  - Added public `NumberOfTargets` property so the reflection-based upgrade flow can discover it.
  - Deep-copied `NumberOfTargets` in `OnEnable` with the existing runtime stat pattern.
- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunSkill.cs`
  - Applies `_config.NumberOfTargets.Value` to all initialized turrets.
  - Subscribes to `NumberOfTargets.OnUpgrade` and `NumberOfTurrets.OnUpgrade` with named handlers.
  - Unsubscribes handlers in `OnDestroy`.
- `Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs`
  - Replaced single-target state with tracked target arrays and a primary target.
  - Selects closest visible enemy colliders using `Physics.OverlapSphereNonAlloc`, enemy layers, terrain line-of-sight checks, and sorted insertion.
  - Captures valid hit targets when the charge VFX finishes, then damages each captured target once.
  - Keeps one shoot sound per turret shot.
  - Reuses the serialized `LineRenderer` as the first beam and creates runtime clones only when target capacity increases.
  - Clears active laser lines when the laser effect ends or the turret disables.
- `Assets/ScriptableObjects/Skills/PlayerSkills/LasergunSkill/LasergunUpgradeableSkillConfig.asset`
  - Added `_numberOfTargets` with value/min `1`, max `5`, range `1..1`, non-subtract mode, no unit, and `CanBeUpgraded = true`.
- `Assets/ScriptableObjects/Skills/PlayerSkills/LasergunSkill/Presets/LasergunSkillSO.preset`
  - Added matching `NumberOfTargets` preset defaults for new lasergun configs.

## Validation

- `dotnet build Assembly-CSharp-firstpass.csproj`
  - Passed.
- `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`
  - Passed after first building `Assembly-CSharp-firstpass.csproj` to generate `Temp/bin/Debug/Assembly-CSharp-firstpass.dll`.
  - Reported existing unrelated `CS0649` warnings for serialized or injected fields in other systems.
  - No warnings or errors were introduced in the touched lasergun files.

## Remaining Manual Unity Checks

- Confirm `NumberOfTargets = 1` matches the previous single-target lasergun behavior.
- Confirm upgrade UI can show `Increase Number Of Targets by 1`.
- Confirm values `2..5` damage up to that many closest visible enemies per turret.
- Confirm enemies outside range or blocked by terrain are not selected.
- Confirm one laser beam renders per hit target and all beams clear after the effect.
- Confirm multiple turrets can each target up to their own configured target count.
- Confirm existing damage, fire delay, turret count, charge VFX, and audio timing remain unchanged.
