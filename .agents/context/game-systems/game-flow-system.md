# Game Flow System Documentation

## Purpose

The Game Flow system manages match lifecycle, multi-tier roguelite run progression, build snapshot carryover, per-tier visual theme swapping, asynchronous scene transitions, global time scale and pause/resume management, and win/loss state progression.

It is responsible for:

- Managing scene navigation across the game lifecycle: Boot scene -> Main Menu scene -> Gameplay scene (RuinedBloodCity).
- Asynchronous scene loading and reloading with double-load protection via IGameSceneLoader.
- Global time scale control (Time.timeScale) and pause/resume state tracking through IGameTimeService / GameTimeService.
- Multi-tier run session state, tier configuration lookup, build snapshot capture/restoration (level, EXP, timer, initialized skills, and upgraded stat values), dev-build god-mode state, and session resets via IRunSessionService / RunSessionService and GameLoopProgressionConfigSO.
- Applying active tier visual themes (post-processing VolumeProfile, RenderSettings.skybox, directional light overrides, and shared water materials) on scene startup via ITierVisualThemeController / TierVisualThemeController.
- Coordinating pause state triggered by pause menu input, developer console overlay, death screens, and end-of-run completion screens.
- Resetting time scale to 1x and unpaused state automatically when transitioning or reloading scenes.

It is not responsible for:

- UI layout formatting or button input logic (owned by UI presenters).
- Skill upgrade selection algorithms or EXP curve calculations (owned by Skills and Level systems).
- Reflex DI container wiring beyond service implementations (owned by DI and Boot Flow system).

## Reading Map

- Primary code locations:
  - Assets/Scripts/GameFlow/GameSceneLoader.cs
  - Assets/Scripts/GameFlow/GameTimeService.cs
  - Assets/Scripts/GameFlow/RunSessionService.cs
  - Assets/Scripts/GameFlow/TierVisualThemeController.cs
  - Assets/Scripts/GameFlow/Config/GameLoopProgressionConfigSO.cs
  - Assets/Scripts/GameFlow/Constants/GameTimeConstants.cs
  - Assets/Scripts/GameFlow/Constants/ProgressionConstants.cs
- Designer-authored data:
  - Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset
  - Assets/Settings/Tier2VolumeProfile.asset
  - Assets/Materials/Skybox/Tier2Skybox.mat
  - Assets/Materials/Environment/Water/WaterLarge_Tier2.mat
  - Assets/Materials/Environment/Water/WaterSmall_Tier2.mat
