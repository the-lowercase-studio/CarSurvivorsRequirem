# Game Flow System Documentation

## Purpose

The Game Flow system manages match lifecycle, scene transitions, time scale manipulation (pausing and resuming game time), and win/loss state progression.

It is responsible for:

- Managing scene navigation across the game lifecycle: Boot scene -> Main Menu scene -> Gameplay scene (RuinedBloodCity).
- Asynchronous scene loading and reloading via IGameSceneLoader.
- Global time scale control (Time.timeScale) through GameTime.Pause() and GameTime.Resume().
- Coordinating pause state triggered by pause menu input and death screens.
- Resuming time scale automatically when transitioning or reloading scenes.

It is not responsible for:

- UI layout formatting or button input logic (owned by UI presenters).
- Skill upgrade selection logic or EXP calculations (owned by Skills and Level systems).
- Reflex DI container configuration (owned by DI and Boot Flow system).

## Reading Map

- Primary code locations:
  - Assets/Scripts/GameFlow/GameScenesLoader.cs
  - Assets/Scripts/GameFlow/GameTime.cs
  - Assets/Scripts/ReflexDI/BootLoader.cs
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/UI/Pause/PausePresenter.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/Player/PlayerDeathHandler.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
- Related docs:
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - GameSceneLoader (implementing IGameSceneLoader): Project-level singleton registered in ProjectInstaller. Handles async scene loading (LoadNewSceneAsync, ReloadCurrentSceneAsync), tracks CurrentLoadedScene, and emits OnStartLoadingScene and OnSceneLoaded events.
  - GameScene: Serialized enum defining available scenes (MainMenu = 1, RuinedBloodCity = 2). Boot scene index is 0.
  - GameTime: Static utility class providing Pause() (sets Time.timeScale = 0f) and Resume() (sets Time.timeScale = 1f).
  - BootLoader: Attached to Boot scene. Registers boot-provided services with scene containers and triggers async loading of GameScene.MainMenu after one frame (WaitForEndOfFrame).
  - PausePresenter: Handles player pause toggle via InputSystem. Toggles UI active state and invokes GameTime.Pause() or GameTime.Resume().
  - PlayerDeathPresenter: Listens for player death via PlayerDeathHandler. Saves final score, enables death screen UI, updates audio, and calls GameTime.Pause().
  - SkillUpgradePresenter: Handles presentation and keyboard hotkey selection for skill upgrade requests. Shows skill/upgrade options directly without calling GameTime.Pause().
- Key interfaces:
  - IGameSceneLoader: Contract for scene loading, reloading, current scene state, and load lifecycle events.
  - IPlayerDeathPresenter: Contract exposing EnableDeathScreen() called by PlayerDeathHandler.
- Runtime flow:
  1. Boot sequence: Boot.unity loads, ProjectInstaller registers GameSceneLoader. BootLoader waits one frame and calls IGameSceneLoader.LoadNewSceneAsync(GameScene.MainMenu).
  2. Scene transition: GameSceneLoader emits OnStartLoadingScene, executes SceneManager.LoadSceneAsync, and on completion calls GameTime.Resume(), updates CurrentLoadedScene, and emits OnSceneLoaded.
  3. Pause flow: Player presses pause key -> PausePresenter toggles UI visibility and alternates between GameTime.Pause() and GameTime.Resume().
  4. Skill reward flow: Level up or skill crate collection queues a request in SkillUpgradeFlow -> SkillUpgradePresenter displays upgrade modal without pausing time scale.
  5. Death flow: Player HP hits 0 -> PlayerDeathHandler calls IPlayerDeathPresenter.EnableDeathScreen() -> score saved -> death UI active -> GameTime.Pause().
  6. Reload flow: Player selects restart in pause/death UI -> IGameSceneLoader.ReloadCurrentSceneAsync() reloads current scene -> GameSceneLoader completion callback calls GameTime.Resume().

## Rules and Invariants

- Critical behavior rules:
  - GameSceneLoader.LoadNewSceneAsync MUST invoke GameTime.Resume() in its completion callback to guarantee new scenes do not start paused.
  - GameScene enum integer values must match Unity Build Settings scene indices.
  - GameTime.Pause() sets Time.timeScale = 0f, halting scaled deltaTime, physics steps, and scaled particle/animation updates.
  - Logic or animations intended to run during pause must use Time.unscaledDeltaTime or unscaled tween updates (SetUpdate(true) in DOTween).
- Ordering or sequencing guarantees:
  - OnStartLoadingScene is emitted before SceneManager.LoadSceneAsync completes.
  - OnSceneLoaded is emitted after GameTime.Resume() executes and CurrentLoadedScene updates.
- Constraints contributors must preserve:
  - Do not call Time.timeScale directly in individual UI scripts; use GameTime.Pause() and GameTime.Resume().
  - Inject IGameSceneLoader for scene loading rather than calling SceneManager directly in domain code.

## Extension Points

- Safe extension areas:
  - Add new scene options by adding entries to GameScene enum and Unity Build Settings.
  - Subscribe to IGameSceneLoader.OnStartLoadingScene or OnSceneLoaded for cross-scene state cleanup or audio track switches.
  - Trigger game pause via PausePresenter or dedicated flow components calling GameTime.Pause() / GameTime.Resume().
- Required dependencies and contracts:
  - Scene navigation requests must use IGameSceneLoader.
- Testing implications:
  - C# compile check: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
  - Manual Unity Play Mode testing: test Boot -> Main Menu -> Gameplay transition, pause toggle, death screen freeze, and restart scene reload to confirm time scale returns to 1f.

## Integration Notes

- Upstream dependencies:
  - Unity SceneManager and Time APIs.
  - Reflex DI ProjectInstaller for IGameSceneLoader singleton registration.
  - Unity Input System for pause input actions.
- Downstream consumers:
  - BackgroundAudioManager (subscribes to IGameSceneLoader events for track transitions).
  - UI Presenters (PausePresenter, PlayerDeathPresenter, SkillUpgradePresenter).
  - ScoreBoard system (saves score on death).
- Cross-system coupling risks:
  - Setting Time.timeScale = 0f stops all scaled-time systems (enemy movement, flow field updates, projectile motion). Components executing during pause must explicitly use unscaled updates.

## Known Risks and Open Questions

- Known limitations:
  - GameTime is static without pause reason tracking, so a single Resume() call unpauses time regardless of multiple pause sources.
  - Skill upgrade modals currently display over active gameplay without forcing Time.timeScale = 0f.
- Suggested follow-up tasks:
  - Consider introducing an injected pause manager service if stack-based pause control becomes necessary.

