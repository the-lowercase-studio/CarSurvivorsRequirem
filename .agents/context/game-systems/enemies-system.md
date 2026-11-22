# Enemies System Documentation

## Purpose

The Enemies system manages runtime enemy entities in Car Survivors. It is responsible for:
- Standard melee enemy units (such as walking and crawling zombies) and exploding suicide units (such as barrel enemies).
- Object pooling, pre-warming, and pool recycling for high-density enemy swarms.
- Locomotion driven by flow fields or off-grid targets, smooth rotation, ground detection, slope snapping, fall physics, fall suppression, lethal void checks, and safeguards against voluntary movement through invalid internal cells.
- Swarm spawn materialization: immediate inactive enemy checkout alongside Swarm spawn circle VFX with random Y rotation, bottom-to-top vertical shader reveal (`Custom/Tier2ShinyEnemy` and `EnemyTierShineController`), untouchable non-combat gating while materializing, and seamless combat activation upon VFX completion.
- Collision detection against the player (`EntityLayers.Player`).
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
- Boss state machines or internal boss attack routines (owned by GolemStateMachine, MortarTowerStateMachine, and RailgunTowerStateMachine).

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/EnemyTierShineController.cs
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
  - Assets/Scripts/Enemies/Constants/EnemyVisualConstants.cs
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/Enemies/Bosses/Constants/BossEncounterConstants.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs
  - Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs
  - Assets/Scripts/ReflexDI/ReflexPrefabInstantiator.cs
  - Assets/Scripts/Editor/Tests/TierConfigDrivenEnemySpawningTests.cs
  - Assets/Scripts/Editor/Tests/EnemyCoreAndDeathTests.cs
  - Assets/Scripts/Editor/Tests/EnemyMovementControllerTests.cs
  - Assets/Scripts/Editor/Tests/EnemyCombatAndAnimatorTests.cs
  - Assets/Scripts/Editor/Tests/BarrelEnemyTests.cs
  - Assets/Scripts/Editor/Tests/EnemyDropsAndCollectibleNotifierTests.cs
  - Assets/Scripts/Editor/Tests/BossEncounterServiceTests.cs
  - Assets/Scripts/Editor/Tests/EnemiesSpawnerStandardWaveTests.cs
  - Assets/Scripts/Editor/Tests/EnemiesSpawnChanceRedistributionTests.cs
  - Assets/Scripts/Editor/Tests/EnemiesOutsidePlayerChunkTeleporterTests.cs
  - Assets/Scripts/Editor/Tests/SwarmSystemIntegrationTests.cs
  - Assets/Tests/PlayMode/EnemiesAndCombatPlayModeTests.cs
  - Assets/Tests/PlayMode/SwarmPlayModeTests.cs
