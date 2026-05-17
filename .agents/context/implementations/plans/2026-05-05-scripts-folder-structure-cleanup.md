# Scripts Folder Structure Cleanup Plan

## Purpose

Use this plan to incrementally align `Assets/Scripts/` with project folder-structure best practices while preserving Unity serialized references, namespaces, Reflex bindings, and gameplay behavior.

This is a structure cleanup plan only. It should not introduce gameplay, balance, UI, audio, VFX, or scene-behavior changes.

## Source Documents

- `AGENTS.md`
- `.agents/README.md`
- `.agents/context/project-coding-standards.md`
- `.agents/context/project-scripts-folder-map.md`
- `.agents/context/ai-game-dev-best-practices.md`
- `.agents/context/technology-documentation.md`

## Current Problem

`Assets/Scripts/` is mostly domain-oriented, but several folders act as type buckets or catch-all buckets:

- `Helpers/`
- `Utils/`
- `Providers/`
- `Activators/`
- `Initializers/`
- `EventHandlers/`
- `CustomTypes/`
- `CustomEventArgs/`
- single-interface folders such as `Movement/`, `Collectibles/`, and `AnimationPlayers/`

These folders make ownership less clear and encourage future scripts to be placed by implementation type instead of gameplay/system ownership.

## Target Direction

Keep the current domain-oriented structure, but gradually reduce generic buckets by moving scripts closer to their owning system.

Preferred long-term shape:

```text
Assets/Scripts/
  Audio/
  Common/
    Extensions/
    Utilities/
  Enemies/
  GameFlow/
  GridSystem/
  FlowFieldSystem/
  HealthSystem/
  LevelSystem/
  Player/
    Car/
    Damage/
    Death/
  Projectiles/
  ReflexDI/
  Settings/
  Skills/
  Spawners/
  UI/
  Waves/
  Editor/
```

Do not create this final structure in one large pass. Use it as a direction for small, reviewable moves.

## Invariants

1. Preserve Unity `.meta` GUIDs for moved scripts.
2. Do not hand-edit `.prefab`, `.unity`, `.asset`, or `.meta` files unless explicitly approved.
3. Preserve serialized field names and public type names unless the user approves a breaking change.
4. Preserve namespaces or update all C# references in the same change.
5. Preserve Reflex bindings in `Assets/Scripts/ReflexDI/`.
6. Do not introduce singleton access, static mutable services, or scene-wide lookup shortcuts.
7. Keep each change behavior-neutral and compile-validated.

## Phase 1: Document and Enforce Placement Rules

Status: ready.

Actions:

1. Treat `.agents/context/project-scripts-folder-map.md` as the placement source of truth.
2. When adding new scripts, prefer the owning domain folder over `Helpers/`, `Utils/`, `Providers/`, or similar generic folders.
3. Add new `Constants/` folders only under the owning system root when constants are introduced.

Validation:

- Documentation review only.
- No compile needed unless source files are moved or namespaces change.

## Phase 2: Move Obvious Domain-Owned Helpers

Status: completed for listed candidates.

Move candidates:

| Current Path | Proposed Path | Reason |
| --- | --- | --- |
| `Assets/Scripts/Helpers/ScreenSerializableResolutionHelper.cs` | `Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs` | Done. Resolution-specific helper belongs with resolution settings. |
| `Assets/Scripts/Helpers/EntityManipulationHelper.cs` | `Assets/Scripts/StatusEffects/EntityManipulationHelper.cs` | Done. It only adapts colliders to `IDamageable`, `IKnockable`, and `IStunnable`, so ownership is status effect capability handling. |
| `Assets/Scripts/Utils/EaseUtils.cs` | `Assets/Scripts/UI/Level/EaseUtils.cs` | Done. Current usage is only level UI exp slider easing. |
| `Assets/Scripts/Utils/RandomUtility.cs` | keep in `Assets/Scripts/Utils/RandomUtility.cs` | Used by unrelated damage-number and exp-particle placement systems. |
| `Assets/Scripts/Utils/TimeConversionUtility.cs` | keep in `Assets/Scripts/Utils/TimeConversionUtility.cs` | Used by scoreboard and death UI time displays. |
| `Assets/Scripts/Utils/DeepCopyUtility.cs` | keep in `Assets/Scripts/Utils/DeepCopyUtility.cs` | Used by multiple skill ScriptableObject configs for runtime stat copies. |

Implementation notes:

1. Search references with `rg "TypeName" Assets/Scripts -g "*.cs"` before each move.
2. Move scripts through Unity Editor when possible to preserve GUIDs.
3. If moved outside Unity Editor, verify `.meta` files move with the script.
4. Update namespaces only when the project is ready for namespace churn; path-only moves are lower risk.

Validation:

- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Open Unity Editor and check for missing script references if any MonoBehaviour scripts were moved.

## Phase 3: Collapse Single-Interface Buckets Where Ownership Is Clear

Status: completed for clear-ownership candidates; shared-contract decisions remain deferred.

Move candidates:

