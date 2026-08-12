# Enemies System Documentation

## Purpose

The Enemies system owns runtime enemy entities, enemy pooling and spawning, enemy movement via flow fields, contact-based melee attacks, death presentation, experience particle payout, collectible item drop scattering, and recycling enemies back into their pools.

It does not own wave timing, grid generation, flow-field direction calculation, player health semantics, skill targeting, projectile behavior, or experience collection. Those systems consume enemy contracts or provide services that enemies depend on.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs
  - Assets/Scripts/Enemies/Base/EnemyAnimator.cs
  - Assets/Scripts/Enemies/Base/EnemyCollisionsController.cs
  - Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
- Designer-authored data:
  - Assets/ScriptableObjects/Enemy/EnemyConfigSO.cs
  - Enemy prefabs referenced by `EnemiesSpawner._poolEnemiesInfo`
- Related systems:
  - Wave timing: Assets/Scripts/Waves/WaveManager.cs
  - Grid and off-camera spawn cells: Assets/Scripts/Navigation/GridSystem/
  - Flow-field movement: Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
  - Health: Assets/Scripts/HealthSystem/Health.cs
  - Status effects: Assets/Scripts/StatusEffects/StunController.cs
  - Damage numbers and VFX: Assets/Scripts/DamageNumbers/, Assets/Scripts/VFX/
  - Experience particles: Assets/Scripts/LevelSystem/Exp/
  - Collectible items: Assets/Scripts/UI/Skills/ (e.g. `SkillUpgradeButton.cs`)
  - DI installer: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - `Enemy`: The aggregate runtime component. Implements `IHealthy`, `IDamageable`, `IKnockable`, `IStunnable`, and `IPoolable`. It exposes `Health`, `StunController`, `CollisionsController`, `MovementController`, `AudioClipPlayer`, `EnemyAnimator`, and `Config`. On `TakeDamage`, it spawns floating damage numbers via `_damageNumbersSpawner` (Hemisphere shape), reduces health, and plays blood VFX (`_bloodVfxPlayer`) if still alive.
  - `EnemyMovementController`: Moves enemies along grid flow fields via `FlowFieldMovementController`. Handles knockbacks (`MoveToPositionInTimeIgnoringSpeed`) using DOTween (`SetEase(Ease.OutSine)`) and sphere-casting against `TerrainLayers.Impassable` with a safety buffer (`OBSTACLE_SAFETY_BUFFER` = 0.1f) to prevent wall clipping. Pauses movement during attack animations and for `_movementDelayAfterAttack` (0.2s) post-attack. Smoothly rotates facing toward movement direction (`_enemy.Config.RotationSpeed`).
  - `EnemyCollisionsController`: Periodically sphere-casts against `EntityLayers.All` and emits `OnCollisionWithPlayer` events when player colliders are detected.
  - `EnemyAttackController`: Listens to `OnCollisionWithPlayer`. Validates target range (`_attackRange`), attack arc angle (`_attackArcAngle`, default 60°), and line-of-sight raycast against `TerrainLayers.All`. Triggers attack animation via `EnemyAnimator` and applies `Enemy.Config.Damage` to the target on `OnAttackHitFrame`.
  - `EnemyDeathHandler`: Implements `INeedToCompleteBeforeDisable`. Listens to `Health.OnNoHealth`. Disables collider and sets Rigidbody `isKinematic = true`, hides visual transform (`_visual`), plays death VFX (`_deathVfxPlayer`) and death SFX (`"Death"`), spawns experience particles via `_expParticleSpawner`, and raises `OnCompleted` when both VFX and audio finish (`_startEffectsToFinish` = 2).
  - `EnemyDropHandler`: Attached to enemy prefabs. Listens to `Health.OnNoHealth`. Evaluates configured `CollectibleDropEntry` drop percentages. Calculates scattered target drop positions on walkable grid cells (verifying direct line steps or performing a spiral search on `WorldGrid` via `_gridManager`), and delegates drop spawning to `_dropNotifier.SpawnCollectible`.
  - `EnemyAnimator`: Bridges Animator parameters and animation events into `IAttackAnimationPlayer` events (`OnAttackAnimationStart`, `OnAttackHitFrame`, `OnAttackAnimationEnd`).
  - `EnemiesOutsidePlayerChunkTeleporter`: Periodically detects enemies outside the active player chunk and teleports them to random off-camera walkable cells.
  - `EnemiesSpawner`: Manages Unity `ObjectPool<Enemy>` instances per `EnemySpawnInfo`, chooses enemy types via weighted probability, places enemies on off-camera walkable cells outside the player chunk during standard waves or inside the player chunk during swarms (with optional `_swarmSpawnVfxPrefab`), pre-warms pools during `Start`, and tracks `CurrentlySpawnedObjectsCount`.
  - `EnemiesSpawnChanceRedistributionSystem`: Mutates `EnemySpawnInfo.SpawnChanceInfo.SpawnChance` after standard spawn batches so higher-difficulty enemies progressively increase in weight.
- Key interfaces:
  - `IOnRandomGridPosSpawner<EnemiesSpawner>`: DI-facing spawn contract used by `WaveManager`.
  - `ISwarmEnemySpawner`: Injected into `SwarmSpawner` for retrieving enemy configurations and spawning targeted enemy types.
  - `IEnemySpawnDifficultyController`: Allows map objects (e.g. `IncreaseDifficultyTotem`) to boost spawn chance redistribution speed.
  - `IPoolable`: Defines enemy pool lifecycle (`OnGet`, `ReturnToPool`, `OnRelease`) and `OnCanBeReleased` signal.
  - `IHealthy`, `IDamageable`, `IKnockable`, `IStunnable`: Target interfaces for damage, knockback, and status effects.
  - `INeedToCompleteBeforeDisable`: Delays pool release until death presentation callbacks complete.