- Designer-authored data:
  - Assets/ScriptableObjects/Enemy/EnemyConfigSO.cs
  - Assets/ScriptableObjects/Enemy/Basic/Tier2/CrawlingZombie3Config_Tier2.asset
  - Assets/ScriptableObjects/Enemy/Basic/Tier2/StandingZombie3Config_Tier2.asset
  - Assets/ScriptableObjects/Enemy/Basic/Tier2/StandingZombie4Config_Tier2.asset
  - Assets/ScriptableObjects/Enemy/Basic/Tier2/BarrelEnemyConfig_Tier2.asset
  - Assets/ScriptableObjects/Enemy/Basic/Barrel/BarrelExplosionConfigSO.cs
  - Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset
  - Assets/Shaders/Tier2ShinyEnemy.shader
  - Assets/Materials/Colors/DefaultColors/
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs
  - Standard Enemy prefabs configured with Enemy, EnemyTierShineController, EnemyMovementController, EnemyAttackController, EnemyDeathHandler, EnemyDropHandler, EnemyAnimator, EnemyCollisionsController, FlowFieldMovementController, Health, Collider, Rigidbody, Audio, and VFX (no GameObjectSelfInjector)
  - Barrel Enemy prefabs configured with Enemy, EnemyTierShineController, EnemyMovementController, EnemyCollisionsController, BarrelEnemyExplosionController, BarrelEnemyDeathHandler, BarrelEnemyAttackFeedback, CircularTelegraphIndicator, EnemyAnimator, Health, BoxCollider, Rigidbody, Audio, and VFX (no GameObjectSelfInjector)
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
  - .agents/context/game-systems/power-ups-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/game-systems/dev-console-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - Enemy: Aggregate root component for enemy GameObjects. Implements IHealthy, IDamageable, IKnockable, and IPoolable. Injects IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> and ITimeFreezeState via Reflex (pre-activation via ReflexPrefabInstantiator). Caches root _collider = GetComponent<Collider>() and serialized _config into _baseConfig in Awake() (or overrides both _baseConfig and _config via SetBaseConfig(EnemyConfigSO) before activation when EnemiesSpawner applies an EnemySpawnInfo.ConfigOverride). Exposes ApplyRuntimeConfig(EnemyConfigSO) and ApplyTierShine(bool) to synchronize Health.MaxHealth, EnemyAnimator, and _tierShineController with Config.IsShiny in Awake(), OnGet(), and runtime config switches, and restores _baseConfig in OnRelease() if a temporary runtime config override was applied. Owns Swarm spawn materialization state (IsMaterializing, MaterializationToken, BeginMaterialization(float), SetMaterializationDuration(float), CompleteMaterialization(), scaled-time Update() shader progress driver, and ResetMaterializationState() on OnGet/OnRelease/OnDisable): while IsMaterializing is true, root _collider is disabled and TakeDamage, TakeFullHpDamage, and ApplyKnockBack return early. When active and alive (Health.IsAlive()), TakeDamage emits floating damage numbers in a hemisphere pattern, decrements health, and triggers blood VFX if still alive; ApplyKnockBack delegates to EnemyMovementController when neither IsFrozen nor IsMaterializing; and OnCanBeReleased fires when death sequences finish.
  - EnemyTierShineController: [ExecuteAlways] visual component attached to the root of all basic enemy prefabs (CrawlingZombie.prefab, StandingZombie.prefab, BarrelEnemy.prefab) with explicit serialized _targetRenderers (pointing to the body SkinnedMeshRenderer without runtime hierarchy fallback searches, per ADR-005), _isShineEnabled (default false), and _fallbackMaterializeHeight (default 1.6f). Tracks _hasAppliedShine and _hasAppliedMaterialize so that when neither shine nor materialization is active or dirty, RefreshPropertyBlocks() returns immediately with zero MaterialPropertyBlock allocations or writes (keeping standard wave enemies 100% SRP-Batcher compatible on the shared DefaultColors materials using Custom/Tier2ShinyEnemy). When _isShineEnabled is true (driven by EnemyConfigSO.IsShiny on Tier 2 configs), writes EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID (_ShineEnabled = 1f) per compatible material slot via GetPropertyBlock/SetPropertyBlock. During Swarm spawn materialization (BeginMaterialization, SetMaterializeProgress, EndMaterialization), computes world-space vertical bounds across _targetRenderers (expanded by MATERIALIZE_BOUNDS_MARGIN = 0.05f, falling back to transform.position.y and _fallbackMaterializeHeight when bounds are degenerate) and updates _MaterializeEnabled, _MaterializeProgress, _MaterializeMinY, and _MaterializeHeight on Custom/Tier2ShinyEnemy while preserving _ShineEnabled and BarrelEnemyAttackFeedback overrides.
  - EnemyMovementController: Implements IMovementController. Coordinates flow-field locomotion, external speed multipliers (SetMovementSpeedMultiplier), grounding, gravity, and obstacle-checked knockback. While _enemy.IsMaterializing is true, FixedUpdate() zeroes _currentVelocity and returns early, MoveToPositionInTimeIgnoringSpeed() returns early, and CanBeTeleported returns false. Ground probes ignore triggers and preserve the existing 1.5-unit origin/3.5-unit range and 20-unit snapping speed. Ungrounded enemies accumulate gravity, suppress ordinary horizontal pursuit after falling one unit below the last ground sample, and die below Y = -10. Knockback continues in XZ without a world clamp while gravity owns Y. Grounded voluntary displacement is checked after separation and acceleration, scaling effectiveSpeed by _movementSpeedMultiplier. CanBeTeleported excludes materializing, stopped, knocked, airborne, falling, and frozen movement, and enforces a 3.0s unfrozen grace period (_teleportAllowedUnfrozenTime, set via RefreshTeleportGracePeriod() using Enemy.UnfrozenTime in OnEnable and ResetAfterRelocation); ResetAfterRelocation resets _movementSpeedMultiplier to 1f, clears spatial state while preserving lifecycle/attack flags, and refreshes the teleport grace deadline, while OnDisable resets _movementSpeedMultiplier to 1f and clears _teleportAllowedUnfrozenTime to 0f.
  - EnemyCollisionsController: Implements ICollisionsController. Performs periodic SphereCastAll queries (default every 0.05s) on EntityLayers.Player using _collisionRadius (1.0f), skipping checks while _enemy.IsMaterializing is true, filtering out its own trigger colliders, and emitting OnCollisionWithPlayer.
  - EnemyAttackController: Handles standard melee attacks. Subscribes to EnemyCollisionsController.OnCollisionWithPlayer. Ignores collisions when _enemy.IsFrozen, _enemy.IsMaterializing, or _enemyAnimator.IsPlayingAttackAnimation is true. Validates candidate colliders via CanAttackTarget (within _attackRange, within _attackArcAngle cone [default 60 deg], and unobstructed Physics.Raycast line-of-sight against TerrainLayers.All) before assigning _currentAttackedTarget and triggering PlayAttackAnimation on EnemyAnimator. When OnAttackHitFrame fires (and !_enemy.IsMaterializing), verifies _currentAttackedTarget is still within _attackRange by scanning all colliders returned by Physics.OverlapSphere and applies Config.Damage to IDamageable.
  - EnemyAnimator: Implements IAttackAnimationPlayer. Sets Animator parameters (Speed, IsOnGround, IsMovingByCrawling, Attack). Toggles layer weights between walking layer (0) and crawling layer (1) based on Config.IsMovingByCrawling. Bridges animation events into OnAttackAnimationStart, OnAttackHitFrame, and OnAttackAnimationEnd. Periodically synchronizes attack animation state (every 0.05s), automatically calling OnAttackAnimationEnd if transition state enters falling or if duration exceeds MAX_ATTACK_ANIMATION_DURATION (3.5s) in Enemy.UnfrozenTime.
  - BarrelEnemyExplosionController: Controls suicide bomber logic for barrel enemies. Follows a state machine: Inactive, Approaching, Priming, Canceling, Detonating, Dying. Ignores contacts and hit frames while _enemy.IsMaterializing is true. On collision with player body within ActivationRange (from BarrelExplosionConfigSO), transitions to Priming, suppresses pursuit on EnemyMovementController, displays attached CircularTelegraphIndicator, triggers the attack animation, and activates BarrelEnemyAttackFeedback. Implements a watchdog in Update() (using _enemy.UnfrozenTime) that cancels priming if hit event is delayed past 3.5s. When OnAttackHitFrame fires, enters Detonating, triggers explosion VFX from IExplosionVfxPool, tests capsule overlap against player body with DISK_CONTACT_TOLERANCE (0.0001f), applies damage, hides body visuals via BarrelEnemyDeathHandler.HideBody(), calls TakeFullHpDamage(), and signals CompleteDetonationAfterDispatch().
  - BarrelEnemyAttackFeedback: Coordinates telegraph feedback during priming on barrel enemies. Drives an infinite DOPunchRotation loop on a dedicated visual pivot (_visualPivot). Collects model renderers and builds MaterialPropertyBlock bindings for _BaseColor / _Color and _EmissionColor. Samples Animator normalized time up to _hitNormalizedTime, advancing a DOTween sequence to lerp colors towards warning tints. Fully resets rotation and restores original colors when cancelled or disabled.
  - BarrelEnemyDeathHandler: Implements INeedToCompleteBeforeDisable. Manages death and detonation presentation for barrel enemies. Subscribes to Health.OnNoHealth, hides the body, disables collider, makes Rigidbody kinematic, spawns EXP particles, and triggers death VFX and death audio. Includes an explicit watchdog timer (_presentationTimeout, default 5.0s) in LateUpdate: if visual or audio callbacks are dropped, the watchdog automatically forces completion to ensure the pooled enemy is never orphaned.
  - EnemyDeathHandler: Standard enemy death sequence handler. Implements INeedToCompleteBeforeDisable. Validates positive finite _presentationTimeout (default 5.0s) fail-fast in Awake(). On OnEnable, dynamically counts non-null _deathVfxPlayer and _enemy.AudioClipPlayer subscriptions into _effectsToFinish and subscribes to Health.OnNoHealth. On death, disables collider, sets Rigidbody isKinematic = true, hides visuals, plays death VFX and "Death" SFX, spawns EXP particles via IInWorldSpaceSpawner<ExpParticleSpawner, float>, and fires idempotent OnCompleted once all subscribed effects finish or when LateUpdate()'s _presentationTimeout (5.0s) watchdog elapses.
  - EnemyDropHandler: Subscribes to Health.OnNoHealth. Fails fast if injected _animationConfig or _gridManager is null. Skips entries where Prefab == null or DropChancePercent <= 0f before rolling RNG. Evaluates configured CollectibleDropEntry drop chance percentages, calculates 360-degree radial scatter positions with height offsets, resolves valid walkable destination points using a 5-tier algorithm (direct target cell, ray-stepping back toward start, start cell, 5-ring spiral search around start cell, or startPos fallback including when WorldGrid is uninitialized), and delegates instantiation to ICollectibleDropNotifier.SpawnCollectible.
  - CollectibleDropNotifier: Implements ICollectibleDropNotifier and ICollectibleDropSuppressor. Validates non-null prefab (ArgumentNullException), manages ObjectPool<GameObject> instances per collectible item prefab, kills competing ScaleToAnimationOnEnable tweens via go.transform.DOKill() on checkout, animates drops with a target-bound DOTween Sequence (.SetTarget(go.transform)) combining DOScale (Ease.OutBack) and DOJump to the resolved target position, and listens to ICollectible.OnCollected and IPoolable.OnCanBeReleased to remove _instancePoolMap tracking and recycle items back into their pools.
  - EnemiesOutsidePlayerChunkTeleporter: Every two seconds, computes exact GridPlayerChunk XZ bounds from corner cells (Cells[0, 0] and Cells[Width - 1, Height - 1] expanded by CellSize * 0.5f, aborting safely if playerChunk is invalid or corner cells are null) and selects active, living, movable enemies outside those XZ bounds whose CanBeTeleported passes (respecting the 3.0s unfrozen post-spawn/post-relocation grace window and skipping materializing enemies). It rechecks eligibility before committing, validates supported hidden placements, and calls ResetAfterRelocation() after assignment (clearing movement state and starting a fresh 3.0s grace period). Knockback/falling enemies retain their trajectory and gravity; there is no below-chunk rescue.
  - EnemiesSpawner: Implements IOnRandomGridPosSpawner<EnemiesSpawner>, ISwarmEnemySpawner, IEnemySpawnDifficultyController, and IActiveEnemiesProvider. Injects IGridManager, Camera, IRunSessionService, and Reflex.Core.Container. In Awake(), clones active tier EnemySpawnInfo entries (from IRunSessionService.ActiveTierConfig.EnemySpawning.EnemySpawnInfos or scene fallback) via EnemySpawnInfo.CreateRuntimeCopy() into _runtimeEnemiesInfo so redistribution never mutates ScriptableObject assets, and instantiates pooled enemies under an inactive staging parent via ReflexPrefabInstantiator.InstantiateInjectedObject with beforeActivate: enemy => enemy.SetBaseConfig(localInfo.ConfigOverride). Pre-warms pools on Start (suppressing OnSpawnedEntityReleased during pre-warming). Spawns standard wave enemies on off-camera walkable cells outside the player chunk (using _outerSpawnBufferCells and _maxEnemiesPerCell). Spawns swarm enemies inside the player chunk (allowing camera-visible walkable supported cells via requireHiddenFromCamera: false) with immediate inactive materialization during _swarmSpawnVfxPrefab playback. Drives spawn weight updates via EnemiesSpawnChanceRedistributionSystem.
  - EnemiesSpawnChanceRedistributionSystem: Gradually decrements spawn chance of lower-tier enemies after spawn batches and redistributes probability geometrically across higher-tier enemies whose threshold flags are unlocked, operating strictly on cloned runtime EnemySpawnInfo instances. Supports difficulty scalars from external game events via IncreaseSpawnChanceRedistributionFactor.
  - BossEncounterService: Implements IBossEncounterService. Tracks tower landmarks and GolemStatue (RegisterGolemStatue, UnregisterGolemStatue), counts defeated towers against required threshold (default 2, ignoring null tower references in NotifyTowerDefeated) and emits OnTowerDefeated(DefeatedTowersCount, RequiredTowersCount) to unlock GolemStatue for player-triggered Golem awakening, binds boss health to IBossHUDPresenter, suppresses standard swarms via ISwarmFreezer, applies tier boss config overrides from IRunSessionService, and instantiates ground-snapped NextStagePortal instances via ReflexPrefabInstantiator upon Golem defeat.

