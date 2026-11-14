# Dev Console System Documentation

## Purpose

The Dev Console system provides an in-game developer Command Line Interface (CLI) for executing runtime debugging routines, cheat commands, entity and collectible spawning, and progression manipulation during gameplay in Unity Editor play mode and Development builds.

It is responsible for:
- Providing a runtime command registry, tokenization engine, and log history buffer through Assets/Scripts/UI/DevConsole/DevConsoleService.cs.
- Presenting a toggleable overlay console UI with scrollable output, prompt input, and Up/Down arrow history navigation through Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs.
- Isolating input and pausing gameplay time safely when open (GameTime.Pause() / GameTime.Resume()) by acquiring InputContextScopeKind.DevConsole through IInputContexts and consuming prioritized commands from IInputCommands.
- Enforcing release-build security and zero overhead in production builds by destroying UI visual trees, disabling components, and neutralizing command registration when not running in the Unity Editor or development builds.
- Registering standard console commands (help, clear) and gameplay domain commands (golem, spawn <entity> [count], exp <amount>[k|m|b] [--auto], powerup <nuke|freeze|shield|armor|firetails|crate>) through Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs.
- Calculating ground-snapped, forward-fanned world positions for dynamic entity and collectible spawning.
- Orchestrating automated level progression and skill resolution (--auto / -a flag) by fast-forwarding level presentation and auto-selecting upgrades without opening modal UI dialogs.