- Related runtime integrations:
  - Assets/Scripts/ReflexDI/BootLoader.cs
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/UI/Loading/LoadingOverlayPresenter.cs
  - Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs
  - Assets/Scripts/UI/Common/MenuButtonsFunctionality.cs
  - Assets/Scripts/UI/Pause/PausePresenter.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs
  - Assets/Scripts/Interactables/NextStagePortal.cs
  - Assets/Scripts/Interactables/GolemStatue.cs
  - Assets/Scripts/Player/PlayerDeathHandler.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Skills/SkillsRegistry.cs
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/UI/HUD/TimerPresenter.cs
- Related docs:
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/interactables-system.md
  - .agents/context/game-systems/scoreboard-system.md
  - .agents/context/game-systems/dev-console-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - GameSceneLoader (implementing IGameSceneLoader): Project-level singleton registered in ProjectInstaller. Injects IGameTimeService via constructor. Handles async scene loading (LoadNewSceneAsync, ReloadCurrentSceneAsync), tracks IsLoading and CurrentLoadedScene, ignores duplicate scene load requests while a load is active (returning the active AsyncOperation), resets IGameTimeService on load completion, and emits OnStartLoadingScene and OnSceneLoaded events.
  - GameScene: Serialized enum defining available scenes (MainMenu = 1, RuinedBloodCity = 2). Boot scene index is 0.
  - GameTimeService (implementing IGameTimeService): Project-level singleton registered in ProjectInstaller (replacing the removed static GameTime utility). Tracks TargetTimeScale (default 1f, bounded within [0.05f, 100f]) and IsPaused. Pause() sets IsPaused = true and applies Time.timeScale = 0f while preserving TargetTimeScale; Resume() sets IsPaused = false and applies TargetTimeScale; SetTimeScale(float) validates finite bounds and applies immediately only when !IsPaused; Reset() restores TargetTimeScale = 1f, IsPaused = false, and Time.timeScale = 1f. Provides an injectable Action<float> constructor overload for deterministic unit testing.
  - GameLoopProgressionConfigSO: ScriptableObject defining ordered LoopTierConfig entries (_tiers), _portalVortexDuration (default 1.2s), and _portalVortexSpins (default 2.0), with fail-fast ValidateConfiguration(), GetTier(int), and TryGetTier(int, out LoopTierConfig). Each LoopTierConfig groups TierId, DisplayName, StatueProgressFormat, StatueChallengePrompt, optional OverrideScene/SceneOverride, TierEnemySpawningConfig, TierBossConfig, and TierVisualThemeConfig.
  - RunSessionService (implementing IRunSessionService): Project-level singleton registered in ProjectInstaller. Tracks CurrentTierIndex, TotalTiersCount, IsFinalTier, ActiveTierConfig, ProgressionConfig, HasPendingRestorationSnapshot, AccumulatedRunSeconds, and dev-build-gated IsGodModeEnabled. Captures in-memory RunSessionSnapshot data (Level, Exp, ElapsedSeconds, and SavedSkillSnapshot / SavedStatValue lists) via CaptureSnapshotAndAdvanceTier and CaptureSnapshotAndSetTier, records final run seconds via CaptureFinalRunTimer, exposes TryGetRestoredLevel and TryGetRestoredSkills, and resets run state via ResetSession() and ResetSessionToTier(int).
  - TierVisualThemeController (implementing ITierVisualThemeController): Scene MonoBehaviour bound in DefaultGameplaySceneInstaller. In Start(), reads IRunSessionService.ActiveTierConfig.VisualTheme and swaps the scene post-processing Volume.sharedProfile, RenderSettings.skybox (with DynamicGI.UpdateEnvironment()), directional light color/intensity/rotation (failing fast if _directionalLight is unassigned when OverrideDirectionalLight is true), and shared materials on _waterLargeRenderers and _waterSmallRenderers without mutating material assets on disk.
  - LoadingOverlayPresenter (implementing ILoadingOverlayPresenter): Persistent Boot-scene presenter bound into every scene container by BootLoader. Displays a high-priority Canvas (sortingOrder = 1000) with animated ellipsis dots ("Loading." -> "Loading.." -> "Loading..." or "Loading Tier {N}...") on OnStartLoadingScene and fades out one frame after OnSceneLoaded using unscaled DOTween.
  - DemoThankYouPresenter (implementing IDemoThankYouPresenter): Scene UI presenter bound in DefaultGameplaySceneInstaller. Opened by NextStagePortal when completing the final tier; saves a survived ScoreBoardRecord, displays final level, completion time, and SkillsStatsPresenter breakdown, acquires InputContextScopeKind.Death, and calls IGameTimeService.Pause().
  - MenuButtonsFunctionality: Scene button helper injecting IGameSceneLoader and IRunSessionService. Calls IRunSessionService.ResetSession() before OnTryAgainClicked() and OnSceneLoadClicked(string) transitions so retries and menu navigations always reset tier progression, snapshots, timer, and god mode.