- Key interfaces:
  - IHealthy, IDamageable, IKnockable: Health management, damage reception, and knockback handling.
  - IPoolable: Pool lifecycle contracts (OnGet, ReturnToPool, OnRelease, OnCanBeReleased).
  - INeedToCompleteBeforeDisable: Contract delaying pool release until death presentation finishes (OnCompleted).
  - IMovementController: Locomotion, knockback, grounding, CanBeTeleported, ResetAfterRelocation, and SetMovementSpeedMultiplier(float multiplier). Concrete EnemyMovementController also owns pursuit suppression and movement stops.
  - IAttackAnimationPlayer: Animation event forwarding (OnAttackAnimationStart, OnAttackHitFrame, OnAttackAnimationEnd).
  - ICollisionsController: Contact detection interface emitting OnCollisionWithPlayer.
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
    - POST_RELOCATION_TELEPORT_GRACE_SECONDS = 3.0f (unfrozen seconds after `OnEnable` spawn/activation or `ResetAfterRelocation` relocation during which `CanBeTeleported` stays false)
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
  - EnemyVisualConstants:
    - SHINE_ENABLED_PROPERTY_ID = Shader.PropertyToID("_ShineEnabled")
    - MATERIALIZE_ENABLED_PROPERTY_ID = Shader.PropertyToID("_MaterializeEnabled")
    - MATERIALIZE_PROGRESS_PROPERTY_ID = Shader.PropertyToID("_MaterializeProgress")
    - MATERIALIZE_MIN_Y_PROPERTY_ID = Shader.PropertyToID("_MaterializeMinY")
    - MATERIALIZE_HEIGHT_PROPERTY_ID = Shader.PropertyToID("_MaterializeHeight")
    - DEFAULT_MATERIALIZE_HEIGHT = 1.6f
    - MATERIALIZE_BOUNDS_MARGIN = 0.05f
    - MIN_MATERIALIZE_HEIGHT = 0.1f
  - BossEncounterConstants:
    - DEFAULT_GOLEM_DISPLAY_NAME = "ANCIENT GOLEM"
    - DEFAULT_SPAWN_OFFSET_DISTANCE = 15f
    - DEFAULT_REQUIRED_TOWERS = 2
    - DEFAULT_DELAY_BEFORE_GOLEM_SPAWN = 10.0f
    - PORTAL_SPAWN_RAYCAST_HEIGHT = 25f
    - PORTAL_SPAWN_RAYCAST_DISTANCE = 60f

