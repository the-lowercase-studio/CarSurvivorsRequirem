# Golem Boss System Documentation

## Purpose

The Golem Boss system manages the multi-phase boss encounter for the Ancient Golem in Car Survivors. It is responsible for encounter lifecycle management, boss spawning and placement, swarm wave suppression, finite state machine execution, obstacle-sliding pursuit navigation, Mecanim animation bridging and event forwarding, detachable rocket arm projectile behaviors, dynamic linear attack wavefront collisions, ground telegraph indicators, multi-phase enrage progression, boss HUD presentation, and victory stage progression portal spawning.

It is not responsible for standard enemy wave timers and pooling, flow-field vector computation, player vehicle physics, or general UI layouts outside the Boss HUD contract.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Bosses/BossManager.cs
  - Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs
  - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Constants/GolemBossConstants.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Movement/GolemMovementController.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimationEventsForwarder.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmSocketController.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemLinearAttackHitbox.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemStompTrigger.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/IGolemState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/GolemStateMachine.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemPursuitState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemStompState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemDeathState.cs
- Designer-authored data and prefabs:
  - Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs
  - Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfig.asset
  - Assets/Prefabs/Enemies/Bosses/Golem/GolemBoss.prefab
  - Assets/Prefabs/Enemies/Bosses/Golem/Golem_L_Arm_Projectile.prefab
  - Assets/Prefabs/Enemies/Bosses/Golem/Golem_R_Arm_Projectile.prefab
  - Assets/Animations/Enemies/Bosses/Golem/GolemBossAnimationContoller.controller
- Related systems:
  - UI HUD: Assets/Scripts/UI/HUD/BossHUDPresenter.cs
  - Swarm suppression: Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs (ISwarmFreezer)
  - Ground telegraphs: Assets/Scripts/Indicators/CircularTelegraphIndicator.cs, Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs
  - Health and damage: Assets/Scripts/HealthSystem/Health.cs, Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Experience reward: Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
  - VFX: Assets/Scripts/VFX/VFXPlayer.cs
  - Audio: Assets/Scripts/Audio/AudioClipPlayer.cs
  - DI installer: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/vfx-system.md
  - .agents/context/game-systems/audio-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/unity-root-cause/SKILL.md

## Architecture and Data Flow

