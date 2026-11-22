# DI and Boot Flow System Documentation

## Purpose

The DI and Boot Flow system owns Reflex container setup, project-level and scene-level service registration, boot-scene cross-scene service injection, pre-activation code-driven prefab injection, scene loading, and global time scale service bindings.

It is responsible for:

- Project-level service bindings that survive scene transitions (input, time scale, scene loading, multi-tier run progression, and scoreboard).
- Scene-level gameplay and main-menu dependency injection containers.
- Boot-scene extra service registration injected into each scene container (audio mixers, background audio, loading overlay, and damage numbers spawner).
- Code-driven pre-activation prefab injection via ReflexPrefabInstantiator (replacing prefab-attached GameObjectSelfInjector components).
- Initial scene loading from Boot scene to Main Menu and emitting scene loading events for audio, UI, and loading curtain consumers.

It is not responsible for:

- Gameplay logic implemented within bound services.
- Unity scene visual or layout setup beyond installer references.
- Pause UI, death UI, end-of-run UI, or skill selection logic.

## Reading Map

- Primary code locations:
  - Assets/Scripts/ReflexDI/BootLoader.cs
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/ReflexDI/ReflexPrefabInstantiator.cs
  - Assets/Scripts/GameFlow/GameSceneLoader.cs
  - Assets/Scripts/GameFlow/GameTimeService.cs
  - Assets/Scripts/GameFlow/RunSessionService.cs
  - Assets/Scripts/GameFlow/TierVisualThemeController.cs
  - Assets/Scripts/GameFlow/Config/GameLoopProgressionConfigSO.cs
  - Assets/Scripts/UI/Loading/LoadingOverlayPresenter.cs
  - Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs
  - Assets/Scripts/CarSurvivors.Runtime.asmdef
  - Assets/Scripts/AssemblyInfo.cs
  - Assets/ScriptableObjects/CarSurvivors.ScriptableObjects.asmref
  - Assets/Scripts/Editor/CarSurvivors.Editor.asmdef
  - Assets/Scripts/Editor/Tests/CarSurvivors.Editor.Tests.asmdef
  - Assets/Tests/PlayMode/CarSurvivors.PlayMode.Tests.asmdef
  - Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef
- Automated tests:
  - Assets/Tests/PlayMode/PlayModeTestHarness.cs
  - Assets/Tests/PlayMode/BootMainMenuGameplaySmokeTests.cs
- Authoritative prefabs and scenes:
  - Assets/Resources/ProjectScope.prefab
  - Assets/Scenes/Boot.unity
  - Assets/Scenes/MainMenu.unity
  - Assets/Scenes/RuinedBloodCity.unity
