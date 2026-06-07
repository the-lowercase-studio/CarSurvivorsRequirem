# Skill Upgrade Rarity Visuals Implementation Summary

## Completed

- Added `SkillUpgradeRarity` values for `Common`, `Rare`, and `UltraRare`.
- Threaded rarity through `SkillUpgradeOption`, `ClickableButtonData`, `SkillUpgradePresenter`, and `SkillUpgradeButton`.
- Added `SkillUpgradeRarityCalculator` to classify rolled upgrade values against the stat upgrade range.
- Exposed upgrade range metadata through `IUpgradeableStat` for rarity calculation.
- Added property-name rarity overrides to `SkillUpgradeableStatsConfig` so finite/high-impact stats can be explicitly tagged in the Editor.
- Added `SkillUpgradeRaritySpriteMapping` for mapping rarity values to button background sprites.

## Notes

- Integer range rarity accounts for Unity's exclusive `Random.Range(int min, int max)` upper bound.
- Subtract-mode stats use upgrade benefit size for rarity because the rolled upgrade value is still the positive amount applied as a decrease.
- `_alwaysUseMinValueForUpgrade` stats use the configured upgrade range minimum for rarity instead of the random roll.
- Rarity sprite assets, the mapping asset, button prefab references, and special stat overrides still need Unity Editor wiring.

## Validation

- Ran `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Build succeeded with existing CS0649 Unity/DI serialization warnings unrelated to this change.
