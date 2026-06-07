# Interval-Gated New Skill Unlocks

## Summary

Change skill rewards so the player starts with exactly one skill, receives only upgrades on levels 2-6, and receives the next new skill on level 7 when `_newSkillLevelInterval = 6`. Future new skills unlock at levels `1 + interval`, `1 + interval * 2`, etc. Skill upgrade option generation stays unchanged.

## Key Changes

- In `SkillUpgradePresenter`, add a serialized field: `[SerializeField] private int _newSkillLevelInterval = 6;`.
- Split reward event handling:
  - Level-up rewards check `level > 1 && (level - 1) % _newSkillLevelInterval == 0`.
  - If true and uninitialized skills remain, queue a new skill.
  - Otherwise queue an upgrade.
  - Crate rewards queue upgrades only.
- Replace ambiguous `ISkillUpgradeFlow.QueueRandomRequest(...)` with explicit methods:
  - `QueueRandomNewSkillRequest(ISkillsRegistry skillsRegistry)`.
  - `QueueRandomSkillUpgradeRequest(ISkillsRegistry skillsRegistry)`.
  - Keep `TryGetNextRequest(...)` as the presenter-facing dequeue/display method.
- Update `RandomUpgradeableSkillFinder` so it only selects initialized/owned upgradeable skills.
- Keep `SkillsRegistry` start behavior: register all child skills, initialize only `Skills[0]`, and leave all other skill acquisition to the reward flow.

## Behavior Rules

- Level 1/start: player has the first skill only.
- Levels 2-6 with interval 6: offer upgrades only.
- Level 7: offer one random locked skill if any remain.
- Levels 8-12: offer upgrades only.
- Level 13, 19, etc.: offer another random locked skill if any remain.
- If an unlock interval is reached but no locked skills remain, fall back to normal upgrade reward behavior.
- If no initialized skill can be upgraded, no reward panel is shown for that request.

## Test Plan

- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Manual Unity play check:
  - Confirm only one skill is active at game start.
  - Level up to 2-6 and confirm only upgrade choices appear.
  - Level up to 7 and confirm a new skill screen appears.
  - Continue to level 8 and confirm upgrades resume.
  - Pick up a skill crate before level 7 and confirm it offers upgrades only.
  - Set `_newSkillLevelInterval` to another value, such as `3`, and confirm new skills appear at levels 4, 7, 10.

## Assumptions

- The first skill remains the first child registered by `SkillsRegistry`, matching current behavior.
- New skill unlock timing uses levels gained after level 1, so interval `6` means first unlock at level `7`, not level `6`.
- No prefab or scene YAML should be text-edited for this change; the serialized interval defaults from code and can be adjusted in the Unity Inspector.