| Current Path | Proposed Path | Reason |
| --- | --- | --- |
| `Assets/Scripts/Movement/IMovementController.cs` | `Assets/Scripts/Enemies/IMovementController.cs` | Done. Current implementation and consumers are enemy-owned. |
| `Assets/Scripts/Collectibles/ICollectible.cs` | `Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs` | Done. Current implementation and spawner are skill-crate-owned. |
| `Assets/Scripts/AnimationPlayers/IAttackAnimationPlayer.cs` | `Assets/Scripts/Enemies/IAttackAnimationPlayer.cs` | Done. Current implementation and consumers are enemy-owned. |
| `Assets/Scripts/Activators/ItemsWithScriptableConfigsActivator.cs` | `Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs` | Done. Current consumers are player skills that activate skill-owned scene items from scriptable configs. |
| `Assets/Scripts/Providers/IGameObjectProvider.cs` | keep in `Assets/Scripts/Providers/IGameObjectProvider.cs` | Shared by player, exp particles, and collectibles; defer shared contracts folder decision. |
| `Assets/Scripts/Initializers/IInitializable*.cs` | keep in `Assets/Scripts/Initializers/` | Shared by skills, projectiles, damage numbers, skill activation, and enemy spawn redistribution; defer shared contracts folder decision. |

Decision rule:

- If one implementation owns the interface, colocate the interface above the implementation.
- If multiple unrelated systems use the interface, move it to a clearly named shared contracts location.
- Do not create a shared contracts folder until there are enough truly shared contracts to justify it.

Validation:

- Compile after each small batch.
- Inspect Reflex installers if any moved interface is bound or injected.

## Phase 4: Clarify Ambiguous Domain Names

Status: completed for approved renames.

Potential renames:

| Current Folder | Candidate Name | Notes |
| --- | --- | --- |
| `GameManipulators/` | `GameFlow/` | Done. Contains `GameTime` and `GameScenesLoader`, both broad runtime flow services. |
| `StatusAffectables/` | `StatusEffects/` | Done. Contains damage, stun, and knockback effect capability contracts/controllers. |
| `ObjectLifeCycle/` | `ObjectLifecycle/` | Done. Spelling/style cleanup with namespace/path churn. |
| `CustomTypes/` | `Common/Types/` | Done. `ValueRange` is shared across stats, enemies, damage numbers, and exp particles. |
| `CustomEventArgs/` | `Common/EventArgs/` | Done. `ValueEventArgs` is shared by scene loading and level progression UI. |

These renames were performed after explicit user approval. Namespaces were updated with the physical folder moves.

## Phase 5: Improve UI Subfolders

Status: completed for listed candidates.

Potential moves:

| Current File | Proposed Area | Reason |
| --- | --- | --- |
| `Assets/Scripts/UI/PausePresenter.cs` | `Assets/Scripts/UI/Pause/PausePresenter.cs` | Done. Pause screen ownership. Namespace preserved for Unity serialized references. |
| `Assets/Scripts/UI/TimerPresenter.cs` | `Assets/Scripts/UI/HUD/TimerPresenter.cs` | Done. Runtime HUD display. Namespace preserved for Unity serialized references. |
| `Assets/Scripts/UI/MenuButtonsFunctionality.cs` | `Assets/Scripts/UI/Common/MenuButtonsFunctionality.cs` | Done. Used by main menu, pause, and death UI prefabs. Namespace preserved for UnityEvent serialized references. |
| `Assets/Scripts/UI/ButtonsAudioClipPlayer.cs` | `Assets/Scripts/UI/Common/ButtonsAudioClipPlayer.cs` | Done. Shared UI audio component. Namespace preserved for Unity serialized references. |
| `Assets/Scripts/UI/ClickableButtonData.cs` | `Assets/Scripts/UI/Skills/ClickableButtonData.cs` | Done. Current owner is skill upgrade UI button construction. Namespace preserved for low-risk path-only move. |
| `Assets/Scripts/EventHandlers/PointerEnterHandler.cs` | `Assets/Scripts/UI/Skills/PointerEnterHandler.cs` | Done. Current owner is skill upgrade button hover behavior. Namespace updated because the component is created dynamically, not serialized. |

Validation:

- Compile after namespace/reference updates.
- Manually check menus, pause, death, settings, level, skills, and HUD in Unity Editor.

## Phase 6: Optional Player and Car Ownership Cleanup

Status: completed after user confirmation that car behavior should be player-owned.

Current shape:

```text
Assets/Scripts/Player/
  Car/
```

`CarController`, `CarVfxEffectsController`, and `ICarController` now live under `Assets/Scripts/Player/Car/`. The namespace follows the physical folder move as `Assets.Scripts.Player.Car`.

Validation:

- Compile.
- Manual play-mode check for car movement, car VFX, player damage, player death, UI death flow, and scene reload/restart behavior.

## Recommended Execution Order

1. Phase 2: move the most obvious domain-owned helpers one at a time.
2. Phase 5: add UI subfolders when touching related UI files.
3. Phase 3: collapse interface buckets only when working on the implementations that use them.
4. Phase 4: completed after explicit approval.
5. Phase 6: completed after confirming ownership vocabulary.

## Pre-Move Checklist

Before moving any script:

1. Search references with `rg`.
2. Check whether the type is a `MonoBehaviour` attached to scenes or prefabs.
3. Check whether the type is serialized, injected, or used by Reflex installers.
4. Decide whether to preserve the namespace for a path-only move or update namespaces with references.
5. Move the `.cs` and `.meta` together, preferably through Unity Editor.

## Post-Move Checklist

After each small batch:

1. Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
2. Check Unity Console after opening the project.
3. Check moved MonoBehaviours for missing script references.
4. Test any affected scene, prefab, UI screen, or gameplay flow manually.
5. Update `.agents/context/project-scripts-folder-map.md` if the folder map changes.

## Open Questions

1. Resolved for Phase 4: `Assets/Scripts/Common/` now owns truly shared event args and generic value types.
2. Resolved for Phase 4: namespaces followed the approved physical folder moves.
3. Resolved for Phase 6: `Car/` is treated as player-owned and now lives under `Assets/Scripts/Player/Car/`.
