# Navigation and Skill Upgrade Structure Plan

## Purpose

Incrementally update `Assets/Scripts/` structure based on the approved direction:

1. Extract skill upgrade flow orchestration out of UI.
2. Create a `Navigation/` boundary for grid and flow-field code.
3. Align namespaces with physical folder structure where they currently remain broader than the path.
4. Leave assembly definition files deferred.

This plan is structure-focused. Preserve current gameplay behavior, balance, serialized field names, prefab references, scene references, UI timing, audio, VFX, and Reflex bindings unless a later implementation step explicitly calls out a user-approved change.

## Source Documents

- `AGENTS.md`
- `.agents/README.md`
- `.agents/context/project-coding-standards.md`
- `.agents/context/project-scripts-folder-map.md`
- `.agents/context/ai-game-dev-best-practices.md`
- `.agents/context/technology-documentation.md`

## Current Pressure Points

### Skill Upgrade UI Owns Gameplay Flow

`Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs` currently mixes:

- UI section visibility and upgrade button creation.
- Skill initialization and upgrade queue management.
- Random skill selection.
- Game pause/resume calls.
- Reactions to level-up visuals and collectible release events.
- Runtime lookup of `SkillsVisualPresenter`.

The UI folder should own presentation. Skill upgrade selection and queue flow should move closer to `Skills/`.

### Grid and Flow Field Form One Navigation Boundary

`Assets/Scripts/GridSystem/` and `Assets/Scripts/FlowFieldSystem/` are structurally separate but conceptually coupled:

- `GridManager` creates and updates `FlowField`.
- `FlowField` and flow-field movement depend on grid cells, directions, and coordinate conversion.
- Enemies and exp particles consume navigation behavior through flow-field movement.

Use a top-level `Assets/Scripts/Navigation/` boundary with subfolders for grid and flow-field responsibilities.

### Some Namespaces No Longer Match Paths

The previous UI folder cleanup preserved broader namespaces for low-risk moves. The approved direction is to align these namespaces with the actual folder structure.

Current mismatches:

| File | Current Namespace | Target Namespace |
| --- | --- | --- |
| `Assets/Scripts/UI/Common/ButtonsAudioClipPlayer.cs` | `Assets.Scripts.UI` | `Assets.Scripts.UI.Common` |
| `Assets/Scripts/UI/Common/MenuButtonsFunctionality.cs` | `Assets.Scripts.UI` | `Assets.Scripts.UI.Common` |
| `Assets/Scripts/UI/HUD/TimerPresenter.cs` | `Assets.Scripts.UI` | `Assets.Scripts.UI.HUD` |
| `Assets/Scripts/UI/Pause/PausePresenter.cs` | `Assets.Scripts.UI` | `Assets.Scripts.UI.Pause` |
| `Assets/Scripts/UI/Skills/ClickableButtonData.cs` | `Assets.Scripts.UI` | `Assets.Scripts.UI.Skills` |

## Invariants

1. Preserve Unity `.meta` GUIDs for moved scripts.
2. Move `.cs` and `.meta` files together, preferably through Unity Editor when possible.
3. Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files by hand unless separately approved.
4. Preserve serialized field names and public type names.
5. Preserve existing gameplay event ordering for level-up, skill upgrade, crate collection, pause/resume, and enemy movement.
6. Preserve Reflex DI access and installer bindings.
7. Do not introduce singleton access, static mutable service state, or scene-wide lookup shortcuts.
8. Compile after each phase with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.

## Phase 1: Extract Skill Upgrade Flow From UI

Status: completed.

Target shape:

```text
Assets/Scripts/Skills/
  UpgradeFlow/
    SkillUpgradeFlow.cs
    ISkillUpgradeFlow.cs
    SkillUpgradeRequest.cs
    SkillUpgradeOption.cs

Assets/Scripts/UI/Skills/
  SkillUpgradePresenter.cs
  SkillsVisualPresenter.cs
  PointerEnterHandler.cs
  ClickableButtonData.cs
```

Implementation direction:

1. Add a skill-owned service under `Skills/UpgradeFlow/` that owns:
   - queued new skills;
   - queued upgradeable skills;
   - random initialization/upgrade selection;
   - calls to `SkillsRegistry.InitializeSkill`;
   - determining the next UI request.
2. Keep `SkillUpgradePresenter` responsible for:
   - rendering new skill and upgrade sections;
   - creating buttons;
   - playing UI audio;
   - showing skill visuals;
   - pausing/resuming only if the current implementation cannot move that responsibility safely into the flow service.
3. Prefer an explicit interface for the flow service if it is injected or consumed by UI.
4. Register the new service in the relevant Reflex installer if it requires DI.
5. Replace runtime `GameObject.FindGameObjectWithTag` lookup for `SkillsVisualPresenter` with an inspector reference or DI-backed dependency. Preserve serialized compatibility if the field is introduced on an existing scene component.

Risk notes:

- This touches level-up to skill-upgrade event flow, so validate event ordering carefully.
- Pause/resume ownership must remain behavior-identical.
- Do not change skill choice probability, upgrade option count, or button text.

Validation:

1. Compile.
2. In Unity, trigger an exp level-up and confirm the new skill section appears as before.
3. Confirm skill upgrade buttons still show up to `3` options.
4. Confirm upgrade clicks apply the stat change once.
5. Confirm game pause/resume behavior matches the current flow.
6. Confirm crate release still triggers skill initialization or upgrade presentation.

## Phase 2: Create Navigation Boundary

Status: completed.

Target shape:

```text
Assets/Scripts/Navigation/
  GridSystem/
  FlowFieldSystem/
```

Move candidates:

