# Projectiles System Documentation

## Purpose

The Projectiles system owns shared projectile runtime behavior, projectile spawn payloads, and projectile ScriptableObject configuration.

It is responsible for:

- Initializing projectile runtime values from `ProjectileConfigSO`.
- Moving initialized projectiles in a fixed direction.
- Applying projectile damage and piercing rules to enemies and impassable terrain overlaps.
- Ending projectile life by range, collision, or pool return.
- Providing events used by projectile pools and projectile-owning skills.

It is not responsible for:

- Skill firing cadence, target choice, turret rotation, muzzle VFX, or shooting audio.
- Enemy health implementation or damage-number spawning.
- Pool ownership; current pooling lives in `MinigunTurret`.
- Final projectile balance values stored in ScriptableObject assets.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Projectiles/Projectile.cs
  - Assets/Scripts/Projectiles/ProjectileSpawnConfig.cs
  - Assets/ScriptableObjects/ProjectileConfigSO.cs
- Current projectile owner:
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs
  - Assets/Scripts/Skills/Turret.cs
- Related code:
  - Assets/Scripts/StatusEffects/EntityManipulationHelper.cs
  - Assets/Scripts/LayerMasks/EntityLayers.cs
  - Assets/Scripts/LayerMasks/TerrainLayers.cs
  - Assets/Scripts/Extensions/TransformTweenExtensions.cs
- Related docs:
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/pooling-and-object-lifecycle-system.md
  - .agents/context/game-systems/status-effects-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md

## Architecture and Data Flow

- Core components:
  - `ProjectileConfigSO` stores serialized starting damage, size, speed, range, max piercing, and disappearance duration. Damage and max piercing are `int` values. `OnEnable` copies starting values into mutable runtime properties.
  - `ProjectileSpawnConfig` carries world position, rotation, movement direction, and projectile config for spawner-style projectile creation.
  - `Projectile` is a `MonoBehaviour` implementing `IInitializableWithScriptableConfig<ProjectileConfigSO>` and `IPoolable`.
- Runtime flow:
  - A projectile owner gets a projectile from its pool and calls `Projectile.OnGet()`.
  - The owner positions and rotates the projectile, then calls `SetMovementDirection`.
  - The owner calls `Initialize(config)`, which assigns config, resets pierce count (`_piercedCounter = config.MaxPiercing`), scales the projectile (`new Vector3(config.Size, config.Size, transform.localScale.y)`), and marks it initialized.
  - `FixedUpdate` moves only while `_isAlive` and `_isInitialized` are true.
  - `OnTriggerEnter` delegates to `HandleCollisions`, which performs `Physics.OverlapSphere` at `transform.position + _sphereCollider.center` using `_sphereCollider.radius` against `EntityLayers.Enemy | TerrainLayers.Impassable`.
  - Each non-null overlap target is passed to `EntityManipulationHelper.Damage`.
  - Piercing decrements until it reaches zero; then the projectile marks `_isAlive = false` and raises `OnLifeEnd`.
  - Range expiration plays a shrink animation using `transform.DOScale(Vector3.zero, _config.DisapearingDuration).SetEase(Ease.Flash)` and raises `OnLifeEnd` upon completion.
  - Pool owners listen to `OnLifeEnd` and `OnCanBeReleased`, release the projectile, and call `OnRelease`.

## Rules and Invariants

- Critical behavior rules:
  - Projectiles do not move or collide meaningfully until initialized and alive.
  - `SetMovementDirection` rejects `Vector3.zero`; owners should avoid firing if no valid direction exists.
  - `OnRelease` kills active shrink DOTween tweens on the projectile transform, clears initialization, and restores `_startScale`.
  - `ReturnToPool` calls `OnRelease` and raises `OnCanBeReleased`.
  - Collision damage uses capability lookup through `IDamageable`; projectile code should not directly depend on concrete enemy types.
  - Projectile size, speed, range, damage, and piercing are player-facing balance values.
  - Damage and max piercing are no longer byte-limited in code; validate asset values and UI assumptions before relying on byte-size bounds.
