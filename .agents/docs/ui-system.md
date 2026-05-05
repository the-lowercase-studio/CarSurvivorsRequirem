# UI System Documentation

## Purpose

The UI system presents runtime game state, menu actions, settings options, pause/death screens, and skill initialization or upgrade choices. It is mostly scene-wired Unity UI backed by small presenter components under `Assets/Scripts/UI/`.

The UI system is not responsible for owning gameplay state, scoring rules, settings storage, scene loading, audio modes, skill selection algorithms, or player lifecycle. Those behaviors live in their owning gameplay, settings, audio, score, and scene-loading systems; UI components consume them through inspector references, events, or Reflex-injected interfaces.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/UI/`
  - `Assets/Scripts/UI/Level/`
  - `Assets/Scripts/UI/Skills/`
  - `Assets/Scripts/UI/Death/`
  - `Assets/Scripts/UI/Settings/`
- Related runtime code:
  - `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`
  - `Assets/Scripts/ReflexDI/MainMenuInstaller.cs`
  - `Assets/Scripts/ReflexDI/ProjectInstaller.cs`
  - `Assets/Scripts/Player/PlayerDeathHandler.cs`
  - `Assets/Scripts/Settings/`
  - `Assets/Scripts/ScoreBoard/`
  - `Assets/Scripts/GameManipulators/`
- Related docs:
  - `.agents/docs/level-system.md`
  - `.agents/docs/health-system.md`
  - `.agents/docs/damage-numbers-system.md`
  - `.agents/docs/collectibles-system.md`
  - `.agents/docs/spawners-system.md`
  - `.agents/docs/technology-documentation.md`
- Related agents or instructions:
  - Root `AGENTS.md`
  - `.agents/docs/project-coding-standards.md`
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/di-integration/SKILL.md` when adding injected UI dependencies

## Architecture and Data Flow

- Core components:
  - `TimerPresenter` increments a scene timer once per second with `InvokeRepeating`, updates a TMP label, and exposes `TimerValue` through `ITimerPresenter`.
  - `PlayerLevelPresenter` reads `IPlayerManager.LevelController`, subscribes to exp and level-up events, animates the exp slider, updates the level label, and raises `OnExpSliderVisualEndValueReached` when a level-up visual reaches the transition point.
  - `SkillUpgradePresenter` reacts to collectible release events and `IPlayerLevelPresenter.OnExpSliderVisualEndValueReached`, queues new skills or upgrades, pauses `GameTime` while a choice panel is active, and resumes before advancing to the next queued UI item.
  - `SkillsVisualPresenter` maps `SkillInfoSO.Name` to scene visual objects by matching GameObject names, and can hide all registered visuals.
  - `PlayerDeathPresenter` saves the current timer score, displays final level and time alive, switches background audio to death mode, enables the death screen, and pauses `GameTime`.
  - `PausePresenter` listens to the Input System `Pause` action and toggles a pause visual plus `GameTime.Pause` or `GameTime.Resume`.
  - `MenuButtonsFunctionality` backs menu button actions for scene loading, current-scene retry, application exit, and mutually exclusive panel toggling.
  - `IOptionComponent<T>` standardizes settings UI components with `LoadComponent` and `PerformValueChange`.
  - `AudioVolumeOption`, `DamageNumbersOption`, `FullScreenOption`, `GraphicOption`, and `ResolutionOption` load current setting values into Unity UI controls and save/load settings when controls change.
  - `ButtonsAudioClipPlayer` is intended to play button click and hover sounds through an `AudioClipPlayer` component.
- Key interfaces:
  - `ITimerPresenter` is bound by `DefaultGameplaySceneInstaller` and consumed by `PlayerDeathPresenter`.
  - `IPlayerLevelPresenter` is bound by `DefaultGameplaySceneInstaller` and consumed by `SkillUpgradePresenter`.
  - `IPlayerDeathPresenter` is bound by `DefaultGameplaySceneInstaller` and consumed by `PlayerDeathHandler`.
  - `IOptionComponent<T>` is a local UI option contract, not currently bound in DI.
  - `ISetting<TSelf, TRepresentedBy>` implementations are bound in `MainMenuInstaller` and injected into settings option components.
