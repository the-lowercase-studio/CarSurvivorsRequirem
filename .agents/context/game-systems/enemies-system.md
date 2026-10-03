# Enemies System Documentation

## Purpose

The Enemies system manages runtime enemy entities in Car Survivors. It is responsible for:
- Standard melee enemy units (such as walking and crawling zombies) and exploding suicide units (such as barrel enemies).
- Object pooling, pre-warming, and pool recycling for high-density enemy swarms.
- Locomotion driven by flow fields or off-grid targets, smooth rotation, ground detection, slope snapping, fall physics, fall suppression, lethal void checks, and world grid boundary clamping.
- Collision detection against players and peer enemies.
- Line-of-sight checked arc melee attacks and damage application on animation hit frames.
- Suicide bomber telegraphing, visual feedback (mesh rotation punch and material color/emission lerping), circular telegraph indicators, and area-of-effect detonations.
- Damage reception, floating damage number emission, and blood impact VFX.
- Death presentation sequences, failsafe presentation timeouts, and experience particle payouts.
- Collectible item drops with radial scattering, elevation offsets, and multi-step walkable grid position resolution.
- Off-chunk enemy relocation to prevent orphaned enemies outside the active player area.
- Progressive spawn weight redistribution using geometric decay across enemy tiers.
- Boss encounter coordination, tower landmark tracking, Boss HUD presentation, swarm suppression, and progression portal spawning.

The system is not responsible for:
- Wave timing, interval calculation, or wave schedule progression (owned by WaveManager).
- Navigation grid generation, cell tagging, or obstacle baking (owned by GridManager).
- Flow-field cost, integration, and direction vector field computation (owned by FlowFieldManager).
- Player car physics, input handling, or car health mechanics (owned by PlayerManager, Health, and ArcadeCarPhysics).
- Car weapon targeting, projectile trajectories, or player weapon upgrades (owned by skills and projectile systems).
- Experience collection, level-up calculation, or upgrade selection UI (owned by LevelController and UI system).
- Boss state machines or internal boss attack routines (owned by GolemStateMachine and MortarTowerStateMachine).

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
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyExplosionController.cs
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyDeathHandler.cs
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyAttackFeedback.cs
  - Assets/Scripts/Enemies/Barrel/Constants/BarrelEnemyConstants.cs
  - Assets/Scripts/Enemies/Barrel/Constants/BarrelFeedbackShaderConstants.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs
  - Assets/Scripts/Enemies/Constants/EnemyMovementConstants.cs
  - Assets/Scripts/Enemies/Constants/EnemyCombatConstants.cs
  - Assets/Scripts/Enemies/Constants/EnemyAnimationConstants.cs
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/Enemies/Bosses/Constants/BossEncounterConstants.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
- Designer-authored data:
  - Assets/ScriptableObjects/Enemy/EnemyConfigSO.cs
  - Assets/ScriptableObjects/Enemy/Basic/Barrel/BarrelExplosionConfigSO.cs
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs
  - Standard Enemy prefabs configured with Enemy, EnemyMovementController, EnemyAttackController, EnemyDeathHandler, EnemyDropHandler, EnemyAnimator, EnemyCollisionsController, FlowFieldMovementController, Health, Collider, Rigidbody, Audio, and VFX
  - Barrel Enemy prefabs configured with Enemy, EnemyMovementController, EnemyCollisionsController, BarrelEnemyExplosionController, BarrelEnemyDeathHandler, BarrelEnemyAttackFeedback, CircularTelegraphIndicator, EnemyAnimator, Health, BoxCollider, Rigidbody, Audio, and VFX