- Key interfaces:
  - IGameSceneLoader: Contract for scene loading, reloading, IsLoading guard, current scene state, and load lifecycle events. Colocated in Assets/Scripts/GameFlow/GameSceneLoader.cs.
  - IGameTimeService: Contract for TargetTimeScale, IsPaused, Pause(), Resume(), SetTimeScale(float), and Reset(). Colocated in Assets/Scripts/GameFlow/GameTimeService.cs.
  - IRunSessionService: Contract for multi-tier session progression, build snapshot capture/restore, run timer accumulation, dev-build god mode, and session resets. Colocated in Assets/Scripts/GameFlow/RunSessionService.cs.
  - ITierVisualThemeController: Contract exposing ApplyActiveTierTheme(). Colocated in Assets/Scripts/GameFlow/TierVisualThemeController.cs.
  - ILoadingOverlayPresenter: Contract exposing IsVisible and ShowImmediate(string). Colocated in Assets/Scripts/UI/Loading/LoadingOverlayPresenter.cs.
  - IDemoThankYouPresenter: Contract exposing EnableThankYouScreen(). Colocated in Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs.
  - IPlayerDeathPresenter: Contract exposing EnableDeathScreen() called by PlayerDeathHandler. Colocated in Assets/Scripts/UI/Death/PlayerDeathPresenter.cs.
- Centralized constants:
  - GameTimeConstants (Assets/Scripts/GameFlow/Constants/GameTimeConstants.cs):
    - DEFAULT_TIME_SCALE = 1f
    - PAUSED_TIME_SCALE = 0f
    - MIN_TIME_SCALE = 0.05f
    - MAX_TIME_SCALE = 100f
  - ProgressionConstants (Assets/Scripts/GameFlow/Constants/ProgressionConstants.cs):
    - DEFAULT_PORTAL_VORTEX_DURATION = 1.2f
    - DEFAULT_PORTAL_VORTEX_SPINS = 2.0f
    - DEFAULT_STATUE_PROGRESS_FORMAT = "Towers destroyed ({0}/{1})"
    - DEFAULT_STATUE_CHALLENGE_PROMPT = "Challenge first Titan"
    - DEFAULT_MAX_ENEMIES_IN_WAVE_CAP = 45
    - LOADING_OVERLAY_SORTING_ORDER = 1000
    - LOADING_FADE_OUT_DURATION = 0.35f
    - LOADING_DOTS_INTERVAL = 0.4f
    - DEFAULT_LOADING_TIER_FORMAT = "Loading Tier {0}"
    - DEFAULT_LOADING_TEXT = "Loading"
- Runtime flow:
  1. Boot sequence: Boot.unity loads, ProjectInstaller validates GameLoopProgressionConfigSO and registers IGameTimeService, IGameSceneLoader, GameLoopProgressionConfigSO, and IRunSessionService. BootLoader registers ILoadingOverlayPresenter into scene containers, waits one frame, and calls IGameSceneLoader.LoadNewSceneAsync(GameScene.MainMenu).
  2. Scene transition: GameSceneLoader checks IsLoading, starts SceneManager.LoadSceneAsync, emits OnStartLoadingScene (causing LoadingOverlayPresenter to cover the screen immediately), and on completion calls IGameTimeService.Reset(), updates CurrentLoadedScene, and emits OnSceneLoaded (causing LoadingOverlayPresenter to yield one frame and fade out).
  3. Tier startup and state restoration: When RuinedBloodCity loads, TierVisualThemeController applies the active tier's visual theme; LevelController restores saved (Lvl, Exp) in Awake(); SkillsRegistry restores saved skills and UpgradeableStat values and invokes SyncDerivedRuntimeState() in Start(); TimerPresenter seeds TimerValue from IRunSessionService.AccumulatedRunSeconds in Start(); EnemiesSpawner, WaveManager, SwarmSpawner, TowerBossBase, GolemStatue, and BossEncounterService apply active tier configs.
  4. Stage progression flow: Defeating required Tower Bosses unlocks GolemStatue. Awakening GolemStatue (1.0s shake) spawns the active tier's GolemBoss. Defeating GolemBoss spawns NextStagePortal. Driving into NextStagePortal runs a 1.2s DOTween vortex while protecting the player; on non-final tiers (Tier 1 -> Tier 2), it calls IRunSessionService.CaptureSnapshotAndAdvanceTier and reloads the scene; on the final tier (Tier 2), it calls CaptureFinalRunTimer and opens IDemoThankYouPresenter.EnableThankYouScreen().
  5. Pause and Dev Console flow: PausePresenter toggles pause UI and calls IGameTimeService.Pause() / Resume() (preserving any active TargetTimeScale multiplier set via the Dev Console timescale command). DevConsolePresenter records whether the game was already paused before opening, calls IGameTimeService.Pause() on Show(), and calls Resume() on Hide() only if the player is alive and the game was not paused prior to opening.
  6. Terminal end-of-run flows (Death and Demo Thank You): On death, PlayerDeathHandler waits for death VFX completion and calls IPlayerDeathPresenter.EnableDeathScreen() -> saves died ScoreBoardRecord -> enables death UI -> switches to death audio -> calls IGameTimeService.Pause() -> freezes SkillsStatsPresenter. On final tier portal completion, DemoThankYouPresenter dismisses any active skill upgrade modal -> saves survived ScoreBoardRecord -> hides pause -> enables completion UI -> acquires InputContextScopeKind.Death -> calls IGameTimeService.Pause() -> freezes SkillsStatsPresenter while keeping default audio mode.
  7. Retry and menu reset flow: Selecting Try Again or Main Menu in MenuButtonsFunctionality calls IRunSessionService.ResetSession() (resetting tier index to 0, clearing snapshots and timer, and disabling god mode) before reloading or loading the target scene.

