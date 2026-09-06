# Specification: Skill Acquisition & Selection Overhaul

**Date:** 2026-09-06  
**Author:** Antigravity  
**Target Systems:** Assets/Scripts/Skills/, Assets/Scripts/UI/Skills/, Assets/Scripts/UI/HUD/, Assets/ScriptableObjects/Skills/, Assets/Prefabs/Skills/, Assets/Prefabs/UI/, Assets/Scripts/ReflexDI/  

---

## 1. Overview & Player Experience

### Summary
This feature completely overhauls the player skill acquisition and upgrade loop:
1. **Max 3 Skills Constraint:** The player starts the run with 1 default skill (currently the Saw blade) and can acquire at most 2 additional skills during gameplay (total cap of 3 active skills). Once 3 skills are equipped, the player cannot acquire any more skills. All subsequent rewards (level-ups and crates) offer stat upgrades exclusively for the 3 possessed skills.
2. **2-Choice Skill Selection Popup:** When a new skill reward triggers, the popup displays 2 randomly chosen uninitialized skills side-by-side. Each option features its 3D preview model (rendered in real-time via dual RenderTextures from isolated preview stations), its name, its description, and its dedicated keyboard hotkey (`1` or `2`). Pressing `1` commits Option 1; pressing `2` commits Option 2. The chosen skill is initialized into active combat; the unchosen skill returns to the uninitialized candidate pool.
3. **Active Skills HUD Display:** The top-left of the screen displays 3 persistent skill slots. Empty slots are visible from the start as empty frames/sockets. As skills are acquired, their respective 2D icons (`SkillInfoSO.Icon`) populate the slots.

### Player-Facing Goals
- **Build Customization Agency:** Players make active choices between two randomly offered skills, shaping their tactical build rather than being forced into an automatic random roll.
- **Immediate Build Clarity:** The persistent 3-slot HUD at the top-left provides at-a-glance feedback on equipped capabilities and remaining build slots.
- **Fast, Focused Controls:** Streamlined keyboard selection using keys `1` and `2` (supporting both top-row Alpha1/Alpha2 and Keypad1/Keypad2) without requiring mouse interaction during pauses.

### In-Scope vs. Out-of-Scope
- **In-Scope:**
  - Enforcing a strict cap of 3 active skills in `SkillConstants`, `SkillsRegistry`, and `SkillUpgradeFlow`.
  - Creating a 2-choice skill popup in `SkillUpgradePresenter` with keyboard `1` / `2` selection.
  - Adding dual-station 3D preview rendering in `SkillsVisualPresenter` and `SkillsVisualRenderer.prefab` using two RenderTextures (`Option A`).
  - Adding `Sprite Icon` property to `SkillInfoSO`.
  - Creating `PlayerSkillsHUDPresenter` and 3-slot HUD widget at top-left with empty slot frames.
  - Reflex binding in `DefaultGameplaySceneInstaller`.
- **Out-of-Scope:**
  - Creating brand-new skill archetypes beyond the existing 4 (Saw, Minigun, Lasergun, Landmine).
  - Changing starting car skill logic (Saw remains default starting skill).
  - Altering skill combat balance equations or damage numbers.
  - Mouse click selection for new skill choices (explicitly keyboard `1`/`2` only as requested).

---

## 2. Open Questions & Resolved Decisions

### Resolved Decisions
- [x] **Decision 1 (Preview Rendering):** Option A approved. Dual preview stations with two cameras and two RenderTextures (`SkillPreviewLeft.renderTexture` and `SkillPreviewRight.renderTexture`) in `SkillsVisualRenderer.prefab`. No UV-rect cropping.
- [x] **Decision 2 (HUD Placement & Slot State):** Top-left screen positioning (under the level/health bar). All 3 slots visible from the start, displaying empty frames until a skill is unlocked.
- [x] **Decision 3 (2D Skill Icons):** Designer/user has 2D icons ready for Saw, Minigun, Lasergun, and Landmine. We will expose `Sprite Icon` on `SkillInfoSO` and assign in Inspector.
- [x] **Decision 4 (Edge Case - Maxed Out Skills):** It is virtually impossible to exhaust upgrades due to infinite stats; however, if all upgrades were somehow exhausted, the system silently ignores further skill unlocks (hard cap of 3 active skills is inviolable).
- [x] **Decision 5 (Input Controls):** Strictly keyboard keys `1` and `2` (Alpha1/Numpad1 for Option 1, Alpha2/Numpad2 for Option 2) for the skill choice popup.

