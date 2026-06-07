# Skill Upgrade Rarity Visuals Implementation Plan

## Summary

Add visual rarity to shown skill stat upgrade buttons. Each generated stat upgrade option carries a rarity value, and `SkillUpgradeButton` uses that rarity to change the button background sprite.

This first phase focuses on presentation and testing. Rarity should be visible to the player, but it does not need to control random chance yet.

## Intended Rarity Rules

- Add rarity values:
  - `Common`
  - `Rare`
  - `UltraRare`
- For normal scalable stats, calculate rarity from the rolled upgrade value compared to that stat's possible upgrade range.
- For subtract-mode stats such as cooldowns, calculate rarity from player benefit, not from whether the resulting stat value gets larger.
  - A larger cooldown decrease is better.
  - A smaller raw final cooldown value should not confuse the tier calculation.
- For finite high-impact stats, allow the stat to be tagged in the Editor so it can be shown as `UltraRare` even when its upgrade value is fixed.
  - Examples: extra turret count, extra saw count, or similar structural upgrades.
- Do not automatically mark every finite stat as `UltraRare`; finite stats need explicit designer/editor metadata.
- This phase may visually show rarity even if the actual option chance is not rarity-weighted yet.

## Key Changes

- Add a skill upgrade rarity enum under the skill upgrade flow domain, for example `SkillUpgradeRarity`.
- Extend `SkillUpgradeOption` with a `Rarity` property.
- Extend `ClickableButtonData` with a `Rarity` property so the UI layer receives the option rarity.
- Update `SkillUpgradeFlow.CreateUpgradeOptions` to assign rarity when each option is created.
- Add a small rarity calculation helper near the upgrade flow or stats domain.
- Expose enough stat upgrade range data to calculate rolled-value rarity safely.
  - Current `IUpgradeableStat` exposes `GetUpgradeValueBasedOnUpdateRange()`, but does not expose the min/max upgrade range.
  - Add a narrow API rather than relying on reflection over private fields.
- Add editor-configurable rarity metadata for special finite/high-impact stats.
  - Prefer metadata owned by the skill config or stat wrapper, so designers can tag specific stats without hardcoding property names in UI.
  - If using property-name metadata as a first pass, document that renamed stat properties must update the metadata.
- Update `SkillUpgradePresenter.ShowStatsUpgradeSection` to copy `SkillUpgradeOption.Rarity` into `ClickableButtonData`.
- Update `SkillUpgradeButton.Initialize` to accept rarity and apply the matching background sprite.
- Add a rarity sprite mapping, preferably a ScriptableObject under `Assets/ScriptableObjects/UI/`, similar in spirit to `SkillUpgradeKeyboardIconMapping`.
- Wire common, rare, and ultra rare sprites through the Unity Editor.

## Suggested Implementation Order

1. Add `SkillUpgradeRarity` and thread it through `SkillUpgradeOption` and `ClickableButtonData`.
2. Update `SkillUpgradeButton` to accept rarity and change the target background `Image` sprite.
3. Add a serialized rarity sprite mapping asset reference to `SkillUpgradeButton`.
4. Add stat upgrade range access to `IUpgradeableStat`.
5. Add rarity calculation for normal scalable stats.
6. Add designer/editor metadata for finite high-impact stat rarity overrides.
7. Update `SkillUpgradeFlow.CreateUpgradeOptions` to combine calculated rarity and metadata override.
8. Wire sprites and any special stat metadata in the Unity Editor.
9. Validate in Play Mode with several skill upgrade option types.

## Rarity Calculation Notes

- Integer upgrade ranges currently use `UnityEngine.Random.Range(int min, int max)`, where `max` is exclusive.
  - A range displayed or authored as `4..7` can roll `4`, `5`, or `6`.
  - The rarity calculator must not classify an unreachable value as `UltraRare`.
- Float ranges use rounded random values from the configured min/max range.
- Small ranges may not produce genuinely rare outcomes.
  - For example, three possible integer values make the highest value about one third of rolls.
  - In this first visual phase that can still be acceptable, but later chance-balancing should revisit thresholds.
- For `_alwaysUseMinValueForUpgrade` stats, rarity should not depend on the random roll if the applied upgrade ignores that roll.
  - These are good candidates for explicit editor metadata instead.
- If an upgrade is capped by max value and applies less benefit than the rolled value, rarity should be based on actual applied benefit if possible.
  - If actual applied benefit is hard to compute safely in this phase, avoid escalating rarity only because the stat reaches max.

## Risks And Gaps

- Rarity may look chance-weighted before it actually is. Keep this as an intentional testing-phase behavior and avoid promising rarity odds in UI text.
- Automatic finite-stat detection could make too many upgrades look ultra rare. Use explicit metadata for structural upgrades.
- Property-name-based metadata can break silently if a stat property is renamed.
- Cooldown and other subtract-mode stats can be classified backwards if rarity uses final value instead of player benefit.
- Max-capped upgrades can look more valuable than they are if rarity is based only on the rolled value.
- Changing serialized stat structures can affect existing ScriptableObject assets and presets. Prefer additive fields and Editor wiring over reshaping existing serialized data.

## Tests And Validation

- Run:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

- Manual Unity checks:
  - Upgrade buttons still show and apply upgrades correctly.
  - Keyboard hotkeys still invoke the correct button.
  - Common, rare, and ultra rare backgrounds render on instantiated upgrade buttons.
  - Cooldown/decrease upgrades classify higher benefit as higher rarity.
  - Fixed structural upgrades tagged in the Editor show the configured rarity.
  - Untagged finite stats do not automatically become ultra rare.
  - Missing sprite mapping or missing background image fails gracefully without blocking upgrade selection.

## Assumptions

- This phase is presentation-first and does not need rarity-weighted option selection.
- Rarity visuals are only for stat upgrade buttons, not new-skill unlock screens.
- Sprite assets and prefab wiring will be done in the Unity Editor instead of direct prefab text edits.
- Existing skill upgrade flow ownership remains unchanged: `SkillUpgradeFlow` creates option data, and `SkillUpgradePresenter` plus `SkillUpgradeButton` render it.
