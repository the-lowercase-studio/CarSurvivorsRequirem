# Golem Boss System Documentation

## Purpose

The Golem Boss system manages the multi-phase boss encounter for the Ancient Golem in Car Survivors. It is responsible for boss lifecycle management, encounter spawning, combat pacing, finite state machine execution, obstacle-sliding pursuit navigation, Mecanim animation bridging, detachable rocket arm projectile behaviors, ground telegraph indicators, multi-phase enrage progression, boss HUD presentation, and victory stage progression portal spawning.

It is not responsible for regular enemy pooling and spawning, standard wave progression timers, flow-field generation, player vehicle mechanics, or general UI layouts outside the Boss HUD contract.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Bosses/BossManager.cs
  - Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs
  - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Constants/GolemBossConstants.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Movement/GolemMovementController.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmSocketController.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
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
  - BossManager: Scene-level controller implementing IBossManager. Injected with IBossHUDPresenter, ISwarmFreezer, and IPlayerManager. Spawns the GolemBoss prefab (ahead of player or at a target vector), suppresses active swarm waves during the encounter, shows the Boss HUD, listens to OnBossDefeated, restores swarm spawning on victory, and spawns the next stage portal prefab.
  - GolemBoss: Aggregate boss entity implementing IGolemBoss, IDamageable, and IKnockable. Manages Reflex injected dependencies (IPlayerManager, IGridManager, damage number spawner, EXP particle spawner), health change events, phase tracking (Phase 1, 2, 3), enrage material tinting via MaterialPropertyBlock, body contact damage in OnCollisionStay, knockback immunity, telegraph list tracking and cleanup, and state machine updates.
  - GolemStateMachine: Discrete state machine coordinating active IGolemState instances and tracking independent cooldown timers for all 4 attacks (LeapCooldownTimer, StompCooldownTimer, LinearFistCooldownTimer, SkyBarrageCooldownTimer). Cooldowns initialize with staggered values to guarantee an opening pursuit phase.
  - GolemPursuitState: Default navigation state. Directs GolemMovementController to pursue PlayerPosition strictly while IsMovingAnimationPlaying is true to prevent sliding during attack animations, transitions into GolemStompState when player is within StompRadius, interrupts into Leap Slam if player distance exceeds LeapTriggerMaxDistance (anti-kiting), and transitions into arm attacks when Arms.AreBothArmsDocked according to cooldown priority (Sky Barrage -> Linear Fist -> Leap Slam).
  - GolemStompState: Dedicated melee stomp attack state. Halts boss movement, plays the Stomp animation trigger, applies damage via TriggerStompDamage at exact impact timing (STOMP_IMPACT_DELAY), and returns smoothly to the calling state (GolemPursuitState or GolemSkyBarrageState) after the total animation duration (STOMP_TOTAL_DURATION).
  - GolemLeapSlamState: Anti-kiting AOE attack state. Halts boss movement, sets Rigidbody to kinematic during the parabolic aerial arc to eliminate physics collision drift, aligns facing direction toward target, triggers leap animation, spawns a circular ground telegraph at the player's grid position, animates parabolic leap arc via DOTween (LeapMaxHeight, LeapAirTime), snaps to exact telegraph center position (snappedTarget) on impact, restores non-kinematic physics, plays GolemSlam SFX, deals AOE damage via Physics.OverlapSphere, applies phase cooldown multiplier, and transitions back to pursuit.
  - GolemLinearFistState: Horizontal rocket punch state. Stops movement, aligns rotation to player direction, displays a rectangular ground telegraph, and fires both detached GolemArmProjectile entities along the ground via DOTween. Deals trigger damage on collision, retracts arms to sockets, and transitions back to pursuit upon docking.
  - GolemSkyBarrageState: Detached mortar barrage state. Sequentially launches both rocket arms into the sky (LaunchToSky) with SkyArmInitialStaggerDelay. Movement is fully halted during the initial HandsUpAttack launch phase (SKY_BARRAGE_LAUNCH_DURATION); once arms are launched and walking animation resumes, the body continues pursuing the player and delegates close-range stomps to GolemStompState. In the sky, each arm independently performs multiple target drops (total cycles determined by current phase) on randomized offset positions around the player, telegraphed by circular indicators. Upon cycle completion, arms return to sockets (ReturnAndDock) and the boss resumes standard pursuit.
  - GolemDeathState: Terminal state triggered by Health.OnNoHealth. Halts movement and animations, dismisses active telegraphs, docks all arms, plays GolemDeath SFX, plays death VFX, spawns EXP reward particles, and triggers OnBossDefeated.
  - GolemMovementController: Implements IGolemMovementController. Drives a Rigidbody with frozen rotation, supports kinematic mode switching (SetKinematic) for scripted aerial trajectories, resets linear and angular velocities on SetPosition, computes smooth sliding directions around impassable terrain obstacles using Physics.SphereCast and Vector3.ProjectOnPlane, and sets desired velocity and rotation toward target positions.
  - GolemArmSocketController: Implements IGolemArmSocketController. Manages Left and Right arm projectile pairing, socket transforms, and switching between rigged mesh visuals (while docked) and detached projectile gameObjects (while airborne).
  - GolemArmProjectile: Standalone component for each detached arm. Implements GolemArmState lifecycle (Docked, LinearThrust, SkyAirborne, SkyDropping, Returning) driven by DOTween sequences, manages trail renderers, trigger damage collision against EntityLayers.Player, and impact VFX.
  - GolemAnimator: Bridges Mecanim Animator triggers (LeapSlam, Stomp, LinearFist, SkyBarrage) and parameters (IsMoving, Speed) via cached string hashes. Exposes IsMovingAnimationPlaying to evaluate active transitions and current state against the Walking animation state hash, preventing movement during non-walking animations.
  - GolemBossConfigSO: Designer-authored ScriptableObject containing all combat attributes, health percentages, phase multipliers, attack cooldowns, telegraph warning durations, projectile speeds, and enrage colors.
  - BossHUDPresenter: UI presenter implementing IBossHUDPresenter. Binds to IHealth, displays boss name, animates health slider changes with DOValue, evaluates health gradient colors, and hides on boss defeat.
