# Increase Difficulty Totem and Map Spawning System Implementation Plan

**Date**: 2026-07-08

## Summary

This plan covers two main features:

1. **Interactive Difficulty Totem**: A one-use prefab component that detects the player in a custom circle radius, displays an "E" interaction hint UI canvas, deactivates the totem's visuals on usage, plays a VFX effect using `VFXPlayer`, and triggers an enemy spawn difficulty increase.
2. **Pre-Gameplay Map Spawner**: A system running before gameplay starts that randomly spawns interactable objects (like the totem) on valid grid cells based on parameterized rules (spawn count range, distance to closest impassable cell, and distance to closest spawned object of the same type).

> [!IMPORTANT]
> After implementing each phase of this plan, the completed changes and progress must be reflected in an implementation summary file under .agents/context/implementations/summaries folder.

> [!WARNING]
> **Asset & Prefab Read-Only Constraint**: All Unity asset files (such as `.prefab`, `.unity`, `.asset`, and `.meta` files) are strictly read-only for the agent. The agent will write and modify only C# scripts and markdown documentation. All inspector configurations, scene wiring, prefab modifications, and script attachments must be performed manually by the user in the Unity Editor as detailed in Phase 3.

---

## Phase 1: Interactive Totem System

### Key Changes

- Add `IEnemySpawnDifficultyController` interface in `Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs` (or in its own file) with:
  `void IncreaseSpawnChanceRedistributionFactor(float amount);`
- Update `EnemiesSpawner` to implement `IEnemySpawnDifficultyController` and delegate the increase to `EnemiesSpawnChanceRedistributionSystem`.
- Update `EnemiesSpawnChanceRedistributionSystem` to support accelerating redistribution:
  - Add a private float field `_redistributionFactorBonus` initialized to `0f`.
  - Implement `public void IncreaseSpawnChanceRedistributionFactor(float amount)` which adds `amount` to `_redistributionFactorBonus`.
  - In `RedistributeSpawnChance()`, calculate the spawn chance scalar as:
    `float spawnChanceScalar = _spawnChanceDecreaseFactor.GetRandomValueInRange() + _redistributionFactorBonus;`
    This scalar is then subtracted from the current enemy's spawn chance and distributed. This directly accelerates the shift of spawn chances from lower-tier to higher-tier enemy configurations.
- Register `_enemiesSpawner` as `IEnemySpawnDifficultyController` in `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`.
- Add `Assets/Scripts/Enemies/IncreaseDifficultyTotem.cs` component:
  - Inject `IPlayerManager` and `IEnemySpawnDifficultyController`.
  - Serialized fields:
    - `_interactionRadius` (float, default `3f`)
    - `_interactionCanvas` (GameObject for interaction HUD)
    - `_totemVisuals` (GameObject to hide on usage)
    - `_vfxPlayer` (VFXPlayer reference)
    - `_difficultyIncreaseAmount` (float, default `4f`)
  - Use `OnDrawGizmos` or `OnDrawGizmosSelected` inside `#if UNITY_EDITOR` to draw a wire sphere showing the interaction radius.
  - In `Update`:
    - Check distance to player: `Vector3.Distance(transform.position, _playerManager.GameObject.transform.position)`.
    - If player is within `_interactionRadius` and the totem has not been used:
      - Set `_interactionCanvas` to active.
      - On pressing `Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame` (guarded against `Keyboard.current` being null to avoid exceptions in headless/automated test/editor runs):
        - Trigger the spawn difficulty increase.
        - Deactivate `_interactionCanvas`.
        - Deactivate `_totemVisuals`.
        - Play VFX using `_vfxPlayer.Play(new VFXPlayConfig())`.
        - Set `_hasBeenUsed = true` and disable the component.
    - If player is not within `_interactionRadius` or totem is used:
      - Set `_interactionCanvas` to inactive.

---

## Phase 2: Map Spawning System

### Key Changes