- Ordering or sequencing guarantees:
  - `_startScale` is captured in `Start`; pooled prefabs should have their expected starting scale before first release.
  - `OnLifeEnd` is raised before the current pool owner releases the projectile.
  - `OnCanBeReleased` is the forced pool-return signal used by `DeathVolume` and direct `ReturnToPool` paths.
- Constraints contributors must preserve:
  - Preserve serialized field names and `ProjectileConfigSO` asset compatibility.
  - Keep projectile lifetime event-driven so pool owners can unsubscribe and decrement counts exactly once.
  - Do not add hidden balance constants in projectile code; use config assets or explicit serialized fields.
  - Do not edit projectile assets or prefabs directly unless explicitly requested.

## Extension Points

- Safe extension areas:
  - Add a projectile-owning skill by using `ProjectileSpawnConfig`, `ProjectileConfigSO`, and existing pool lifecycle events.
  - Add new projectile visuals through prefab setup while preserving `Projectile` lifecycle and collider references.
  - Add new projectile config fields only when asset serialization and UI/balance review are intentional.
- Required dependencies and contracts:
  - Projectile prefabs require a configured `SphereCollider`.
  - Pool owners must subscribe and unsubscribe to projectile life/release events around pool get/release.
  - Projectile-based damage requires target colliders to expose `IDamageable`.
- Testing implications:
  - Compile after C# changes.
  - In Unity, validate projectile spawn direction, range end, collision end, piercing count, tween completion, pool reuse scale reset, and forced return through `DeathVolume`.
  - For physics changes, test enemy and impassable terrain layer masks.

## Integration Notes

- Upstream dependencies:
  - Skill configs and `TurretConfigSO` provide projectile config.
  - Minigun currently owns the projectile pool and uses `ProjectilesHolder` tag lookup from `Turret<TConfig>.Awake`.
  - DOTween drives range-expiration disappearance.
- Downstream consumers:
  - Enemies receive damage through `IDamageable`.
  - Pool owners consume `OnLifeEnd` and `OnCanBeReleased`.
  - Spawner docs treat turret/projectile creation as world-space spawning semantics, but projectiles are not currently scene-level DI spawners.
- Cross-system coupling risks:
  - Projectile config runtime properties are mutable and can be changed by skill upgrade configs; damage and piercing upgrades now use int-backed stats/config values.
  - Current projectile collision scans all overlapping enemy and impassable colliders on trigger entry, so collider/layer setup directly changes damage behavior.
  - Pool count stability depends on owners not releasing the same projectile twice from both `OnLifeEnd` and `OnCanBeReleased`.

## Known Risks and Open Questions

- Known limitations:
  - `ProjectileSpawnConfig.ProjectileConfigSO` is populated by `MinigunTurret` but `Projectile.Initialize` currently receives `_config.ProjectileStatsSO` directly.
  - `Projectile.MoveProjectileInDirection(Vector3 direction)` ignores its `direction` parameter and uses `_movementDir`.
  - `Projectile.OnTriggerEnter` does not use the `other` collider directly; it runs a fresh overlap sphere at `transform.position + _sphereCollider.center`.
  - `ProjectileConfigSO.DisapearingDuration` contains a spelling error that is part of the current public API.
  - `Projectile.OnLifeEnd` and `OnCanBeReleased` can both be subscribed to the same release handler by current pool owners; double-release paths should be reviewed when changing lifecycle.
- Open design questions:
  - Should projectile pooling move to a shared projectile spawner if more skills fire projectiles?
  - Should collision handling distinguish terrain impact from enemy piercing more explicitly?
  - Should projectile config runtime mutation be copied per run or per skill instance instead of mutating a ScriptableObject instance?
- Suggested follow-up tasks:
  - Review projectile release paths for duplicate release safety.
  - Decide whether `ProjectileSpawnConfig.ProjectileConfigSO` should become authoritative in all projectile spawner paths.