| Current Path | Target Path |
| --- | --- |
| `Assets/Scripts/GridSystem/*` | `Assets/Scripts/Navigation/GridSystem/*` |
| `Assets/Scripts/FlowFieldSystem/*` | `Assets/Scripts/Navigation/FlowFieldSystem/*` |

Namespace targets:

| Current Namespace | Target Namespace |
| --- | --- |
| `Assets.Scripts.GridSystem` | `Assets.Scripts.Navigation.GridSystem` |
| `Assets.Scripts.FlowFieldSystem` | `Assets.Scripts.Navigation.FlowFieldSystem` |

Implementation direction:

1. Move grid and flow-field scripts as one batch to avoid temporary broken references.
2. Update all `using Assets.Scripts.GridSystem` references.
3. Update all `using Assets.Scripts.FlowFieldSystem` references.
4. Update fully qualified `GridSystem.Grid` references in flow-field code.
5. Update Reflex installer imports and serialized installer field types if namespaces change.
6. Update agent docs that reference old paths or namespaces.

Likely affected domains:

- `Enemies`
- `LevelSystem/Exp`
- `Spawners/GridSpace`
- `ReflexDI`
- `Editor/GUI`
- `Skills/ObjectsImpactingSkills/Crate`

Risk notes:

- This is a broad namespace and path move. It should be behavior-neutral but high-churn.
- Moving MonoBehaviours can affect Unity script references if `.meta` GUIDs are not preserved.
- Editor scripts referencing `GridManager` must compile after namespace updates.

Validation:

1. Compile.
2. Open Unity and confirm no missing scripts on grid, flow-field, enemy, exp particle, and spawner objects.
3. Check enemy movement toward the player.
4. Check exp particle movement/collection.
5. Check grid debug and flow-field debug if debug options are available.
6. Check enemy and collectible spawning on grid positions.

## Phase 3: Align UI Namespaces With Paths

Status: completed.

Update these namespaces and all C# references:

| File | Target Namespace |
| --- | --- |
| `Assets/Scripts/UI/Common/ButtonsAudioClipPlayer.cs` | `Assets.Scripts.UI.Common` |
| `Assets/Scripts/UI/Common/MenuButtonsFunctionality.cs` | `Assets.Scripts.UI.Common` |
| `Assets/Scripts/UI/HUD/TimerPresenter.cs` | `Assets.Scripts.UI.HUD` |
| `Assets/Scripts/UI/Pause/PausePresenter.cs` | `Assets.Scripts.UI.Pause` |
| `Assets/Scripts/UI/Skills/ClickableButtonData.cs` | `Assets.Scripts.UI.Skills` |

Implementation direction:

1. Change namespaces only; preserve type names.
2. Update `using` directives in all affected files, especially Reflex installers and UI presenters.
3. Do not move files in this phase.

Risk notes:

- Unity serialized references are type-name and GUID sensitive; namespace changes are usually safe when script GUIDs stay intact, but check scene and prefab references in Unity.
- UnityEvent method bindings on `MenuButtonsFunctionality` must be manually checked after namespace change.

Validation:

1. Compile.
2. Open Main Menu, Pause, Death, HUD, and Skill Upgrade UI.
3. Confirm menu buttons, pause buttons, retry/exit, HUD timer, hover/click audio, and skill upgrade buttons still work.

## Phase 4: Documentation Updates

Status: completed.

Update after source changes are implemented:

- `.agents/context/project-scripts-folder-map.md`
- Related system docs that mention `GridSystem`, `FlowFieldSystem`, `UI/Common`, `UI/HUD`, `UI/Pause`, or `UI/Skills`
- The implementation summary for this plan

Documentation requirements:

1. Mark `GridSystem/` and `FlowFieldSystem/` as moved under `Navigation/`.
2. Add placement guidance for `Navigation/GridSystem/` and `Navigation/FlowFieldSystem/`.
3. Preserve guidance that generic spawning contracts remain under `Spawners/`.
4. Preserve guidance that enemy-specific movement behavior remains under `Enemies/`.
5. Document the new skill upgrade flow ownership after implementation.

Validation:

- Review paths and namespaces in docs against actual files.

## Deferred: Assembly Definitions

Status: deferred by user decision.

Do not add `.asmdef` files in this implementation. Revisit only after:

1. Navigation boundary has stabilized.
2. Skill upgrade flow extraction is complete.
3. Namespaces match folder structure.
4. Compile and Unity manual checks are clean.

## Recommended Execution Order

1. Phase 3 first if a low-risk namespace-only pass is desired.
2. Phase 1 next to reduce UI/gameplay responsibility mixing.
3. Phase 2 after Phase 1, because it is the broadest path and namespace churn.
4. Phase 4 after source changes are complete.

Alternative:

- Do Phase 2 before Phase 1 if the priority is establishing the `Navigation/` boundary immediately. This increases merge and review noise but is still behavior-neutral if `.meta` files are preserved.

## Pre-Implementation Checklist

1. Check `git status` and protect unrelated user changes.
2. Search references with `rg` before each move or namespace update.
3. Preserve `.meta` files for moved scripts.
4. Confirm affected MonoBehaviours are not missing after Unity reload.
5. Keep each phase as a separate, reviewable change set when possible.

## Post-Implementation Checklist

1. Run:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

2. Open Unity and check Console for missing script or namespace errors.
3. Run manual play checks for:
   - enemy movement;
   - grid and flow-field behavior;
   - exp particle movement and collection;
   - skill initialization and upgrade presentation;
   - crate-triggered skill flow;
   - pause/resume behavior;
   - menu, pause, death, HUD, settings, and skill UI interactions.
4. Create an implementation summary under `.agents/context/implementations/summaries/`.