- Add `Assets/Scripts/Spawners/MapInteractablesSpawner.cs` component:
  - Inject `IGridManager`.
  - Define a serializable `InteractableSpawnRule` class:
    - `_prefab` (GameObject)
    - `_minSpawnCount` (int)
    - `_maxSpawnCount` (int)
    - `_minDistanceToImpassable` (int, radius in grid cells)
    - `_minDistanceToSameType` (int, radius in grid cells)
  - Serialized field: `List<InteractableSpawnRule> _spawnRules`.
  - In `Start()`:
    - Retrieve `WorldGrid` from `IGridManager`. (Note: Since `GridManager` computes the `WorldGrid` cost field in `OnEnable()`, and `Start()` runs after `OnEnable()` of all active components, the cost field is guaranteed to be fully initialized without modifying `IGridManager` or `GridManager` interfaces).
    - Collect all walkable cells using `CellStatusDescriber.IsWalkable`.
    - Create a randomized copy of the walkable cells list.
    - For each `InteractableSpawnRule`:
      - Roll a target spawn count: `Random.Range(min, max + 1)`.
      - Maintain a list of `Vector2Int` spawned cell coordinates for the current prefab type.
      - Search the randomized walkable cells for valid spots:
        - **Impassable Distance Check**: Verify all cells within `_minDistanceToImpassable` grid cells in x and y coordinates are inside grid bounds and walkable (`IsWalkable`).
        - **Proximity to Same Type Check**: Verify no other spawned object coordinates of this type are within `_minDistanceToSameType` grid cells in x and y coordinates.
      - If both checks pass:
        - Instantiate the prefab at the cell's `WorldPos`.
        - Record the spawned coordinate.
        - Decrement target spawn count.
        - Stop checking candidates if target count is reached.
      - Log a warning if target count cannot be satisfied due to layout constraints.
- **Timing and DI Injection Guarantees**:
  - Running the spawner in `Start()` avoids touching `IGridManager` entirely, since the grid cost field is built during `GridManager.OnEnable()`.
  - It resolves the Reflex injection timing risk. Standard `Instantiate` calls made during `OnEnable()` occur before the Reflex container has finished initialization and scene object injection. Moving to `Start()` ensures the Reflex container is fully built and active, so `[Inject]` fields on the dynamically spawned prefabs are properly resolved on instantiation.
  - It runs before any gameplay waves begin, as `WaveManager` starts its wave spawning countdown in `Start()` (with a `_firstWaveDelay` of 1 second), and actual spawning occurs in `Update()`.

---

## Phase 3: Scene Setup and Installation

### Key Changes

> [!IMPORTANT]
> **To be performed by the User in the Unity Editor** (as prefabs and scenes are read-only for the AI agent):

- Create/update prefab for the difficulty totem:
  - Add visual models to a `Visuals` child GameObject.
  - Add a world-space interaction Canvas with the "E" prompt graphic.
  - Add a particle VFX GameObject with a `VFXPlayer` component.
  - Assign these references to the fields in the `IncreaseDifficultyTotem` component.
- Add a new GameObject in the gameplay scene with the `MapInteractablesSpawner` component.
- Configure spawning rules in the inspector (e.g. adding a rule for the difficulty totem prefab).

---

## Phase 4: Tests and Validation

### Automated Validation

- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` to verify compile success.

### Manual Editor Validation

- In the Unity Scene view (Edit Mode), verify the interaction radius gizmo is drawn around the difficulty totem instances.
- In Unity Play Mode, test the following:
  - **Interaction Radius Proximity**: Approaching the totem shows the "E" canvas; backing away hides it.
  - **Activation Action**: Pressing `E` while near the totem hides the visuals, plays the VFX, hides the canvas, and increases difficulty.
  - **One-Use Guarantee**: Confirm the totem cannot be triggered again and the canvas does not reappear.
  - **Spawning Rules Logic**: Check spawned positions of the totems to verify they are at least the specified cells away from obstacles (walls/rough terrain edges) and from other totems of the same type.
  - **Spawn Count Randomization**: Start play mode multiple times and verify that the number of spawned totems varies randomly between the min and max limits.

---

## Phase 5: Documentation

### Key Changes

- Once all systems are implemented, tested, and marked as completed, trigger the `document-system` skill.
- Create or update the technical documentation under `.agents/context/game-systems/difficulty-system.md` (or another appropriate system document file in that folder) describing:
  - The Interactive Totem's architecture, including its Reflex dependency injection, inputs, and visual states.
  - The Pre-Gameplay Map Spawning system rules, parameterization, and grid placement logic.

---

## Assumptions

- Spawning takes place on the world grid coordinate coordinates; spawned prefabs automatically participate in Reflex injection at runtime when instantiated in `Start()`.
- Input checks use standard Unity Input System `Keyboard.current.eKey` API with a null-guard.

---

## Future Considerations

- **Input Action Mapping**: Migrate from direct `Keyboard.current.eKey` API to Unity's input action mappings (e.g. `PlayerInput`) to support customizable key bindings and gamepad controller interactions.
- **Dynamic Interaction Prompts**: Update the canvas UI prompt to dynamically swap sprites based on the active control device (e.g., showing a controller button icon instead of "E").
- **Spawning Optimization / Object Pooling**: Currently, interactable map objects are spawned using standard `Instantiate()` at startup. If the scale of map interactables increases significantly, migrate to a pool-based structure (reusing `IPoolable` / `ObjectPool`).
- **Dynamic Spawning**: If map features change mid-game (e.g., dynamic obstacles destroying or altering grid passability), update the spawning logic to handle grid cost recalculation dynamically if objects need to spawn mid-level.
