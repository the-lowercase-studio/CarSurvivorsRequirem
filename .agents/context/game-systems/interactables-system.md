# Interactables System Documentation

## Purpose

The Interactables system is responsible for spawning interactive objects onto the game board and managing their life cycle, input handling, and side effects.

It is responsible for:
- Spawning interactive objects (such as difficulty-increasing totems) onto walkable grid cells on scene startup.
- Checking player proximity to interactable objects.
- Presenting interaction UI prompts when a player is within range.
- Processing player interaction inputs (such as pressing the 'E' key) and executing target behaviors (e.g., increasing wave spawn difficulty).
- Deactivating interactables post-interaction, executing visual feedback (VFX), and resolving object state.

It is not responsible for:
- Managing the underlying grid coordinates or computing cell walkability.
- Controlling enemy behavior directly, only adjusting spawn chance redistribution parameters.
- Handling player driving physics or movement mechanics.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
- Related code:
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Providers/IGameObjectProvider.cs
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/enemies-spawn-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - MapInteractablesSpawner: A scene-bound component that reads InteractableSpawnRule configs to instantiate interactable prefabs at start. It queries the GridManager for walkable cells, shuffles them, filters candidates using spawn constraints, instantiates prefabs, and injects dependencies via Reflex.
  - InteractableSpawnRule: A serialized class holding configuration data for a specific interactable prefab, including spawn counts (min/max), minimum distance to impassable (unwalkable) cells, minimum distance to other interactables, and minimum distance to other spawned objects of the same type.
  - IncreaseDifficultyTotem: A concrete interactable component that manages proximity checks with the player manager, displays/hides an interaction Canvas, listens for user keyboard input, applies difficulty adjustments, triggers VFX, and disables its update loop on activation.
- Key interfaces:
  - The system does not currently define a generic interface for interactables or their spawners. Custom interactables are implemented as separate components (e.g., IncreaseDifficultyTotem is a standalone MonoBehaviour).
- Runtime flow:
  1. On scene Start(), MapInteractablesSpawner initializes.
  2. The spawner fetches the list of walkable cells from IGridManager.WorldGrid.
  3. Walkable cells are shuffled randomly to prevent predictable patterns.
  4. For each configured InteractableSpawnRule, the spawner attempts to place the prefab on candidates. It ensures cells are not too close to impassable/blocked cells, any existing interactable spawns across all rules, or existing spawns of the same type (using grid-cell distance).
  5. The spawner instantiates matching prefabs at the cell's world position.
  6. The spawner injects dependencies recursively into the spawned game object and its children using the scene's Reflex Container (Reflex.Injectors.GameObjectInjector.InjectRecursive(spawnedObject, _container)).
  7. When a player drives near IncreaseDifficultyTotem (within _interactionRadius), the totem displays _interactionCanvas.
  8. If the player presses 'E' (Keyboard.current.eKey.wasPressedThisFrame), the totem increases difficulty via IEnemySpawnDifficultyController.IncreaseSpawnChanceRedistributionFactor(_difficultyIncreaseAmount), hides the canvas/visuals, triggers a VFXPlayer instance, and disables its own MonoBehaviour (enabled = false).

## Rules and Invariants

- Critical behavior rules:
  - Spawning must only occur on walkable cells.
  - Interactables must respect spatial constraints: minimum distance to impassable blocks, minimum distance to any other interactable, and minimum spacing between identical interactable types.
  - Dependency injection for spawned interactables must always be done immediately after instantiation using Reflex's GameObjectInjector.InjectRecursive.
  - Interactables must check for a valid player GameObject reference (_playerManager.GameObject) before performing distance checks.
- Constraints contributors must preserve:
  - Keep player-facing interactions decoupled from direct player code; interactables check player distance and local keyboard state themselves.
  - Avoid using FindAnyObjectByType or static singletons to resolve dependencies; always inject IPlayerManager and other services via Reflex.
  - Always clean up the interaction UI (deactivate _interactionCanvas) when the interactable is used or disabled (OnDisable()).

## Extension Points

- Safe extension areas:
  - Creating new types of interactables by writing new MonoBehaviour classes and creating matching prefabs.
  - Adding new rules under MapInteractablesSpawner's _spawnRules list in the Unity inspector.
- Required dependencies and contracts:
  - Interactables that need player or enemy state must define [Inject] fields for dependency injection.
  - Interactables require proximity detection (typically measuring Vector3.Distance to the player position in Update()).
- Testing implications:
  - Compile-check: Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` to ensure changes compile.
  - Play-test: Verify interactables spawn on the map correctly (e.g. inside RuinedBloodCity). Check if the UI prompt appears when close, disappears when far, and disappears after interacting. Confirm the difficulty modification or other side-effects execute properly.

## Integration Notes

- Upstream dependencies:
  - IGridManager: Used by MapInteractablesSpawner to inspect layout walkability.
  - IPlayerManager: Used by individual interactables to track player position.
  - IEnemySpawnDifficultyController: Used by IncreaseDifficultyTotem to modify enemy spawning weights.
  - Reflex DI: Used to resolve dependencies on dynamically spawned prefabs.
- Downstream consumers:
  - Individual interactable components that trigger effects.
  - Player HUD/UI: Screens or overlays displayed for interactables.
- Cross-system coupling risks:
  - High dependency on Keyboard.current directly from the Unity Input System, which assumes active keyboard input and doesn't support gamepad binding naturally.
  - Spawner directly reads Grid data at startup, which requires grid initialization to be completed first (ensure correct execution/startup order).

## Known Risks and Open Questions

- Known limitations:
  - Lack of a common IInteractable abstraction makes it harder to have a generic Player interaction component or generic interaction manager. Each interactable handles its own distance detection and keyboard input checks.
  - Interaction key 'E' is hardcoded within IncreaseDifficultyTotem.cs instead of using the Unity Input System actions, meaning it cannot easily be rebound via player settings.
- Open design questions:
  - Should the game transition to a centralized interaction manager (e.g., player vehicle has an interaction trigger/raycast and presses interaction key, triggering the nearest IInteractable) rather than each interactable polling player distance in Update?
  - Should we bind spawning of interactables to wave transitions or allow mid-game spawning? Currently spawning is only performed once at scene start.
