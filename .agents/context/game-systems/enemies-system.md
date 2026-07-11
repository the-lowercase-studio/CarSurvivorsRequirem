# Enemies System Documentation

## Purpose

The Enemies system owns runtime enemy entities, enemy pooling and spawning, enemy movement toward the player, contact-based melee attacks, death presentation, experience payout, and recycling enemies back into their pools.

It does not own wave timing, grid generation, flow-field direction calculation, player health semantics, skill targeting, projectile behavior, or experience collection. Those systems consume enemy contracts or provide services that enemies depend on.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs
  - Assets/Scripts/Enemies/Base/EnemyAnimator.cs
  - Assets/Scripts/Enemies/Base/EnemyCollisionsController.cs
  - Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs
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
  - DI installer: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - `Enemy` is the aggregate runtime component. It exposes health, stun, collision, movement, audio, animator, damage, knockback, stun, and pool lifecycle contracts.
  - `EnemiesSpawner` owns one Unity `ObjectPool<Enemy>` per `EnemySpawnInfo`, selects spawn entries by weighted chance, places enemies on non-visible walkable cells in the player grid chunk, and tracks active enemy count.
  - `EnemiesSpawnChanceRedistributionSystem` mutates `EnemySpawnInfo.SpawnChanceInfo.SpawnChance` after spawn batches so chance can shift from earlier entries toward later entries.
  - `EnemyMovementController` moves enemies through `FlowFieldMovementController`, pauses during attack animations and a short post-attack delay, supports knockback via DOTween, and rotates toward movement.
  - `EnemyCollisionsController` periodically sphere-casts against `EntityLayers.All` and emits player/enemy collision events while ignoring the enemy's own trigger colliders.
  - `EnemyAttackController` listens for player collision events, starts attack animation when the current target is in range and inside the attack arc, and applies `Enemy.Config.Damage` on the animation hit frame.
  - `EnemyDeathHandler` listens to `Health.OnNoHealth`, disables physical interaction and visuals, plays death VFX and audio, spawns experience, then raises `OnCompleted` after both death effects finish.
  - `EnemyAnimator` bridges Animator parameters and animation events into `IAttackAnimationPlayer` events.
  - `EnemiesOutsidePlayerChunkTeleporter` periodically moves enemies outside the player chunk back to shuffled walkable cells not visible by the main camera.
- Key interfaces:
  - `IOnRandomGridPosSpawner<EnemiesSpawner>` is the DI-facing spawn contract used by `WaveManager`.
  - `IObjectReleaseNotifier` and `ISpawnedObjectsCounter` let upstream systems observe active pooled enemies.
  - `IPoolable` defines enemy get/release lifecycle and the `OnCanBeReleased` release signal.
  - `IHealthy`, `IDamageable`, `IKnockable`, and `IStunnable` make enemies targetable by damage, knockback, and stun systems.
  - `ICollisionsController`, `IMovementController`, `IFlowFieldMovementController`, and `IAttackAnimationPlayer` split enemy behavior into replaceable component contracts.
  - `INeedToCompleteBeforeDisable` delays pool release until death presentation completes.
