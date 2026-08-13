# Collisions and Physics System Documentation

## Purpose

The Collisions and Physics system manages collision detection, physics layer masks, trigger-based damage and environmental volume mechanics, and entity collision queries across gameplay entities.

It is responsible for:

- Caching and exposing physics layer masks for entities (`EntityLayers`) and terrain (`TerrainLayers`).
- Defining generic collision contracts (`ICollisionsController` and `CollisionEventArgs`) for entity interaction.
- Executing periodic collision detection for enemies (`EnemyCollisionsController`) via physics queries.
- Handling instant death and object recycling volumes (`DeathVolume`).
- Standardizing trigger contact behaviors for projectiles, melee saw blades, landmine traps, crates, and experience particles.

It is not responsible for:

- Physics-based vehicle movement or driving physics forces (owned by Player Car system).
- Flow field grid generation or pathfinding algorithms (owned by Grid and Flow Field systems, though they query `TerrainLayers`).
- Health reduction or damage calculations (owned by Health system).

## Reading Map

- Primary code locations:
  - Assets/Scripts/Collisions/ICollisionsController.cs
  - Assets/Scripts/Collisions/CollisionEventArgs.cs
  - Assets/Scripts/Enemies/Base/EnemyCollisionsController.cs
  - Assets/Scripts/LayerMasks/EntityLayers.cs
  - Assets/Scripts/LayerMasks/TerrainLayers.cs
  - Assets/Scripts/Volumes/DeathVolume.cs
  - Assets/Scripts/Projectiles/Projectile.cs
  - Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticle.cs
- Designer-authored data:
  - Physics collision matrix settings in Unity Project Settings (`Physics.matrix`).
  - Layer assignments on prefabs and scene geometry ("Enemy", "Player", "Impassable", "RoughTerrain", "Ground").
  - Trigger colliders configured on enemy objects, player vehicles, weapons, and pickup items.
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/projectiles-system.md
  - .agents/context/game-systems/interactables-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/project-coding-standards.md

## Architecture and Data Flow

- Core components:
  - `EntityLayers`: Static helper caching `LayerMask` instances for "Enemy", "Player", and `All` ("Enemy" + "Player") via `LayerMask.GetMask(...)`.
  - `TerrainLayers`: Static helper caching `LayerMask` instances for "Impassable", "RoughTerrain", "Ground", and `All` ("Impassable" + "RoughTerrain" + "Ground").
  - `ICollisionsController`: Interface defining events for `OnCollisionWithOtherEnemy` and `OnCollisionWithPlayer`.
  - `EnemyCollisionsController`: Attached to enemy prefabs (`[RequireComponent(typeof(Enemy))]`). On `Awake`, collects all child trigger colliders into `_colliders`. On `OnEnable`, starts `InvokeRepeating` for `HandleCollisionsCheck` every `_collisionCheckDelay` (default 0.05s). On `OnDisable`, cancels the invocation. Performs `Physics.SphereCastAll` using `_collisionRadius` against `EntityLayers.All`. Ignores colliders in `_colliders`, evaluates target layer via `1 << layer`, and invokes `OnCollisionWithOtherEnemy` or `OnCollisionWithPlayer`.
  - `DeathVolume`: `MonoBehaviour` with `[RequireComponent(typeof(BoxCollider))]`. On `OnTriggerEnter`, checks if entering object implements `IDamageable` to call `TakeFullHpDamage()`, or `IPoolable` to call `ReturnToPool()`.
  - Contact & Trigger Mechanics:
    - `Projectile`: Uses `OnTriggerEnter` to detect target colliders matching enemy/player layers, applies damage via `IDamageable`, and returns to pool (`IPoolable`).
    - `SawBlade`: Rotating skill collider triggering `IDamageable.TakeDamage` on contact with enemy entities.
    - `Landmine`: Trap trigger invoking explosion damage on nearby entities when stepped on.
    - `SkillCrate` & `ExpParticle`: Detect player contact via `OnTriggerEnter` to award skill upgrades or EXP.