## Rules and Invariants

- Critical behavior rules:
  - GameSceneLoader.LoadNewSceneAsync MUST invoke IGameTimeService.Reset() in its completion callback to guarantee new or reloaded scenes start unpaused at 1x time scale.
  - GameSceneLoader.LoadNewSceneAsync MUST guard overlapping load requests via IsLoading and return the active AsyncOperation rather than starting concurrent single-mode scene loads.
  - GameScene enum integer values must match Unity Build Settings scene indices.
  - Do not mutate Time.timeScale directly in UI or gameplay scripts; always inject and use IGameTimeService (Pause, Resume, SetTimeScale, Reset).
  - IGameTimeService.SetTimeScale updates TargetTimeScale and only writes Time.timeScale immediately when !IsPaused, ensuring changing time scale from the open Dev Console never unpauses background simulation until the console/pause overlay closes.
  - Callers changing IRunSessionService.CurrentTierIndex before a scene reload (such as NextStagePortal or GameplayDevCommandsRegistrar.HandleLoadTierCommand) must update IRunSessionService BEFORE calling IGameSceneLoader.LoadNewSceneAsync / ReloadCurrentSceneAsync, because OnStartLoadingScene fires synchronously and LoadingOverlayPresenter reads CurrentTierIndex + 1 immediately.
  - Temporary combat state (player HP, Armor plates, active power-up timers, wave/swarm counters) resets naturally on tier reload, while player Level, EXP, initialized skills, upgraded stats, accumulated run timer, and dev-build god mode carry over across non-final tiers via IRunSessionService.
  - Logic or animations intended to run while paused or during loading transitions must use Time.unscaledDeltaTime, WaitForSecondsRealtime, or unscaled DOTween updates (SetUpdate(true)).
- Ordering or sequencing guarantees:
  - OnStartLoadingScene is emitted synchronously right after SceneManager.LoadSceneAsync starts.
  - OnSceneLoaded is emitted after IGameTimeService.Reset() executes and CurrentLoadedScene updates.
  - LoadingOverlayPresenter yields one frame after OnSceneLoaded before starting its fade-out tween so scene Awake() and Start() initialization completes behind the opaque curtain.
- Constraints contributors must preserve:
  - Keep IGameTimeService, IGameSceneLoader, GameLoopProgressionConfigSO, and IRunSessionService bound as project-level singletons in ProjectInstaller.
  - Inject IGameSceneLoader for scene loading rather than calling SceneManager directly in domain code.
  - Ensure any new retry or quit-to-menu entry point calls IRunSessionService.ResetSession() so tier state never leaks into a fresh run.

## Extension Points

