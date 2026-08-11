# Implementation Plan - Upgradable Stat Icons in Skill Configs & UI Presenter

Date: 2026-08-11

This plan details the steps to add icon support to all upgradeable stats defined in skill ScriptableObject configs (`SkillUpgradeableStatsConfig`), propagate those icons through the skill upgrade flow (`SkillUpgradeOption`), and display the corresponding stat icon next to each drawn stat upgrade button in the skill upgrade presenter (`SkillUpgradePresenter` / `SkillUpgradeButton`).

## User Review Required

- Unity Inspector UI: `UpgradeableStatDrawer` will automatically render the new `Icon` field in the Unity Inspector for all `FloatUpgradeableStat` and `IntUpgradeableStat` fields in skill configs.
- Prefab Wiring: The `_upgradeButtonPrefab` referenced in `SkillUpgradePresenter` will need an `Image` component assigned to `_statIconImage` in Unity Editor (or auto-resolved by child object name `"StatIcon"` / `"Icon"`).

## Open Questions

None.

## Proposed Changes

### Core Stats & Serialization

#### [MODIFY] Assets/Scripts/Stats/UpgradeableStat.cs
- Add `Sprite Icon { get; }` and `void SetIcon(Sprite icon)` to `IUpgradeableStat`.
- Add `[field: SerializeField] public Sprite Icon { get; protected set; }` to `UpgradeableStat<T>`.
- Implement `SetIcon(Sprite icon)` to allow setting/cloning the icon reference on runtime instances.

#### [MODIFY] Assets/Scripts/Utils/DeepCopyUtility.cs
- Update `DeepCopy<T>(T obj)` to perform a clean deep copy using `JsonUtility`.

---

### Skill Upgrade Flow Data Structures

#### [MODIFY] Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeOption.cs
- Add `Sprite Icon { get; }` property to `SkillUpgradeOption`.
- Update constructor `SkillUpgradeOption(string text, Action apply, SkillUpgradeRarity rarity, Sprite icon = null)`.

#### [MODIFY] Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
- In `CreateUpgradeOptions`, pass `upgradeableStat.Icon` into `new SkillUpgradeOption(..., rarity, icon)`.

---

### Skill Upgrade UI Presenter & Buttons

#### [MODIFY] Assets/Scripts/UI/Skills/ClickableButtonData.cs
- Add `public Sprite Icon { get; set; }` property to `ClickableButtonData`.

#### [MODIFY] Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
- In `ShowStatsUpgradeSection`, populate `Icon = option.Icon` when constructing `ClickableButtonData`.

#### [MODIFY] Assets/Scripts/UI/Skills/SkillUpgradeButton.cs
- Add `[SerializeField] private Image _statIconImage;` reference.
- Update `Initialize` signature to accept `Sprite icon = null`.
- Add `UpdateStatIcon(Sprite icon)` helper to set sprite and enable/disable `_statIconImage`.
- Update `ResolveMissingReferences()` to automatically resolve `_statIconImage` if null by looking for child image named `"StatIcon"` or `"Icon"`.

---

## Verification Plan

### Automated Tests
- Build verification via `dotnet build`:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. Open Unity Editor and inspect skill config ScriptableObjects (e.g. `LasergunSkillSO`, `SawSkillSO`, `MinigunSkillSO`, `LandmineSkillSO`).
2. Verify that each upgradeable stat field shows an `Icon` sprite field in the inspector drawer.
3. Assign test icons to upgradeable stats in a skill config.
4. Run the game in Play Mode, trigger a skill upgrade reward (e.g. via EXP gain or Crate drop).
5. Verify that drawn stat upgrade buttons display the corresponding icon next to the stat description text.
6. Verify that options with missing/null icons gracefully hide the icon image without errors.
