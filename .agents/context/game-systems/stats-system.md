# Stats System Documentation

## Purpose

The Stats system owns reusable upgradeable stat value types used by skill configs and skill upgrade UI.

It is responsible for:
- Representing upgradeable numeric values with min/max bounds.
- Calculating random upgrade values from configured upgrade ranges.
- Applying upgrades and raising upgrade events.
- Supporting Unity serialization for float and int stat variants.
- Providing stat icon references (`Sprite`) and display units consumed by skill UI.

It is not responsible for:
- Skill selection, unlock flow, or upgrade UI timing.
- Final balance values stored in ScriptableObject assets.
- Applying stat effects to gameplay objects beyond raising `OnUpgrade`.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Stats/UpgradeableStat.cs
  - Assets/Scripts/Stats/FloatUpgradeableStat.cs
  - Assets/Scripts/Stats/IntUpgradeableStat.cs
  - Assets/Scripts/Common/Types/ValueRange.cs
  - Assets/Scripts/Skills/StatsUnits.cs
- Main consumers:
  - Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeableSkill.cs
  - Assets/ScriptableObjects/Skills/PlayerSkills
  - Assets/Scripts/Skills/PlayerSkills
- Related docs:
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/projectiles-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/Stats/UpgradeableStat.cs exposes `CanBeUpgraded`, `HasUnlimitedMaxValue`, `IsSubstractModeOn`, `AlwaysUseMinValueForUpgrade`, `IsIntegerUpgradeRange`, upgrade range metadata (`UpgradeRangeMin`, `UpgradeRangeMax`), optional rarity override metadata (`OverrideDefaultRarity`, `Rarity`), `Icon` (`Sprite`), `SetIcon(Sprite icon)`, `StatsUnits Unit`, upgrade calculation methods, `Upgrade(float)`, and `OnUpgrade`.
  - Assets/Scripts/Stats/UpgradeableStat.cs stores a typed `Value`, min/max range, upgrade-value range, unit, subtract mode, unlimited-max flag, rarity override fields, icon, and upgrade availability.
  - Assets/Scripts/Stats/FloatUpgradeableStat.cs and Assets/Scripts/Stats/IntUpgradeableStat.cs provide non-generic serializable Unity variants.
  - Assets/Scripts/Common/Types/ValueRange.cs currently supports random upgrade values for `float` and `int`; byte ranges are no longer implemented.
  - Assets/Scripts/Skills/StatsUnits.cs defines `None`, `Percentage`, `Seconds`, and `Meters`; `ToDisplayString` maps them to UI suffixes.
  - Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs reflects public instance properties assignable to Assets/Scripts/Stats/UpgradeableStat.cs and exposes only stats where `CanBeUpgraded` is true.
- Runtime flow:
  - Skill config ScriptableObjects hold serialized starting stat fields.
  - Config `ResetRuntimeState()` / `OnEnable` methods deep-copy starting stats into public runtime properties.
  - Skill upgrade UI asks a config for available upgradeable stats.
  - UI asks each stat for an upgrade value through `GetUpgradeValueBasedOnUpdateRange`.
  - UI displays percentage stats as percent-of-current-value text and other stats as the rolled upgrade value where currently implemented.
  - Stat icon (`IUpgradeableStat.Icon`) is passed into `SkillUpgradeOption` and rendered on `SkillUpgradeButton`.
  - Button callbacks call `IUpgradeableStat.Upgrade(upgradeValue)`.
  - `Upgrade` applies delta, optionally clamps to the max boundary, updates `Value`, flips `CanBeUpgraded` off when a limited max is reached, and raises `OnUpgrade`.
  - Skill configs and skill components listen to `OnUpgrade` to update projectile/turret config values or activate more child objects.

## Rules and Invariants

- Critical behavior rules:
  - Upgradeable stats intended for skill UI must be public properties assignable to Assets/Scripts/Stats/UpgradeableStat.cs.
  - Serialized starting stat fields should be deep-copied before runtime mutation so ScriptableObject-authored starting values are not directly mutated.
  - `Upgrade` is a synchronous state change and raises `OnUpgrade` immediately after updating `Value`.
  - `CanBeUpgraded` becomes false when the value reaches the max boundary only when max limiting applies (`ShouldApplyMaxValueLimit()`).
  - `HasUnlimitedMaxValue` disables max-value limiting for non-subtract stats; subtract-mode stats still apply the configured max boundary as the stopping target.
  - Subtract-mode stats invert the delta and use max-boundary checks in the opposite direction.
  - Units are presentation hints; gameplay code should use the stat value itself.
  - Rarity override fields and icon references are presentation hints consumed by skill upgrade option creation and button display; stat math does not branch on rarity or icon.
