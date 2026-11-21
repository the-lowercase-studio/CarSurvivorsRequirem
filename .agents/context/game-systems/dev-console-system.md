# Dev Console System Documentation

## Purpose

The Dev Console system provides an in-game developer Command Line Interface (CLI) for executing runtime debugging routines, cheat commands, entity and collectible spawning, tier jumping, death protection, time scale manipulation, and progression testing during gameplay in Unity Editor play mode and Development builds.

It is responsible for:
- Providing a runtime command registry, tokenization engine, and log history buffer through Assets/Scripts/UI/DevConsole/DevConsoleService.cs.
- Presenting a toggleable overlay console UI with scrollable output, prompt input, and Up/Down arrow history navigation through Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs.
- Isolating input and pausing gameplay time safely when open (IGameTimeService.Pause() / IGameTimeService.Resume()) by acquiring InputContextScopeKind.DevConsole through IInputContexts and consuming prioritized commands from IInputCommands.
- Enforcing release-build security and zero overhead in production builds by destroying UI visual trees, disabling components, and neutralizing command registration when not running in the Unity Editor or development builds.
- Registering standard console commands (help, clear) and gameplay domain commands (golem, spawn <entity> [count], exp <amount>[k|m|b] [--auto], powerup <nuke|freeze|shield|armor|firetails|crate>, loadtier <tier> [--reset], godmode [on|off], timescale [multiplier]) through Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs.
- Calculating ground-snapped, forward-fanned world positions for dynamic entity and collectible spawning.
- Orchestrating automated level progression and skill resolution (--auto / -a flag) by fast-forwarding level presentation and auto-selecting upgrades without opening modal UI dialogs.

It is not responsible for:
- Shipping player-facing cheat menus or console interfaces in release builds.
- Defining enemy combat behavior, boss phase logic, power-up mechanics, or health initialization (delegated to boss encounter services, enemy spawner pools, and power-up services).
- Persisting state to disk across application launches (run-scoped cheats like godmode live only in memory on IRunSessionService for the current run, and timescale resets on scene load).
- Global cross-scene UI persistence outside configured gameplay scenes.

## Reading Map

- Primary code locations:
  - Assets/Scripts/UI/DevConsole/DevCommandInfo.cs
  - Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs
  - Assets/Scripts/UI/DevConsole/DevConsoleService.cs
  - Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs
- Editor tooling:
  - Assets/Scripts/Editor/Tools/DevConsolePrefabBuilder.cs
- Prefabs and scene wiring:
  - Assets/Prefabs/UI/DevConsole/DevConsolePresenter.prefab
  - Assets/Prefabs/Collectibles/Nuke.prefab
  - Assets/Prefabs/Collectibles/FreezeTimer.prefab
  - Assets/Prefabs/Collectibles/BubbleShield.prefab
  - Assets/Prefabs/Collectibles/Armor.prefab
  - Assets/Prefabs/Collectibles/FireTails.prefab
  - Assets/Prefabs/Collectibles/SkilCrate.prefab
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scenes/RuinedBloodCity.unity
- Integrated domain systems:
  - Assets/Scripts/GameFlow/GameTimeService.cs
  - Assets/Scripts/GameFlow/RunSessionService.cs
  - Assets/Scripts/GameFlow/GameSceneLoader.cs
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Player/Constants/PlayerDamageConstants.cs
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/UI/HUD/TimerPresenter.cs
  - Assets/Scripts/UI/Level/PlayerLevelPresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
