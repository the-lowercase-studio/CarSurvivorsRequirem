# Increase Difficulty Totem Implementation Plan

## Summary

Add an `IncreaseDifficultyTotem` prefab component that detects the player in trigger range and, on pressing `E`, increases enemy spawn chance redistribution difficulty by a serialized additive interval, default `4f`.

The totem is one-use per instance and uses direct `Keyboard.current.eKey` input.

## Key Changes

- Add `IEnemySpawnDifficultyController` above `EnemiesSpawner` with `void IncreaseSpawnChanceRedistributionFactor(float amount);`.
- Update `EnemiesSpawner` to implement the interface and delegate the increase to `EnemiesSpawnChanceRedistributionSystem`.
- Update `EnemiesSpawnChanceRedistributionSystem` to store an additive redistribution bonus and use random serialized factor plus accumulated bonus.
- Do not immediately redistribute spawn chances on activation; the effect applies on future spawn redistributions.
- Register `_enemiesSpawner` in `DefaultGameplaySceneInstaller` as `IEnemySpawnDifficultyController`.
- Add `Assets/Scripts/Enemies/IncreaseDifficultyTotem.cs`.
- Inject `IEnemySpawnDifficultyController` into `IncreaseDifficultyTotem`.
- Use trigger enter/exit with `EntityLayers.Player`.
- In `Update`, when the player is in range and `Keyboard.current.eKey.wasPressedThisFrame`, apply the increase once.
- Set `_hasBeenUsed = true` and disable the component after activation.
- Document the implemented system under `.agents/context/`, including prefab setup, DI dependency, input behavior, and how the difficulty increase affects enemy spawn chance redistribution.

## Tests And Validation

- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- In Unity Play Mode, confirm player outside range pressing `E` does nothing.
- In Unity Play Mode, confirm player inside range pressing `E` increases the redistribution factor once.
- In Unity Play Mode, confirm repeated `E` presses on the same totem do not stack.
- In Unity Play Mode, confirm multiple separate totems can each apply their own increase.
- In Unity Play Mode, confirm enemy spawn chance redistribution still runs after waves and spawns without DI errors.
- Review the new `.agents/context/` documentation for accurate file paths and behavior.

## Assumptions

- No input asset change: use `Keyboard.current.eKey` directly.
- One-use means the script disables itself, but the prefab GameObject is not destroyed or visually changed unless a later request adds used-state visuals.
- The totem prefab has a trigger collider on the same GameObject and is placed/injected in the gameplay scene through existing Reflex scene injection.
- Existing unrelated modified files are left untouched.
