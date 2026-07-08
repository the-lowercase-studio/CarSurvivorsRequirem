# Increase Difficulty Totem and Map Spawning System Implementation Summary

**Date:** 2026-07-08
**Status:** Completed

## Summary

Implemented a range-based, interactive difficulty-increasing totem prefab component and a pre-gameplay map interactables spawner. The spawner executes before gameplay starts, placing objects on valid random walkable cells using distance constraint parameters (proximity to obstacles and same-type interactables).

## Key Changes

### Phase 1: Interactive Totem System
- Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Added the IEnemySpawnDifficultyController interface colocated above EnemiesSpawner.
  - Implemented the IEnemySpawnDifficultyController interface on EnemiesSpawner to delegate calls to the EnemiesSpawnChanceRedistributionSystem.
- Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Added a private float _redistributionFactorBonus field.
  - Implemented the IncreaseSpawnChanceRedistributionFactor(float amount) method.
  - Updated the RedistributeSpawnChance() method to add _redistributionFactorBonus to the spawn chance decrease scalar, which accelerates lower-to-higher tier configuration progression.
- Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Registered the EnemiesSpawner under the new IEnemySpawnDifficultyController interface.
- Assets/Scripts/Enemies/IncreaseDifficultyTotem.cs
  - Implemented the interactive totem component class, injecting IPlayerManager and IEnemySpawnDifficultyController.
  - Configured range-based UI canvas display and E key activation using UnityEngine.InputSystem.Keyboard.current.
  - Included a editor-only wire sphere Gizmo that displays only when not playing.

### Phase 2: Map Spawning System
- Assets/Scripts/Spawners/MapInteractablesSpawner.cs
  - Created the MapInteractablesSpawner component and its serializable InteractableSpawnRule rules class.
  - In Start(), retrieves WorldGrid, filters walkable cells using CellStatusDescriber.IsWalkable, shuffles them, and evaluates distance constraints.
  - Applies a check for impassable cells and checks minimum distance between spawned items of the same type.
  - Added a serialized field for _spawnParent to parent spawned interactables inside the Unity scene hierarchy.
  - Applies a randomized Y rotation to spawned objects upon instantiation.

## Verification Plan & Execution

### Automated Compile Checks
- Compiled successfully via dotnet build with 0 errors.

### Manual Verification Required (Unity Editor)
- Configure the totem prefab visuals, interaction canvas, and VFX player, then link them to the IncreaseDifficultyTotem component.
- Add MapInteractablesSpawner to the Boot/Gameplay scene, define a spawn rule for the difficulty totem prefab, and verify:
  - Totems are spawned on walkable tiles, at least MinDistanceToImpassable cells away from obstacles.
  - Spawned totems of the same type are placed at least MinDistanceToSameType cells away from each other.
  - Approaching a totem displays the Canvas overlay and backing away hides it.
  - Pressing E near a totem hides the visuals, plays the VFX, deactivates the canvas, and increases the difficulty.