- Runtime flow:
  - **Spawn**: `EnemiesSpawner` retrieves an `Enemy` instance from its pool, calls `Enemy.OnGet`, subscribes to `Enemy.OnCanBeReleased`, positions the enemy on a walkable grid cell, activates its GameObject, and increments `CurrentlySpawnedObjectsCount`.
  - **Movement & Attack**: The enemy follows flow-field vectors toward the player. When `EnemyCollisionsController` detects player contact, `EnemyAttackController` checks range, arc angle, and obstacle occlusion. If valid, it triggers an attack animation, halting movement. On the animation's hit frame, `Damage` is applied to the player.
  - **Damage & Death**: Incoming damage spawns a floating damage number, decreases health, and plays blood VFX. On death (`Health.OnNoHealth`), `EnemyDeathHandler` disables colliders/physics, hides visuals, plays death VFX and audio, spawns EXP particles, while `EnemyDropHandler` calculates walkable drop positions and spawns collectible item drops. Once death VFX and SFX complete, `EnemyDeathHandler` raises `OnCompleted`, triggering `Enemy.OnCanBeReleased`.
  - **Release**: `EnemiesSpawner` handles release by calling `Enemy.OnRelease`, deactivating the GameObject, raising `OnSpawnedEntityReleased`, and decrementing `CurrentlySpawnedObjectsCount`.

## Rules and Invariants

- Critical behavior rules:
  - Spawned enemies must come from configured pool entries; prefabs are pre-warmed during `Start()`.
  - Standard wave spawning places enemies on walkable cells outside the main camera view and outside the player chunk.
  - Enemy health resets to `EnemyConfigSO.MaxHealth` upon retrieval from the pool.
  - Enemy death presentation path (VFX + SFX) must complete before pool release occurs.
  - Damage is applied on `EnemyAnimator.OnAttackHitFrame`, not on initial collision detection.
  - `EnemyDropHandler` ensures collectible item drops land only on walkable grid cells by stepping along vectors or performing spiral cell searches.
- Ordering or sequencing guarantees:
  - Pools are created in `Awake` and pre-warmed in `Start` before wave spawning begins.
  - `EnemyMovementController` pauses flow-field navigation during attack animations and for 0.2s post-attack.
  - `EnemyDeathHandler` requires both VFX and audio completion events before invoking `OnCompleted`.
- Constraints contributors must preserve:
  - Keep the scene spawner bound through Reflex DI instead of static singletons or scene queries.
  - Preserve inspector-driven setup for enemy configs, VFX, colliders, audio, and pool limits.
  - Keep player-facing balance values in `EnemyConfigSO` or explicit serialized fields.

## Extension Points

- Safe extension areas:
  - Add new enemy types by creating an enemy prefab with required components, configuring an `EnemyConfigSO`, and adding an `EnemySpawnInfo` entry in `EnemiesSpawner`.
  - Add new item drops by adding `CollectibleDropEntry` rules to `EnemyDropHandler` on the enemy prefab.
  - Tune movement speed, rotation speed, damage, max health, and EXP reward values in `EnemyConfigSO`.
- Required dependencies and contracts:
  - Enemy prefabs require `Enemy`, `EnemyMovementController`, `EnemyAttackController`, `EnemyDeathHandler`, `EnemyDropHandler`, `EnemyAnimator`, `EnemyCollisionsController`, `FlowFieldMovementController`, `RegenativeHealth` (or `Health`), `StunController`, `Collider`, `Rigidbody`, and visual/VFX components.
  - `EnemyDeathHandler` requires Reflex injection for `IInWorldSpaceSpawner<ExpParticleSpawner, float>`.
  - `Enemy` requires Reflex injection for `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`.
  - `EnemyDropHandler` requires Reflex injection for `ICollectibleDropNotifier`, `IGridManager`, and `DropAnimationConfiguration`.
- Testing implications:
  - Compile changes via `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Validate pool pre-warming, off-camera spawn placement, flow-field movement, attack hit-frame damage, death VFX/SFX completion, EXP particle spawning, and item drop scattering in Unity Play Mode.

## Integration Notes

- Upstream dependencies:
  - `WaveManager` drives standard enemy spawn batches via `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - `SwarmSpawner` drives swarm event enemy spawns via `ISwarmEnemySpawner`.
  - `IGridManager` supplies world grid state for spawning, flow fields, teleporters, and drop placement.
- Downstream consumers:
  - Player receives damage through `IDamageable`.
  - Skills and projectiles detect enemies via `EntityLayers.Enemy`.
  - `LevelController` receives XP from spawned experience particles.
  - `CollectibleDropNotifier` handles collectible item drops.
  - `WaveManager` monitors `CurrentlySpawnedObjectsCount`.

## Known Risks and Open Questions

- Known limitations:
  - In `EnemyMovementController`, `_isStunnable` defaults to `false` and is not toggled, meaning stun controller state is ignored during standard movement unless `_isStunnable` is updated.
  - `EnemyDeathHandler` expects exactly two completion callbacks (`_startEffectsToFinish` = 2). If either VFX or SFX fails to complete, the enemy remains unreleased in active play.
  - In `EnemiesSpawner`, `Enemy_OnRelease` calls `OnEnemyRelease` directly rather than invoking `pool.Release(enemy)`. While active flags and counters update correctly, Unity `ObjectPool` internal stack tracking should be noted.
- Open design questions:
  - Should `_isStunnable` in `EnemyMovementController` be serialized or linked dynamically to `IStunController`?
  - Should spawn chance redistribution operate on runtime copies so inspector asset values remain pristine across Play Mode sessions?