- Related docs:
  - .agents/context/game-systems/audio-system.md
  - .agents/context/game-systems/game-flow-system.md
  - .agents/context/game-systems/input-system.md
  - .agents/context/game-systems/power-ups-system.md
  - .agents/context/game-systems/dev-console-system.md
  - .agents/context/game-systems/settings-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs registers project-scoped singletons on Assets/Resources/ProjectScope.prefab:
    - Input: validates _inputActions, _inputGlyphCatalog, and _inputRuntimeConfig fail-fast, binds them as singletons, registers Assets/Scripts/Input/GameInputService.cs (as IGameplayInput, IInputCommands, IInputContexts, IInputDeviceContext, IInputActionSource, IInputRuntime) and Assets/Scripts/Input/InputPromptService.cs (as IInputPromptProvider), and subscribes builder.OnContainerBuilt += SpawnInputRuntimeDriver to instantiate, AttributeInjector.Inject, and activate a persistent DontDestroyOnLoad InputRuntimeDriver before any scene Awake runs.
    - Scene Loading, Time Scale & Progression: validates _progressionConfig (calling GameLoopProgressionConfigSO.ValidateConfiguration()) and binds Assets/Scripts/GameFlow/GameTimeService.cs (as IGameTimeService), Assets/Scripts/GameFlow/GameSceneLoader.cs (as IGameSceneLoader), _progressionConfig (GameLoopProgressionConfigSO), and Assets/Scripts/GameFlow/RunSessionService.cs (as IRunSessionService).
    - ScoreBoard: binds Assets/Scripts/ScoreBoard/StoredScoreBoard.cs, Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs (as IScoreBoardNewScoreSaver), and Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs (as IScoreBoardBestScoreGetter).
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs registers menu-scoped settings services (scoped lifetime): AudioVolumeSetting, GraphicSetting, FullScreenSetting, ResolutionSetting, and DamageNumbersSetting (each bound to their ISetting<TSelf, TValue> and ISettingLoader interfaces), plus the scene UiInputScope instance (fail-fast validated).
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds scene object references and services:
    - Camera: Main Camera (Camera) and optional CinemachineCombatFollowOffsetController (ICinemachineCombatFollowOffsetController).
    - Player: PlayerManager (IPlayerManager) and child ISkillUpgradePresenter.
    - UI Presenters & Input Scope: UiInputScope (fail-fast validated), SkillsStatsPresenter (ISkillsStatsPresenter, fail-fast validated), PausePresenter (IPausePresenter, fail-fast validated), PlayerDeathPresenter (IPlayerDeathPresenter), optional DemoThankYouPresenter (IDemoThankYouPresenter), PlayerLevelPresenter (IPlayerLevelPresenter), SkillsVisualPresenter (ISkillsVisualPresenter), TimerPresenter (ITimerPresenter), SwarmNotificationPresenter (ISwarmNotificationPresenter), and optional BossHUDPresenter (IBossHUDPresenter), ArenaLeashWarningPresenter (IArenaLeashWarningPresenter), and PlayerSkillsHUDPresenter (IPlayerSkillsHUDPresenter).
    - Dev Console: DevConsoleService (IDevConsoleService), and optional DevConsolePresenter (IDevConsolePresenter) and GameplayDevCommandsRegistrar (IGameplayDevCommandsRegistrar).
    - Grid & Skill Upgrade Flow: GridManager (IGridManager) and SkillUpgradeFlow (ISkillUpgradeFlow).
    - Spawners & Collectibles: EnemiesSpawner (IOnRandomGridPosSpawner<EnemiesSpawner>, ISwarmEnemySpawner, IEnemySpawnDifficultyController, IActiveEnemiesProvider), CollectibleDropNotifier (ICollectibleDropNotifier, ICollectibleDropSuppressor), DropAnimationConfiguration, ExpParticleSpawner (IInWorldSpaceSpawner<ExpParticleSpawner, float>), and ExplosionVfxPool (IExplosionVfxPool, fail-fast validated).
    - Waves, Swarms & Bosses: WaveManager (IWaveFreezer), optional SwarmSpawner (ISwarmFreezer), and optional BossEncounterService (IBossEncounterService).
    - Post-Processing, Tier Visuals & Power-Up Services: Post-Processing Volume (Volume), optional TierVisualThemeController (ITierVisualThemeController), and fail-fast validated NukeEffectService, TimeFreezeService (bound as ITimeFreezeState), BubbleShieldService (bound as IBubbleShield), ArmorService (bound as IArmorService), and FireTailsService (bound as IFireTailsService).
  - Assets/Scripts/ReflexDI/BootLoader.cs validates _loadingOverlayPresenter fail-fast in Start() and hooks into SceneScope.OnSceneContainerBuilding to register boot-scene services into every newly created scene container: AudioMixersManager (IAudioMixersManager), BackgroundAudioManager (IBackgroundAudioManager), LoadingOverlayPresenter (ILoadingOverlayPresenter), and DamageNumbersSpawner (IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> and IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>). It also inspects root scene objects for DefaultGameplaySceneInstaller to pass the active scene MainCamera to DamageNumbersSpawner.Initialize(Camera).
  - Assets/Scripts/ReflexDI/ReflexPrefabInstantiator.cs provides static pre-activation injection helpers (CreateInactiveStagingParent and InstantiateInjectedObject overloads). It instantiates prefabs under an inactive staging transform so Awake() and OnEnable() do not run yet, calls GameObjectInjector.InjectObject(instance, container) to populate all [Inject] fields on root MonoBehaviours, optionally executes an Action<T> beforeActivate callback (for example, EnemiesSpawner calling Enemy.SetBaseConfig(ConfigOverride)), and reparents the instance to its active destination parent so Awake() and OnEnable() execute with all dependencies and overrides already in place.