- Key interfaces:
  - `ICollisionsController`: Exposes `OnCollisionWithOtherEnemy` and `OnCollisionWithPlayer` events.
  - `IDamageable`: Consumed by collision volume triggers (`DeathVolume`, weapons, projectiles) to apply damage.
  - `IPoolable`: Consumed by volume triggers to return objects to pools upon out-of-bounds contact.

## Rules and Invariants

- Critical behavior rules:
  - `EntityLayers` and `TerrainLayers` rely on exact string matching with Unity Editor physics layer names ("Enemy", "Player", "Impassable", "RoughTerrain", "Ground"). Changing layer names in Unity Editor requires updating these string constants.
  - `EnemyCollisionsController` MUST exclude all child triggers of its owning enemy object during `Physics.SphereCastAll` inspection to prevent self-collision false positives.
  - `DeathVolume` MUST be attached to a GameObject containing a `BoxCollider` configured as a trigger (`isTrigger = true`).
- Ordering or sequencing guarantees:
  - `EnemyCollisionsController` uses `InvokeRepeating` starting at `0f` with an interval of `_collisionCheckDelay` (default 0.05s).
  - Physics trigger events (`OnTriggerEnter`) fire during Unity's internal physics simulation step prior to `Update`.
- Constraints contributors must preserve:
  - Maintain physics layer assignments on prefabs and scene geometry.
  - Preserve interface checking (`TryGetComponent<IDamageable>`, `TryGetComponent<IPoolable>`) inside collision handlers rather than hardcoding concrete component types.

## Extension Points

- Safe extension areas:
  - Adding new entity or terrain layers by declaring static properties in `EntityLayers` or `TerrainLayers`.
  - Creating new volume hazard scripts (e.g., slow zones, poison areas) following `DeathVolume` pattern using `OnTriggerEnter` and interface detection.
  - Implementing `ICollisionsController` on custom entity types.
- Required dependencies and contracts:
  - New physics layers must be created in Unity Project Settings before referencing in `LayerMask.GetMask()`.
- Testing implications:
  - C# compile check: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Unity Play Mode testing: Verify enemy player-attack triggers, enemy-enemy separation/collision events, projectile hit detection, crate/EXP collection on contact, and death volume out-of-bounds recycling.

## Integration Notes

- Upstream dependencies:
  - Unity 3D Physics Engine (`Physics.SphereCastAll`, `OnTriggerEnter`, `LayerMask.GetMask`).
  - Unity Project Physics Layer setup.
- Downstream consumers:
  - `EnemyAttackController` (listens to `OnCollisionWithPlayer` from `ICollisionsController`).
  - `GridSystem` & `FlowFieldSystem` (query `TerrainLayers` for obstacle and path cost evaluation).
  - `HealthSystem` (`IDamageable` interface implementation).
  - `PoolingSystem` (`IPoolable` interface implementation).
- Cross-system coupling risks:
  - Renaming or deleting physics layers in Unity Editor without updating `EntityLayers`/`TerrainLayers` will cause `LayerMask.GetMask` to return 0 (empty mask), silently disabling collision filtering across combat, spawning, and navigation.

## Known Risks and Open Questions

- Known limitations:
  - `EnemyCollisionsController` uses `InvokeRepeating` with `Physics.SphereCastAll` polling rather than native physics `OnTriggerEnter`/`OnCollisionEnter` callbacks. At high enemy counts, sphere casting every 0.05s per enemy creates garbage allocations (`RaycastHit[]` array allocations and `CollisionEventArgs` instantiations).
  - `1 << collider.gameObject.layer == EntityLayers.Enemy` checks equality against a layer mask value. Bitwise AND comparison `((1 << layer) & mask) != 0` is recommended if multi-layer masks are evaluated.
- Suggested follow-up tasks:
  - Consider `Physics.SphereCastNonAlloc` or native trigger callbacks in `EnemyCollisionsController` to reduce GC allocations during high-density enemy waves.