- Runtime flow:
  - `DefaultGameplaySceneInstaller` binds the scene `EnemiesSpawner` as `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - `WaveManager` waits for its delay rules, then calls `SpawnAtRandomGridPos(_maxEnemiesInWave)` and grows the next wave size.
  - `EnemiesSpawner` chooses enemy entries by current `SpawnChanceInfo.SpawnChance`, retrieves instances from the matching pool, calls `Enemy.OnGet`, subscribes to `Enemy.OnCanBeReleased`, positions enemies on off-camera walkable cells, activates them, and increments `CurrentlySpawnedObjectsCount`.
  - Active enemies use flow-field directions from the grid to move toward the player while separation logic reduces stacking with other enemies.
  - Collision checks notify `EnemyAttackController` when the player is nearby. The attack controller validates range, arc, and terrain obstruction before playing an attack and damaging the target on the animation hit frame.
  - Incoming damage spawns a damage number, decreases health, and plays blood VFX while the enemy remains alive.
  - On death, `EnemyDeathHandler` disables collider and rigidbody interaction, hides the visual, plays death effects, spawns experience, and completes after VFX plus audio callbacks. `Enemy` then raises `OnCanBeReleased`.
  - `EnemiesSpawner` handles release by calling `Enemy.OnRelease`, unsubscribing from release events, deactivating the GameObject, raising `OnSpawnedEntityReleased`, and decrementing `CurrentlySpawnedObjectsCount`.

## Rules and Invariants

- Critical behavior rules:
  - Spawned enemies must come from the configured pool entries; enemy prefabs are not spawned ad hoc during waves.
  - Enemies are positioned on walkable cells outside the main camera view when spawned or teleported back into the player chunk.
  - Enemy health is reset from `EnemyConfigSO.MaxHealth` when an enemy is retrieved from the pool.
  - Enemy death must complete its presentation path before the spawner deactivates the GameObject.
  - Enemy damage, movement speed, rotation speed, experience value, danger level, and crawling animation mode come from `EnemyConfigSO`.
  - Player damage is applied on `EnemyAnimator.OnAttackHitFrame`, not when collision is first detected.
  - Enemy release is event-driven through `OnCanBeReleased`; downstream counters depend on this signal to stay accurate.
- Ordering or sequencing guarantees:
  - `EnemiesSpawner.Awake` creates pools before `Start` initializes spawn chance redistribution.
  - `Enemy.OnGet` subscribes to death sequence completion before the enemy is made active by the spawner.
  - `EnemyDeathHandler.OnEnable` subscribes to health, VFX, and audio completion events before combat can kill the enemy.
  - `EnemyMovementController` stops flow-field movement while an attack animation is playing and for a short delay after attack animation end.
  - Spawn chance redistribution runs once per spawn batch after spawn attempts complete.
- Constraints contributors must preserve:
  - Keep the scene spawner bound through Reflex instead of adding singleton access or scene searches.
  - Preserve serialized fields and prefab-driven setup for enemy configs, visuals, VFX, colliders, audio, and pool entries.
  - Do not bypass `EnemyDeathHandler` when killing an enemy unless the design explicitly wants immediate despawn.
  - Keep player-facing combat values in `EnemyConfigSO` or explicit serialized fields, not hidden constants.
  - Treat changes to spawn chance redistribution as balance changes; they should be reviewable and manually tested.

## Extension Points

- Safe extension areas:
  - Add new enemy types by creating or configuring an enemy prefab with the required components and an `EnemyConfigSO`, then adding an `EnemySpawnInfo` entry to the spawner.
  - Add new enemy visuals or animation sets through prefab and animator setup while keeping `EnemyAnimator` animation event callbacks intact.
  - Add new reactions to damage, death, stun, or release by subscribing to existing health, stun, animation, and pool lifecycle events.
  - Add alternate movement or collision components by implementing the existing interfaces and preserving the `Enemy` aggregate expectations.
- Required dependencies and contracts:
  - Enemy prefabs are expected to provide `IHealth`, `IStunController`, `ICollisionsController`, `IMovementController`, `IAudioClipPlayer`, `INeedToCompleteBeforeDisable`, `EnemyAnimator`, collider, rigidbody, visual, blood VFX, and death VFX setup.
  - `EnemyMovementController` requires `Enemy` and `FlowFieldMovementController`.
  - `EnemyAttackController`, `EnemyCollisionsController`, and `EnemyDeathHandler` require `Enemy`.
  - `EnemyDeathHandler` requires DI for `IInWorldSpaceSpawner<ExpParticleSpawner, float>`.
  - `Enemy` requires DI for `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>`.
  - `EnemiesSpawner` and `EnemiesOutsidePlayerChunkTeleporter` require DI for `IGridManager`.
- Testing implications:
  - Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, validate spawning from the first wave, off-camera placement, wave count growth, enemy movement toward the player, attack hit-frame damage timing, death VFX/audio completion, experience spawning, and pool count stability.
  - For spawn chance changes, run a deterministic or logged sampling pass to confirm weighted selection and redistribution behave as intended.
  - For prefab or animator changes, verify animation events call `Call_OnAttackAnimationStart`, `Call_OnAttackHitFrame`, and `Call_OnAttackAnimationEnd`.

## Integration Notes

- Upstream dependencies:
  - `WaveManager` drives enemy spawn batches through `IOnRandomGridPosSpawner<EnemiesSpawner>`.
  - `IGridManager` supplies the player chunk and world grid used by spawning, teleporting, and flow-field movement.
  - `EntityLayers` and `TerrainLayers` define enemy/player detection, separation, attack obstruction, and ground checks.
  - Reflex injection supplies grid, damage number, and experience particle spawners.
- Downstream consumers:
  - Player damage is applied through the player's `IDamageable` implementation.
  - Skills and projectiles detect enemies through `EntityLayers.Enemy` and use enemy damage/status interfaces.
  - Experience progression depends on `EnemyDeathHandler.SpawnExp`.
  - Wave pacing depends on `EnemiesSpawner.CurrentlySpawnedObjectsCount`.
- Cross-system coupling risks:
  - The spawner directly mutates `EnemySpawnInfo.SpawnChanceInfo` values, so inspector-authored spawn chances are runtime state after `Start`.
  - Attack timing depends on animation event callbacks; missing callbacks can leave attacks without damage or movement locked during an attack.
  - Death release depends on both VFX and audio finish events. If either completion event does not fire, the enemy can remain unreleased.
  - Movement, animation, and death behavior are split across sibling components but cached by `Enemy.Awake`; prefab composition is part of the contract.

## Known Risks and Open Questions

- Known limitations:
  - `EnemyAttackController.OnEnable` subscribes to `OnAttackHitFrame` using an inline lambda and does not unsubscribe it in `OnDisable`, so pooled reuse may accumulate duplicate hit-frame handlers.
  - `EnemyMovementController` checks `_isStunable` before honoring `StunController.IsStunned`, but `_isStunable` is not set in the current implementation. Stun application may therefore not stop movement.
  - `EnemiesSpawnChanceRedistributionSystem.Initialize` assumes at least one non-fixed spawn chance entry. A configuration where every entry has `SpawnChanceWillNotChange` can index an empty list.
  - `EnemyDeathHandler` assumes exactly two completion callbacks for death sequence completion: death VFX and death audio.
  - `EnemiesSpawner.OnEnemyGet` returns early when no walkable cell is found after the pool has handed out an enemy; confirm pool state behavior before changing this path.
- Open design questions:
  - Should `DangerLevel` in `EnemyConfigSO` influence wave selection, spawn chance redistribution, or skill targeting priority?
  - Should stun always block movement and attacks, or should some enemies opt out through configuration?
  - Should spawn chance redistribution operate on runtime copies so inspector-authored values remain unchanged for resets and debugging?
- Suggested follow-up tasks:
  - Add focused play-mode or edit-mode coverage for enemy pool release, attack hit-frame damage, spawn chance redistribution edge cases, and stun movement blocking.
  - Review enemy prefab component requirements and consider a validation helper or editor check if prefab drift becomes frequent.
  - Consider documenting expected animator event names and clip timing near enemy prefab setup.
