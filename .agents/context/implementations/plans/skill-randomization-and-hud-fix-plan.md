# Implementation Plan - Skill Randomization & Starting Saw HUD Visibility Fix

Date: 2026-09-06

Fixes the deterministic skill selection defect by introducing in-place shuffling and deferred candidate rolling, and ensures the starting Saw skill icon is correctly visible in the Active Skills HUD at game start.

## User Review Required

> [!IMPORTANT]
> - Candidate skill selection for new skill rewards will be evaluated at popup dequeue time (`TryGetNextRequest`) rather than at queue time (`QueueRandomNewSkillRequest`). This ensures candidates are always rolled dynamically and genuinely randomized from the currently uninitialized skills, avoiding stale or depleted choices when multiple levels are gained in quick succession.
> - The 3 HUD slots at the top-left will render empty sockets using a translucent empty slot color (`_emptySlotColor`), transitioning to full opacity with the skill icon upon unlock. Slot 0 will immediately display the default starting Saw skill upon game launch.

## Open Questions

None. All requirements have been confirmed by the user.

## Proposed Changes

### Core Extensions & Collections

#### [MODIFY] Assets/Scripts/Extensions/ListExtensions.cs
- Add `public static void ShuffleInPlace<T>(this IList<T> list)` extension method performing Fisher-Yates shuffle directly on the provided list without allocating intermediate objects.
- Retain existing `Shuffle<T>` overloads for non-mutating copy workflows to preserve backward compatibility.

---

### Skills Domain & Upgrade Flow

#### [MODIFY] Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
- Refactor `QueueRandomNewSkillRequest`: Enqueue a `NewSkillChoice` request without freezing candidate choices prematurely.
- Refactor `TryGetNextRequest`:
  - When processing `NewSkillChoice`, query `skillsRegistry.GetUninitializedSkills()`.
  - Shuffle candidates using `candidates.ShuffleInPlace()`.
  - Pick up to `SkillConstants.NEW_SKILL_CHOICE_COUNT` (2) skills from the shuffled pool.
  - If cap is reached or no uninitialized skills remain, cleanly fall back to upgradeable skill stats.
- In `CreateUpgradeOptions`: Call `options.ShuffleInPlace()` so stat upgrades are also genuinely randomized rather than deterministically ordered.

---

### UI HUD & Scene Setup

#### [MODIFY] Assets/Scripts/UI/HUD/PlayerSkillsHUDPresenter.cs
- Add `_emptySlotColor` (`new Color(0.15f, 0.15f, 0.15f, 0.5f)`) and `_filledSlotColor` (`Color.white`) serialized fields.
- In `InitializeSlots()`: Set empty slot visuals so all 3 slots are immediately visible as dark sockets without hiding GameObjects.
- In `AssignSkillToSlot()`:
  - Add `_assignedSkills` (`HashSet<ISkillBase>`) check to prevent duplicate slot registrations.
  - Assign `skill.SkillInfo.Icon`, set color to `_filledSlotColor`, and enable the icon image.
  - Add safety guard: Only call `_emptySlotFrames[slotIndex].SetActive(false)` if `_emptySlotFrames[slotIndex] != iconHolder.gameObject` to prevent accidental deactivation of the icon's own GameObject.
- Ensure `Start()` binds and displays the starting `SawSkill` on slot 0 immediately upon boot.

#### [MODIFY] Assets/Scenes/RuinedBloodCity.unity
- Fix slot index ordering on `PlayerSkillsHUDPresenter`:
  - Slot 0: `SkillIcon` (leftmost)
  - Slot 1: `SkillIcon (1)` (middle)
  - Slot 2: `SkillIcon (2)` (rightmost)

---

## Verification Plan

### Automated Checks
- Solution build verification:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Expectation: Build succeeds with 0 errors and 0 warnings.

### Manual Verification
1. Launch `RuinedBloodCity.unity` in Unity Editor play mode.
2. Verify that upon scene boot, Slot 0 (leftmost) in the top-left HUD immediately displays the 2D Saw skill icon, while Slot 1 and Slot 2 are visible as empty sockets.
3. Level up to Level 4 (triggering a new skill choice):
   - Verify that 2 choices appear side-by-side.
   - Run multiple test runs to verify that the 2 offered skills are truly random (can be Minigun + Lasergun, Minigun + Landmine, or Lasergun + Landmine) rather than always the same two in hierarchy order.
4. Select a skill via hotkey `1` or `2`:
   - Verify that the chosen skill populates Slot 1 in the HUD with punch scale animation.
   - Verify that the next new skill reward (Level 7) rolls from the remaining uninitialized skills.