- Related systems:
  - Wave orchestration: Assets/Scripts/Waves/WaveManager.cs
  - Swarm events: Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Boss systems: Assets/Scripts/Enemies/Bosses/Golem/ (documented in .agents/context/game-systems/golem-boss-system.md) and Assets/Scripts/Enemies/Bosses/Towers/ (documented in .agents/context/game-systems/tower-boss-system.md)
  - Grid and off-camera cells: Assets/Scripts/Navigation/GridSystem/
  - Flow-field navigation: Assets/Scripts/Navigation/FlowFieldSystem/
  - Health and damage: Assets/Scripts/HealthSystem/
  - Status effects and knockback: Assets/Scripts/StatusEffects/
  - Damage numbers: Assets/Scripts/DamageNumbers/
  - Telegraph indicators: Assets/Scripts/Indicators/
  - Visual effects and explosion VFX: Assets/Scripts/VFX/
  - Experience particles: Assets/Scripts/LevelSystem/Exp/
  - Collectible items: Assets/Scripts/Skills/ObjectsImpactingSkills/
  - DI registration: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/flow-field-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/spawners-system.md
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
  - Enemy: Aggregate root component for enemy GameObjects. Implements IHealthy, IDamageable, IKnockable, and IPoolable. Injects IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> via Reflex. Emits floating damage numbers in a hemisphere pattern, decrements health, triggers blood VFX, delegates knockback to EnemyMovementController, and fires OnCanBeReleased when death sequences finish. Restores MaxHealth and resets animator state in OnGet().
  - EnemyMovementController: Implements IMovementController. Coordinates locomotion along flow fields (via IFlowFieldMovementController) or off-grid positions (MoveToPositionInTimeIgnoringSpeed). FixedUpdate checks ground contact using Physics.Raycast against TerrainLayers.Walkable from an origin offset of GROUND_CHECK_ORIGIN_Y (1.5f) over GROUND_CHECK_DISTANCE (3.5f), snapping smoothly to terrain with GROUND_SNAP_LERP_SPEED (20f). If ungrounded, applies FALL_GRAVITY (25f), suppresses horizontal movement when falling past FALL_SUPPRESSION_Y_OFFSET (1.0f) below last grounded Y, and kills the entity if falling below FALL_DEATH_Y_THRESHOLD (-10f). Enforces world grid boundary bounds using WORLD_BOUNDARY_SAFETY_PADDING (0.5f). Obstacle SphereCasting against TerrainLayers.Impassable uses OBSTACLE_CHECK_RADIUS (0.4f) and OBSTACLE_SAFETY_BUFFER (0.1f) to prevent wall clipping during knockbacks. Knocks back using sine easing without extra allocations. Smoothly accelerates towards target velocity (using Config.Acceleration or speed multiplier 4.0f) and rotates toward movement direction with Config.RotationSpeed. Halts movement during attack animations and enforces a 0.2s post-attack delay.
  - EnemyCollisionsController: Implements ICollisionsController. Performs periodic SphereCastAll queries (default every 0.05s) on EntityLayers.All using _collisionRadius (1.0f), filtering out its own trigger colliders and emitting OnCollisionWithPlayer and OnCollisionWithOtherEnemy.
  - EnemyAttackController: Handles standard melee attacks. Subscribes to EnemyCollisionsController.OnCollisionWithPlayer. Checks if target is within _attackRange and within _attackArcAngle (default 60°), and executes a Physics.Raycast line-of-sight check against TerrainLayers.All to prevent attacking through obstacles. Plays the attack animation on EnemyAnimator, and applies Config.Damage to IDamageable targets when OnAttackHitFrame is fired.
  - EnemyAnimator: Implements IAttackAnimationPlayer. Sets Animator parameters (Speed, IsOnGround, IsMovingByCrawling, Attack). Toggles layer weights between walking layer (0) and crawling layer (1) based on Config.IsMovingByCrawling. Bridges animation events into OnAttackAnimationStart, OnAttackHitFrame, and OnAttackAnimationEnd. Periodically synchronizes attack animation state (every 0.05s), automatically calling OnAttackAnimationEnd if transition state enters falling or if duration exceeds MAX_ATTACK_ANIMATION_DURATION (3.5s).
  - BarrelEnemyExplosionController: Controls suicide bomber logic for barrel enemies. Follows a state machine: Inactive, Approaching, Priming, Canceling, Detonating, Dying. On collision with player body within ActivationRange (from BarrelExplosionConfigSO), transitions to Priming, suppresses pursuit on EnemyMovementController, displays attached CircularTelegraphIndicator, triggers the attack animation, and activates BarrelEnemyAttackFeedback. Implements a watchdog in Update() that cancels priming if hit event is delayed past 3.5s. When OnAttackHitFrame fires, enters Detonating, triggers explosion VFX from IExplosionVfxPool, tests capsule overlap against player body with DISK_CONTACT_TOLERANCE (0.0001f), applies damage, hides body visuals via BarrelEnemyDeathHandler.HideBody(), calls TakeFullHpDamage(), and signals CompleteDetonationAfterDispatch().
  - BarrelEnemyAttackFeedback: Coordinates telegraph feedback during priming on barrel enemies. Drives an infinite DOPunchRotation loop on a dedicated visual pivot (_visualPivot). Collects model renderers and builds MaterialPropertyBlock bindings for _BaseColor / _Color and _EmissionColor. Samples Animator normalized time up to _hitNormalizedTime, advancing a DOTween sequence to lerp colors towards warning tints. Fully resets rotation and restores original colors when cancelled or disabled.
  - BarrelEnemyDeathHandler: Implements INeedToCompleteBeforeDisable. Manages death and detonation presentation for barrel enemies. Subscribes to Health.OnNoHealth, hides the body, disables collider, makes Rigidbody kinematic, spawns EXP particles, and triggers death VFX and death audio. Includes an explicit watchdog timer (_presentationTimeout, default 5.0s) in LateUpdate: if visual or audio callbacks are dropped, the watchdog automatically forces completion to ensure the pooled enemy is never orphaned.
  - EnemyDeathHandler: Standard enemy death sequence handler. Implements INeedToCompleteBeforeDisable. Subscribes to Health.OnNoHealth. Disables collider, sets Rigidbody isKinematic = true, hides visuals, plays death VFX, plays death SFX ("Death"), spawns EXP particles via IInWorldSpaceSpawner<ExpParticleSpawner, float>, and fires OnCompleted once both VFX and SFX finish (_startEffectsToFinish = 2).
  - EnemyDropHandler: Subscribes to Health.OnNoHealth. Evaluates configured CollectibleDropEntry drop chance percentages. Calculates 360° radial scatter positions with jitter and height offsets. Resolves valid walkable destination points using a 4-step algorithm: (1) direct target position, (2) stepping along ray toward start, (3) start position, (4) 5-ring spiral search around start cell, (5) fallback. Delegates instantiation to ICollectibleDropNotifier.SpawnCollectible.
  - CollectibleDropNotifier: Implements ICollectibleDropNotifier. Manages ObjectPool<GameObject> instances per collectible item prefab. Spawns items and animates them with DOTween DOScale (Ease.OutBack) and DOJump to the resolved target position. Listens to ICollectible.OnCollected and IPoolable.OnCanBeReleased to recycle items back into their pools.
  - EnemiesOutsidePlayerChunkTeleporter: Periodic monitor (every 2.0s) that identifies active enemies positioned outside the current player chunk boundary. Queries GridCellsNotVisibleByMainCamera.FillWalkableCells to collect hidden walkable cells inside the player chunk, shuffles them, teleports off-chunk enemies to these locations, and resets their vertical velocity.
  - EnemiesSpawner: Implements IOnRandomGridPosSpawner<EnemiesSpawner>, ISwarmEnemySpawner, and IEnemySpawnDifficultyController. Manages ObjectPool<Enemy> instances per EnemySpawnInfo. Pre-warms pools on Start. Spawns standard wave enemies on off-camera walkable cells outside the player chunk (using _outerSpawnBufferCells and _maxEnemiesPerCell). Spawns swarm enemies inside the player chunk with optional spawn VFX. Drives spawn weight updates via EnemiesSpawnChanceRedistributionSystem.
  - EnemiesSpawnChanceRedistributionSystem: Gradually decrements spawn chance of lower-tier enemies after spawn batches and redistributes probability geometrically across higher-tier enemies whose threshold flags are unlocked. Supports difficulty scalars from external game events via IncreaseSpawnChanceRedistributionFactor.
  - BossEncounterService: Implements IBossEncounterService. Tracks tower landmarks, counts defeated towers against required threshold (default 2), pauses before spawning GolemBoss (default 10s delay), binds boss health to IBossHUDPresenter, suppresses standard swarms via ISwarmFreezer, and instantiates stage progression portals upon Golem defeat.

