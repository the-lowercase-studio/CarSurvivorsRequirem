# Enemies System Documentation

## Purpose

The Enemies system owns runtime enemy entities, enemy object pooling and pre-warming, flow-field and off-grid movement, grounding and fall physics, contact-based arc melee attacks, damage reception and VFX, death presentation sequences, experience particle payout, collectible item drop scattering with walkable grid resolution, off-chunk enemy teleportation, and pool recycling.

It does not own wave timing, grid generation, flow-field vector computation, player health mechanics, projectile simulation, experience particle collection, or boss state machine logic. Those systems interact with enemies via explicit interfaces, events, and DI bindings.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs
  - Assets/Scripts/Enemies/Base/EnemyAnimator.cs
  - Assets/Scripts/Enemies/Base/EnemyCollisionsController.cs
  - Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs
  - Assets/Scripts/Enemies/Base/IAttackAnimationPlayer.cs
  - Assets/Scripts/Enemies/Base/IMovementController.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs
  - Assets/Scripts/Enemies/Constants/EnemyMovementConstants.cs
  - Assets/Scripts/Enemies/Constants/EnemyCombatConstants.cs
  - Assets/Scripts/Enemies/Bosses/BossManager.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
- Designer-authored data:
  - Assets/ScriptableObjects/Enemy/EnemyConfigSO.cs
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs
  - Enemy prefabs configured with Enemy, EnemyMovementController, EnemyAttackController, EnemyDeathHandler, EnemyDropHandler, EnemyAnimator, EnemyCollisionsController, FlowFieldMovementController, Health/RegenativeHealth, Collider, Rigidbody, Audio, and VFX
- Related systems:
  - Wave orchestration: Assets/Scripts/Waves/WaveManager.cs
  - Swarm events: Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Boss system: Assets/Scripts/Enemies/Bosses/Golem/ (documented in .agents/context/game-systems/golem-boss-system.md)
  - Grid & off-camera cells: Assets/Scripts/Navigation/GridSystem/
  - Flow-field navigation: Assets/Scripts/Navigation/FlowFieldSystem/
  - Health & damage: Assets/Scripts/HealthSystem/
  - Status effects: Assets/Scripts/StatusEffects/
  - Damage numbers & VFX: Assets/Scripts/DamageNumbers/, Assets/Scripts/VFX/
  - Experience particles: Assets/Scripts/LevelSystem/Exp/
  - Collectible items: Assets/Scripts/Skills/ObjectsImpactingSkills/
  - DI registration: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - Enemy: Aggregate root for standard enemy prefabs. Implements IHealthy, IDamageable, IKnockable, and IPoolable. Injects IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> via Reflex. Dispatches floating damage numbers on TakeDamage, reduces Health, plays blood VFX if alive, handles knockback delegation to MovementController, and triggers pool release via OnCanBeReleased when the death sequence completes.
  - EnemyMovementController: Implements IMovementController. Drives grid movement via IFlowFieldMovementController, or handles off-grid movements (e.g. knockback) via MoveToPositionInTimeIgnoringSpeed with DOTween. Performs obstacle SphereCasts against TerrainLayers.Impassable using OBSTACLE_CHECK_RADIUS (0.4f) and OBSTACLE_SAFETY_BUFFER (0.1f) to prevent wall clipping. Handles grounding via SphereCast against TerrainLayers.Ground, applying FALL_GRAVITY (25f) and killing enemies falling below FALL_DEATH_Y_THRESHOLD (-10f). Pauses movement during attack animations and for a 0.2s post-attack delay. Rotates smoothly toward movement direction using Config.RotationSpeed.
  - EnemyCollisionsController: Implements ICollisionsController. Periodically checks collisions (default every 0.05s) with a SphereCastAll on EntityLayers.All, emitting OnCollisionWithPlayer and OnCollisionWithOtherEnemy events while ignoring self triggers.
  - EnemyAttackController: Subscribes to EnemyCollisionsController.OnCollisionWithPlayer. Verifies attack range (_attackRange) and attack cone angle (_attackArcAngle, default 60°), and executes a Physics.Raycast line-of-sight check against TerrainLayers.All to prevent attacking through walls. Triggers attack animation on EnemyAnimator, and inflicts EnemyConfigSO.Damage to IDamageable targets on OnAttackHitFrame.
  - EnemyDeathHandler: Implements INeedToCompleteBeforeDisable. Subscribes to Health.OnNoHealth. Disables collider, marks Rigidbody isKinematic = true, hides visuals, plays death VFX, plays death SFX ("Death"), spawns EXP particles via IInWorldSpaceSpawner<ExpParticleSpawner, float>, and fires OnCompleted once both VFX and SFX finishes (_startEffectsToFinish = 2).
  - EnemyDropHandler: Subscribes to Health.OnNoHealth. Evaluates configured CollectibleDropEntry drop chance percentages. Computes scattered target drop positions across 360° with jitter, verifies or finds walkable grid positions using direct ray stepping or a spiral search on IGridManager.WorldGrid, and delegates drop instantiation to ICollectibleDropNotifier.SpawnCollectible.
  - CollectibleDropNotifier: Implements ICollectibleDropNotifier. Manages ObjectPool<GameObject> instances for collectible item prefabs. Animates item drop bounce with DOTween (DOScale with Ease.OutBack and DOJump) to the target walkable location. Listens to ICollectible.OnCollected and IPoolable.OnCanBeReleased.
  - EnemyAnimator: Implements IAttackAnimationPlayer. Sets animator parameters ("Speed", "IsOnGround", "IsMovingByCrawling", "Attack") and bridges animation events into OnAttackAnimationStart, OnAttackHitFrame, and OnAttackAnimationEnd events.
  - EnemiesOutsidePlayerChunkTeleporter: Periodic monitor that detects enemies drifting outside the active player chunk bounds and relocates them to hidden, walkable cells within the player chunk (using GridCellsNotVisibleByMainCamera.FillWalkableCells) and resets vertical velocity.
  - EnemiesSpawner: Implements IOnRandomGridPosSpawner<EnemiesSpawner>, ISwarmEnemySpawner, and IEnemySpawnDifficultyController. Manages ObjectPool<Enemy> per configured EnemySpawnInfo. Pre-warms pools on Start. Spawns standard wave enemies on off-camera walkable cells outside the player chunk. Spawns swarm enemies inside the player chunk with optional spawn VFX. Coordinates dynamic spawn weight adjustments via EnemiesSpawnChanceRedistributionSystem.
  - EnemiesSpawnChanceRedistributionSystem: Adjusts EnemySpawnInfo.SpawnChance values progressively after spawn batches, shifting spawn probability toward higher-tier enemies geometrically. Supports difficulty multipliers from totems or events via IncreaseSpawnChanceRedistributionFactor.
  - BossManager: Implements IBossManager. Bridges boss spawning into the scene, instantiating GolemBoss, binding the boss health to IBossHUDPresenter, suppressing standard swarms via ISwarmFreezer, and spawning stage progression portals on defeat.