- Core components:
  - BossManager: Scene-level manager implementing IBossManager. Injected with IBossHUDPresenter, ISwarmFreezer, and IPlayerManager. Spawns the GolemBoss prefab (either ahead of the player via _spawnOffsetDistance or at a provided position), freezes active swarm waves during the encounter via ISwarmFreezer.IsSuppressed, initializes the Boss HUD display, listens to OnBossDefeated, restores swarm spawning on victory, and instantiates the next stage portal prefab at the defeat location. Exposes debug spawning via _debugSpawnKey (P key).
  - GolemBoss: Root aggregate entity implementing IGolemBoss, IDamageable, and IKnockable. Requires a Health component. Injected through Reflex with IPlayerManager, IGridManager, damage number spawner, and EXP particle spawner. Manages health changes, phase progression (Phase 1, 2, 3), enrage visual state via MaterialPropertyBlock, knockback immunity, active telegraph tracking and cleanup, linear attack hitbox deactivation, state machine updates, and defeat event emission (OnBossDefeated).
  - GolemStateMachine: Discrete state machine coordinating active IGolemState instances and ticking individual cooldown timers (LeapCooldownTimer, StompCooldownTimer, LinearFistCooldownTimer, SkyBarrageCooldownTimer). Cooldown timers initialize with staggered values (GolemBossConstants.INITIAL_*_COOLDOWN) to ensure an opening pursuit phase.
  - GolemPursuitState: Primary navigation state. Coordinates GolemMovementController to follow PlayerPosition strictly while IsMovingAnimationPlaying is true. Checks attack priorities when arms are docked and executes immediate melee stomps when within StompRadius or anti-kiting leap slams when distance exceeds LeapTriggerMaxDistance.
  - GolemStompState: Dedicated melee stomp attack state. Sets movement kinematic and locks position, triggers the Stomp animation, handles damage application through TriggerStompDamage (with dual-path trigger via animation event or STOMP_IMPACT_DELAY fallback), and returns smoothly to the calling state (GolemPursuitState or GolemSkyBarrageState) after STOMP_TOTAL_DURATION.
  - GolemLeapSlamState: Anti-kiting and rotational AOE attack state. Locks movement, sets Rigidbody to kinematic, rotates toward target, spawns a grid-snapped CircularTelegraphIndicator at the player position, triggers LeapTakeoff animation, executes a parabolic DOTween arc to the apex and snapped landing point over LeapAirTime, snaps to exact landing coordinates via SetPosition, restores non-kinematic physics, plays LeapLand animation and GolemSlam SFX, applies area capsule damage, and transitions back to pursuit after LeapLandingDuration.
  - GolemLinearFistState: Horizontal rocket punch state. Halts movement, locks kinematic mode, rotates to face the player, displays a RectangularTelegraphIndicator, plays LinearFist animation, activates GolemLinearAttackHitbox to sweep the lane synchronously with arms via DOTween, detaches both GolemArmProjectile visual entities, and transitions back to pursuit once both arms dock.
  - GolemLinearAttackHitbox: Indicator-synchronized attack wavefront controller implementing IGolemLinearAttackHitbox. Dynamically configures a trigger BoxCollider with kinematic Rigidbody to LinearFistWidth, LinearFistHitboxHeight, and LinearFistHitboxDepth, translates forward synchronously with flying arms via DOTween, deduplicates player hits per thrust pass using a reusable HashSet and zero-alloc overlap box checks, and deactivates on reaching maximum range or state exit.
  - GolemSkyBarrageState: Detached mortar bombardment state. Halts boss movement and sets kinematic mode during the initial launch phase, playing SkyBarrage animation and GolemRoar SFX. Once arms launch into flight, the boss body resumes standard pursuit and can execute localized foot stomps when within StompRadius without interrupting active aerial bombardments. In the sky, each arm independently executes multi-cycle drops on randomized circular telegraph positions around the player, dealing capsule damage on impact. Upon completing all phase cycles, arms dock via ReturnAndDock and the boss resumes standard pursuit.
  - GolemDeathState: Terminal state triggered on Health.OnNoHealth. Halts movement and animations, dismisses active telegraphs, docks all arms, plays GolemDeath SFX, plays death VFX, spawns EXP particles, and triggers OnBossDefeated.
  - GolemMovementController: Implements IGolemMovementController. Drives a Rigidbody with frozen rotation, supports kinematic mode switching (SetKinematic) for scripted aerial trajectories, resets velocities on SetPosition, and calculates smooth sliding directions around impassable obstacles using Physics.SphereCast against TerrainLayers.Impassable and Vector3.ProjectOnPlane.
  - GolemArmSocketController: Implements IGolemArmSocketController. Manages Left and Right arm projectile pairing, socket transforms, and toggles between rigged mesh visuals (docked) and detached projectile gameObjects (airborne).
  - GolemArmProjectile: Standalone visual projectile controller for each detached arm. Implements GolemArmState lifecycle (Docked, LinearThrust, SkyAirborne, SkyDropping, Returning) driven by DOTween sequences, managing trail renderers, impact VFX, and OverlapCapsule damage on sky landing. Contains no physical colliders.
  - GolemAnimator: Implements IGolemAnimator. Bridges Mecanim Animator triggers and parameters via cached string hashes. Exposes IsMovingAnimationPlaying (verifies current or transitioning state is Walking) and IsAttackAnimationPlaying. Exposes animation event callbacks for attack release and landing timing.
  - GolemAnimationEventsForwarder: Helper component attached alongside Animator clips to forward Unity Animation Events (Call_OnLinearFistRelease, Call_OnSkyBarrageRelease, Call_OnLeapTakeoffComplete, Call_OnLeapLandComplete, Call_OnStompImpact) to GolemAnimator.
  - GolemStompTrigger: Optional leg trigger component. Tracks overlapping player colliders via OnTriggerEnter/Exit and applies stomp damage, with fallback to Physics.OverlapBox over bounds.
  - GolemBossConfigSO: ScriptableObject holding combat balance parameters: health, movement and rotation speed, body contact damage, EXP reward, phase multipliers, leap slam timings/ranges, stomp timings/ranges, linear fist dimensions/speeds, sky barrage cycles/radii, and enrage colors.
  - BossHUDPresenter: UI presenter implementing IBossHUDPresenter. Binds to IHealth, displays boss name, animates health slider changes with DOValue, evaluates health gradient fill color, and hides on boss defeat.
