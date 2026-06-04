# Skill Stat Unlimited Upgrade Limits Implementation Summary

## Scope

Implemented `.agents/context/implementations/plans/2026-05-26-skill-stat-unlimited-upgrade-limits.md`.

The change is limited to shared upgradeable stat behavior. It does not change skill configs, skill upgrade option text, prefabs, scenes, or balance values.

## Completed Changes

- Added a per-stat serialized `_hasUnlimitedMaxValue` toggle to `UpgradeableStat<T>`.
- Added `IUpgradeableStat.HasUnlimitedMaxValue` for read-only runtime access.
- Updated upgrade application so increasing stats with unlimited max enabled skip max clamping and remain upgradeable after passing their authored max.
- Preserved existing max/floor behavior for limited stats and all subtract-mode stats.
- Updated deserialization upgradeability recalculation so unlimited increasing stats can remain upgradeable even when min and max are equal.
- Corrected `FloatUpgradeableStat` and `ByteUpgradeableStat` deserialization order so serialized ranges are assigned before base upgradeability logic runs.

## Validation

- Ran `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` successfully.
- The build completed with 0 errors.
- The build still reports existing CS0649 warnings for DI/serialized fields. No new compile errors were introduced.

## Manual Checks Still Needed

- In Unity Inspector, enable the unlimited max toggle on one increasing skill stat.
- Enter play mode and repeatedly upgrade that stat to confirm it can exceed its old max and continue appearing in upgrade options.
- Confirm unchanged limited stats still stop at their authored max.
- Confirm subtract-mode stats still stop at their authored terminal lower value.