### Open Questions
*None. All open questions have been resolved and agreed upon.*

---

## 3. Data Model & Serialization

### ScriptableObjects
- **`SkillInfoSO` (`Assets/ScriptableObjects/Skills/SkillInfoSO.cs`):**
  - Add serialized field:
    ```csharp
    [field: SerializeField] public Sprite Icon { get; private set; }
    ```
  - Designer assigns 2D icons in `SawSkillInfo.asset`, `MinigunSkillInfo.asset`, `LasergunSkillInfo.asset`, `LandmineSkillInfo.asset`.

### Constants
- **`SkillConstants` (`Assets/Scripts/Skills/Constants/SkillConstants.cs`):**
  - Add:
    ```csharp
    public const int MAX_ACTIVE_SKILLS = 3;
    public const int NEW_SKILL_CHOICE_COUNT = 2;
    ```

### RenderTextures
- **`SkillItemRenderTextureLeft.renderTexture` (`Assets/Textures/Skills/`):**
  - Left station output (or existing `SkillItemRenderTexture.renderTexture` repurposed as Left).
- **`SkillItemRenderTextureRight.renderTexture` (`Assets/Textures/Skills/`):**
  - Right station output.

---

## 4. Architecture & Reflex DI Contracts

### Domain & Services

#### `ISkillsRegistry` & `SkillsRegistry` (`Assets/Scripts/Skills/SkillsRegistry.cs`)
- Extend contract:
  ```csharp
  public interface ISkillsRegistry
  {
      IReadOnlyList<ISkillBase> Skills { get; }
      int UninitializedSkillsCount { get; }
      int InitializedSkillsCount { get; }
      IReadOnlyList<ISkillBase> GetInitializedSkills();
      IReadOnlyList<ISkillBase> GetUninitializedSkills();
      ISkillBase InitializeSkill(ISkillBase skill);
      event Action<ISkillBase> OnSkillInitialized;
  }
  ```
- Implementation tracks `InitializedSkillsCount`.
- `InitializeSkill` triggers `OnSkillInitialized?.Invoke(skill)`.
- Starting skill (`Skills[0]`) triggers `OnSkillInitialized` upon initialization in `Start()`.

#### `SkillUpgradeRequest` & `SkillUpgradeFlow` (`Assets/Scripts/Skills/UpgradeFlow/`)
- Extend `SkillUpgradeRequestType`:
  ```csharp
  public enum SkillUpgradeRequestType
  {
      NewSkillChoice,
      UpgradeSkill
  }
  ```
- `SkillUpgradeRequest` carries `IReadOnlyList<ISkillBase> SkillChoices` when `RequestType == SkillUpgradeRequestType.NewSkillChoice`.
- `SkillUpgradeFlow`:
  - `QueueRandomNewSkillRequest`:
    - Validates `skillsRegistry.InitializedSkillsCount < SkillConstants.MAX_ACTIVE_SKILLS`. If cap is reached, returns `false`.
    - Shuffles uninitialized skills and takes up to `SkillConstants.NEW_SKILL_CHOICE_COUNT` (normally 2, or 1 if only 1 remains).
    - Enqueues `QueuedSkillRewardRequest.ForNewSkillChoice(candidates)`.
  - `TryGetNextRequest`:
    - Dequeues `NewSkillChoice` request without auto-initializing either skill.
    - Yields `SkillUpgradeRequest.ForNewSkillChoice(candidates)`.
  - `QueueRandomSkillUpgradeRequest`:
    - Filters upgrade candidates to initialized skills.
    - If no upgradeable skill found and `InitializedSkillsCount < SkillConstants.MAX_ACTIVE_SKILLS`, falls back to `QueueRandomNewSkillRequest`.
    - If cap is reached and no upgrades available, cleanly exits without doing anything.

#### `SkillsVisualPresenter` (`Assets/Scripts/UI/Skills/SkillsVisualPresenter.cs`)
- Extend contract:
  ```csharp
  public interface ISkillsVisualPresenter
  {
      void ShowSkillVisual(SkillInfoSO skillInfoSO, int slotIndex = 0);
      void HideAll();
  }
  ```