- Key interfaces:
  - IBossManager: Encounters contract for boss spawning and active boss status query.
  - IGolemBoss: Aggregates subsystem access (Movement, Arms, LinearAttackHitbox, Animator, AudioClipPlayer, WorldGrid, Transform, Config, Phase multipliers, player distance/direction properties) and telegraph helper methods.
  - IGolemLinearAttackHitbox: Controls activation, dynamic sizing, forward wavefront translation, and deactivation for linear attack hitbox.
  - IGolemMovementController: Controls navigation enable/disable, kinematic toggling (SetKinematic), movement targeting, position snapping (SetPosition), and obstacle sliding.
  - IGolemArmSocketController: Controls arm projectiles, docking states, and socket initialization.
  - IGolemAnimator: Controls animation trigger and parameter updates, exposing IsMovingAnimationPlaying.
  - IGolemState: State lifecycle contract (Enter, Update, FixedUpdate, Exit).
  - IBossHUDPresenter: UI contract for showing and hiding the boss health bar.
  - ISwarmFreezer: Service contract to suppress swarm spawns during boss combat.
- Runtime flow:
  - Spawn & Initialization: BossManager instantiates GolemBoss prefab, subscribes to OnBossDefeated, sets ISwarmFreezer.IsSuppressed = true, and displays the boss health bar via IBossHUDPresenter.
  - State Loop & Pacing: GolemStateMachine updates the active state in Update() and FixedUpdate(), ticking attack cooldown timers.
  - Pursuit & Attack Priority: GolemPursuitState tracks the player with GolemMovementController strictly while walking animations play. Attack selection follows strict conditions:
    - Melee Stomp: Checked continuously during pursuit and sky barrage whenever the player enters StompRadius, transitioning into GolemStompState which locks movement for the stomp duration.
    - Anti-Kiting Leap Slam: Triggered immediately if the player is beyond LeapTriggerMaxDistance, launching the boss along a kinematic parabolic arc to land precisely at the circular telegraph center.
    - Linear Rocket Fists: Initiated when Arms.AreBothArmsDocked is true, cooldown <= 0, and player is within LinearFistMaxDistance * 1.3f. Both arms detach and thrust forward along a rectangular telegraph lane while GolemLinearAttackHitbox sweeps the corridor dealing single-pass damage, then retract and dock.
    - Sky Arm Barrage: Highest priority arm attack when Arms.AreBothArmsDocked is true. Both arms launch skyward while the body holds still during the launch phase, then the body resumes pursuit and executes localized melee stomps while arms execute staggered multi-cycle bombardments on circular ground telegraphs around the player before returning to dock.
  - Damage & Phase Enrage: Incoming damage spawns damage numbers, reduces Health, and plays blood VFX. When HP drops below Phase2HealthPercent (60%) or Phase3HealthPercent (30%), phase multipliers accelerate movement speed, arm velocity, and cooldown recovery. Phase 3 triggers Enrage with roar SFX, enrage VFX, and red material emission property overrides via MaterialPropertyBlock.
  - Defeat & Stage Progression: On Health.OnNoHealth, GolemDeathState cleans up active telegraphs and resets arms. EXP particles and death VFX spawn, BossManager restores swarm spawning, hides the HUD, and instantiates the next stage progression portal.

## Rules and Invariants