- Key interfaces:
  - IOnRandomGridPosSpawner<EnemiesSpawner>: Contract for wave manager spawning.
  - ISwarmEnemySpawner: Contract for spawning targeted enemy counts during swarm events.
  - IEnemySpawnDifficultyController: Contract for adjusting difficulty redistribution scalars.
  - ICollectibleDropNotifier: Contract for spawning collectible items with drop physics/tweens.
  - IBossManager: Contract for managing boss lifecycle and HUD.
  - IPoolable: Pool lifecycle interface (OnGet, ReturnToPool, OnRelease, OnCanBeReleased).
  - IHealthy, IDamageable, IKnockable: Health, combat, and crowd-control contracts.
  - INeedToCompleteBeforeDisable: Contract delaying pool recycling until visual/audio death sequences complete.
  - IMovementController: Movement and knockback interface for enemy locomotion.
  - IAttackAnimationPlayer: Animation event interface for attack start, hit frame, and finish.
  - ICollisionsController: Contact detection interface emitting player and enemy collision signals.
- Constants:
  - EnemyMovementConstants:
    - GROUND_CHECK_ORIGIN_Y = 1.0f
    - GROUND_CHECK_SPHERE_RADIUS = 0.3f
    - GROUND_CHECK_DISTANCE = 3.0f
    - GROUND_SNAP_LERP_SPEED = 20.0f
    - FALL_GRAVITY = 25.0f
    - FALL_DEATH_Y_THRESHOLD = -10.0f
    - MOVING_TO_POSITION_ACCURACY = 0.02f
    - OBSTACLE_CHECK_RADIUS = 0.4f
    - OBSTACLE_SAFETY_BUFFER = 0.1f
  - EnemyCombatConstants:
    - ARC_DEBUG_SEGMENTS = 16
    - CIRCLE_DEBUG_SEGMENTS = 16
