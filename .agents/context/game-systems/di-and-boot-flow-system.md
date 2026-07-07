# DI and Boot Flow System Documentation

## Purpose

The DI and Boot Flow system owns Reflex container setup, boot-scene service registration, scene loading, and global time pause/resume helpers.

It is responsible for:

- Project-level service bindings that should survive scene changes.
- Scene-level gameplay and main-menu bindings.
- Boot-scene extra bindings injected into each scene container.
- Loading the first menu scene from the boot scene.
- Raising scene loading events for audio, UI, and navigation consumers.

It is not responsible for:

- Gameplay behavior owned by bound services.
- Unity scene asset setup beyond serialized installer references.
- Player-facing pause/death/skill-choice rules, although those rules call Assets/Scripts/GameFlow/GameTime.cs.

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
  - .agents/context/game-systems/settings-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs registers project-level singletons: Assets/Scripts/GameFlow/GameScenesLoader.cs (as `IGameSceneLoader`), Assets/Scripts/ScoreBoard/StoredScoreBoard.cs, Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs (as `IScoreBoardNewScoreSaver`), and Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs (as `IScoreBoardBestScoreGetter`).
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs registers menu-scoped settings services: `AudioVolumeSetting`, `GraphicSetting`, `FullScreenSetting`, `ResolutionSetting`, and `DamageNumbersSetting` (each bound to their `ISetting<TSelf, TValue>` and `ISettingLoader` interfaces).
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds scene references for player, UI presenters, grid manager, enemy spawner, exp particle spawner, and collectible spawner.
  - Assets/Scripts/ReflexDI/BootLoader.cs subscribes to `SceneScope.OnSceneContainerBuilding` and adds boot-scene audio and damage-number services to every scene container. It also resolves camera dependencies for Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs by locating Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs in loaded scenes.
  - Assets/Scripts/GameFlow/GameScenesLoader.cs wraps `SceneManager.LoadSceneAsync`, tracks `CurrentLoadedScene`, and raises load-start and load-completed events.
  - Assets/Scripts/GameFlow/GameTime.cs is a static helper that sets `Time.timeScale` to `0f` or `1f`.
- Runtime flow:
  - Boot scene creates the project Reflex container and injects `IGameSceneLoader` into `BootLoader`.
  - `BootLoader.Start` registers `InstallExtra` with `SceneScope.OnSceneContainerBuilding`.
  - After one `WaitForEndOfFrame`, `BootLoader` loads `GameScene.MainMenu`.
  - `GameSceneLoader.LoadNewSceneAsync` raises `OnStartLoadingScene`, starts a single-mode scene load, then resumes `GameTime`, updates `CurrentLoadedScene`, and raises `OnSceneLoaded` when Unity completes the load.
  - As scene containers are built, `BootLoader.InstallExtra` registers the serialized `AudioMixersManager`, `BackgroundAudioManager`, and `DamageNumbersSpawner` instances.
  - When initializing scene camera dependencies, `BootLoader` checks the loaded scene's root game objects for a Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs components, extracting the main camera reference and initializing `DamageNumbersSpawner` with it.

## Rules and Invariants

- Critical behavior rules:
  - Keep scene/runtime dependencies explicit through Reflex bindings where DI is already used.
  - Register gameplay-scene object references in Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs, menu settings in Assets/Scripts/ReflexDI/MainMenuInstaller.cs, project lifetime services in Assets/Scripts/ReflexDI/ProjectInstaller.cs, and cross-scene boot extras in Assets/Scripts/ReflexDI/BootLoader.cs.
  - Do not replace existing injected dependencies with singleton access, `FindAnyObjectByType`, or broad scene searches.
  - Preserve `GameScene` enum integer values unless Unity build settings and all call sites are intentionally updated together.
  - Treat changes to boot scene loading order, first loaded scene, and `GameTime.Resume` on scene completion as flow changes requiring Unity play-mode validation.
- Ordering or sequencing guarantees:
  - `OnStartLoadingScene` fires before the async scene operation completes.
  - `OnSceneLoaded` fires only from the async operation completion callback, after `GameTime.Resume()` and `CurrentLoadedScene` assignment.
  - Boot extra bindings are attached through `SceneScope.OnSceneContainerBuilding` before target scene injection completes.
  - `BootLoader.OnDisable` unsubscribes from `SceneScope.OnSceneContainerBuilding`.
- Constraints contributors must preserve:
  - Preserve serialized references in installers and boot scene objects.
  - Keep installer code limited to wiring; gameplay services should live in their owning folders.
  - Do not edit `.unity`, `.prefab`, `.asset`, or `.meta` files directly unless explicitly requested.

## Extension Points

- Safe extension areas:
  - Add a project-wide service in Assets/Scripts/ReflexDI/ProjectInstaller.cs when it should survive scene changes and has no scene-object dependency.
  - Add a gameplay scene service in Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs when it is a serialized scene object or gameplay-scene scoped dependency.
  - Add a menu-only setting or service in Assets/Scripts/ReflexDI/MainMenuInstaller.cs.
  - Add a boot-provided cross-scene MonoBehaviour service in Assets/Scripts/ReflexDI/BootLoader.cs's `InstallExtra` when it is owned by the boot scene and consumed by multiple scenes.
- Required dependencies and contracts:
  - New Reflex bindings should use existing narrow interfaces where possible.
  - New scene enum entries must match Unity build index assumptions in `SceneManager.LoadSceneAsync((int)scene, LoadSceneMode.Single)`.
  - Boot extras must remain valid serialized references in the boot scene.
- Testing implications:
  - Documentation-only changes need path/link review.
  - C# binding or scene-flow changes should compile with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, validate boot to main menu, menu to gameplay, scene reload, background audio switching, settings load, damage number setting, death reload, and pause state after scene loads.

## Integration Notes

- Upstream dependencies:
  - Reflex provides installer execution, container building, and injection.
  - Unity build settings must align with `GameScene` numeric values.
  - Boot scene serialized references provide audio and damage-number cross-scene services.
- Downstream consumers:
  - Menu buttons and death/reload UI call `IGameSceneLoader`.
  - `BackgroundAudioManager` listens to `IGameSceneLoader` events.
  - Settings consume boot-provided audio mixer and damage-number functionality services.
  - Gameplay systems consume scene-bound player, grid, spawner, and UI presenter interfaces.
- Cross-system coupling risks:
  - Moving `DamageNumbersSpawner` registration can break both enemy damage feedback and damage-number setting application.
  - Changing scene load event order can break background audio and UI assumptions.
  - Assets/Scripts/GameFlow/GameTime.cs is static and global; pause/resume calls from pause UI, skill UI, death UI, and scene loading can override each other.

## Known Risks and Open Questions

- Known limitations:
  - Assets/Scripts/GameFlow/GameTime.cs has no pause reason stack, so the last caller wins.
  - Assets/Scripts/GameFlow/GameScenesLoader.cs's `CurrentLoadedScene` defaults to `MainMenu` before any scene load completes.
  - Scene loading depends on enum integer values rather than scene names or typed build-setting validation.
  - Assets/Scripts/ReflexDI/BootLoader.cs loads main menu after one frame; scene setup that assumes a longer boot initialization window must be verified in Unity.
- Open design questions:
  - Should pause state become an injected service with reason tracking if more systems pause independently?
  - Should scene references move from enum integer values to explicit scene asset/name configuration?
  - Should boot extras be split into dedicated installers if more global services are added?
- Suggested follow-up tasks:
  - Add a boot-flow checklist for Unity scene/build-settings validation.
  - Consider centralizing pause ownership before adding more pause-capable overlays.