- Runtime flow:
  - Gameplay scene DI registers the player manager, death presenter, level presenter, timer presenter, grid manager, enemy spawner, collectible spawner, and exp particle spawner.
  - The level presenter initializes from the player's current `LevelData`, subscribes to level events, and drives visual progression independently from immediate gameplay state changes.
  - Skill upgrade UI can be triggered by either collectible release or exp-slider level-up visual completion. The UI chooses between initializing an uninitialized skill and upgrading an existing upgradeable skill, then pauses gameplay while the player is choosing.
  - Player death is initiated by health reaching zero. `PlayerDeathHandler` hides player visuals, disables non-wheel colliders, plays death VFX, and only calls `IPlayerDeathPresenter.EnableDeathScreen` after death VFX finishes.
  - Settings UI option components load stored/default values when enabled, subscribe to Unity UI value-change events, save changed values through their injected setting, and immediately call `Load` to apply the setting.

## Rules and Invariants

- Critical behavior rules:
  - Keep UI presenters as consumers of gameplay state. Do not move skill, level, score, health, settings persistence, or scene-loading ownership into UI classes.
  - Keep Reflex bindings for gameplay UI contracts in `DefaultGameplaySceneInstaller` and settings contracts in `MainMenuInstaller`.
  - Preserve inspector references and serialized field names when changing presenter setup.
  - Preserve the death flow: player health reaches zero, VFX plays, death screen enables after VFX completion, score is saved from `ITimerPresenter.TimerValue`, background audio switches, then game time pauses.
  - Preserve skill-upgrade timing: level-up rewards are queued from the level slider visual completion event, not directly from `LevelController.OnLvlUp`.
  - Preserve settings option event subscription symmetry: add Unity UI listeners in `OnEnable` and remove them in `OnDisable`.
- Ordering or sequencing guarantees:
  - `PlayerLevelPresenter.OnExpSliderVisualEndValueReached` is raised after level text and slider max/value are transitioned for a level-up.
  - `SkillUpgradePresenter` resumes `GameTime` before deciding whether to show the next queued UI item, then pauses again if a new-skill or upgrade panel is shown.
  - `PlayerDeathPresenter.EnableDeathScreen` saves the score before comparing the timer against the best score text.
  - `PausePresenter` toggles both visual state and `GameTime` state in the same action path.
- Constraints contributors must preserve:
  - Do not use broad scene searches or singleton access for dependencies that are already available through Reflex.
  - Do not edit scene, prefab, asset, or meta files directly unless the user explicitly requests it and the text change is safe to review.
  - Do not change player-facing UI timing, pause behavior, settings semantics, or score saving without a gameplay/UX review.
  - Keep TMP labels, sliders, toggles, dropdowns, buttons, panels, and audio clip players scene-wired through serialized fields unless adding a DI-backed runtime dependency is clearly justified.

## Extension Points

- Safe extension areas:
  - Add a new settings option by implementing `IOptionComponent<T>`, injecting the appropriate `ISetting<TSelf, TRepresentedBy>`, binding the setting in `MainMenuInstaller`, loading without notifying the control, and subscribing/unsubscribing in `OnEnable`/`OnDisable`.
  - Add a new gameplay presenter by creating a narrow interface only if another runtime system must consume it, then bind the scene instance in `DefaultGameplaySceneInstaller`.
  - Add new skill choice visuals by extending the scene objects referenced by `SkillsVisualPresenter`, keeping names aligned with `SkillInfoSO.Name`.
  - Add button behaviors through `MenuButtonsFunctionality` only when they remain scene/menu operations; put gameplay state changes in the owning gameplay system instead.
