# Skill Stat Unlimited Upgrade Limits

## Summary

Implement a per-stat ScriptableObject switch that disables the upper max limit only for increasing upgrade stats. Existing stats remain limited by default. Subtract-mode stats, such as cooldown reduction, keep their current floor/terminal guard even if the switch is enabled.

## Key Changes

- Add a serialized boolean to `UpgradeableStat<T>`, named `_hasUnlimitedMaxValue` or similar, defaulting to `false`.
- Expose a read-only public property through `IUpgradeableStat`, for example `HasUnlimitedMaxValue`, so upgrade flow/debugging can inspect the setting if needed.
- Update `UpgradeableStat<T>.Upgrade(float upgradeValue)`:
  - For normal/increasing stats with unlimited enabled, skip max clamping and never set `CanBeUpgraded = false` because of max.
  - For normal/increasing stats with unlimited disabled, keep current clamp-to-`MinMaxRange.Max` behavior.
  - For subtract-mode stats, always keep current lower-bound clamp behavior.
- Update `OnAfterDeserialize()` so unlimited increasing stats remain upgradeable even when `MinMaxRange.Min` and `MinMaxRange.Max` are equal; limited stats keep current behavior.
- Keep `MinMaxRange.Min` as the initial serialized value for all stats. `MinMaxRange.Max` remains the terminal limit for default limited stats and remains serialized for compatibility.

## Inspector/Data Behavior

- Existing skill config assets require no manual migration to preserve current behavior because the new bool defaults to `false`.
- Designers can opt in per stat on existing serialized `FloatUpgradeableStat` and `ByteUpgradeableStat` fields.
- Do not directly edit `.asset` files by hand; set the new switch through Unity Inspector when choosing which stats are unlimited.

## Test Plan

- Add focused edit-mode tests if the project test setup supports it; otherwise validate through a small temporary/debug harness and compile:
  - Increasing limited stat clamps to max and becomes not upgradeable.
  - Increasing unlimited stat exceeds max and remains upgradeable.
  - Increasing unlimited stat with `Min == Max` starts at min and remains upgradeable.
  - Subtract-mode stat still clamps at its terminal lower value and becomes not upgradeable.
  - `SkillUpgradeableStatsConfig.GetUpgradeableStatsThatCanBeUpgraded()` continues returning unlimited stats after they pass their old max.
- Run:
  - `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`
- Manual Unity check:
  - Enable the new switch on one increasing skill stat in a skill ScriptableObject.
  - Enter play mode, repeatedly upgrade that stat, and confirm upgrade options keep appearing past the old max while other stats still stop at their max.

## Assumptions

- "Unlimited" means unlimited upward growth only.
- Subtract-mode stats keep their current floor guard to avoid negative or invalid gameplay values.
- The switch is per stat, not per whole skill config.
