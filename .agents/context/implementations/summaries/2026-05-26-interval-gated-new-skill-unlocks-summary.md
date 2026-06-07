# Interval-Gated New Skill Unlocks Implementation Summary

## Scope

Implemented `.agents/context/implementations/plans/2026-05-26-interval-gated-new-skill-unlocks.md`.

The change is limited to skill reward routing. It does not change skill upgrade option generation, skill stat upgrade application, skill configs, prefabs, scenes, balance data, or the existing start-skill registration order.

## Completed Changes

- Added `_newSkillLevelInterval = 6` to `SkillUpgradePresenter`.
- Split reward request handling so level-up rewards and crate rewards have separate code paths.
- Level-up rewards now grant a new skill only when `(currentLevel - 1) % _newSkillLevelInterval == 0`, so the default first new skill arrives at level 7.
- Crate rewards now queue skill upgrades only and cannot unlock locked skills early.
- Replaced `ISkillUpgradeFlow.QueueRandomRequest` with explicit methods:
  - `QueueRandomNewSkillRequest`.
  - `QueueRandomSkillUpgradeRequest`.
- Updated `RandomUpgradeableSkillFinder` to only choose initialized skills, preventing locked skills from being offered for upgrades before they are unlocked.

## Validation

- Ran `dotnet build Assembly-CSharp-firstpass.csproj -p:BuildProjectReferences=false` successfully to generate the firstpass DLL required by the targeted build.
- Ran `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` successfully.
- The build still reports existing CS0649 warnings for DI/serialized fields. No new compile errors were introduced.

## Manual Checks Still Needed

- In Unity play mode, confirm only the first skill is active at start.
- Confirm levels 2-6 offer upgrades only with the default interval.
- Confirm level 7 offers one new skill when locked skills remain.
- Confirm crates before level 7 offer upgrades only.
- Optionally set `_newSkillLevelInterval` to another value, such as `3`, and confirm unlocks occur at levels 4, 7, and 10.