- Key interfaces:
  - IHealthy, IDamageable, IKnockable: Health management, damage reception, and knockback handling.
  - IPoolable: Pool lifecycle contracts (OnGet, ReturnToPool, OnRelease, OnCanBeReleased).
  - INeedToCompleteBeforeDisable: Contract delaying pool release until death presentation finishes (OnCompleted).
  - IMovementController: Locomotion, knockback, vertical velocity reset, pursuit suppression, and movement stops.
  - IAttackAnimationPlayer: Animation event forwarding (OnAttackAnimationStart, OnAttackHitFrame, OnAttackAnimationEnd).
  - ICollisionsController: Contact detection interface emitting player and peer enemy collision events.
  - ICollectibleDropNotifier: Collectible item spawning with jump/scale animation and collection notification.
  - IOnRandomGridPosSpawner<EnemiesSpawner>: Wave spawner contract for batch enemy spawning.
  - ISwarmEnemySpawner: Swarm spawner contract for spawning specific enemy configurations.
  - IEnemySpawnDifficultyController: Interface for modifying dynamic spawn difficulty scalars.
  - IBossEncounterService: Boss encounter tracking, tower counting, HUD coupling, and Golem lifecycle orchestration.

- Centralized Constants:
  - EnemyMovementConstants:
    - GROUND_CHECK_ORIGIN_Y = 1.5f (raycast start height above root)
    - GROUND_CHECK_DISTANCE = 3.5f (maximum raycast distance for ground detection)
    - GROUND_SNAP_LERP_SPEED = 20.0f (vertical lerp rate to ground surface)
    - FALL_GRAVITY = 25.0f (acceleration applied during falling)
    - FALL_DEATH_Y_THRESHOLD = -10.0f (lethal void Y coordinate)
    - FALL_SUPPRESSION_Y_OFFSET = 1.0f (downward distance before horizontal movement is suppressed)
    - MOVING_TO_POSITION_ACCURACY = 0.02f (position arrival tolerance)
    - OBSTACLE_CHECK_RADIUS = 0.4f (sphere cast radius for impassable obstacles)
    - OBSTACLE_SAFETY_BUFFER = 0.1f (offset subtracted from obstacle hit distance)
    - DEFAULT_ACCELERATION_SPEED_MULTIPLIER = 4.0f (multiplier used when Config.Acceleration <= 0)
    - MIN_VELOCITY_FOR_ROTATION_SQR = 0.001f (minimum squared velocity before updating facing rotation)
    - WORLD_BOUNDARY_SAFETY_PADDING = 0.5f (margin inside world grid boundaries)
  - EnemyCombatConstants:
    - ARC_DEBUG_SEGMENTS = 16 (gizmo circle segments for attack arc)
    - CIRCLE_DEBUG_SEGMENTS = 16 (gizmo circle segments for collision check radius)
  - EnemyAnimationConstants:
    - ANIM_PARAM_SPEED = "Speed"
    - ANIM_PARAM_IS_ON_GROUND = "IsOnGround"
    - ANIM_PARAM_IS_MOVING_BY_CRAWLING = "IsMovingByCrawling"
    - ANIM_TRIGGER_ATTACK = "Attack"
    - ANIM_STATE_ZOMBIE_ATTACK_STANDING = "Zombie Attack Standing"
    - ANIM_STATE_ZOMBIE_BITING_CRAWLING = "Zombie Biting Crawling"
    - ANIM_STATE_ATTACK = "Attack"
    - ANIM_STATE_FALLING = "Falling"
    - ANIM_STATE_FALL_OVER = "FallOver"
    - MAX_ATTACK_ANIMATION_DURATION = 3.5f (failsafe watchdog duration for attack animations)
  - BarrelEnemyConstants:
    - INITIAL_ANIMATOR_STATE = "StandingLayer.Idle"
    - DEATH_AUDIO = "Death"
    - ATTACK_HIT_EVENT = "Call_OnAttackHitFrame"
    - DISK_CONTACT_TOLERANCE = 0.0001f
  - BarrelFeedbackShaderConstants:
    - EMISSION_KEYWORD = "_EMISSION"
    - BASE_COLOR_ID = Shader.PropertyToID("_BaseColor")
    - COLOR_ID = Shader.PropertyToID("_Color")
    - EMISSION_COLOR_ID = Shader.PropertyToID("_EmissionColor")
  - BossEncounterConstants:
    - DEFAULT_GOLEM_DISPLAY_NAME = "ANCIENT GOLEM"
    - DEFAULT_SPAWN_OFFSET_DISTANCE = 15f
    - DEFAULT_REQUIRED_TOWERS = 2
    - DEFAULT_DELAY_BEFORE_GOLEM_SPAWN = 10.0f
    - DEFAULT_DEBUG_SPAWN_KEY = KeyCode.P

