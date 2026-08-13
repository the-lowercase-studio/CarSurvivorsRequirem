# DI and Boot Flow System Documentation

## Purpose

The DI and Boot Flow system owns Reflex container setup, boot-scene service registration, scene loading, and global time pause/resume helpers.

It is responsible for:

- Project-level service bindings that survive scene transitions.
- Scene-level gameplay and main-menu dependency injection containers.
- Boot-scene extra service registration injected into each scene container.
- Initial scene loading from Boot scene to Main Menu.
- Emitting scene loading events for audio, UI, and navigation consumers.

It is not responsible for:

- Gameplay logic implemented within bound services.
- Unity scene visual or layout setup beyond installer references.
- Pause UI, death UI, or skill selection logic (although pause triggers invoke Assets/Scripts/GameFlow/GameTime.cs).

## Reading Map

- Primary code locations:
  - Assets/Scripts/ReflexDI/BootLoader.cs
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/GameFlow/GameScenesLoader.cs
  - Assets/Scripts/GameFlow/GameTime.cs
- Related docs:
  - .agents/context/game-systems/audio-system.md
  - .agents/context/game-systems/game-flow-system.md
  - .agents/context/game-systems/settings-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs registers project-scoped singletons: Assets/Scripts/GameFlow/GameScenesLoader.cs (as IGameSceneLoader), Assets/Scripts/ScoreBoard/StoredScoreBoard.cs, Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs (as IScoreBoardNewScoreSaver), and Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs (as IScoreBoardBestScoreGetter).
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs registers menu-scoped settings services (scoped lifetime): AudioVolumeSetting, GraphicSetting, FullScreenSetting, ResolutionSetting, and DamageNumbersSetting (each bound to their ISetting<TSelf, TValue> and ISettingLoader interfaces).
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds scene object references and services: Main Camera (Camera), Player (IPlayerManager), UI Presenters (IPlayerDeathPresenter, IPlayerLevelPresenter, ISkillsVisualPresenter, ITimerPresenter, ISwarmNotificationPresenter), Grid System (IGridManager), Skill Upgrade Flow (ISkillUpgradeFlow), Spawners (IOnRandomGridPosSpawner<EnemiesSpawner>, ISwarmEnemySpawner, IEnemySpawnDifficultyController, ICollectibleDropNotifier, DropAnimationConfiguration, IInWorldSpaceSpawner<ExpParticleSpawner, float>), Waves (IWaveFreezer), and Post-Processing Volume (Volume).
  - Assets/Scripts/ReflexDI/BootLoader.cs hooks into SceneScope.OnSceneContainerBuilding to register boot-scene services into every newly created scene container. It registers AudioMixersManager (IAudioMixersManager), BackgroundAudioManager (IBackgroundAudioManager), and DamageNumbersSpawner (IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> and IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>). It also inspects root scene objects for DefaultGameplaySceneInstaller to pass the main camera to DamageNumbersSpawner.
  - Assets/Scripts/GameFlow/GameScenesLoader.cs wraps SceneManager.LoadSceneAsync, tracks CurrentLoadedScene, and fires OnStartLoadingScene and OnSceneLoaded events.
  - Assets/Scripts/GameFlow/GameTime.cs is a static helper class controlling Time.timeScale via Pause() (sets 0f) and Resume() (sets 1f).
- Runtime flow:
  - Boot scene initializes the Reflex project container and injects IGameSceneLoader into BootLoader.
  - BootLoader.Start subscribes InstallExtra to SceneScope.OnSceneContainerBuilding and starts a coroutine that waits one frame (WaitForEndOfFrame) before invoking IGameSceneLoader.LoadNewSceneAsync(GameScene.MainMenu).
  - When loading a scene, GameSceneLoader fires OnStartLoadingScene, executes single-mode async scene loading, and on completion calls GameTime.Resume(), updates CurrentLoadedScene, and fires OnSceneLoaded.
  - As new scene containers build, BootLoader.InstallExtra resolves main camera dependencies for DamageNumbersSpawner if DefaultGameplaySceneInstaller is present, and registers boot-level audio and damage-number spawner singletons into the scene container.

## Rules and Invariants

- Critical behavior rules:
  - Explicit dependency injection via Reflex must be preserved; avoid direct GameObject.Find, FindAnyObjectByType, or static service locators.
  - Scene-specific bindings belong in DefaultGameplaySceneInstaller.cs, menu settings in MainMenuInstaller.cs, project-lifetime singletons in ProjectInstaller.cs, and cross-scene boot singletons in BootLoader.cs.
  - GameScene enum values (MainMenu = 1, RuinedBloodCity = 2) must remain consistent with Unity Build Settings scene indices.
  - GameSceneLoader.LoadNewSceneAsync must invoke GameTime.Resume() upon completion to guarantee newly loaded scenes start unpaused.
- Ordering or sequencing guarantees:
  - OnStartLoadingScene is raised before async loading starts.
  - OnSceneLoaded is raised after scene loading finishes, GameTime.Resume() executes, and CurrentLoadedScene updates.
  - BootLoader hooks SceneScope.OnSceneContainerBuilding in Start and unhooks in OnDisable.
- Constraints contributors must preserve:
  - Installer classes should focus exclusively on dependency registration, avoiding embedded gameplay or presentation logic.
  - Do not modify binary or asset files (.unity, .prefab, .asset, .meta) directly unless requested.

## Extension Points

- Safe extension areas:
  - ProjectInstaller.cs: register new global services with scene-independent lifetimes.
  - DefaultGameplaySceneInstaller.cs: bind new gameplay-scoped services, UI presenters, or scene object references.
  - MainMenuInstaller.cs: bind menu-scoped settings or menu-only UI presenters.
  - BootLoader.cs (InstallExtra): register cross-scene MonoBehaviour services owned by the boot scene.
- Required dependencies and contracts:
  - New Reflex bindings should prefer existing narrow interfaces.
  - New scenes added to GameScene enum must match Unity Build Settings indices.
- Testing implications:
  - C# compile verification: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
  - Unity Play Mode verification: test flow from Boot -> Main Menu -> Gameplay -> Death/Pause reload to confirm container resolution and service injection.

## Integration Notes

- Upstream dependencies:
  - Reflex DI package handles container construction and attribute-based injection ([Inject]).
  - Unity SceneManager and Build Settings configuration.
- Downstream consumers:
  - UI Presenters, ScoreBoard components, Audio Managers, Spawners, and Gameplay managers consume Reflex-bound services.
  - BackgroundAudioManager subscribes to IGameSceneLoader events for track switching across scene transitions.
- Cross-system coupling risks:
  - Modifying BootLoader bindings or event subscriptions can break global audio management or damage number popups across scene loads.
  - GameTime static calls affect all scaled-time gameplay logic globally.

## Known Risks and Open Questions

- Known limitations:
  - GameTime is static without stack-based pause reason tracking; the latest call to Pause() or Resume() overwrites state.
  - GameSceneLoader.CurrentLoadedScene defaults to GameScene.MainMenu before any scene loading operation completes.
  - BootLoader relies on searching scene root objects for DefaultGameplaySceneInstaller to pass the main camera to DamageNumbersSpawner.
- Open design questions:
  - Should GameTime be converted to an injected IGameTime service with pause reason stack support?
  - Should scene loading transition from integer enum indices to scene reference assets?
- Suggested follow-up tasks:
  - Audit scene container building to ensure no redundant lookups occur on scene loads.

