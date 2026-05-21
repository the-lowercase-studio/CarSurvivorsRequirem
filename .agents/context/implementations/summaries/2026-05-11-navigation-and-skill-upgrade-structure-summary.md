# Navigation and Skill Upgrade Structure Implementation Summary

## Scope

Started implementation of `.agents/context/implementations/plans/2026-05-11-navigation-and-skill-upgrade-structure.md`.

Completed:

- Phase 3: align selected UI namespaces with physical folders.
- Phase 1: extract skill upgrade flow orchestration out of `UI/Skills/SkillUpgradePresenter.cs`.
- Phase 2: create `Assets/Scripts/Navigation/` boundary for grid and flow-field scripts.
- Phase 4: update folder map and related system docs after source moves.

Not completed:

- Deferred assembly definition work remains deferred.

## Completed Source Changes

### UI Namespace Alignment

Updated these files to use namespaces that match their current folder paths:

- `Assets/Scripts/UI/Common/ButtonsAudioClipPlayer.cs` to `Assets.Scripts.UI.Common`.
- `Assets/Scripts/UI/Common/MenuButtonsFunctionality.cs` to `Assets.Scripts.UI.Common`.
- `Assets/Scripts/UI/HUD/TimerPresenter.cs` to `Assets.Scripts.UI.HUD`.
- `Assets/Scripts/UI/Pause/PausePresenter.cs` to `Assets.Scripts.UI.Pause`.
- `Assets/Scripts/UI/Skills/ClickableButtonData.cs` to `Assets.Scripts.UI.Skills`.

Updated consumers:

- `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs` now imports `Assets.Scripts.UI.HUD` for `TimerPresenter` and `ITimerPresenter`.
- `Assets/Scripts/UI/Death/PlayerDeathPresenter.cs` now imports `Assets.Scripts.UI.HUD` for `ITimerPresenter`.

No files were moved in this phase.

### Skill Upgrade Flow Extraction

Added the new skill-owned flow folder:

```text
Assets/Scripts/Skills/UpgradeFlow/
  SkillUpgradeFlow.cs
  SkillUpgradeFlow.cs.meta
  SkillUpgradeOption.cs
  SkillUpgradeOption.cs.meta
  SkillUpgradeRequest.cs
  SkillUpgradeRequest.cs.meta
```

`SkillUpgradeFlow` now owns:

- queued new skills;
- queued upgradeable skills;
- random new-skill and upgradeable-skill selection;
- skill initialization calls through `ISkillsRegistry.InitializeSkill`;
- upgrade option text and apply callbacks;
- limiting upgrade options to three choices.

`SkillUpgradePresenter` now owns:

- subscribing to level-up visual completion and collectible release events;
- section visibility;
- upgrade button creation;
- UI audio;
- skill visual display;
- pause/resume timing.

Pause/resume behavior intentionally remains in the UI presenter to preserve the current ordering around showing and dismissing skill UI.

### DI Integration

Updated `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`:

- Added `using Assets.Scripts.Skills.UpgradeFlow`.
- Registered `SkillUpgradeFlow` as `ISkillUpgradeFlow`.
- Registered the serialized `SkillsVisualPresenter _skillsVisualPresenter` scene reference as `ISkillsVisualPresenter`.

`SkillUpgradePresenter` now injects `ISkillUpgradeFlow` and `ISkillsVisualPresenter`.

### Runtime Lookup Removal

Removed the runtime lookup:

```csharp
GameObject.FindGameObjectWithTag(typeof(SkillsVisualPresenter).Name)
```

`SkillUpgradePresenter` now receives `ISkillsVisualPresenter` through Reflex injection.

Unity setup required:

- Assign `_skillsVisualPresenter` on the scene instance that owns `DefaultGameplaySceneInstaller`.
- Verify the existing `SkillsVisualPresenter` scene object remains wired to its skill visuals.

### Navigation Boundary

Moved grid and flow-field scripts under:

```text
Assets/Scripts/Navigation/
  GridSystem/
  FlowFieldSystem/
```

Preserved moved script and folder `.meta` files for `GridSystem` and `FlowFieldSystem`.

Updated namespaces:

- `Assets.Scripts.GridSystem` to `Assets.Scripts.Navigation.GridSystem`.
- `Assets.Scripts.FlowFieldSystem` to `Assets.Scripts.Navigation.FlowFieldSystem`.

Updated C# consumers across enemies, EXP particles, spawners, skill crate spawning, editor GUI, and `DefaultGameplaySceneInstaller`.

Updated `Assembly-CSharp.csproj` compile includes so the targeted `dotnet build` command works before Unity regenerates project files.

Resolved the `UnityEngine.Grid` name collision in moved consumers with local aliases where unqualified `Grid` would be ambiguous.

### Documentation Updates

Updated:

- `.agents/context/project-scripts-folder-map.md`
- `.agents/context/grid-system.md`
- `.agents/context/flow-field-system.md`
- `.agents/context/enemies-system.md`
- `.agents/context/collectibles-system.md`
- `.agents/context/waves-system.md`

## Behavior Preservation Notes

- Public type names were preserved.
- Existing serialized field names were preserved except for the new `_skillsVisualPresenter` field on `DefaultGameplaySceneInstaller`.
- Upgrade option count remains three.
- Button text format remains unchanged.
- Skill initialization still routes through `ISkillsRegistry.InitializeSkill`.
- The upgrade-selection path preserves the previous extra random finder call before enqueueing an upgradeable skill.
- No `.unity` or `.asset` files were edited by hand.
- Existing moved `.meta` files were moved with their folders and scripts to preserve GUIDs.

## Validation

Ran:

```powershell
dotnet build Assembly-CSharp-firstpass.csproj -p:BuildProjectReferences=false
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

Result:

- Build succeeded.
- Existing `CS0649` warnings remain for injected and serialized fields.
- No new compile errors after the completed phases.
- Current re-validation on 2026-05-21 required building `Assembly-CSharp-firstpass.csproj` first because `Assembly-CSharp.csproj` expected `Temp/bin/Debug/Assembly-CSharp-firstpass.dll`.

## Remaining Manual Checks

Run these in Unity before treating these phases as complete:

- Assign the new `_skillsVisualPresenter` reference on `DefaultGameplaySceneInstaller`.
- Trigger an exp level-up and confirm the new skill section appears as before.
- Confirm skill upgrade buttons still show up to three options.
- Confirm upgrade clicks apply the stat change once.
- Confirm pause/resume behavior matches the previous flow.
- Confirm crate release still triggers skill initialization or upgrade presentation.
- Check Main Menu, Pause, Death, HUD, and Skill Upgrade UI after namespace changes.
- Check Unity Console for missing script, missing reference, or namespace serialization issues.
- Check enemy movement toward the player.
- Check EXP particle movement and collection.
- Check enemy and collectible spawning on grid positions.
- Check grid and flow-field debug views if debug options are available.