- Runtime flows:
  - Standard Wave Spawning Flow:
    1. WaveManager triggers EnemiesSpawner.SpawnAtRandomGridPos(count).
    2. Spawner queries GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk.
    3. RandomEnemyInfoBasedOnSpawnChance selects enemy prefab configurations based on current weighted probabilities.
    4. ObjectPool<Enemy>.Get() retrieves an instance, calls Enemy.OnGet(), hooks OnCanBeReleased, sets position, activates GameObject, and increments CurrentlySpawnedObjectsCount.
    5. EnemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance() progressively shifts spawn weights toward higher tiers.
  - Swarm Spawning Flow:
    1. SwarmSpawner invokes EnemiesSpawner.SpawnSpecificEnemy(enemyInfo, count).
    2. Spawner selects hidden walkable cells inside the active player chunk via GridCellsNotVisibleByMainCamera.GetRandomWalkableCells.
    3. If _swarmSpawnVfxPrefab is assigned, instantiates VFX and waits for OnVFXFinished before retrieving and activating the pooled enemy at that position; otherwise activates immediately.
  - Movement, Grounding, Falling, and Knockback Flow:
    1. FixedUpdate runs EnemyMovementController.MovementHandler().
    2. Raycasts downward (3.5m) against TerrainLayers.Walkable. If grounded, snaps Y smoothly to terrain point with speed 20. If ungrounded, applies 25m/s² downward gravity; if Y drops below -10m, triggers TakeFullHpDamage(). If falling past 1m below last ground point, horizontal movement is paused.
    3. If knockback is active, interpolates position toward target using sine easing (Mathf.Sin(t * PI * 0.5f)). Knockback paths perform SphereCasts against TerrainLayers.Impassable and clamp within world grid boundary bounds (WORLD_BOUNDARY_SAFETY_PADDING = 0.5f).
    4. When moving along flow field, samples desired direction from IFlowFieldMovementController, accelerates toward target velocity, and updates position.
    5. Rotates toward movement direction with Config.RotationSpeed when squared velocity exceeds 0.001.
  - Standard Melee Attack Flow:
    1. EnemyCollisionsController detects player layer in periodic SphereCastAll and fires OnCollisionWithPlayer.
    2. EnemyAttackController validates that target is within _attackRange and within _attackArcAngle cone.
    3. Executes Physics.Raycast against TerrainLayers.All to verify unobstructed line of sight.
    4. Sets "Attack" trigger on EnemyAnimator; movement controller halts pursuit.
    5. When animation hits the impact frame, EnemyAnimator fires OnAttackHitFrame.
    6. EnemyAttackController applies Config.Damage to player IDamageable.
    7. On animation end, movement controller starts a 0.2s post-attack delay before resuming locomotion.
  - Barrel Suicide Bomber Attack and Explosion Flow:
    1. BarrelEnemyExplosionController receives OnCollisionWithPlayer from EnemyCollisionsController.
    2. Verifies player body collider eligibility within ActivationRange (from BarrelExplosionConfigSO).
    3. Transitions to Priming state, locks pursuit on EnemyMovementController, displays CircularTelegraphIndicator, plays attack animation, and starts BarrelEnemyAttackFeedback.
    4. Attack feedback begins an infinite punch rotation tween on the visual pivot and lerps material base color and emission toward warning colors over the attack animation timeline.
    5. If animation hit event is delayed past 3.5s, the watchdog in Update() forces CancelPriming(), collapsing the telegraph indicator and restoring normal pursuit.
    6. On hit frame (OnAttackHitFrame), transitions to Detonating state, plays explosion VFX from IExplosionVfxPool, checks player capsule overlap within ExplosionRadius, inflicts Config.Damage, hides visuals via BarrelEnemyDeathHandler.HideBody(), calls TakeFullHpDamage(), and signals CompleteDetonationAfterDispatch().
  - Standard Enemy Death and EXP Payout Flow:
    1. When Health reaches 0, Health.OnNoHealth fires.
    2. EnemyDeathHandler disables collider, sets Rigidbody isKinematic = true, hides visuals, plays death VFX, plays "Death" SFX, and spawns EXP particles via ExpParticleSpawner.
    3. EnemyDropHandler evaluates drop chances, resolves walkable grid landing positions, and calls CollectibleDropNotifier.SpawnCollectible.
    4. When both death VFX and SFX finish, EnemyDeathHandler fires OnCompleted.
    5. Enemy dispatches OnCanBeReleased, signaling EnemiesSpawner to release the instance back into ObjectPool<Enemy>.
  - Barrel Death and Detonation Sequence Flow:
    1. Triggered either by lethal external damage (Health.OnNoHealth) or by self-detonation (TakeFullHpDamage()).
    2. BarrelEnemyDeathHandler hides body, disables collider, makes Rigidbody kinematic, and spawns EXP particles.
    3. If killed before detonation, contracts warning indicator, plays death VFX, and plays death audio.
    4. A LateUpdate watchdog monitors presentation time: if callbacks do not arrive within _presentationTimeout (5.0s), completion is forced automatically to prevent pooling leaks.
    5. If self-detonated, CompleteDetonationAfterDispatch() completes immediately after explosion dispatch.
    6. Fires OnCompleted, signaling Enemy.OnCanBeReleased for pool recycling.
  - Collectible Drop Scattering and Walkable Grid Snapping Flow:
    1. EnemyDropHandler iterates over configured CollectibleDropEntry items and tests drop chance percentages.
    2. For each drop, calculates a radial angle with jitter and offsets spawn position by YOffset.
    3. Evaluates target landing position using a 4-step walkable check:
       a. Tests target cell directly on IGridManager.WorldGrid.
       b. Ray-steps along the trajectory from target back to start position (4 steps).
       c. Tests the start position (where the enemy died).
       d. Executes a 5-ring spiral search around the start cell to find the nearest walkable neighbor.
       e. Falls back to start position if all checks fail.
    4. Passes prefab, spawn position, and resolved walkable target position to ICollectibleDropNotifier.SpawnCollectible().
    5. CollectibleDropNotifier retrieves instance from ObjectPool<GameObject>, attaches pool tracking, and plays concurrent DOScale (Ease.OutBack) and DOJump tweens.
  - Off-Chunk Enemy Relocation Flow:
    1. EnemiesOutsidePlayerChunkTeleporter executes every 2.0s.
    2. Identifies active enemies whose positions fall outside the active player chunk bounds.
    3. Gathers hidden walkable cells inside the player chunk via GridCellsNotVisibleByMainCamera.FillWalkableCells and shuffles them.
    4. Moves off-chunk enemies to these shuffled cells and resets their vertical velocities via ResetVerticalVelocity().
  - Pool Recycling Flow:
    1. EnemiesSpawner observes Enemy.OnCanBeReleased.
    2. Calls Enemy.OnRelease() to unsubscribe internal death listeners and reset animator triggers.
    3. Deactivates GameObject, removes instance from _instancePoolMap, releases instance back to its ObjectPool<Enemy>, emits OnSpawnedEntityReleased, and decrements CurrentlySpawnedObjectsCount.

