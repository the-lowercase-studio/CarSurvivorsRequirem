# Scripts Folder Structure Cleanup Implementation Summary

## Scope

Implemented the completed portions of `.agents/docs/implementation-plans/2026-05-05-scripts-folder-structure-cleanup.md`.

The work is structure-focused. It avoids gameplay, balance, UI behavior, audio, VFX, prefab, scene, and asset changes except for one source compatibility fix in `CarController` described below.

Final cleanup pass:

- Completed the leftover `ObjectLifeCycle` to `ObjectLifecycle` cleanup by keeping the canonical lifecycle files under `Assets/Scripts/ObjectLifecycle/` and updating `Assembly-CSharp.csproj` to reference that path.
- Completed Phase 6 after user confirmation that the car is player-owned by moving car scripts to `Assets/Scripts/Player/Car/` and updating the namespace to `Assets.Scripts.Player.Car`.
- Updated related agent docs so the plan, folder map, car system doc, and this summary all point to the final structure.

## Completed Moves

### Phase 2: Domain-Owned Helpers

- Moved `Assets/Scripts/Helpers/ScreenSerializableResolutionHelper.cs` to `Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs`.
- Moved `Assets/Scripts/Helpers/EntityManipulationHelper.cs` to `Assets/Scripts/StatusEffects/EntityManipulationHelper.cs`.
- Moved `Assets/Scripts/Utils/EaseUtils.cs` to `Assets/Scripts/UI/Level/EaseUtils.cs`.
- Removed the now-empty `Assets/Scripts/Helpers/` folder and `.meta`.
- Kept these utilities in `Assets/Scripts/Utils/` because current usage is shared:
  - `RandomUtility.cs`
  - `TimeConversionUtility.cs`
  - `DeepCopyUtility.cs`

### Phase 5: UI Subfolders

Moved UI scripts into clearer owning subfolders while preserving existing `Assets.Scripts.UI` namespaces for Unity serialized references:

- `Assets/Scripts/UI/PausePresenter.cs` to `Assets/Scripts/UI/Pause/PausePresenter.cs`.
- `Assets/Scripts/UI/TimerPresenter.cs` to `Assets/Scripts/UI/HUD/TimerPresenter.cs`.
- `Assets/Scripts/UI/MenuButtonsFunctionality.cs` to `Assets/Scripts/UI/Common/MenuButtonsFunctionality.cs`.
- `Assets/Scripts/UI/ButtonsAudioClipPlayer.cs` to `Assets/Scripts/UI/Common/ButtonsAudioClipPlayer.cs`.
- `Assets/Scripts/UI/ClickableButtonData.cs` to `Assets/Scripts/UI/Skills/ClickableButtonData.cs`.
- `Assets/Scripts/EventHandlers/PointerEnterHandler.cs` to `Assets/Scripts/UI/Skills/PointerEnterHandler.cs`.

Removed the now-empty `Assets/Scripts/EventHandlers/` folder and `.meta`.

### Phase 3: Single-Interface Buckets

Moved single-interface folders where ownership is clear:

- `Assets/Scripts/Movement/IMovementController.cs` to `Assets/Scripts/Enemies/IMovementController.cs`.
- `Assets/Scripts/AnimationPlayers/IAttackAnimationPlayer.cs` to `Assets/Scripts/Enemies/IAttackAnimationPlayer.cs`.
- `Assets/Scripts/Collectibles/ICollectible.cs` to `Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs`.
- `Assets/Scripts/Activators/ItemsWithScriptableConfigsActivator.cs` to `Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs`.
- Removed the now-empty `Movement/`, `AnimationPlayers/`, `Collectibles/`, and `Activators/` folders and `.meta` files.

Kept these shared contracts in place for now:

- `Assets/Scripts/Providers/IGameObjectProvider.cs`
- `Assets/Scripts/Initializers/IInitializable.cs`
- `Assets/Scripts/Initializers/IInitializableWithScriptableConfig.cs`

### Phase 4: Ambiguous Domain Names

Renamed ambiguous top-level folders after explicit approval:

- `Assets/Scripts/GameManipulators/` to `Assets/Scripts/GameFlow/`.
- `Assets/Scripts/StatusAffectables/` to `Assets/Scripts/StatusEffects/`.
- `Assets/Scripts/ObjectLifeCycle/` to `Assets/Scripts/ObjectLifecycle/`.

Moved shared generic data types out of "custom" buckets:

- `Assets/Scripts/CustomTypes/ValueRange.cs` to `Assets/Scripts/Common/Types/ValueRange.cs`.
- `Assets/Scripts/CustomEventArgs/ValueEventArgs.cs` to `Assets/Scripts/Common/EventArgs/ValueEventArgs.cs`.