- Runtime flows:
  - Standard Wave Spawning Flow:
    1. WaveManager triggers EnemiesSpawner.SpawnAtRandomGridPos(count).
    2. Spawner queries GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk.
    3. For each cell, RandomEnemyInfoBasedOnSpawnChance selects an enemy type according to current weighted probabilities.
    4. Spawner retrieves an instance from ObjectPool<Enemy>, calls Enemy.OnGet(), hooks OnCanBeReleased, positions the enemy, activates the GameObject, and increments CurrentlySpawnedObjectsCount.
    5. EnemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance() adjusts probability weights.
  - Swarm Spawning Flow:
    1. SwarmSpawner calls EnemiesSpawner.SpawnSpecificEnemy(enemyInfo, count).
    2. GridCellsNotVisibleByMainCamera.GetRandomWalkableCells selects hidden walkable cells inside the player chunk.
    3. If _swarmSpawnVfxPrefab is set, spawns a spawn VFX and waits for OnVFXFinished before activating the pooled enemy at the position.
  - Movement, Grounding, and Knockback Flow:
    1. In FixedUpdate, EnemyMovementController checks ground contact using SphereCast down to TerrainLayers.Ground.
    2. If grounded, snaps Y smoothly to terrain point. If ungrounded, applies fall gravity until grounding or taking lethal void damage if Y < -10f.
    3. If not attacking and no post-attack delay, moves along flow-field grid vectors via FlowFieldMovementController or along off-grid paths.
    4. Knocks back on ApplyKnockBack, performing an obstacle check against TerrainLayers.Impassable to avoid clipping through obstacles, moving via DOTween OutSine.
    5. Smoothly rotates facing toward movement vector with Config.RotationSpeed.
  - Player Collision and Arc Attack Flow:
    1. EnemyCollisionsController detects player layer in periodic SphereCastAll and raises OnCollisionWithPlayer.
    2. EnemyAttackController verifies target is within _attackRange and within _attackArcAngle cone.
    3. Raycasts against TerrainLayers.All to ensure line-of-sight is unobstructed.
    4. If valid, triggers "Attack" on EnemyAnimator.
    5. On animation hit frame (OnAttackHitFrame), inflicts Config.Damage to player IDamageable.
    6. Movement resumes after animation completion plus _movementDelayAfterAttack (0.2s).
  - Damage Feedback, Death Sequence, and EXP Payout Flow:
    1. On TakeDamage(damage), Enemy spawns floating damage numbers (Hemisphere spread) via DamageNumbersSpawner.
    2. Decreases Health. If still alive, plays blood VFX.
    3. On fatal damage (Health.OnNoHealth), EnemyDeathHandler disables collider, makes Rigidbody kinematic, hides visuals, plays death VFX, plays "Death" SFX, and spawns EXP particles via ExpParticleSpawner.
    4. EnemyDropHandler evaluates drop chances, resolves walkable grid landing positions, and calls CollectibleDropNotifier.SpawnCollectible.
    5. When both death VFX and SFX finish playing, EnemyDeathHandler raises OnCompleted, signaling Enemy.OnCanBeReleased.
  - Pool Recycling Flow:
    1. EnemiesSpawner catches Enemy.OnCanBeReleased.
    2. Calls Enemy.OnRelease(), deactivates the GameObject, releases the instance back to ObjectPool<Enemy>, emits OnSpawnedEntityReleased, and decrements CurrentlySpawnedObjectsCount.

## Rules and Invariants

- Critical behavior rules:
  - Prefabs for all configured enemies must be pre-warmed during Start() up to their MaxAmount pool capacity.
  - Standard wave spawning must select walkable cells outside both camera visibility and player chunk boundary.
  - Swarm wave spawning selects walkable cells hidden from the camera inside the player chunk.
  - Enemy MaxHealth is restored from EnemyConfigSO upon pool retrieval (OnGet).
  - Melee damage is strictly applied on EnemyAnimator.OnAttackHitFrame, not immediately on collision trigger.
  - Attack line-of-sight must be clear of terrain obstacles before attack animations initiate.
  - Collectible item drops scattered on enemy death must land on valid walkable cells (verified via step checking or spiral grid search).
  - Pool release must never occur until both death VFX and death SFX have completed their callbacks.
- Ordering or sequencing guarantees:
  - ObjectPool<Enemy> instances are created in Awake() and pre-warmed in Start().
  - Movement is halted immediately when attack animations start and remains locked for 0.2s post-attack.
  - Death completion counter (_startEffectsToFinish = 2) must reach zero before OnCompleted is raised.