- Runtime flows:
  - Standard Wave Spawning Flow:
    1. WaveManager triggers EnemiesSpawner.SpawnAtRandomGridPos(count).
    2. Spawner queries GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk.
    3. RandomEnemyInfoBasedOnSpawnChance selects enemy prefab configurations based on current weighted probabilities.
    4. ObjectPool<Enemy>.Get() retrieves an instance, calls Enemy.OnGet(), hooks OnCanBeReleased, sets position, activates GameObject, and increments CurrentlySpawnedObjectsCount.
    5. EnemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance() progressively shifts spawn weights toward higher tiers.
  - Swarm Spawning & Materialization Flow:
    1. SwarmSpawner invokes EnemiesSpawner.SpawnSpecificEnemy(enemyInfo, count).
    2. Spawner selects walkable, ground-supported, occupancy-valid cells inside the active player chunk via GridCellsNotVisibleByMainCamera.GetRandomWalkableCells(..., requireHiddenFromCamera: false) and TryGetPlacement(..., requireHiddenFromCamera: false) (allowing placement inside the camera view).
    3. If _swarmSpawnVfxPrefab is assigned, instantiates VFX at spawnPos, immediately checks out the pooled Enemy at spawnPos with random Y rotation, calls enemy.BeginMaterialization(0f), records PendingSpawn with enemy.MaterializationToken, subscribes to vfx.OnVFXFinished, plays the VFX, and sets enemy.SetMaterializationDuration(vfx.LastScheduledDuration). While materializing, the enemy stands idle with its root Collider disabled, revealing bottom-to-top via Custom/Tier2ShinyEnemy shader clipping and fiery crimson-orange emissive edge/rim lighting, and ignores all movement, attacks, collisions, knockback, damage, Nuke clears, and chunk teleportation. When OnVFXFinished fires, HandleSwarmVfxFinished validates the materialization token and ground support and calls enemy.CompleteMaterialization() (or enemy.ReturnToPool() if ground support was lost or the spawner disabled). If _swarmSpawnVfxPrefab is null, the enemy spawns and activates immediately.
  - Movement, Grounding, Falling, and Knockback Flow:
    1. FixedUpdate runs EnemyMovementController.MovementHandler().
    2. Raycasts downward (3.5m) against TerrainLayers.Walkable. If grounded, snaps Y smoothly to terrain point with speed 20. If ungrounded, applies 25m/s² downward gravity; if Y drops below -10m, triggers TakeFullHpDamage(). If falling past 1m below last ground point, horizontal movement is paused.
    3. Active knockback interpolates XZ toward its obstacle-checked target with sine easing while gravity continues to own Y. It is not clamped to the world rectangle; skills can knock enemies off supported terrain and outside world bounds.
    4. When moving along flow field, samples desired direction from IFlowFieldMovementController, computes effectiveSpeed = Config.MovementSpeed * _movementSpeedMultiplier, accelerates toward target velocity, and updates position.
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
    2. Computes exact `GridPlayerChunk` XZ bounds from `Cells[0, 0]` and `Cells[Width - 1, Height - 1]` expanded by `CellSize * 0.5f` (guarding null corner cells) and identifies active, living enemies outside the chunk in XZ whose `CanBeTeleported` allows relocation (stopped, knocked, airborne, falling, frozen, and recently activated/relocated enemies within `POST_RELOCATION_TELEPORT_GRACE_SECONDS = 3.0f` unfrozen seconds remain untouched; no Y-based rescue is performed).
    3. Gathers hidden walkable cells inside the player chunk via GridCellsNotVisibleByMainCamera.FillWalkableCells and shuffles them.
    4. Revalidates eligibility and exact support before moving. ResetAfterRelocation clears horizontal/vertical velocity, knockback endpoints/timing, off-grid targets, separation, and ground/previous-position samples at the assigned position, and refreshes `_teleportAllowedUnfrozenTime` (`Enemy.UnfrozenTime + 3.0f`) to prevent back-to-back thrashing. Stopped flags, attack delays, and barrel pursuit suppression are preserved.
  - Pool Recycling Flow:
    1. EnemiesSpawner observes Enemy.OnCanBeReleased.
    2. Calls Enemy.OnRelease() to unsubscribe internal death listeners and reset animator triggers.
    3. Deactivates GameObject, removes instance from _instancePoolMap, releases instance back to its ObjectPool<Enemy>, emits OnSpawnedEntityReleased, and decrements CurrentlySpawnedObjectsCount.