- Runtime flow:
  - Boot scene initializes the Reflex project container from ProjectScope.prefab and injects IGameSceneLoader into BootLoader.
  - BootLoader.Start validates _loadingOverlayPresenter, subscribes InstallExtra to SceneScope.OnSceneContainerBuilding, and starts a coroutine that waits one frame (WaitForEndOfFrame) before invoking IGameSceneLoader.LoadNewSceneAsync(GameScene.MainMenu).
  - When loading a scene, GameSceneLoader guards IsLoading, fires OnStartLoadingScene, executes single-mode async scene loading, and on completion calls IGameTimeService.Reset(), updates CurrentLoadedScene, and fires OnSceneLoaded.
  - As new scene containers build, BootLoader.InstallExtra updates DamageNumbersSpawner with the new scene's MainCamera if DefaultGameplaySceneInstaller is present (updating all pooled DamageNumber instances), and registers boot-level audio, loading overlay, and damage-number spawner singletons into the scene container.
  - During gameplay, dynamically pooled or spawned prefabs with root [Inject] dependencies (Enemy prefabs in EnemiesSpawner, ExpParticle in ExpParticleSpawner, GolemBoss and NextStagePortal in BossEncounterService) are instantiated through ReflexPrefabInstantiator.InstantiateInjectedObject rather than prefab-attached GameObjectSelfInjector components.

## Rules and Invariants

- Critical behavior rules:
  - Explicit dependency injection via Reflex must be preserved; avoid direct GameObject.Find, FindAnyObjectByType, or static service locators.
  - Scene-specific bindings belong in DefaultGameplaySceneInstaller.cs, menu settings in MainMenuInstaller.cs, project-lifetime singletons in ProjectInstaller.cs, and cross-scene boot singletons in BootLoader.cs.
  - Do not attach Reflex.Components.GameObjectSelfInjector to prefabs. Use ReflexPrefabInstantiator.InstantiateInjectedObject (with a reusable inactive staging parent created via CreateInactiveStagingParent for object pools) so GameObjectInjector.InjectObject and any pre-activation configuration run deterministically before Awake() and OnEnable(). Use GameObjectInjector.InjectRecursive only when child GameObjects in a spawned hierarchy also carry [Inject] fields (such as MapInteractablesSpawner).
  - Required project assets (_inputActions, _inputGlyphCatalog, _inputRuntimeConfig, _progressionConfig in ProjectInstaller), boot references (_loadingOverlayPresenter in BootLoader), and required gameplay scene services (_uiInputScope, _skillsStatsPresenter, _pausePresenter, _explosionVfxPool, _nukeEffectService, _timeFreezeService, _bubbleShieldService, _armorService, and _fireTailsService in DefaultGameplaySceneInstaller) must fail fast with InvalidOperationException if unassigned.
  - GameScene enum values (MainMenu = 1, RuinedBloodCity = 2) must remain consistent with Unity Build Settings scene indices.
  - GameSceneLoader.LoadNewSceneAsync must invoke IGameTimeService.Reset() upon completion to guarantee newly loaded scenes start unpaused at 1x time scale.