It is not responsible for:
- Shipping player-facing cheat menus or console interfaces in release builds.
- Defining enemy combat behavior, boss phase logic, power-up mechanics, or health initialization (delegated to boss encounter services, enemy spawner pools, and power-up services).
- Persisting state across game sessions (it is a non-persisted diagnostic tool).
- Global cross-scene lifecycle persistence outside configured gameplay scenes.

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
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/UI/Level/PlayerLevelPresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/power-ups-system.md
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/create-dev-console-command/SKILL.md
  - .agents/skills/document-system/SKILL.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/UI/DevConsole/DevCommandInfo.cs is a lightweight read-only struct containing Name, Syntax, and Description metadata for command discovery.
  - Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs holds domain constants for buffer limits (DEFAULT_MAX_LOG_LINES = 100, DEFAULT_MAX_HISTORY_COUNT = 50), EXP unit multipliers (1K, 1M, 1B), spawn defaults (distance 8f, lateral spacing 2f, max count 50), auto flags (--auto, -a), powerup command metadata (POWERUP_COMMAND_NAME = "powerup", POWERUP_COMMAND_SYNTAX = "powerup <nuke|freeze|shield|armor|firetails|crate>", POWERUP_SPAWN_Y_OFFSET = 0.85f), collectible prefab asset paths (NUKE_PREFAB_PATH, FREEZE_TIMER_PREFAB_PATH, BUBBLE_SHIELD_PREFAB_PATH, ARMOR_PREFAB_PATH, FIRE_TAILS_PREFAB_PATH, SKILL_CRATE_PREFAB_PATH), rich text hex colors (#FFCC00, #FF4444), and prompt prefix (> ).
  - Assets/Scripts/UI/DevConsole/DevConsoleService.cs implements IDevConsoleService. It maintains a case-insensitive dictionary of command handlers, an ordered list of registered commands for help output, and a bounded ring buffer of log entries. It raises OnLogAppended and OnLogsCleared.
  - Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs is a MonoBehaviour implementing IDevConsolePresenter. It handles the visual console canvas, prioritized InputCommandKind handling via IInputCommands (ToggleConsole, DevConsoleClose, DevConsoleSubmit, DevConsoleHistoryPrevious, DevConsoleHistoryNext), DevConsole context scope acquisition/release via IInputContexts, LateUpdate deferred single-path command submission and input-field refocusing, and game pausing via GameTime.Pause() / GameTime.Resume().
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs is a MonoBehaviour implementing IGameplayDevCommandsRegistrar. It registers gameplay commands (golem, spawn, exp, powerup) into IDevConsoleService on Start() and removes them on OnDestroy().
  - Assets/Scripts/Editor/Tools/DevConsolePrefabBuilder.cs builds the UI prefab hierarchy with TextMeshPro components, dark semi-transparent panel, scrollable log viewer, and input field, and exposes editor menu items to wire active scenes automatically.
- Key interfaces:
  - IDevConsoleService: Manages command registration, lookup, exception-safe execution, logging (Log, LogWarning, LogError, Clear), and query properties (IsEnabled, RegisteredCommands, LogHistory).
  - IDevConsolePresenter: Exposes visibility and support status (IsSupported, IsVisible) and state control methods (Toggle(), Show(), Hide()).
  - IGameplayDevCommandsRegistrar: Marker interface for DI binding and lifecycle containment.
- Runtime flow:
  - Dependency Registration: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs registers DevConsoleService as singleton IDevConsoleService, DevConsolePresenter as IDevConsolePresenter, and GameplayDevCommandsRegistrar as IGameplayDevCommandsRegistrar.
  - Build Support Evaluation: Presenter, Registrar, and Service check compile-time and runtime flags (#if UNITY_EDITOR || DEVELOPMENT_BUILD with Debug.isDebugBuild). In release builds, DevConsolePresenter destroys its root visual and deactivates itself in Awake(), GameplayDevCommandsRegistrar disables itself in Awake(), and DevConsoleService operates in a dormant inert mode.
  - Startup Registration: In supported environments, DevConsoleService registers built-in commands help and clear upon construction. In Start(), GameplayDevCommandsRegistrar registers golem, spawn, exp, and powerup.
  - User Toggle: InputCommandKind.ToggleConsole triggers DevConsolePresenter.Toggle(). Show() notes whether time was already paused, pauses gameplay via GameTime.Pause(), acquires InputContextScopeKind.DevConsole (which disables Player, Upgrade, and Pause UI cancellation while enabling DevConsole actions), activates the UI panel, focuses the input field, and scrolls to bottom.
  - Command Submission: InputCommandKind.DevConsoleSubmit schedules _hasPendingSubmit for LateUpdate (single submit path without subscribing to TMP_InputField.onSubmit), sanitizes input (stripping backquote and tilde characters), appends unique entries to _commandHistory (up to 50 items), and calls ExecuteCommand(trimmed).
  - Command Execution: DevConsoleService logs the input prompt (> command), splits tokens by whitespace, resolves the command name case-insensitively, extracts arguments, and executes the associated Action<string[]>. Any unhandled exception thrown by handlers is captured, logged as red [ERROR], and reported to Debug.LogException without crashing the game.
  - Power-Up Collectible Spawning (HandlePowerUpCommand): Resolves the requested token (nuke; freeze/freezetimer/ice/timefreeze; shield/bubbleshield/bubble; armor/armour/plate/plates; firetails/fire/tails/napalm; crate/skillcrate/skill) against serialized prefab fields on GameplayDevCommandsRegistrar (falling back to AssetDatabase.LoadAssetAtPath in the Unity Editor if unassigned), computes a target landing point 8m ahead of the player car snapped to TerrainLayers.Walkable + POWERUP_SPAWN_Y_OFFSET (0.85f), and spawns the pooled collectible via ICollectibleDropNotifier.SpawnCollectible.
  - History Navigation: InputCommandKind.DevConsoleHistoryPrevious and DevConsoleHistoryNext (Up and Down arrow keys) navigate through past submitted commands, populating the input field and setting caret position to the end.
  - Console Close: InputCommandKind.DevConsoleClose or ToggleConsole calls Hide(). The presenter deactivates the visual panel, releases InputContextScopeKind.DevConsole, consumes active controls until release via IInputContexts.ConsumeActiveControlsUntilRelease(), and resumes gameplay (GameTime.Resume()) only if the game was not previously paused before opening and the player car is alive (_playerManager.Health.IsAlive()).

## Rules and Invariants

- Release Build Stripping Invariant:
  - The developer console must never expose functionality or visual UI in release distribution.
  - In release builds (!(UNITY_EDITOR || DEVELOPMENT_BUILD) or !Debug.isDebugBuild), UI visuals are destroyed in Awake(), components are disabled, and DevConsoleService registers zero commands and rejects all input silently.
  - Reflex DI bindings remain valid in all build profiles to prevent container resolution exceptions in downstream classes.
- Time Pause & Resume Invariant:
  - Opening the console must pause the game via GameTime.Pause().
  - Closing the console must only call GameTime.Resume() if the game was NOT paused prior to opening AND the player car is alive. If the player died or a pause menu was active, gameplay must remain paused.
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
  - Tower bosses (`tower`, `mortar`, `mortartower`, `mortal`, `mortaltower`, `railgun`, `railguntower`) are explicitly forbidden from dynamic spawning because they are pre-placed arena encounters requiring specific static map geometry.
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
| golem | None | Spawns the Ancient Golem boss encounter immediately via IBossEncounterService. | golem |
| spawn <entity> [count] | <entity>: golem, barrel, zombie [1-4], crawling [2-3], or exact prefab name.<br>[count]: integer between 1 and 50 (default 1). | Spawns pooled enemies or boss encounters in front of the vehicle snapped to walkable ground. | spawn barrel 5<br>spawn zombie 2 3<br>spawn crawling 3 2<br>spawn golem |
| exp <amount>[k|m|b] [--auto] | <amount>: positive float with optional unit suffix (k, m, b).<br>--auto / -a: bypasses UI modals and auto-resolves level choices. | Grants player EXP with standard animation, or fast-forwards level visuals and auto-picks upgrades. | exp 20<br>exp 220 --auto<br>exp 22b -a |
| powerup <nuke\|freeze\|shield\|armor\|firetails\|crate> | <type>: nuke, freeze (or freezetimer, ice, timefreeze), shield (or bubbleshield, bubble), armor (or armour, plate, plates), firetails (or fire, tails, napalm), crate (or skillcrate, skill). | Spawns a pooled power-up or skill crate collectible 8m ahead of the player car via ICollectibleDropNotifier. | powerup nuke<br>powerup freeze<br>powerup shield<br>powerup armor<br>powerup firetails<br>powerup crate |

## Extension Points

- Registering New Commands:
  - Inject IDevConsoleService and invoke RegisterCommand(name, syntax, description, handler).
  - Handlers accept string[] args containing tokens that follow the command name.
- Unregistering Commands on Teardown:
  - Components registering commands must call _devConsoleService.UnregisterCommand(name) in OnDestroy() or teardown hooks to avoid stale references across scene unloads.
- Adding New Spawnable Entities or Power-Ups:
  - Add entity recognition tokens in GameplayDevCommandsRegistrar.TryResolveSwarmTarget or collectible tokens in TryResolveCollectiblePrefab.
  - Ensure the target enemy prefab is configured in ISwarmEnemySpawner.EnemyConfigs or serialize the new collectible prefab on GameplayDevCommandsRegistrar and add its path constant to DevConsoleConstants.
- Adding New Metric Suffixes or Command Flags:
  - Declare tokens in Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs.
  - Parse tokens in domain registrar methods following the case-insensitive parsing pattern used for --auto and [k|m|b].
- Scene Wiring Automation:
  - For new gameplay scenes, execute menu item Tools -> Dev Console -> Setup Dev Console in Active Scene via Assets/Scripts/Editor/Tools/DevConsolePrefabBuilder.cs.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds IDevConsoleService, IDevConsolePresenter, and IGameplayDevCommandsRegistrar.
  - Centralized Input System services (IInputCommands, IInputContexts via GameInputService).
  - UnityEngine Physics raycasting against TerrainLayers.Walkable.
  - TextMeshPro (TextMeshProUGUI, TMP_InputField, TMP_Text) and Unity UI (ScrollRect, Image, CanvasScaler).
  - Assets/Scripts/GameFlow/GameTime.cs for pause and resume control.
- Downstream consumers:
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs (SpawnGolemBoss()).
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs (TrySpawnEnemyAt()).
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs (SpawnCollectible()).
  - Assets/Scripts/Player/PlayerManager.cs (Health, LevelController, GameObject).
  - Assets/Scripts/LevelSystem/LevelController.cs (AddExp(), LevelData).
  - Assets/Scripts/UI/Level/PlayerLevelPresenter.cs (FastForward()).
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs (IsAutoUpgradeEnabled, AutoResolvePendingUpgrades()).
- Cross-system coupling risks:
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs references multiple disparate systems (Bosses, Spawners, Collectibles, Level, Skills). It guards against null injected dependencies before executing each command, allowing the console service to operate even if a specific gameplay subsystem is absent in test scenes.
  - DevConsolePresenter relies on DevConsole and Global/ToggleConsole actions registered in Assets/InputSystem/InputSystem_Actions.inputactions and managed by GameInputService.

## Known Risks and Open Questions

- Known limitations:
  - Tokenization in DevConsoleService.ExecuteCommand currently uses simple whitespace splitting (Split(' ')). Quoted multi-word string parameters (e.g. spawn "Ancient Golem" 5) are not natively grouped by quotes, requiring heuristic token matching in GameplayDevCommandsRegistrar.
  - Dev console is currently scoped per gameplay scene under DefaultGameplaySceneInstaller and instantiated under ====UI==== in RuinedBloodCity.unity. It is not instantiated in Boot.unity or MainMenu.unity.
  - Console toggle and navigation actions are bound to keyboard controls (and Escape / Start close if mapped) in the DevConsole / Global action maps of InputSystem_Actions.inputactions.
- Open design questions:
  - Should Dev Console be promoted to a persistent cross-scene service instantiated in BootLoader.cs with DontDestroyOnLoad so developer commands can be used in menus and test scenes?
  - Should an in-engine log redirect be added so Unity errors (Application.logMessageReceived) optionally output into the dev console log history window?
  - Should autocomplete / tab-completion be added to IDevConsolePresenter for command names and entity keywords?
- Suggested follow-up tasks:
  - Consider adding a simple quoted string tokenizer if complex parameterized commands with multi-word arguments are added in the future.
  - When creating new combat test scenes, use Tools -> Dev Console -> Setup Dev Console in Active Scene to ensure console presenter and registrar instances are present.