## Rules and Invariants

- Critical behavior rules:
  - All configured enemy prefabs in EnemiesSpawner._poolEnemiesInfo must be pre-warmed during Start() up to their configured MaxAmount capacity.
  - Standard wave enemies must spawn on walkable cells that are both outside camera visibility and outside the active player chunk boundary.
  - Swarm enemies spawn on walkable, ground-supported cells inside the active player chunk (including camera-visible cells) and materialize in an inactive, untouchable state over the Swarm spawn VFX duration before activating into combat.
  - Enemy MaxHealth is re-assigned from EnemyConfigSO upon pool retrieval in Enemy.OnGet().
  - Melee damage must only be applied on EnemyAnimator.OnAttackHitFrame, never immediately on collision contact.
  - Attack line-of-sight must be clear of impassable terrain (tested via Physics.Raycast against TerrainLayers.All) before attack animations are allowed to begin.
  - Barrel enemies require an authored visual pivot, model renderers, Animator, CircularTelegraphIndicator, and BarrelExplosionConfigSO with finite positive ranges where ActivationRange <= ExplosionRadius.
  - Suicide bomber detonations must check player body capsule overlap within ExplosionRadius before inflicting damage.
  - Collectible item drops scattered on enemy death must resolve to a valid walkable cell on WorldGrid.
  - Object pool release must never occur until death presentation sequences (VFX, SFX, and indicators) finish or reach their failsafe watchdog timeout.
  - Knockback paths retain obstacle SphereCasts and can cross world/ground edges. Ordinary falls retain DeathVolume and low-Y death behavior.
  - External movement speed modifiers applied via IMovementController.SetMovementSpeedMultiplier (such as the 0.6x slow from FireTailsService) must automatically reset to 1.0f in EnemyMovementController.OnEnable(), OnDisable(), and ResetAfterRelocation() so pooled or relocated enemies never retain stale slow multipliers.
  - Spawn candidates require exact support before pool checkout. Swarm spawns with VFX check out the enemy immediately into IsMaterializing state and revalidate walkability and ground support on VFX completion (regardless of camera visibility or player chunk movement during the VFX); if ground support is lost or EnemiesSpawner disables, the materializing enemy returns to its pool via ReturnToPool().