- Required dependencies and contracts:
  - UI contracts consumed outside UI should be explicit interfaces and registered in the appropriate Reflex installer.
  - Settings options require an `ISetting<TSelf, TRepresentedBy>` implementation that can save, load, and return a stored/default value.
  - Death UI requires `IPlayerManager`, `IBackgroundAudioManager`, scoreboard services, and `ITimerPresenter`.
  - Skill upgrade UI requires `IPlayerManager`, `IPlayerLevelPresenter`, and the collectible spawner release event.
- Testing implications:
  - Compile after C# UI changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Play-mode validation is required for pause input, scene button wiring, settings controls, death VFX-to-screen timing, score text, audio mode changes, and skill upgrade panel timing.
  - Manual UI checks should include repeated enable/disable cycles for settings panels to catch duplicate listeners.

## Integration Notes

- Upstream dependencies:
  - Player state and level data come through `IPlayerManager`.
  - Level UI event timing depends on `ILevelController.OnExpChange` and `ILevelController.OnLvlUp`.
  - Skill reward triggers depend on `IOnRandomGridPosSpawner<CollectibleItemsSpawner>.OnSpawnedEntityReleased`.
  - Death UI activation depends on `PlayerDeathHandler` and the death VFX finished event.
  - Settings UI depends on setting classes under `Assets/Scripts/Settings/`.
  - Scene/menu buttons depend on `IGameSceneLoader`.
  - Pause and death panels manipulate `GameTime`.
- Downstream consumers:
  - `PlayerDeathHandler` consumes `IPlayerDeathPresenter`.
  - `PlayerDeathPresenter` consumes `ITimerPresenter` for score and time-alive display.
  - `SkillUpgradePresenter` consumes `IPlayerLevelPresenter` to delay level-up reward UI until the exp slider finishes the level-up transition.
  - Settings classes and storage are updated from the option components.
- Cross-system coupling risks:
  - Changing level slider animation or event timing can change when gameplay pauses for rewards.
  - Changing collectible release timing can change skill initialization or upgrade prompts.
  - Changing `GameTime` pause/resume behavior can affect pause, death, and skill-choice panels.
  - Renaming skill visual GameObjects or `SkillInfoSO.Name` values can break `SkillsVisualPresenter` lookup.
  - Changing timer behavior can affect scoreboard save values and death-screen text.

## Known Risks and Open Questions

- Known limitations:
  - `PlayerLevelPresenter` subscribes to level controller events in `Start` and does not currently unsubscribe; this is low risk for scene-lifetime presenters but should be reviewed before supporting presenter disable/re-enable or additive scene lifetimes.
  - `SkillUpgradePresenter` subscribes to collectible and level-presenter events in `Start` and does not currently unsubscribe.
  - `SkillUpgradePresenter` finds `SkillsVisualPresenter` through a tagged GameObject lookup instead of DI or a serialized reference.
  - `SkillsVisualPresenter` uses `First` by GameObject name, so a missing or renamed visual can throw instead of failing softly.
  - `ButtonsAudioClipPlayer` declares `RequireComponent(typeof(AudioClipPlayer))` but does not initialize its `_audioClipPlayer` field before playback.
  - `AudioVolumeOption` converts slider value with `Mathf.Log10`; a zero slider value would produce an invalid decibel value unless the slider minimum prevents zero in the scene.
  - `GraphicOption` assumes the stored/default quality string exists in its hard-coded quality-level dictionary.
- Open design questions:
  - Should skill visual lookup become serialized or DI-backed to remove the runtime tag/name dependency?
  - Should UI presenters add lifecycle-safe unsubscribe handling for scene reloads, disabled panels, or future additive scenes?
  - Should timer ownership move out of UI if wave UI, analytics, or non-UI scoring need the same clock?
  - Should settings option components share a base helper for load-without-notify and listener wiring, or is the current duplication acceptable for the small option set?
- Suggested follow-up tasks:
  - Fix `ButtonsAudioClipPlayer` initialization before relying on it for shared button audio.
  - Add unsubscribe handling to `PlayerLevelPresenter` and `SkillUpgradePresenter` if UI objects can be disabled without scene teardown.
  - Add a small validation checklist for scene/prefab UI references when UI prefabs or canvases are edited in the Unity Editor.