## Rules and Invariants

- Critical behavior rules:
  - All configured enemy prefabs in EnemiesSpawner._poolEnemiesInfo must be pre-warmed during Start() up to their configured MaxAmount capacity.
  - Standard wave enemies must spawn on walkable cells that are both outside camera visibility and outside the active player chunk boundary.
  - Swarm enemies must spawn on walkable cells hidden from camera view inside the active player chunk.
  - Enemy MaxHealth is re-assigned from EnemyConfigSO upon pool retrieval in Enemy.OnGet().
  - Melee damage must only be applied on EnemyAnimator.OnAttackHitFrame, never immediately on collision contact.
  - Attack line-of-sight must be clear of impassable terrain (tested via Physics.Raycast against TerrainLayers.All) before attack animations are allowed to begin.
  - Barrel enemies require an authored visual pivot, model renderers, Animator, CircularTelegraphIndicator, and BarrelExplosionConfigSO with finite positive ranges where ActivationRange <= ExplosionRadius.
  - Suicide bomber detonations must check player body capsule overlap within ExplosionRadius before inflicting damage.
  - Collectible item drops scattered on enemy death must resolve to a valid walkable cell on WorldGrid.
  - Object pool release must never occur until death presentation sequences (VFX, SFX, and indicators) finish or reach their failsafe watchdog timeout.
  - Knockback paths must perform obstacle SphereCasts and clamp within world grid bounds to prevent enemies from being knocked into impassable walls or outside the arena.
