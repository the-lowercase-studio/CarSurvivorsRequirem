# Stats System Documentation

## Purpose

The Stats system owns reusable upgradeable stat value types used by skill configs and skill upgrade UI.

It is responsible for:

- Representing upgradeable numeric values with min/max bounds.
- Calculating random upgrade values from configured upgrade ranges.
- Applying upgrades and raising upgrade events.
- Supporting Unity serialization for float and int stat variants.
- Providing display units consumed by skill UI.

It is not responsible for:

- Skill selection, unlock flow, or upgrade UI timing.
- Final balance values stored in ScriptableObject assets.
- Applying stat effects to gameplay objects beyond raising `OnUpgrade`.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Stats/UpgradeableStat.cs`
  - `Assets/Scripts/Stats/FloatUpgradeableStat.cs`
  - `Assets/Scripts/Stats/IntUpgradeableStat.cs`
  - `Assets/Scripts/Common/Types/ValueRange.cs`
  - `Assets/Scripts/Skills/StatsUnits.cs`
- Main consumers:
  - `Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs`
  - `Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs`
  - `Assets/Scripts/Skills/UpgradeableSkill.cs`
  - `Assets/ScriptableObjects/Skills/PlayerSkills/`
  - `Assets/Scripts/Skills/PlayerSkills/`
- Related docs:
  - `.agents/context/game-systems/skills-system.md`
  - `.agents/context/game-systems/projectiles-system.md`
  - `.agents/context/project-coding-standards.md`
- Related agents or instructions:
  - `.agents/skills/document-system/SKILL.md`

## Architecture and Data Flow

- Core components:
  - `IUpgradeableStat` exposes `CanBeUpgraded`, `HasUnlimitedMaxValue`, `IsSubstractModeOn`, `StatsUnits Unit`, upgrade calculation methods, `Upgrade(float)`, and `OnUpgrade`.
  - `UpgradeableStat<T>` stores a typed `Value`, min/max range, upgrade-value range, unit, subtract mode, unlimited-max flag, and upgrade availability.
  - `FloatUpgradeableStat` and `IntUpgradeableStat` provide non-generic serializable Unity variants.
  - `ValueRange<T>` currently supports random upgrade values for `float` and `int`; byte ranges are no longer implemented.
  - `StatsUnits` defines `None`, `Percentage`, `Seconds`, and `Meters`; `ToDisplayString` maps them to UI suffixes.
  - `SkillUpgradeableStatsConfig` reflects public instance properties assignable to `IUpgradeableStat` and exposes only stats where `CanBeUpgraded` is true.
- Runtime flow:
  - Skill config ScriptableObjects hold serialized starting stat fields.
  - Config `OnEnable` methods deep-copy starting stats into public runtime properties.
  - Skill upgrade UI asks a config for available upgradeable stats.
  - UI asks each stat for an upgrade value through `GetUpgradeValueBasedOnUpdateRange`.
  - UI displays percentage stats as raw upgrade value and other stats as percent-of-current-value copy where currently implemented.
  - Button callbacks call `IUpgradeableStat.Upgrade(upgradeValue)`.
  - `Upgrade` applies delta, optionally clamps to the max boundary, updates `Value`, flips `CanBeUpgraded` off when a limited max is reached, and raises `OnUpgrade`.
  - Skill configs and skill components listen to `OnUpgrade` to update projectile/turret config values or activate more child objects.

## Rules and Invariants

- Critical behavior rules:
  - Upgradeable stats intended for skill UI must be public properties assignable to `IUpgradeableStat`.
  - Serialized starting stat fields should be deep-copied before runtime mutation so ScriptableObject-authored starting values are not directly mutated.
  - `Upgrade` is a synchronous state change and raises `OnUpgrade` immediately after updating `Value`.
  - `CanBeUpgraded` becomes false when the value reaches the max boundary only when max limiting applies.
  - `HasUnlimitedMaxValue` disables max-value limiting for non-subtract stats; subtract-mode stats still apply the configured max boundary as the stopping target.
  - Subtract-mode stats invert the delta and use max-boundary checks in the opposite direction.
  - Units are presentation hints; gameplay code should use the stat value itself.
- Ordering or sequencing guarantees:
  - Unity deserialization calls `OnAfterDeserialize`; concrete stat types assign their serialized ranges before calling the base implementation.
  - Skill config `OnEnable` prepares runtime stat copies before skills initialize from those configs.
  - Upgrade subscribers run in event subscription order from the same `Upgrade` call.
- Constraints contributors must preserve:
  - Preserve serialized field names in stat classes and skill config assets unless asset migration is intentional.
  - Treat changes to ranges, values, units, and subtract mode as balance changes.
  - Do not add new stat types or units without checking skill UI display behavior.
  - Do not hide gameplay effects in stat classes; stat classes should update values and notify consumers.

## Extension Points

- Safe extension areas:
  - Add a new upgradeable skill stat by adding a serialized starting stat, deep-copying it in config `OnEnable`, exposing a public property, and subscribing to `OnUpgrade` where derived runtime config must update.
  - Add a new stat value type by deriving from `UpgradeableStat<T>` when Unity serialization requires a concrete non-generic wrapper, and update `ValueRange<T>.GetRandomValueInRange` if random upgrade generation needs that type.
  - Add a new display unit in `StatsUnits` only when all UI formatting paths are reviewed.
- Required dependencies and contracts:
  - Stat value type `T` must be a struct implementing `IComparable<T>` and `IConvertible`; random upgrade values are currently implemented only for `float` and `int`.
  - Min/max and upgrade ranges must be assigned for stats that should upgrade.
  - Public stat properties must remain accessible to reflection in `SkillUpgradeableStatsConfig`.
- Testing implications:
  - Compile after stat or config code changes.
  - In Unity, validate upgrade button text, value changes, maxed-stat removal, unlimited-max behavior, subtract-mode behavior, and runtime reset on scene/domain reload.
  - For new units, validate UI suffix and percent display calculations.

## Integration Notes

- Upstream dependencies:
  - `ValueRange<T>` supplies min/max and random upgrade ranges.
  - `DeepCopyUtility` is used by skill configs to create runtime stat copies.
  - Skill upgrade UI owns presentation and button callbacks.
- Downstream consumers:
  - Skill configs update projectile and turret configs when stat events fire.
  - Concrete skills listen to count stats to activate more turrets or saws.
  - UI filters and displays available stat upgrades through `IUpgradeableStat`.
- Cross-system coupling risks:
  - Reflection in `SkillUpgradeableStatsConfig` couples UI availability to public property shape.
  - ScriptableObject `OnEnable` reset behavior affects run-state persistence.
  - Changing `StatsUnits` display affects player-facing upgrade text.
  - Mutating projectile/turret configs through stat events can affect all consumers sharing that runtime config instance.

## Known Risks and Open Questions

- Known limitations:
  - `IsSubstractModeOn` and `CanBeUgraded` naming errors exist in current public contracts.
  - `FloatUpgradeableStat` and `IntUpgradeableStat` constructors include a `maxValue` parameter that is not used directly.
  - `ByteUpgradeableStat` and `ByteValueRange` have been removed; references to byte upgrade stats are stale.
  - UI percentage display behavior is specialized and should be verified before changing `GetWhatPercentOfValueIsUpgradeValue`.
- Open design questions:
  - Should stat upgrade math be covered by edit-mode tests?
  - Should runtime stat state live in a run-state service instead of ScriptableObject `OnEnable` copies?
  - Should spelling errors be migrated with serialized/API compatibility handling?
- Suggested follow-up tasks:
  - Add focused tests for clamp behavior, unlimited max behavior, subtract mode, int conversion, and upgrade event firing.
  - Review `ValueRange<T>.GetRandomValueInRange` before adding more concrete stat types.