- Constraints contributors must preserve:
  - EnemiesSpawner and CollectibleDropNotifier must be injected via Reflex DI interfaces, never through singletons or FindObjectOfType.
  - Serialized configurations and tuning parameters belong in EnemyConfigSO, DropAnimationConfiguration, or serialized fields.
  - All constants must remain centralized in EnemyMovementConstants and EnemyCombatConstants.
  - Maintain the English language invariant across all code identifiers, comments, tooltips, and documentation.

## Extension Points

- Safe extension areas:
  - Add new enemy types: Create an enemy prefab with required components, author an EnemyConfigSO asset, and add an EnemySpawnInfo entry to EnemiesSpawner._poolEnemiesInfo in the scene.
  - Add new collectible drops: Add CollectibleDropEntry items to the EnemyDropHandler component on enemy prefabs.
  - Adjust combat and movement physics: Tune values in EnemyConfigSO, DropAnimationConfiguration, EnemyMovementConstants, or EnemyCombatConstants.
  - Add custom boss encounters: Implement IBossManager extensions or state machine patterns modeled after GolemBoss.
- Required dependencies and contracts:
  - Standard enemy prefabs require:
    - Enemy
    - EnemyMovementController
    - EnemyAttackController
    - EnemyDeathHandler
    - EnemyDropHandler
    - EnemyAnimator
    - EnemyCollisionsController
    - FlowFieldMovementController
    - Health or RegenativeHealth
    - Collider (trigger and physical setup)
    - Rigidbody
    - VFXPlayer (blood and death VFX)
    - AudioClipPlayer
  - Reflex DI bindings required in scene installer:
    - IOnRandomGridPosSpawner<EnemiesSpawner> (EnemiesSpawner)
    - ISwarmEnemySpawner (EnemiesSpawner)
    - IEnemySpawnDifficultyController (EnemiesSpawner)
    - ICollectibleDropNotifier (CollectibleDropNotifier)
    - DropAnimationConfiguration
    - IInWorldSpaceSpawner<ExpParticleSpawner, float> (ExpParticleSpawner)
    - IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> (DamageNumbersSpawner)
    - IGridManager (GridManager)
    - Camera (MainCamera)
- Testing implications:
  - Verify C# compilation:
    dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  - Play Mode verification checklist:
    - Pools pre-warm without runtime allocations on initial wave spawns.
    - Standard wave enemies spawn off-camera outside player chunk.
    - Swarm enemies spawn inside player chunk with optional VFX.
    - Knockback stops safely at impassable obstacles without wall clipping.
    - Attack raycast checks block attacks through walls.
    - Damage numbers and blood VFX display properly on hit.
    - Death sequence completes VFX and SFX before enemy pool release.
    - Collectible drops scatter cleanly onto walkable grid cells.
    - Enemies outside chunk boundary teleport smoothly to hidden cells.

## Integration Notes

- Upstream dependencies:
  - WaveManager drives standard enemy wave batches.
  - SwarmSpawner drives event-based swarm spawns.
  - IGridManager supplies world and chunk grid geometry for navigation, spawning, and drop placement.
  - IPlayerManager provides player position and transform references.
- Downstream consumers:
  - Player car receives damage via IDamageable.
  - Car weapons and projectile systems target enemies on EntityLayers.Enemy.
  - LevelController consumes EXP particles spawned by EnemyDeathHandler.
  - Skill upgrade UI and CollectibleDropNotifier consume dropped items.
  - BossHUDPresenter receives boss health events from BossManager.
- Cross-system coupling risks:
  - If GridManager grid generation fails or returns zero walkable cells, spawning and drop resolution fall back or fail to find valid points.
  - If death VFX or audio clips are misconfigured on a prefab and fail to fire completion events, the enemy instance will remain unreleased in the scene.

## Known Risks and Open Questions

- Known limitations:
  - EnemyDeathHandler uses a hardcoded _startEffectsToFinish = 2. If a prefab lacks either a death VFX player or a death audio clip, OnCompleted will never be reached.
  - CollectibleDropNotifier relies on GameObject pooling rather than strongly-typed component pools, requiring TryGetComponent checks upon retrieval and release.
- Open design questions:
  - Should EnemyDeathHandler dynamically count configured finishable components (e.g. check if death audio/VFX are assigned) rather than hardcoding _startEffectsToFinish = 2?
  - Should spawn chance redistribution operate on cloned runtime data to ensure editor ScriptableObject assets remain unmodified during testing?