- Ordering or sequencing guarantees:
  - Unity deserialization calls `OnAfterDeserialize`; concrete non-generic stat types (`FloatUpgradeableStat`, `IntUpgradeableStat`) assign their serialized backing ranges (`MinMaxRange` and `_rangeOfPossibleValuesForUpgrade`) before calling `base.OnAfterDeserialize()`.
  - Base `OnAfterDeserialize()` sets starting `Value = MinMaxRange.Min` and evaluates `CanBeUpgraded = !ShouldApplyMaxValueLimit() || !Mathf.Approximately(minValueFloat, maxValueFloat)`.
  - Skill config `OnEnable` and `ResetRuntimeState()` prepare runtime stat copies before skills initialize from those configs.
  - Upgrade subscribers run in event subscription order from the same `Upgrade` call.
- Constraints contributors must preserve:
  - Preserve serialized field names in stat classes and skill config assets unless asset migration is intentional.
  - Treat changes to ranges, values, units, and subtract mode as balance changes.
  - Do not add new stat types or units without checking skill UI display behavior.
  - Do not add or change upgrade range metadata without checking skill upgrade rarity calculation and button visuals.
  - Do not hide gameplay effects in stat classes; stat classes should update values and notify consumers.

## Extension Points

- Safe extension areas:
  - Add a new upgradeable skill stat by adding a serialized starting stat, deep-copying it in config `ResetRuntimeState()`, exposing a public property, and subscribing to `OnUpgrade` where derived runtime config must update.
  - Add a new stat value type by deriving from Assets/Scripts/Stats/UpgradeableStat.cs when Unity serialization requires a concrete non-generic wrapper, and update `ValueRange<T>.GetRandomValueInRange` if random upgrade generation needs that type.
  - Override rarity on an individual stat by enabling `OverrideDefaultRarity` and setting `Rarity` when upgrade range position does not communicate the upgrade's actual impact.
  - Add a stat icon by setting `Icon` in the inspector or calling `SetIcon(Sprite icon)`.
  - Add a new display unit in Assets/Scripts/Skills/StatsUnits.cs only when all UI formatting paths are reviewed.
- Required dependencies and contracts:
  - Stat value type `T` must be a struct implementing `IComparable<T>` and `IConvertible`; random upgrade values are currently implemented only for `float` and `int`.
  - Min/max and upgrade ranges must be assigned for stats that should upgrade.
  - Public stat properties must remain accessible to reflection in Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs.
- Testing implications:
  - Compile after stat or config code changes.
  - In Unity, validate upgrade button text, rarity visuals, icon rendering, value changes, maxed-stat removal, unlimited-max behavior, subtract-mode behavior, and runtime reset on scene/domain reload.
  - For new units, validate UI suffix and percent display calculations.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/Common/Types/ValueRange.cs supplies min/max and random upgrade ranges.
  - Assets/Scripts/Utils/DeepCopyUtility.cs is used by skill configs to create runtime stat copies.
  - Skill upgrade UI owns presentation, icon rendering, rarity background application, and button callbacks.
- Downstream consumers:
  - Skill configs update projectile and turret configs when stat events fire.
  - Concrete skills listen to count stats to activate more turrets or saws.
  - UI filters and displays available stat upgrades through Assets/Scripts/Stats/UpgradeableStat.cs.
- Cross-system coupling risks:
  - Reflection in Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs couples UI availability to public property shape.
  - Rarity calculation depends on `UpgradeRangeMin`, `UpgradeRangeMax`, `AlwaysUseMinValueForUpgrade`, `IsIntegerUpgradeRange`, and optional stat-level overrides.
  - ScriptableObject `ResetRuntimeState()` / `OnEnable` reset behavior affects run-state persistence.
  - Changing Assets/Scripts/Skills/StatsUnits.cs display affects player-facing upgrade text.
  - Mutating projectile/turret configs through stat events can affect all consumers sharing that runtime config instance.

## Known Risks and Open Questions

- Known limitations:
  - `IsSubstractModeOn` and `CanBeUgraded` naming errors exist in current public contracts.
  - Assets/Scripts/Stats/FloatUpgradeableStat.cs and Assets/Scripts/Stats/IntUpgradeableStat.cs constructors include an unused `maxValue` parameter in two constructor overloads.
  - `ByteUpgradeableStat` and `ByteValueRange` have been removed; references to byte upgrade stats are stale.
  - UI percentage display behavior is specialized and should be verified before changing `GetWhatPercentOfValueIsUpgradeValue`.
- Open design questions:
  - Should stat upgrade math be covered by edit-mode tests?
  - Should runtime stat state live in a run-state service instead of ScriptableObject `ResetRuntimeState()` copies?
  - Should spelling errors be migrated with serialized/API compatibility handling?
- Suggested follow-up tasks:
  - Add focused tests for clamp behavior, unlimited max behavior, subtract mode, int conversion, and upgrade event firing.
  - Review `ValueRange<T>.GetRandomValueInRange` before adding more concrete stat types.