- Manages two visual sets (Station 0 for Left, Station 1 for Right).
- `ShowSkillVisual(skillInfoSO, 0)` activates matching GameObject on Left Station.
- `ShowSkillVisual(skillInfoSO, 1)` activates matching GameObject on Right Station.
- `HideAll()` disables all visuals on both stations.

#### `SkillUpgradePresenter` (`Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs`)
- Refactored `_newSkillSection` to a 2-choice presentation view:
  - Card 1 (Left): `RawImage` (sampling Left RT), Name text, Description text, Key `[1]` badge.
  - Card 2 (Right): `RawImage` (sampling Right RT), Name text, Description text, Key `[2]` badge.
- `Update()` input loop:
  - If `Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame`:
    Initialize Choice 1, notify presenter to advance reward queue.
  - If `Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame`:
    Initialize Choice 2, notify presenter to advance reward queue.
  - Frame-debounce protection via `_lastHandledInputFrame == Time.frameCount`.

#### `PlayerSkillsHUDPresenter` (`Assets/Scripts/UI/HUD/PlayerSkillsHUDPresenter.cs`)
- Colocated interface:
  ```csharp
  public interface IPlayerSkillsHUDPresenter { }
  ```
- Dependency injection:
  ```csharp
  [Inject] private readonly IPlayerManager _playerManager = null;
  ```
- Serialized inspector fields:
  ```csharp
  [SerializeField] private Image[] _skillIconHolders; // 3 slots
  [SerializeField] private GameObject[] _emptySlotFrames; // 3 empty socket frames
  ```
- Lifecycle:
  - `Start()`:
    - Populates slots with already-initialized skills (`_playerManager.SkillsRegistry.GetInitializedSkills()`).
    - Subscribes to `_playerManager.SkillsRegistry.OnSkillInitialized += HandleSkillInitialized`.
  - `OnDestroy()`:
    - Unsubscribes: `_playerManager.SkillsRegistry.OnSkillInitialized -= HandleSkillInitialized`.
  - `HandleSkillInitialized(ISkillBase skill)`:
    - Sets slot icon `_skillIconHolders[index].sprite = skill.SkillInfo.Icon`.
    - Enables slot icon GameObject and hides empty frame indicator.

#### Reflex DI Registration (`Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`)
- Inject `PlayerSkillsHUDPresenter` as `IPlayerSkillsHUDPresenter`:
  ```csharp
  [SerializeField] private PlayerSkillsHUDPresenter _playerSkillsHUDPresenter;
  ...
  builder.AddSingleton(_playerSkillsHUDPresenter, typeof(IPlayerSkillsHUDPresenter));
  ```

---

## 5. Visual, Audio & Tweening Integration