- Ordering or sequencing guarantees:
  - ObjectPool<Enemy> instances are created in EnemiesSpawner.Awake() and pre-warmed in Start() (with OnSpawnedEntityReleased suppressed during pre-warming).
  - Movement is paused immediately when attack animations start and remains locked for a 0.2s post-attack delay.
  - Both standard EnemyDeathHandler and BarrelEnemyDeathHandler track pending death effects and enforce a 5.0s presentation watchdog (_presentationTimeout) in LateUpdate to guarantee OnCompleted fires even if animation, VFX, or audio callbacks fail.
  - EnemyAnimator cancels attack animations if the duration exceeds MAX_ATTACK_ANIMATION_DURATION (3.5s) in Enemy.UnfrozenTime.
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
    dotnet build CarSurvivors.Runtime.csproj -p:BuildProjectReferences=false
    dotnet build CarSurvivors.Editor.Tests.csproj -p:BuildProjectReferences=false
    dotnet build CarSurvivors.PlayMode.Tests.csproj -p:BuildProjectReferences=false
  - Automated EditMode & PlayMode tests:
    - Assets/Scripts/Editor/Tests/EnemyCoreAndDeathTests.cs, EnemyMovementControllerTests.cs, EnemyCombatAndAnimatorTests.cs, BarrelEnemyTests.cs, EnemyDropsAndCollectibleNotifierTests.cs, BossEncounterServiceTests.cs, TierConfigDrivenEnemySpawningTests.cs, EnemiesSpawnerStandardWaveTests.cs, EnemiesSpawnChanceRedistributionTests.cs, EnemiesOutsidePlayerChunkTeleporterTests.cs, and SwarmSystemIntegrationTests.cs.
    - Assets/Tests/PlayMode/EnemiesAndCombatPlayModeTests.cs and Assets/Tests/PlayMode/SwarmPlayModeTests.cs.
  - Play Mode verification checklist:
    - Pools pre-warm without runtime allocations on initial wave spawns.
    - Standard wave enemies spawn off-camera outside player chunk.
    - Swarm enemies spawn inside player chunk with optional spawn VFX.
    - Knockback stops safely at impassable obstacles, crosses world edges, and continues falling without teleporter interruption.
    - Attack raycast checks block melee attacks through walls.
    - Barrel enemies trigger priming telegraph indicator upon reaching player contact range.
    - Punch rotation and color/emission transitions play smoothly during barrel attack priming.
    - Barrel detonations trigger explosion VFX, damage player if within radius, and release cleanly to pool.
    - Death sequences complete VFX and SFX before enemy pool release.
    - Collectible drops scatter cleanly onto walkable grid cells.
    - Enemies outside chunk boundary teleport smoothly to hidden cells inside player chunk without re-teleporting during the 3.0s unfrozen post-spawn/post-relocation grace window (countdown pauses while frozen by FreezeTimer).

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
  - If model materials on barrel enemies lack _BaseColor or _Color shader properties, attack feedback color transitions will log errors.

## Known Risks and Open Questions

- Known limitations:
  - CollectibleDropNotifier uses ObjectPool<GameObject> rather than strongly-typed component pools, requiring runtime TryGetComponent lookups upon retrieval and release.
  - StandingZombieLevel1 embedded body material and Emisive* eye sub-materials currently use URP Lit rather than Custom/Tier2ShinyEnemy, so EnemyTierShineController skips those slots during shader materialization clipping until migrated to Custom/Tier2ShinyEnemy.
- Open design questions:
  - Should the shared death presentation watchdog logic in EnemyDeathHandler and BarrelEnemyDeathHandler be extracted into a common base class?
- Suggested follow-up tasks:
  - Convert CollectibleDropNotifier to strongly-typed component pools.
  - Optionally migrate StandingZombieLevel1 body material and emissive eye materials to Custom/Tier2ShinyEnemy if full-mesh clipping on those slots is desired.
