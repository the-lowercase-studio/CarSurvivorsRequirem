# Plan: Update Enemies, Waves, Spawners, and Projectiles System Documentation

This plan outlines the updates to the four gameplay system documents under `.agents/context/game-systems/`:
1. [enemies-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/enemies-system.md)
2. [waves-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/waves-system.md)
3. [spawners-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/spawners-system.md)
4. [projectiles-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/projectiles-system.md)

These updates ensure that the system documentation matches the actual codebase implementation, includes the undocumented **Swarm System** integration, and highlights critical architectural issues (such as the enemy object pool release leak and wave size truncation).

## User Review Required

> [!IMPORTANT]
> The documentation updates will highlight two critical bugs discovered during code auditing:
> 1. **Enemy Pooling Leak**: `EnemiesSpawner` does not call `Release` on the Unity `ObjectPool<Enemy>` when releasing an enemy; it directly calls internal release logic. This prevents actual object recycling and forces new instantiations every time.
> 2. **Wave Size Truncation**: `WaveManager` performs `ushort` truncation on wave size growth: `_maxEnemiesInWave = (ushort)(_maxEnemiesInWave * _maxEnemiesInWaveMultiplier)`. With a base of `4` and multiplier of `1.2f`, `4 * 1.2 = 4.8`, which truncates back to `4`. This causes the wave size to be stuck at `4` permanently.
> No code changes are proposed in this task, only documentation synchronization.

## Proposed Changes

### Documentation Updates

---

#### [MODIFY] [enemies-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/enemies-system.md)
- Remove the inaccurate claim under "Known Risks and Open Questions" that `EnemyAttackController.OnEnable` subscribes via inline lambdas and leaks handlers. The actual C# code correctly unsubscribes in `OnDisable()`.
- Add documentation for `ISwarmEnemySpawner` (implemented by `EnemiesSpawner`) and its role in the Swarm system.
- Document the `ObjectPool<Enemy>.Release` leak under "Known Risks and Open Questions" as a critical performance concern.

---

#### [MODIFY] [waves-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/waves-system.md)
- Add documentation for the `IWaveFreezer` interface implemented by `WaveManager`. Explain that the wave progression can be frozen (e.g., by the Swarm system) by setting `IsFrozen = true`.
- Update the known limitations section to detail the ushort wave size multiplication bug (`4 * 1.2 = 4` truncation) which prevents waves from growing beyond size 4.

---

#### [MODIFY] [spawners-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/spawners-system.md)
- Add `SwarmSpawner` to "Current concrete implementations" (referencing `Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs`).
- Document the Swarm system's flow, timing, UI warning, and spawning mechanism.
- Add the `ObjectPool<Enemy>.Release` leak as a critical known risk.

---

#### [MODIFY] [projectiles-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/projectiles-system.md)
- Audit the document and confirm it aligns with the C# implementation of `Projectile.cs` and `ProjectileConfigSO.cs`.
- Make minor phrasing updates to keep the reading map and terminology fully synchronized.

## Verification Plan

### Manual Verification
- Review the links, filenames, and markdown formatting of all modified files to ensure accuracy.
- Ensure that the documented behaviors match the actual C# code.