- Ordering or sequencing guarantees:
  - ObjectPool<Enemy> instances are created in EnemiesSpawner.Awake() and pre-warmed in Start().
  - Movement is paused immediately when attack animations start and remains locked for a 0.2s post-attack delay.
  - Standard EnemyDeathHandler requires both death VFX and SFX to finish (_startEffectsToFinish = 2) before firing OnCompleted.
  - BarrelEnemyDeathHandler enforces a 5.0s presentation watchdog in LateUpdate to guarantee OnCompleted fires even if animation/audio callbacks fail.
  - EnemyAnimator cancels attack animations if the duration exceeds MAX_ATTACK_ANIMATION_DURATION (3.5s).
- Constraints contributors must preserve:
  - EnemiesSpawner, CollectibleDropNotifier, and BossEncounterService must be injected via Reflex DI interfaces, never accessed via singletons or scene searches.
  - Tunable gameplay values must live in EnemyConfigSO, BarrelExplosionConfigSO, DropAnimationConfiguration, or serialized fields.
  - All constants must be centralized in their respective Constants classes (EnemyMovementConstants, EnemyCombatConstants, EnemyAnimationConstants, BarrelEnemyConstants, BarrelFeedbackShaderConstants, BossEncounterConstants).
  - All repository files, code identifiers, comments, inspector tooltips, and documentation must remain strictly in English.

