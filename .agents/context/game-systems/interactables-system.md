# Interactables System Documentation

## Purpose

The Interactables system is responsible for spawning interactive map objectives onto the game board on scene startup and managing their lifecycle, player proximity checks, UI prompts, interaction mechanics, animation flows, and reward side-effects.

In Car Survivors, map interactables include:
1. **Difficulty Totems (`IncreaseDifficultyTotem`)**: Proximity-triggered interactable objects that accept player input (e.g. 'E' key) to permanently boost enemy spawn chance redistribution speed and wave difficulty.
2. **Capture Points (`CapturePoint`)**: Proximity-driven objective zones that track player car residence within a capture radius, animate expanding/outline ground planes via DOTween, decay progress when the player leaves, swap mesh materials upon acquisition, and reward a skill upgrade upon reaching 100% completion.
3. **Map Interactables Spawner (`MapInteractablesSpawner`)**: Scene component that evaluates spatial rules to place interactables on walkable grid cells during scene initialization.

It does not own grid coordinate generation or cell walkability logic (owned by Grid System) or enemy combat behavior.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs
  - Assets/Scripts/Interactables/CapturePoint/CapturePoint.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
- Related code:
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
- Related docs:
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - MapInteractablesSpawner: Scene-bound spawner that reads `InteractableSpawnRule` configs to place interactables at scene start. Queries `IGridManager` for walkable cells, shuffles candidates, checks spatial distance rules (distance to impassable cells, distance to other interactables, distance to same type), instantiates prefabs, and injects dependencies via Reflex.
  - InteractableSpawnRule: Serialized configuration holding prefab references, min/max spawn counts, and distance constraints.
  - IncreaseDifficultyTotem: Interactive totem component. Measures player distance, displays an interaction Canvas prompt when in range, listens for 'E' key press, calls `IEnemySpawnDifficultyController.IncreaseSpawnChanceRedistributionFactor`, triggers VFX, and deactivates itself.
  - CapturePoint: Proximity-based area objective. Tracks player presence inside `_captureRadius` using squared distance (`sqrMagnitude`), scales an expanding ground circle plane, triggers outline plane pop-in/pulse/shrink animations via DOTween, decays progress when the player exits, swaps target mesh materials upon 100% capture, plays capture VFX, and queues a random skill upgrade request via `ISkillUpgradeFlow`.
- Key interfaces:
  - `IPlayerManager`: Injected dependency used by interactables to query player position and `SkillsRegistry`.
  - `IEnemySpawnDifficultyController`: Injected into `IncreaseDifficultyTotem` to modify enemy spawn chance weights.
  - `ISkillUpgradeFlow`: Injected into `CapturePoint` to queue skill upgrades upon capture completion.
  - `IGridManager`: Injected into `MapInteractablesSpawner` to fetch walkable cells.
- Runtime flow:
  - **Spawning**: On scene `Start()`, `MapInteractablesSpawner` fetches walkable cells from `IGridManager.WorldGrid`, shuffles them, filters candidates using rules, instantiates interactable prefabs, and recursively injects Reflex dependencies via `GameObjectInjector.InjectRecursive`.
  - **Totem Interaction**: `IncreaseDifficultyTotem.Update` measures distance to player. When within `_interactionRadius`, it displays `_interactionCanvas`. If 'E' is pressed (`Keyboard.current.eKey.wasPressedThisFrame`), it increases difficulty factor, hides canvas, triggers VFX, and sets `enabled = false`.
  - **Capture Point Execution**: `CapturePoint.Update` measures squared distance to player. While inside radius, `_progress` increases over `_captureDurationSeconds` and `_expandingCirclePlane` scales up. An outline circle plane pops in with `Ease.OutBack`. If the player leaves, progress decays based on `_decaySpeedMultiplier` and the outline scales down. Upon reaching 1.0 progress:
    1. `_isCaptured` becomes true and progress locks.
    2. Ground and outline circle planes shrink to zero scale via DOTween before deactivating.
    3. Target mesh materials (`_targetRenderer.materials`) swap to `_capturedMaterial1` and `_capturedMaterial2`.
    4. Skill upgrade request is queued via `ISkillUpgradeFlow.QueueRandomSkillUpgradeRequest`.
    5. Capture VFX plays and `CapturePoint` disables itself (`enabled = false`).

## Rules and Invariants

- Critical behavior rules:
  - Spawning occurs strictly on walkable cells during scene `Start()`.
  - Distance checks in `CapturePoint` use squared magnitude (`sqrMagnitude <= _captureRadius * _captureRadius`) to avoid square root calculations.
  - Capture progress is strictly clamped between 0.0 and 1.0.
  - Dependency injection for dynamically spawned interactables must use `Reflex.Injectors.GameObjectInjector.InjectRecursive`.
  - Completed interactables set `enabled = false` to stop `Update` execution.
  - Active DOTween scale tweens must be killed in `OnDestroy` to avoid missing target warnings or memory leaks.
- Constraints contributors must preserve:
  - Do not use static singletons or `FindAnyObjectByType` to resolve dependencies; inject `IPlayerManager`, `ISkillUpgradeFlow`, etc., via Reflex.
  - Clean up UI prompts or canvases when objects are deactivated.

## Extension Points

- **Adding Interactables**: Create a new MonoBehaviour (or prefab), add an entry under `_spawnRules` in `MapInteractablesSpawner` in the Inspector, and configure spatial constraints.
- **Capture Point Visuals**: Customize `_captureRadius`, `_captureDurationSeconds`, `_decaySpeedMultiplier`, `_maxCircleScale`, `_outlineCirclePlane`, and captured materials on the `CapturePoint` Inspector fields.

## Integration Notes

- Upstream dependencies:
  - `IGridManager`: Supplies cell walkability for spawning.
  - `IPlayerManager`: Supplies player position.
  - Reflex DI: Injects dependencies on dynamically spawned prefabs.
- Downstream consumers:
  - `SkillUpgradePresenter`: Displays skill upgrade modal UI when `CapturePoint` completes its reward flow.
  - `EnemiesSpawnChanceRedistributionSystem`: Receives difficulty multiplier increases from totems.

## Known Risks and Open Questions

- **Interaction Key Rebinding**: `IncreaseDifficultyTotem` directly checks `Keyboard.current.eKey`, which bypasses the Unity Input System actions asset and does not support gamepad binding out of the box.