- Ordering or sequencing guarantees:
  - OnStartLoadingScene is raised synchronously before async loading completes.
  - OnSceneLoaded is raised after scene loading finishes, IGameTimeService.Reset() executes, and CurrentLoadedScene updates.
  - BootLoader hooks SceneScope.OnSceneContainerBuilding in Start and unhooks in OnDisable.
  - ReflexPrefabInstantiator runs GameObjectInjector.InjectObject and beforeActivate while the instance is parented under an inactive staging transform, strictly before reparenting triggers Awake() and OnEnable().
- Constraints contributors must preserve:
  - Installer classes should focus exclusively on dependency validation and registration, avoiding embedded gameplay or presentation logic.
  - Follow ADR-004 before editing .unity, .prefab, .asset, or .meta files directly.

## Extension Points

- Safe extension areas:
  - ProjectInstaller.cs: register new global services with scene-independent lifetimes.
  - DefaultGameplaySceneInstaller.cs: bind new gameplay-scoped services, UI presenters, or scene object references.
  - MainMenuInstaller.cs: bind menu-scoped settings or menu-only UI presenters.
  - BootLoader.cs (InstallExtra): register cross-scene MonoBehaviour services owned by the boot scene.
  - ReflexPrefabInstantiator.cs: use InstantiateInjectedObject (and optional beforeActivate callbacks) whenever instantiating pooled or dynamic prefabs that require [Inject] dependencies or config overrides prior to Awake() / OnEnable().
- Required dependencies and contracts:
  - New Reflex bindings should prefer narrow colocated interfaces.
  - New scenes added to GameScene enum must match Unity Build Settings indices.
- Testing implications:
  - C# compile verification: dotnet build CarSurvivors.Runtime.csproj -p:BuildProjectReferences=false, dotnet build CarSurvivors.Editor.csproj -p:BuildProjectReferences=false, dotnet build CarSurvivors.Editor.Tests.csproj -p:BuildProjectReferences=false, and dotnet build CarSurvivors.PlayMode.Tests.csproj -p:BuildProjectReferences=false.
  - EditMode & PlayMode DI test harness patterns: see .agents/skills/unity-write-tests/references/car-survivors-test-harness.md, Assets/Scripts/Editor/Tests/TierConfigDrivenEnemySpawningTests.cs, Assets/Tests/PlayMode/PlayModeTestHarness.cs, and Assets/Tests/PlayMode/BootMainMenuGameplaySmokeTests.cs.
  - Unity Play Mode verification: test flow from Boot -> Main Menu -> Gameplay (Tier 1 -> Tier 2) -> Death/ThankYou/Pause reload to confirm container resolution and service injection.

## Integration Notes

- Upstream dependencies:
  - Reflex DI package handles container construction, constructor injection, and attribute-based injection ([Inject]).
  - Unity SceneManager and Build Settings configuration.
- Downstream consumers:
  - UI Presenters, ScoreBoard components, Audio Managers, Spawners, Bosses, Interactables, Power-Up Services, and Gameplay managers consume Reflex-bound services.
  - LoadingOverlayPresenter and BackgroundAudioManager subscribe to IGameSceneLoader events across scene transitions.
- Cross-system coupling risks:
  - Modifying BootLoader bindings or event subscriptions can break global audio management, loading curtain transitions, or damage number camera references across scene loads.
  - Pooled spawners creating inactive staging parents via ReflexPrefabInstantiator.CreateInactiveStagingParent must destroy the staging parent GameObject in OnDestroy() to avoid leaking empty staging transforms.

## Known Risks and Open Questions

- Known limitations:
  - GameSceneLoader.CurrentLoadedScene defaults to GameScene.MainMenu before any scene loading operation completes (ReloadCurrentSceneAsync synchronizes CurrentLoadedScene from the active scene buildIndex when defined).
  - BootLoader inspects root scene objects for DefaultGameplaySceneInstaller during OnSceneContainerBuilding to forward MainCamera to DamageNumbersSpawner.
- Open design questions:
  - Should scene loading transition from integer enum indices to scene reference assets if additional map scenes are added?
- Suggested follow-up tasks:
  - Audit scene container building to ensure no redundant lookups occur on scene loads.