## Extension Points

- Safe extension areas:
  - Adding a new standard enemy type:
    1. Create an enemy prefab with Enemy, EnemyMovementController, EnemyAttackController, EnemyDeathHandler, EnemyDropHandler, EnemyAnimator, EnemyCollisionsController, FlowFieldMovementController, Health, Collider, Rigidbody, Audio, and VFX.
    2. Create an EnemyConfigSO asset defining health, speed, rotation speed, acceleration, damage, danger level, exp reward, and crawling state.
    3. Add a new EnemySpawnInfo entry to EnemiesSpawner._poolEnemiesInfo in the scene with the prefab, pool capacity (MaxAmount), and spawn chance info.
  - Adding a new self-destructing / suicide enemy type:
    1. Build upon the Barrel enemy architecture: attach BarrelEnemyExplosionController, BarrelEnemyDeathHandler, BarrelEnemyAttackFeedback, CircularTelegraphIndicator, and BoxCollider.
    2. Author a dedicated BarrelExplosionConfigSO specifying ExplosionRadius and ActivationRange.
    3. Ensure the Animator's attack clip includes an animation event named Call_OnAttackHitFrame.
    4. Register the prefab in EnemiesSpawner._poolEnemiesInfo.
  - Adding new collectible drops to enemies:
    1. Add a CollectibleDropEntry element to the EnemyDropHandler component on the enemy prefab.
    2. Set the collectible prefab, drop chance percentage (0-100%), and vertical spawn offset.
  - Tuning locomotion, physics, and combat constants:
    1. Adjust movement and grounding parameters in Assets/Scripts/Enemies/Constants/EnemyMovementConstants.cs.
    2. Adjust debug segments in Assets/Scripts/Enemies/Constants/EnemyCombatConstants.cs.
    3. Adjust animator parameters and watchdog durations in Assets/Scripts/Enemies/Constants/EnemyAnimationConstants.cs.
    4. Adjust suicide bomber defaults in Assets/Scripts/Enemies/Barrel/Constants/BarrelEnemyConstants.cs.
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
    - Health
    - Collider and Rigidbody (isKinematic = true)
    - VFXPlayer (blood and death VFX)
    - AudioClipPlayer
  - Barrel enemy prefabs require:
    - Enemy
    - EnemyMovementController
    - EnemyCollisionsController
    - BarrelEnemyExplosionController
    - BarrelEnemyDeathHandler
    - BarrelEnemyAttackFeedback
    - CircularTelegraphIndicator
    - EnemyAnimator
    - Health
    - BoxCollider and Rigidbody (isKinematic = true)
    - VFXPlayer (blood and death VFX)
    - AudioClipPlayer
  - Scene DI registrations in DefaultGameplaySceneInstaller:
    - IOnRandomGridPosSpawner<EnemiesSpawner> (EnemiesSpawner)
    - ISwarmEnemySpawner (EnemiesSpawner)
    - IEnemySpawnDifficultyController (EnemiesSpawner)
    - ICollectibleDropNotifier (CollectibleDropNotifier)
    - DropAnimationConfiguration
    - IInWorldSpaceSpawner<ExpParticleSpawner, float> (ExpParticleSpawner)
    - IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> (DamageNumbersSpawner)
    - IExplosionVfxPool (ExplosionVfxPool)
    - IBossEncounterService (BossEncounterService)
    - IGridManager (GridManager)
    - Camera (MainCamera)
    - IPlayerManager (PlayerManager)
