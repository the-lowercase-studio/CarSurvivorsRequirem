# Implementation Summary - Skill Randomization & Starting Saw HUD Visibility Fix

Date: 2026-09-06

## Overview

Resolved the deterministic skill selection defect by introducing zero-allocation in-place list shuffling (`ShuffleInPlace`) and deferring new skill candidate rolling to popup dequeue time. In addition, fixed the HUD presenter defect where the starting Saw skill icon was hidden upon boot due to accidental GameObject deactivation and reversed slot ordering in the scene.

## Key Changes

### Core Extensions
- Assets/Scripts/Extensions/ListExtensions.cs: Added `ShuffleInPlace<T>(this IList<T> list)` executing in-place Fisher-Yates shuffling without intermediate object allocations. Reused `ShuffleInPlace` within the existing non-mutating `Shuffle<T>` overload.

### Skills Domain & Upgrade Flow
- Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs:
  - Refactored `QueueRandomNewSkillRequest` to enqueue `NewSkillChoice` without freezing candidate choices prematurely.
  - In `TryGetNextRequest`, dynamically rolled and shuffled (`candidates.ShuffleInPlace()`) up to 2 uninitialized skills at the exact moment the choice is displayed to the player, eliminating stale or depleted choices when leveling up multiple times in rapid succession.
  - In `CreateUpgradeOptions`, switched to `options.ShuffleInPlace()` to ensure stat upgrade cards are also genuinely randomized.

### Active Skills HUD Presenter & Scene Wiring
- Assets/Scripts/UI/HUD/PlayerSkillsHUDPresenter.cs:
  - Added `_emptySlotColor` (`new Color(0.12f, 0.12f, 0.12f, 0.45f)`) and `_filledSlotColor` (`Color.white`) properties to present clean, distinct empty slot sockets at game start.
  - Added `_assignedSkills` tracking (`HashSet<ISkillBase>`) to prevent redundant slot assignment.
  - Added safety guard: Only deactivates `_emptySlotFrames[slotIndex]` if it is distinct from `iconHolder.gameObject`, preventing the slot image's own GameObject from being disabled.
  - In `Start()`, reliably queries `GetInitializedSkills()` to assign and show the default starting `SawSkill` on Slot 0 upon game launch, and subscribes to `OnSkillInitialized` with debounce protection.
- Assets/Scenes/RuinedBloodCity.unity:
  - Reordered `_skillIconHolders` and `_emptySlotFrames` on `PlayerSkillsHUDPresenter` so Slot 0 corresponds to the leftmost slot (`SkillIcon`), Slot 1 to the middle (`SkillIcon (1)`), and Slot 2 to the rightmost (`SkillIcon (2)`).

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/skill-randomization-and-hud-fix-plan.md
- Coding Standards: Verified 100% compliance with .agents/context/project-coding-standards.md (explicit block bodies, no banned LINQ on hot paths, member order: `[Inject]`, `[SerializeField]`, private fields, fail-fast checks).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 new warnings.

### Manual Verification Steps
1. Play `RuinedBloodCity.unity` in Unity Editor.
2. Verify that upon boot:
   - Slot 0 (leftmost) in the Active Skills HUD immediately shows the Saw icon.
   - Slots 1 and 2 are visible as empty translucent sockets.
3. Level up to Level 4:
   - Verify that 2 choices appear side-by-side.
   - Verify that the offered skills are randomly chosen from the uninitialized pool (Minigun, Lasergun, Landmine).
4. Select Option 1 or Option 2:
   - Verify that the selected skill populates Slot 1 in the HUD with a punch scale animation.

## Follow-up / Unity Editor Steps

No additional manual inspector setup required; scene wiring and script defaults are fully configured.