Removed the now-empty `CustomTypes/` and `CustomEventArgs/` folders and `.meta` files.

Removed the duplicate leftover `Assets/Scripts/ObjectLifeCycle/` folder after confirming the canonical files exist under `Assets/Scripts/ObjectLifecycle/`.

### Phase 6: Player-Owned Car Folder

Moved player car scripts under the player domain after user confirmation:

- `Assets/Scripts/Car/CarController.cs` to `Assets/Scripts/Player/Car/CarController.cs`.
- `Assets/Scripts/Car/CarVfxEffectsController.cs` to `Assets/Scripts/Player/Car/CarVfxEffectsController.cs`.

Removed the now-empty `Assets/Scripts/Car/` folder and `.meta`.

## Source Updates

- Updated namespaces and `using` directives for moved helper and interface files where namespace changes were intentionally made.
- Updated namespaces and `using` directives for Phase 4 folder renames:
  - `Assets.Scripts.GameFlow`
  - `Assets.Scripts.StatusEffects`
  - `Assets.Scripts.ObjectLifecycle`
  - `Assets.Scripts.Common.Types`
  - `Assets.Scripts.Common.EventArgs`
- Updated car namespace and references from `Assets.Scripts.Car` to `Assets.Scripts.Player.Car`.
- Updated `Assembly-CSharp.csproj` compile includes for moved files.
- Updated folder guidance in `.agents/docs/project-scripts-folder-map.md`.
- Updated stale paths in:
  - `.agents/docs/audio-system.md`
  - `.agents/docs/collectibles-system.md`
  - `.agents/docs/damage-numbers-system.md`
  - `.agents/docs/di-and-boot-flow-system.md`
  - `.agents/docs/enemies-system.md`
  - `.agents/docs/health-system.md`
  - `.agents/docs/pooling-and-object-lifecycle-system.md`
  - `.agents/docs/projectiles-system.md`
  - `.agents/docs/scoreboard-system.md`
  - `.agents/docs/settings-system.md`
  - `.agents/docs/status-effects-system.md`
  - `.agents/docs/ui-system.md`
- Updated progress and decisions in the implementation plan.

## Compatibility Fixes

- Fixed a `NullReferenceException` in `Assets/Scripts/Player/Car/CarController.cs` caused by Unity serialized wheel data using old nested field names.
- Fixed `ValueEventArgs<T>` after moving it into `Assets.Scripts.Common.EventArgs` by inheriting from `System.EventArgs` explicitly. This avoids the new namespace shadowing the framework `EventArgs` type.

### CarController Serialized Field Migration

Fixed a `NullReferenceException` in `Assets/Scripts/Player/Car/CarController.cs` caused by Unity serialized wheel data using old nested field names:

- `WheelModel`
- `WheelCollider`
- `Axel`

Added `FormerlySerializedAs` attributes for the current private serialized fields:

- `_wheelModel`
- `_wheelCollider`
- `_axel`

This lets Unity migrate existing prefab data without direct prefab editing.

## Validation

Ran:

```powershell
dotnet build Assembly-CSharp-firstpass.csproj -p:BuildProjectReferences=false
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

After the final cleanup pass, reran:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

Current result:

- Build succeeds.
- No compile errors.
- Existing CS0649 warnings remain for injected or serialized fields.

## Manual Unity Checks Still Needed

Open Unity and verify:

- No missing script references after file moves.
- Player car movement no longer throws `WheelCollider.set_motorTorque` null reference.
- Pause UI toggles correctly.
- HUD timer runs and binds through `DefaultGameplaySceneInstaller`.
- Main menu, pause, and death screen buttons still invoke their UnityEvent callbacks.
- Settings resolution dropdown still loads and applies resolution.
- Skill upgrade UI still creates and handles upgrade buttons.
- Crates spawn, collect, release grid occupancy, and trigger skill UI.
- Enemy movement and attack animation flow still work.
- Scene loading and pause/resume flow still work after `GameFlow` namespace changes.
- Damage, stun, knockback, projectile impacts, and death volumes still work after `StatusEffects` namespace changes.
- Damage number lifecycle actions still work after `ObjectLifecycle` namespace changes.
- Car movement, car VFX, player damage, player death, UI death flow, and scene reload/restart behavior still work after the `Player/Car` namespace and path changes.

## Deferred Decisions

- None for the listed cleanup phases.

Resolved during this implementation:

- `Common/Types/` and `Common/EventArgs/` now own truly shared generic value types and event args.
- Phase 4 folder renames were approved and completed.
- Namespaces followed Phase 4 physical folder moves.
- Phase 6 was approved and completed; car code now lives under `Assets/Scripts/Player/Car/`.