- Testing implications:
  - Verify C# compilation:
    dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  - Play Mode verification checklist:
    - Pools pre-warm without runtime allocations on initial wave spawns.
    - Standard wave enemies spawn off-camera outside player chunk.
    - Swarm enemies spawn inside player chunk with optional spawn VFX.
    - Knockback paths stop safely at impassable obstacles and do not breach world grid borders.
    - Attack raycast checks block melee attacks through walls.
    - Barrel enemies trigger priming telegraph indicator upon reaching player contact range.
    - Punch rotation and color/emission transitions play smoothly during barrel attack priming.
    - Barrel detonations trigger explosion VFX, damage player if within radius, and release cleanly to pool.
    - Death sequences complete VFX and SFX before enemy pool release.
    - Collectible drops scatter cleanly onto walkable grid cells.
    - Enemies outside chunk boundary teleport smoothly to hidden cells inside player chunk.

## Integration Notes

- Upstream dependencies:
  - WaveManager drives wave enemy spawn counts.
  - SwarmSpawner triggers event-based swarm spawns.
  - IGridManager supplies world and chunk grid geometry for navigation, spawning, and drop placement.
  - IPlayerManager provides player position, collider bounds, and transform references.
  - IExplosionVfxPool provides explosion visual effects for suicide units.
- Downstream consumers:
  - Player car receives damage via IDamageable.
  - Car weapons and projectile systems target enemies on EntityLayers.Enemy.
  - LevelController consumes EXP particles spawned upon enemy death.
  - Skill upgrade UI and CollectibleDropNotifier consume dropped items.
  - BossHUDPresenter displays boss health bars triggered by BossEncounterService.
  - SwarmFreezer suppresses swarm spawns during boss encounters.
- Cross-system coupling risks:
  - If GridManager grid generation fails or returns zero walkable cells, spawning, teleportation, and drop resolution will fail or fallback to raw start positions.
  - If death VFX or audio clips are misconfigured on a prefab and fail to fire completion events, standard EnemyDeathHandler will hang unless refactored with a watchdog timeout.
  - If model materials on barrel enemies lack _BaseColor or _Color shader properties, attack feedback color transitions will log errors.

## Known Risks and Open Questions

- Known limitations:
  - Standard EnemyDeathHandler uses hardcoded _startEffectsToFinish = 2 without a failsafe watchdog timer. If a prefab is missing an audio clip or VFX player callback, the instance hangs indefinitely and is never recycled to the pool. In contrast, BarrelEnemyDeathHandler implements an explicit 5.0s presentation watchdog.
  - CollectibleDropNotifier uses ObjectPool<GameObject> rather than strongly-typed component pools, requiring runtime TryGetComponent lookups upon retrieval and release.
  - EnemiesSpawnChanceRedistributionSystem mutates SpawnChance on EnemySpawnInfo.SpawnChanceInfo directly in memory during runtime.
- Open design questions:
  - Should the failsafe watchdog pattern from BarrelEnemyDeathHandler be extracted into a shared or standardized death sequence component for all enemy types?
  - Should spawn chance redistribution operate on isolated runtime runtime copies to prevent any risk of mutating editor ScriptableObject assets during play mode?
- Suggested follow-up tasks:
  - Standardize presentation watchdog timeouts across standard EnemyDeathHandler.
  - Convert CollectibleDropNotifier to strongly-typed component pools.