- **Dual 3D Preview Prefab (`SkillsVisualRenderer.prefab`):**
  - Station 0 (Left) at Local (0, 0, 0), Camera Left -> `SkillItemRenderTextureLeft`.
  - Station 1 (Right) at Local (10, 0, 0), Camera Right -> `SkillItemRenderTextureRight`.
  - Layer 10 (culling mask isolated so cameras don't see each other).
- **HUD Animations:**
  - When an icon appears in `PlayerSkillsHUDPresenter`, apply punch scale tween (`transform.DOPunchScale(Vector3.one * 0.2f, 0.3f)`) via `TransformTweenExtensions`, killing existing tweens on disable/destroy.
- **Audio:**
  - Re-use `IAudioClipPlayer`:
    - "Show" sound on popup open.
    - "Click" sound on pressing `1` or `2`.

---

## 6. Edge Cases, Performance & Lifecycle Invariants

- **Single Remaining Uninitialized Skill:** If only 1 skill remains locked in the game, Card 2 is hidden and key `1` selects the only choice.
- **Debounce / Multi-Input Prevention:** `_lastHandledInputFrame = Time.frameCount` prevents rapid accidental double-activation when navigating reward queues.
- **Zero Update Allocations:** HUD presenter runs purely on events; no polling in `Update()`.
- **Clean Event Cleanup:** All event handlers explicitly unsubscribed in `OnDestroy()`.

---

## 7. Implementation Plan (Phases & Steps)

### Phase 1: Core Domain & Data Model
- [ ] **Step 1.1:** Add `MAX_ACTIVE_SKILLS = 3` and `NEW_SKILL_CHOICE_COUNT = 2` to `SkillConstants.cs`.
- [ ] **Step 1.2:** Add `Sprite Icon` property to `SkillInfoSO.cs`.
- [ ] **Step 1.3:** Update `ISkillsRegistry` and `SkillsRegistry.cs`:
  - Implement `InitializedSkillsCount`, `GetInitializedSkills()`, and `event Action<ISkillBase> OnSkillInitialized`.
  - Fire `OnSkillInitialized` on `InitializeSkill()`.
- [ ] **Verification 1:** Compile check: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.

### Phase 2: Upgrade Flow Refactoring
- [ ] **Step 2.1:** Update `SkillUpgradeRequest.cs` to add `SkillUpgradeRequestType.NewSkillChoice` and support candidate skills.
- [ ] **Step 2.2:** Update `SkillUpgradeFlow.cs`:
  - Guard against exceeding `MAX_ACTIVE_SKILLS`.
  - Pick up to 2 uninitialized candidate skills for `NewSkillChoice`.
  - Keep skills uninitialized until explicitly chosen by player.
- [ ] **Step 2.3:** Update `ShouldQueueNewSkillReward` in `SkillUpgradePresenter.cs` to check `InitializedSkillsCount < MAX_ACTIVE_SKILLS`.
- [ ] **Verification 2:** Compile check with zero warnings.

### Phase 3: Dual Station 3D Visual Presenter
- [ ] **Step 3.1:** Create `SkillItemRenderTextureRight.renderTexture` asset.
- [ ] **Step 3.2:** Refactor `ISkillsVisualPresenter` and `SkillsVisualPresenter.cs` to support `ShowSkillVisual(SkillInfoSO info, int slotIndex = 0)` and `HideAll()`.
- [ ] **Step 3.3:** Update `SkillsVisualRenderer.prefab` with second camera and right-station models.
- [ ] **Verification 3:** Compile check and verify visual presenter responds to slot indices.

### Phase 4: Popup UI with 1/2 Keyboard Hotkeys
- [ ] **Step 4.1:** Refactor `SkillUpgradePresenter.cs` to manage two choice cards (Left & Right) with separate RawImages, name labels, description labels, and `[1]` / `[2]` badges.
- [ ] **Step 4.2:** Implement keyboard input detection for keys `1` and `2` (Alpha & Numpad) in `SkillUpgradePresenter.Update()`.
- [ ] **Step 4.3:** Connect choice commit to `SkillsRegistry.InitializeSkill(chosenSkill)` and advance queue.
- [ ] **Verification 4:** Compile check.

### Phase 5: Active Skills HUD Widget
- [ ] **Step 5.1:** Create `PlayerSkillsHUDPresenter.cs` with `IPlayerSkillsHUDPresenter` colocated above it.
- [ ] **Step 5.2:** Bind in `DefaultGameplaySceneInstaller.cs`.
- [ ] **Step 5.3:** Create / wire HUD UI GameObject in top-left with 3 slot frames and icon images.
- [ ] **Verification 5:** Compile check.

### Phase 6: Pre-Commit Gate & Playmode Validation
- [ ] **Step 6.1:** Run `unity-pre-commit-gate` (compilation, zero warnings, coding standards audit).
- [ ] **Step 6.2:** Manual Unity playmode test checklist.

---

## 8. Verification & Acceptance Criteria

- [ ] Project compiles with zero errors and zero new warnings: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- [ ] Player begins with 1 skill (Saw) shown in Slot 1 of the top-left HUD; Slots 2 and 3 show empty frames.
- [ ] Level-up or crate reward triggers 2-choice popup showing 2 distinct 3D models via dual RenderTextures.
- [ ] Pressing `1` selects Option 1; pressing `2` selects Option 2.
- [ ] Chosen skill appears in Slot 2 of HUD.
- [ ] After acquiring 3 skills total, no new skill choices ever trigger; all future rewards are stat upgrades.
- [ ] All coding standards (field order, naming conventions, explicit unsubscriptions) strictly upheld.