- Related docs:
  - .agents/context/game-systems/game-flow-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/power-ups-system.md
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/create-dev-console-command/SKILL.md
  - .agents/skills/document-system/SKILL.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/UI/DevConsole/DevCommandInfo.cs is a lightweight read-only struct containing Name, Syntax, and Description metadata for command discovery.
  - Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs holds domain constants for buffer limits (DEFAULT_MAX_LOG_LINES = 100, DEFAULT_MAX_HISTORY_COUNT = 50), EXP unit multipliers (1K, 1M, 1B), spawn defaults (distance 8f, lateral spacing 2f, max count 50), auto flags (--auto, -a), loadtier metadata and flags (LOAD_TIER_COMMAND_NAME = "loadtier", LOAD_TIER_COMMAND_SYNTAX = "loadtier <tier> [--reset]", RESET_FLAG_LONG = "--reset", RESET_FLAG_SHORT = "-r", MIN_TIER_NUMBER = 1), powerup command metadata (POWERUP_COMMAND_NAME = "powerup", POWERUP_COMMAND_SYNTAX = "powerup <nuke|freeze|shield|armor|firetails|crate>", POWERUP_SPAWN_Y_OFFSET = 0.85f) and collectible prefab paths, godmode metadata and boolean tokens (GOD_MODE_COMMAND_NAME = "godmode", GOD_MODE_COMMAND_SYNTAX = "godmode [on|off]", on/off/true/false/1/0), timescale metadata and bounds (TIMESCALE_COMMAND_NAME = "timescale", TIMESCALE_COMMAND_SYNTAX = "timescale [multiplier]", TIMESCALE_MIN_MULTIPLIER = 0.05f, TIMESCALE_MAX_MULTIPLIER = 100f, TIMESCALE_DISPLAY_FORMAT = "0.###"), rich text hex colors (#FFCC00, #FF4444), and prompt prefix (> ).
  - Assets/Scripts/UI/DevConsole/DevConsoleService.cs implements IDevConsoleService. It maintains a case-insensitive dictionary of command handlers, an ordered list of registered commands for help output, and a bounded ring buffer of log entries. It raises OnLogAppended and OnLogsCleared.
  - Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs is a MonoBehaviour implementing IDevConsolePresenter. It handles the visual console canvas, prioritized InputCommandKind handling via IInputCommands (ToggleConsole, DevConsoleClose, DevConsoleSubmit, DevConsoleHistoryPrevious, DevConsoleHistoryNext), DevConsole context scope acquisition/release via IInputContexts, LateUpdate deferred single-path command submission and input-field refocusing, and game pausing via injected IGameTimeService.Pause() / IGameTimeService.Resume().
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs is a MonoBehaviour implementing IGameplayDevCommandsRegistrar. It injects IDevConsoleService, IBossEncounterService, IPlayerManager, ISwarmEnemySpawner, ISkillUpgradePresenter, IPlayerLevelPresenter, ICollectibleDropNotifier, IGameTimeService, IRunSessionService, IGameSceneLoader, and ITimerPresenter. It registers gameplay commands (golem, spawn, exp, powerup, loadtier, godmode, timescale) into IDevConsoleService on Start() and unregisters them on OnDestroy() (also calling _gameTimeService.Reset() on OnDestroy()).
  - Assets/Scripts/Editor/Tools/DevConsolePrefabBuilder.cs builds the UI prefab hierarchy with TextMeshPro components, dark semi-transparent panel, scrollable log viewer, and input field, and exposes editor menu items to wire active scenes automatically.
- Key interfaces:
  - IDevConsoleService: Manages command registration, lookup, exception-safe execution, logging (Log, LogWarning, LogError, Clear), and query properties (IsEnabled, RegisteredCommands, LogHistory).
  - IDevConsolePresenter: Exposes visibility and support status (IsSupported, IsVisible) and state control methods (Toggle(), Show(), Hide()).
  - IGameplayDevCommandsRegistrar: Marker interface for DI binding and lifecycle containment.
- Runtime flow:
  - Dependency Registration: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs registers DevConsoleService as singleton IDevConsoleService, DevConsolePresenter as IDevConsolePresenter, and GameplayDevCommandsRegistrar as IGameplayDevCommandsRegistrar.
  - Build Support Evaluation: Presenter, Registrar, and Service check compile-time and runtime flags (#if UNITY_EDITOR || DEVELOPMENT_BUILD with Debug.isDebugBuild). In release builds, DevConsolePresenter destroys its root visual and deactivates itself in Awake(), GameplayDevCommandsRegistrar disables itself in Awake(), and DevConsoleService operates in a dormant inert mode.
  - Startup Registration: In supported environments, DevConsoleService registers built-in commands help and clear upon construction. In Start(), GameplayDevCommandsRegistrar registers golem, spawn, exp, powerup, loadtier, godmode, and timescale.
  - User Toggle: InputCommandKind.ToggleConsole triggers DevConsolePresenter.Toggle(). Show() records _wasGamePausedBeforeOpen = _gameTimeService.IsPaused || (Time.timeScale == 0f), pauses gameplay via _gameTimeService.Pause(), acquires InputContextScopeKind.DevConsole, activates the UI panel, focuses the input field, and scrolls to bottom.
  - Command Submission & Execution: InputCommandKind.DevConsoleSubmit schedules _hasPendingSubmit for LateUpdate, sanitizes input (stripping backquote and tilde characters), appends unique entries to _commandHistory (up to 50 items), and calls ExecuteCommand(trimmed). DevConsoleService logs the prompt (> command), splits tokens by whitespace, resolves the command name case-insensitively, and executes the handler inside a try-catch block.
  - Boss & Enemy Spawning (HandleGolemCommand / HandleSpawnCommand): Spawning the Golem via golem or spawn golem calls IBossEncounterService.SpawnGolemBoss(), which automatically dismisses any dormant GolemStatue (releasing its reserved grid cells) and applies the active tier's Golem prefab/config overrides. Spawning basic enemies resolves barrel, zombie [1-4], crawling [2-3], or exact prefab names against _swarmEnemySpawner.EnemyConfigs (which use shared Tier 1 prefab names with per-tier EnemyConfigSO overrides) and spawns up to 50 instances fanned laterally ahead of the player on TerrainLayers.Walkable.
  - Power-Up Collectible Spawning (HandlePowerUpCommand): Resolves the requested token (nuke; freeze/freezetimer/ice/timefreeze; shield/bubbleshield/bubble; armor/armour/plate/plates; firetails/fire/tails/napalm; crate/skillcrate/skill) against serialized prefab fields on GameplayDevCommandsRegistrar (falling back to AssetDatabase.LoadAssetAtPath in the Unity Editor if unassigned), computes a target landing point 8m ahead of the player car snapped to TerrainLayers.Walkable + POWERUP_SPAWN_Y_OFFSET (0.85f), and spawns the pooled collectible via ICollectibleDropNotifier.SpawnCollectible.
  - Tier Jumping (HandleLoadTierCommand): Parses a 1-based tier number within [1, _runSessionService.TotalTiersCount] and optional --reset / -r flag (accepted before or after the tier number). Guards _gameSceneLoader.IsLoading. Updates _runSessionService (calling CaptureSnapshotAndSetTier to preserve level, EXP, skills, upgraded stats, and timer by default, or ResetSessionToTier when --reset / -r is set) BEFORE calling _gameSceneLoader so LoadingOverlayPresenter displays "Loading Tier {N}..." on OnStartLoadingScene. Dismisses any active skill upgrade modal (HideForStageTransition), pauses ITimerPresenter, enables portal transition invulnerability, and loads or reloads the target tier scene.
  - Run-Scoped God Mode (HandleGodModeCommand): Parses optional state argument (on/true/1 or off/false/0; no argument toggles current state). Rejects invocation if the player is dead (!_playerManager.Health.IsAlive()). Updates _runSessionService.SetGodModeEnabled(targetState) (gated by IsDevCheatsSupported). When enabled while the player has CurrentHealth < 1f, calls _playerManager.Health.IncreaseHealth(minHealth - currentHealth) to align HP up to 1 HP. While active, PlayerDamagedHandler.TakeDamage preserves portal invulnerability -> Bubble Shield -> Armor -> base Health order and hit SFX/VFX/shake, clamping base health reduction at minimum 1 HP (PlayerDamageConstants.GOD_MODE_MIN_HEALTH) while TakeFullHpDamage() (DeathVolume) still kills the player. State persists across stage transitions within the run and resets on ResetSession().
  - Global Time Scale Control (HandleTimeScaleCommand): Without arguments, logs _gameTimeService.TargetTimeScale and usage syntax. With one argument, parses a finite float in [0.05, 100] using CultureInfo.InvariantCulture and calls _gameTimeService.SetTimeScale(parsedScale). Because the game is paused while the Dev Console is open, IGameTimeService stores TargetTimeScale and applies it to Time.timeScale when DevConsolePresenter.Hide() (or PausePresenter) resumes gameplay.
  - Console Close: InputCommandKind.DevConsoleClose or ToggleConsole calls Hide(). The presenter deactivates the visual panel, releases InputContextScopeKind.DevConsole, consumes active controls until release via IInputContexts.ConsumeActiveControlsUntilRelease(), and calls _gameTimeService.Resume() only if !_wasGamePausedBeforeOpen and _playerManager.Health.IsAlive().

## Rules and Invariants

- Release Build Stripping Invariant:
  - The developer console and run-scoped cheats must never expose functionality or visual UI in release distribution.
  - In release builds (!(UNITY_EDITOR || DEVELOPMENT_BUILD) or !Debug.isDebugBuild), DevConsolePresenter destroys its UI visuals in Awake(), GameplayDevCommandsRegistrar disables itself in Awake(), DevConsoleService registers zero commands, and RunSessionService.IsGodModeEnabled is unconditionally false.
  - Reflex DI bindings remain valid in all build profiles to prevent container resolution exceptions in downstream classes.
- Time Pause & Resume Invariant:
  - Opening the console must pause the game via _gameTimeService.Pause() after recording _wasGamePausedBeforeOpen = _gameTimeService.IsPaused || (Time.timeScale == 0f).
  - Closing the console must only call _gameTimeService.Resume() if !_wasGamePausedBeforeOpen AND the player car is alive. If the player died, DemoThankYouPresenter is open, or PausePresenter was open before the console, gameplay must remain paused.
  - Any TargetTimeScale multiplier set via timescale persists across closing the console and toggling PausePresenter (Esc), and resets to 1x when GameSceneLoader completes a scene load/reload or when GameplayDevCommandsRegistrar.OnDestroy() runs.
- Input Isolation Invariant:
  - While the console is open, InputContextScopeKind.DevConsole suppresses the Player, Upgrade, and UI maps in GameInputService while enabling the DevConsole and Global/ToggleConsole actions.
  - Upon closing, releasing the scope restores underlying context policy cleanly and ConsumeActiveControlsUntilRelease prevents Escape/Backquote from triggering Pause or underlying UI actions before key release.
  - Backquote (`) and tilde (~) characters must be stripped from input strings so the toggle hotkey does not leave stray characters in the input field.
- Bounded Memory Buffers:
  - Console log history is bounded to DEFAULT_MAX_LOG_LINES (100 lines) by trimming oldest lines.
  - Command navigation history is bounded to DEFAULT_MAX_HISTORY_COUNT (50 entries).
- Safe Exception Containment:
  - All command handler invocations are wrapped inside try-catch blocks within DevConsoleService.ExecuteCommand.
  - Faulty commands log a clear user-facing error message and forward the exception to Debug.LogException(ex), leaving the console functional.
- Dynamic Spawning Constraints:
  - Swarm enemy spawning computes positions forward along the vehicle heading, fans out laterally across instances, and snaps down to TerrainLayers.Walkable using Physics.Raycast.
  - Tower bosses (tower, mortar, mortartower, mortal, mortaltower, railgun, railguntower) are explicitly forbidden from dynamic spawning because they are pre-placed arena encounters requiring specific static map geometry.
  - Spawning clamps counts between 1 and MAX_SPAWN_COUNT (50).
- Auto-Upgrade Progression Invariant:
  - When the --auto (or -a) flag is passed to the exp command, ISkillUpgradePresenter.IsAutoUpgradeEnabled must suppress all UI modal panels, visual tween sequences, sound effects, and input delays.
  - Pending level progression is drained synchronously via IPlayerLevelPresenter.FastForward() and ISkillUpgradePresenter.AutoResolvePendingUpgrades().
  - IsAutoUpgradeEnabled must always be reset to false in a finally block to restore standard interactive UI behavior for regular gameplay.

## Supported Commands Reference

| Command Syntax | Arguments & Flags | Description | Example Usage |
| --- | --- | --- | --- |
| help | None | Lists all registered commands with their syntax and descriptions. | help |
| clear | None | Clears the console log viewer and internal log history. | clear |
| golem | None | Dismisses any dormant GolemStatue and spawns the active tier's Ancient Golem boss encounter immediately via IBossEncounterService. | golem |
| spawn <entity> [count] | <entity>: golem, barrel, zombie [1-4], crawling [2-3], or exact prefab name.<br>[count]: integer between 1 and 50 (default 1). | Spawns pooled enemies (if present in the active tier's spawner roster) or the Golem boss in front of the vehicle snapped to walkable ground. | spawn barrel 5<br>spawn zombie 3 3<br>spawn crawling 3 2<br>spawn golem |
| exp <amount>[k\|m\|b] [--auto] | <amount>: positive float with optional unit suffix (k, m, b).<br>--auto / -a: bypasses UI modals and auto-resolves level choices. | Grants player EXP with standard animation, or fast-forwards level visuals and auto-picks upgrades. | exp 20<br>exp 220 --auto<br>exp 22b -a |
| powerup <nuke\|freeze\|shield\|armor\|firetails\|crate> | <type>: nuke, freeze (or freezetimer, ice, timefreeze), shield (or bubbleshield, bubble), armor (or armour, plate, plates), firetails (or fire, tails, napalm), crate (or skillcrate, skill). | Spawns a pooled power-up or skill crate collectible 8m ahead of the player car via ICollectibleDropNotifier. | powerup nuke<br>powerup freeze<br>powerup shield<br>powerup armor<br>powerup firetails<br>powerup crate |
| loadtier <tier> [--reset] | <tier>: 1-based integer within 1..TotalTiersCount (currently 1 or 2).<br>--reset / -r: resets level, EXP, skills, timer, and god mode instead of carrying over current build. | Loads the specified map tier via IGameSceneLoader, preserving current player progress by default or starting fresh with --reset (-r). | loadtier 2<br>loadtier 1<br>loadtier 2 --reset<br>loadtier 1 -r |
| godmode [on\|off] | Optional state token: on, off, true, false, 1, 0 (omitting toggles current state). Rejected while player is dead. | Toggles or sets run-scoped player death protection (clamps normal damage to minimum 1 HP; aligns alive sub-1 HP up to 1 HP; DeathVolume still kills). | godmode<br>godmode on<br>godmode off |
| timescale [multiplier] | Optional float multiplier within [0.05, 100] (invariant dot decimal; omit to query current multiplier; use timescale 1 to reset). | Queries or sets global game time scale via IGameTimeService; persists across console/pause toggles and resets to 1x on scene reload. | timescale<br>timescale 2.5<br>timescale 0.5<br>timescale 1 |

## Extension Points

- Registering New Commands:
  - Follow .agents/skills/create-dev-console-command/SKILL.md.
  - Inject IDevConsoleService and invoke RegisterCommand(name, syntax, description, handler) in GameplayDevCommandsRegistrar.RegisterCommands().
  - Handlers accept string[] args containing tokens that follow the command name.
- Unregistering Commands on Teardown:
  - Components registering commands must call _devConsoleService.UnregisterCommand(name) in OnDestroy() or teardown hooks to avoid stale references across scene unloads.
- Adding New Spawnable Entities or Power-Ups:
  - Add entity recognition tokens in GameplayDevCommandsRegistrar.TryResolveSwarmTarget or collectible tokens in TryResolveCollectiblePrefab.
  - Ensure the target enemy prefab is configured in ISwarmEnemySpawner.EnemyConfigs for the active tier, or serialize the new collectible prefab on GameplayDevCommandsRegistrar and add its path constant to DevConsoleConstants.
- Adding New Metric Suffixes or Command Flags:
  - Declare tokens in Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs.
  - Parse tokens in domain registrar methods following the case-insensitive, culture-invariant parsing pattern used for --auto, --reset, [k|m|b], and timescale.
- Scene Wiring Automation:
  - For new gameplay scenes, execute menu item Tools -> Dev Console -> Setup Dev Console in Active Scene via Assets/Scripts/Editor/Tools/DevConsolePrefabBuilder.cs.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds IDevConsoleService, IDevConsolePresenter, and IGameplayDevCommandsRegistrar.
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs binds IGameTimeService, IRunSessionService, and IGameSceneLoader.
  - Centralized Input System services (IInputCommands, IInputContexts via GameInputService).
  - UnityEngine Physics raycasting against TerrainLayers.Walkable.
  - TextMeshPro (TextMeshProUGUI, TMP_InputField, TMP_Text) and Unity UI (ScrollRect, Image, CanvasScaler).
- Downstream consumers:
  - Assets/Scripts/GameFlow/GameTimeService.cs (Pause, Resume, SetTimeScale, Reset).
  - Assets/Scripts/GameFlow/RunSessionService.cs (CaptureSnapshotAndSetTier, ResetSessionToTier, SetGodModeEnabled).
  - Assets/Scripts/GameFlow/GameSceneLoader.cs (IsLoading, LoadNewSceneAsync, ReloadCurrentSceneAsync).
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs (SpawnGolemBoss()).
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs (TrySpawnEnemyAt()).
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs (SpawnCollectible()).
  - Assets/Scripts/Player/PlayerManager.cs (Health, LevelController, DamagedHandler, GameObject).
  - Assets/Scripts/LevelSystem/LevelController.cs (AddExp(), LevelData).
  - Assets/Scripts/UI/HUD/TimerPresenter.cs (TimerValue, SetPaused()).
  - Assets/Scripts/UI/Level/PlayerLevelPresenter.cs (FastForward()).
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs (IsAutoUpgradeEnabled, AutoResolvePendingUpgrades(), HideForStageTransition()).
- Cross-system coupling risks:
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs references multiple disparate systems (GameFlow, Bosses, Spawners, Collectibles, Level, Skills, Timer). It guards against null injected dependencies before executing each command, allowing the console service to operate even if a specific gameplay subsystem is absent in isolated test scenes.
  - In HandleLoadTierCommand, RunSessionService state must be updated before calling IGameSceneLoader so LoadingOverlayPresenter displays the new tier index on OnStartLoadingScene, and DevConsolePresenter.Hide() must NOT be called before scene reload (to avoid resuming time scale prematurely before scene unload).

## Known Risks and Open Questions

- Known limitations:
  - Tokenization in DevConsoleService.ExecuteCommand currently uses simple whitespace splitting (Split(' ')). Quoted multi-word string parameters (e.g. spawn "Ancient Golem" 5) are not natively grouped by quotes, requiring multi-token matching in GameplayDevCommandsRegistrar.
  - Dev console is currently scoped per gameplay scene under DefaultGameplaySceneInstaller and instantiated under ====UI==== in RuinedBloodCity.unity. It is not instantiated in Boot.unity or MainMenu.unity.
- Open design questions:
  - Should Dev Console be promoted to a persistent cross-scene service instantiated in BootLoader.cs with DontDestroyOnLoad so developer commands can be used in menus and test scenes?
  - Should an in-engine log redirect be added so Unity errors (Application.logMessageReceived) optionally output into the dev console log history window?
  - Should autocomplete / tab-completion be added to IDevConsolePresenter for command names and entity keywords?
- Suggested follow-up tasks:
  - Consider adding a simple quoted string tokenizer if complex parameterized commands with multi-word arguments are added in the future.
  - When creating new combat test scenes, use Tools -> Dev Console -> Setup Dev Console in Active Scene to ensure console presenter and registrar instances are present.