- Critical behavior rules:
  - Knockback immunity: GolemBoss implements IKnockable but ignores knockback force to preserve heavy boss presence.
  - Kinematic locking during attack stances: Non-walking animations and attack stances (Stomp, Linear Fist charge, Sky Barrage launch, Leap Slam jump, Death) lock boss movement in place using SetKinematic(true) to eliminate physics jitter and collision drift. FixedUpdate navigation only drives movement when walking and CanMove is true.
  - Kinematic leap trajectory & exact landing: During Leap Slam, the Rigidbody is set to kinematic during DOTween flight, and SetPosition(snappedTarget) is executed on impact to guarantee landing at the exact center of the circular ground telegraph.
  - Stomp state delegation & return preservation: Melee stomps during pursuit or airborne Sky Barrage delegate to GolemStompState with SetReturnState, ensuring clean return to the caller state without breaking active aerial barrage sequences.
  - Arm availability constraint: Detached arm attacks (Linear Fist, Sky Barrage) can only initiate when Arms.AreBothArmsDocked is true.
  - Staggered dual-arm Sky Barrage: Left and right arms execute independent drop sequences with staggered initial launch timing (SkyArmInitialStaggerDelay), allowing asynchronous impact and repositioning.
  - Body mobility during Sky Barrage: The boss body remains active, continuing pursuit and executing foot stomps once the initial launch animation phase completes and arms are in flight.
  - Telegraph safety: All active telegraph indicators are tracked in _activeTelegraphs and dismissed cleanly upon state transitions, boss defeat, or component deactivation.
  - Initial cooldown stagger: Cooldowns initialize with non-zero delays (GolemBossConstants.INITIAL_*_COOLDOWN) to ensure an initial pursuit phase before attack execution.
  - Swarm suppression: Swarm waves must remain suppressed while IsBossActive is true.
- Ordering or sequencing guarantees:
  - Rigged arm mesh is hidden and projectile GameObject activated on detachment; projectile GameObject is deactivated and rigged mesh restored upon docking.
  - MaterialPropertyBlock is used for Enrage tinting to avoid creating runtime material instance leaks.
  - Telegraph indicators snap to the WorldGrid to match arena geometry.
- Constraints contributors must preserve:
  - Keep BossManager bound as a singleton through Reflex DI in DefaultGameplaySceneInstaller.
  - Preserve designer configuration values in GolemBossConfigSO rather than hardcoding combat values.
  - Ensure any new attacks or state transitions properly clean up DOTween sequences in Exit().

## Extension Points

- Safe extension areas:
  - Adding new boss attacks: Create a new IGolemState class in Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/, add a cooldown timer to GolemStateMachine, add animation triggers to GolemAnimator and GolemBossConstants, and configure balance parameters in GolemBossConfigSO.
  - Tuning combat balance: Modify GolemBossConfig.asset in the Unity Inspector to adjust health, movement speed, cooldowns, telegraph warning times, damage, and phase multipliers without touching code.
  - Expanding multi-boss encounters: Extend IBossManager to accept specific boss prefab IDs or configurations.
- Required dependencies and contracts:
  - GolemBoss requires Health, Rigidbody, Collider, GolemMovementController, GolemArmSocketController, GolemAnimator, AudioClipPlayer, and telegraph indicator references on its prefab hierarchy.
  - GolemBoss requires Reflex dependency injection for IPlayerManager, IGridManager, IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>, and IInWorldSpaceSpawner<ExpParticleSpawner, float>.
- Testing implications:
  - Debug spawning can be triggered using the debug spawn key (P key by default in BossManager) in gameplay scenes.
  - Compile changes via:
    dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false

## Integration Notes

- Upstream dependencies:
  - Reflex DI: Injects scene services (IPlayerManager, IGridManager, spawners, IBossHUDPresenter, ISwarmFreezer).
  - DOTween: Drives projectile trajectories, leap jump arcs, returning arm motions, and HUD slider animations.
  - Physics & Layers: Collisions and raycasts query EntityLayers.Player for combat damage and TerrainLayers.Impassable for obstacle sliding.
  - Indicators: Spawns CircularTelegraphIndicator and RectangularTelegraphIndicator for combat feedback.
- Downstream consumers:
  - BossManager listens to OnBossDefeated to trigger stage progression portal instantiation and clear swarm suppression.
  - BossHUDPresenter consumes IHealth events to update boss health bar visuals.
- Cross-system coupling risks:
  - Prefab instantiation: Dynamically instantiated GolemBoss instances require Reflex container injection (via GameObjectInjector or Dynamic DI component) to resolve injected fields at runtime.

## Known Risks and Open Questions

- Known limitations:
  - Single active boss: BossManager is currently structured around managing a single active boss instance at a time.
  - Tween lifecycle: Rapid scene unload or sudden boss deactivation requires strict killing of active DOTween sequences across arm projectiles and states to prevent orphaned tweens.
- Open design questions:
  - Portal interaction: The stage progression portal currently spawns at the defeat position; future mechanics may require custom entrance animations or player proximity triggers.
- Suggested follow-up tasks:
  - Monitor frame rate during multi-cycle Sky Barrage when combined with active particle effects.