- Key interfaces:
  - IBossManager: Controls boss spawning and exposes IsBossActive status.
  - IGolemBoss: Aggregates subsystem access (Movement, Arms, Animator, AudioClipPlayer, WorldGrid, Transform, Config, Phase multipliers, player distance/direction properties) and telegraph helper methods.
  - IGolemMovementController: Controls navigation enable/disable, kinematic toggling (SetKinematic), movement targeting, position snapping (SetPosition), and obstacle sliding.
  - IGolemArmSocketController: Controls arm projectiles, docking states, and socket initialization.
  - IGolemAnimator: Controls animation trigger and parameter updates, exposing IsMovingAnimationPlaying.
  - IGolemState: State lifecycle contract (Enter, Update, FixedUpdate, Exit).
  - IBossHUDPresenter: UI contract for showing and hiding the boss health bar.
  - ISwarmFreezer: Service contract to suppress swarm spawns during boss combat.
- Runtime flow:
  - Spawn: BossManager instantiates GolemBoss prefab, connects OnBossDefeated, sets ISwarmFreezer.IsSuppressed = true, and displays the boss health bar via IBossHUDPresenter.
  - State Loop: GolemStateMachine updates the active state in Update() and FixedUpdate(), ticking attack cooldown timers.
  - Pursuit & Attacks: GolemPursuitState tracks the player with GolemMovementController strictly while walking animations play. When cooldowns elapse, attacks are selected:
    - Stomp: Checked continuously during pursuit and sky barrage whenever the player enters StompRadius, transitioning into GolemStompState which locks movement for the stomp duration.
    - Anti-Kiting Leap: Triggered immediately if the player is beyond LeapTriggerMaxDistance, launching the boss along a kinematic parabolic arc to land precisely at the circular telegraph center.
    - Linear Fists: Both arms detach and thrust forward along a rectangular telegraph line while the body remains stationary, then retract and dock.
    - Sky Barrage: Both arms launch skyward while the body holds still during the launch phase, then the body resumes pursuit and melee stomps while arms execute staggered multi-cycle bombardments on circular ground telegraphs around the player before returning to dock.
  - Damage & Phases: Incoming damage triggers damage numbers, reduces Health, and plays blood VFX. When HP drops below Phase2HealthPercent (60%) or Phase3HealthPercent (30%), phase multipliers accelerate movement speed, arm velocity, and cooldown recovery. Phase 3 triggers Enrage with roar SFX, enrage VFX, and red material emission property overrides.
  - Defeat: On Health.OnNoHealth, GolemDeathState cleans up active telegraphs and resets arms. EXP particles and death VFX spawn, BossManager restores swarm spawning, hides the HUD, and spawns the next stage portal.

## Rules and Invariants

- Critical behavior rules:
  - Knockback immunity: GolemBoss implements IKnockable but ignores knockback force to preserve heavy boss presence.
  - Animation movement restriction: Non-walking animations (Stomp, Linear Fist charge, Sky Barrage launch, Leap Slam jump, Death) lock boss movement in place; FixedUpdate navigation only drives movement when IsMovingAnimationPlaying is true.
  - Kinematic leap trajectory & exact landing: During Leap Slam, the Rigidbody is set to kinematic to eliminate physics velocity drift during DOTween flight, and SetPosition(snappedTarget) is executed on impact to guarantee landing at the exact center of the circular ground telegraph.
  - Stomp state delegation & preservation: Melee stomps during pursuit or airborne Sky Barrage delegate to GolemStompState with SetReturnState, ensuring clean return to the caller state without breaking active aerial barrage sequences.
  - Arm availability constraint: Detached arm attacks (Linear Fist, Sky Barrage) can only initiate when Arms.AreBothArmsDocked is true.
  - Dual-cycle independence in Sky Barrage: Left and right arms execute independent drop sequences with staggered timing, allowing asynchronous impact and repositioning.
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
