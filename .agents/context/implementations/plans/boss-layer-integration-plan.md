# Implementation Plan - Boss Layer Integration

Date: 2026-08-25

Adapt weapon targeting, projectile overlap checks, trap/skill triggers, enemy collisions, and flow field navigation separation to recognize and support the new Boss layer alongside the standard Enemy layer.

## User Review Required

> [!IMPORTANT]
> - Ensure the Unity Physics Layer Collision Matrix in the Unity Editor (*Project Settings -> Physics -> Layer Collision Matrix*) enables collisions between the new `Boss` layer and `Player`, `Projectiles` (or `Default`), and `Terrain`/`Impassable`.
> - The new layer name in Unity Tag & Layer settings must match `"Boss"`.

## Open Questions

- None.

## Proposed Changes

### Layer Masks & Utilities

#### [MODIFY] Assets/Scripts/LayerMasks/EntityLayers.cs
- Add `BOSS = "Boss"` constant and `public static readonly LayerMask Boss = LayerMask.GetMask(BOSS);`.
- Add `public static readonly LayerMask Enemies = LayerMask.GetMask(ENEMY, BOSS);` to represent any hostile target (regular enemy or boss).
- Update `public static readonly LayerMask All = LayerMask.GetMask(ENEMY, BOSS, PLAYER);` to include the Boss layer.

#### [MODIFY] Assets/Scripts/Extensions/LayerMaskExtensions.cs
- Add `public static bool ContainsLayer(this LayerMask mask, int layer)` extension method to cleanly check if a layer is included in a layer mask: `(mask.value & (1 << layer)) != 0`.
- Add `public static bool Contains(this LayerMask mask, GameObject gameObject)` convenience overload.

---

### Player Weapons & Skills

#### [MODIFY] Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs
- Update target acquisition in `AssignNewTargets()` to use `EntityLayers.Enemies` instead of `EntityLayers.Enemy` so the laser turret acquires and tracks bosses.

#### [MODIFY] Assets/Scripts/Projectiles/Projectile.cs
- Update `HandleCollisions()` to use `EntityLayers.Enemies | TerrainLayers.Impassable` so bullets damage bosses upon collision.

#### [MODIFY] Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs
- Update `OnTriggerEnter` layer check to use `EntityLayers.Enemies.ContainsLayer(other.gameObject.layer)`.
- Update `Explode()` overlap sphere query to use `EntityLayers.Enemies` so explosion damage and knockback apply to bosses.

#### [MODIFY] Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs
- Update `OnTriggerEnter` layer check to use `EntityLayers.Enemies.ContainsLayer(other.gameObject.layer)`.

---

### Enemies & Navigation Systems

#### [MODIFY] Assets/Scripts/Enemies/Base/EnemyCollisionsController.cs
- Update collision classification to check `EntityLayers.Enemies.ContainsLayer(collider.gameObject.layer)` for other enemy collisions.

#### [MODIFY] Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
- Update flocking separation overlap query in `PreventEntitiesFromStackingOnEachOther()` to use `EntityLayers.Enemies` so regular enemies steer away from bosses instead of clipping through them.

#### [MODIFY] Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs
- Update `GetEnemyCountOnCell()` to use `EntityLayers.Enemies` so boss occupancy is counted during cell occupancy checks.

---

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp-firstpass.csproj
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. **Lasergun**: Spawn a boss on the `Boss` layer and verify the Lasergun turret acquires the boss as a target, rotates toward it, and deals damage.
2. **Minigun**: Fire minigun projectiles at the boss and verify damage numbers appear and boss HP decreases.
3. **Landmines & Saws**: Place landmines and drive saws into the boss; verify triggers activate and deal damage.
4. **Regular Enemies**: Verify regular enemies on `Enemy` layer still behave correctly, separate from bosses, and take damage from all weapons.