- Safe extension areas:
  - Add new tiers (e.g., Tier 3+) by appending a LoopTierConfig entry to Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset with enemy spawn overrides, boss config overrides, and visual theme assets.
  - Add new scene options by adding entries to GameScene enum and Unity Build Settings.
  - Subscribe to IGameSceneLoader.OnStartLoadingScene or OnSceneLoaded for cross-scene state cleanup or audio track switches.
  - Query IRunSessionService.ProgressionConfig.TryGetTier(CurrentTierIndex + 1, out LoopTierConfig nextTier) when implementing future cross-tier mechanics (such as rare higher-tier enemies spawning in lower tiers).
- Required dependencies and contracts:
  - Scene navigation requests must use IGameSceneLoader.
  - Time scale and pause control must use IGameTimeService.
  - Tier-dependent gameplay systems must inject IRunSessionService and tolerate null or empty tier overrides in isolated test scenes by falling back to scene-authored defaults.
- Testing implications:
  - C# compile check: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false and dotnet build Assembly-CSharp-Editor.csproj -p:BuildProjectReferences=false.
  - EditMode unit tests: Assets/Scripts/Editor/Tests/SkillsTierCarryoverTests.cs, Assets/Scripts/Editor/Tests/TierConfigDrivenEnemySpawningTests.cs, and Assets/Scripts/Editor/Tests/ScoreBoardDualSortTests.cs.
  - Manual Unity Play Mode testing: verify Boot -> Main Menu -> Tier 1 -> GolemStatue -> Golem -> NextStagePortal -> Tier 2 -> DemoThankYouPresenter -> Main Menu ScoreBoard flow, pause/console time scale persistence, and Try Again session reset.

## Integration Notes

- Upstream dependencies:
  - Unity SceneManager, RenderSettings, DynamicGI, Volume, and Time APIs.
  - Reflex DI ProjectInstaller, BootLoader, and DefaultGameplaySceneInstaller.
  - DOTween for unscaled loading curtain fade-out and portal vortex transitions.
- Downstream consumers:
  - LoadingOverlayPresenter and BackgroundAudioManager subscribe to IGameSceneLoader events.
  - PausePresenter, PlayerDeathPresenter, DemoThankYouPresenter, DevConsolePresenter, and GameplayDevCommandsRegistrar consume IGameTimeService.
  - LevelController, SkillsRegistry, TimerPresenter, PlayerDamagedHandler, TierVisualThemeController, EnemiesSpawner, WaveManager, SwarmSpawner, BossEncounterService, GolemBoss, TowerBossBase, GolemStatue, NextStagePortal, MenuButtonsFunctionality, and GameplayDevCommandsRegistrar consume IRunSessionService.
- Cross-system coupling risks:
  - Setting Time.timeScale = 0f via IGameTimeService.Pause() stops all scaled-time systems (enemy movement, flow field updates, projectile motion). Components executing during pause or loading must explicitly use unscaled updates.
  - Mutating ScriptableObject assets referenced by GameLoopProgressionConfigSO at runtime would corrupt asset state across runs; consumers must clone runtime data (EnemySpawnInfo.CreateRuntimeCopy() in EnemiesSpawner and DeepCopyUtility clones in SkillUpgradeableStatsConfig.ResetRuntimeState()).

## Known Risks and Open Questions

- Known limitations:
  - Pending skill upgrade requests queued in SkillUpgradeFlow at the moment the player enters NextStagePortal are cleared on scene reload rather than blocking portal entry until spent.
  - GameSceneLoader.CurrentLoadedScene defaults to GameScene.MainMenu before any scene loading operation completes (ReloadCurrentSceneAsync syncs CurrentLoadedScene from SceneManager.GetActiveScene().buildIndex when defined in GameScene).
- Open design questions:
  - Should entering NextStagePortal remain immediate when unspent level-up upgrades are pending, or auto-open/gate on remaining upgrade choices?
- Suggested follow-up tasks:
  - Complete the dedicated Tier 2 visual art polish pass (post-processing grading, skybox, water shader tuning, and statue break / boss intro animations).
