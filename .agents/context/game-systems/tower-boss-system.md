# Tower Boss System Documentation

## Purpose

The Tower Boss system manages stationary miniboss combat encounters and spatial arena mechanics in Car Survivors. It is responsible for stationary boss lifecycle and state machine management, circular arena boundary generation and pulse animations, distance-based leash enforcement with countdown warnings, combat camera framing transitions via Cinemachine, zero-allocation artillery projectile pooling, multi-stage ballistic bombardment patterns, rolling hazard collisions, ground telegraph synchronization, impassable navigation grid reservation, and enrage phase transformations.

Additionally, this system serves as the prerequisite phase for the stage climax: defeating both Tower Bosses across the map will trigger the awakening and spawn of the Ancient Golem Boss.

It is not responsible for mobile pathfinding or pursuit steering (handled by mobile bosses like the Golem), global wave timing (handled by WaveManager), vehicle movement physics (handled by CarController), or stage progression portal activation (handled by BossEncounterService after the Golem Boss is defeated).

## Reading Map

- Primary code locations:
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Constants/MortarTowerConstants.cs
  - Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs
  - Assets/Scripts/Enemies/Bosses/Towers/Arena/Constants/ArenaConstants.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Combat/MortarBoulderHitbox.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellPool.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/IMortarTowerState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/MortarTowerStateMachine.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarIdleState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarCooldownState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarClusterBurstState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarDiagonalBounceState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarDefeatedState.cs
  - Assets/Scripts/Camera/CinemachineCombatFollowOffsetController.cs
  - Assets/Scripts/UI/HUD/ArenaLeashWarningPresenter.cs
- Designer-authored data and prefabs:
  - Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs
  - Assets/ScriptableObjects/Enemy/Towers/MortarTowerConfig.asset
  - Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortalTower.prefab
  - Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortarBoulderHitbox.prefab
- Related systems:
  - Boss progression & Golem boss: Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - UI HUD & Leash warning: Assets/Scripts/UI/HUD/BossHUDPresenter.cs, Assets/Scripts/UI/HUD/ArenaLeashWarningPresenter.cs
  - Combat camera: Assets/Scripts/Camera/CinemachineCombatFollowOffsetController.cs
  - Swarm suppression: Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs (ISwarmFreezer)
  - Navigation grid: Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Ground telegraphs: Assets/Scripts/Indicators/CircularTelegraphIndicator.cs, Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs
  - Health & floating damage numbers: Assets/Scripts/HealthSystem/Health.cs, Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Experience reward: Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
  - DI installer: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/grid-system.md
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
  - EncounterArenaController: Coordinates the spatial encounter circle around the tower. Checks squared distance from the player car every frame against the arena radius (default 22m). Manages perimeter border expansion and continuous idle pulsing via DOTween. Automatically invokes ICinemachineCombatFollowOffsetController to ease the camera out to combat framing, delegates Boss HUD presentation and regular swarm suppression to BossEncounterService, and starts the boss combat loop. Enforces leash mechanics when the car exits the boundary, ticking a grace countdown timer and resetting the encounter if the player fails to return in time. Locks out re-activation via IsEncounterCompleted once the boss is defeated.
  - MortarTowerBoss: Aggregate stationary boss entity implementing IMortarTowerBoss, IDamageable, and IKnockable. Controls the state machine, health thresholds, enrage visual state using MaterialPropertyBlock, procedural recoil tweens on barrel and turret housing, smooth yaw tracking toward the player car, telegraph creation and cleanup, and death cleanup. On Start, marks a 5x5 cell footprint on the navigation grid as impassable (cost 255) so pathfinding flows around the structure.
  - MortarTowerStateMachine: Discrete finite state machine managing IMortarTowerState instances (Idle, Cooldown, ClusterBurst, DiagonalBounce, RollingBoulder, Defeated).
  - MortarIdleState: Default dormant state. Awaits arena trigger activation from EncounterArenaController or direct damage taken from outside the arena.
  - MortarCooldownState: Inter-attack transition state. Selects attacks cyclically (Cluster Burst -> Diagonal Bounce -> Rolling Boulder). Randomizes pause duration between designer-configured bounds, scaled down when enraged.
  - MortarClusterBurstState: Attack 1. Snaps turret aim and launches a primary ballistic mortar shell at the player's position. Upon landing, detonates for center area damage, then scatters 6 sub-shells in a hexagonal ring using parabolic jump trajectories. When enraged, spawns an additional outer concentric blast wave of sub-shells.
  - MortarDiagonalBounceState: Attack 2. Launches an artillery shell to an offset location near the player. Upon landing, splits into 4 diagonal rays (45°, 135°, 225°, 315°), each bouncing outward 3 times with straight-line DOJump arcs and circular ground telegraphs. When enraged, fires 3 consecutive salvos in rapid succession.
  - MortarRollingBoulderState: Attack 3. Drops a heavy boulder directly in front of the tower facing the player. Displays a circular impact telegraph followed by a long rectangular hazard telegraph along the roll trajectory. Once dropped, translates MortarBoulderHitbox forward while rotating its visual mesh, dealing contact damage to the player car.
  - MortarDefeatedState: Terminal state entered upon zero health. Halts active attack coroutines, dismisses all telegraphs, unparents death VFX so particle bursts finish cleanly, spawns EXP particles, triggers OnBossDefeated, and disables the entity.
  - MortarShellPool: Dedicated object pool wrapping UnityEngine.Pool.IObjectPool<MortarShellProjectile> for Zero-GC projectile allocation. Employs safe tail-popping in ReturnAll() to prevent collection mutation errors during resets.
  - MortarShellProjectile: Parabolic artillery projectile. Evaluates analytical parabolic or custom height curves over normalized flight time with singularity-free pitch/yaw heading rotation and axial rifling spin. Delays pool recycling after ground detonation so particle explosions and audio finish playing while hiding the projectile mesh.
  - MortarBoulderHitbox: Kinematic trigger volume for the rolling boulder attack. Sweeps forward along the attack vector, performs OverlapBoxNonAlloc detection against the player car layer, applies single-pass damage, and plays hit VFX/audio.
  - CinemachineCombatFollowOffsetController: Scene service implementing ICinemachineCombatFollowOffsetController. Smoothly tweens CinemachineFollow.FollowOffset between default exploration framing (0, 11, -7) and combat framing (0, 17, -13) over 0.8 seconds using DOTween.
  - ArenaLeashWarningPresenter: HUD presenter implementing IArenaLeashWarningPresenter. Displays a RETURN TO ARENA: Xs warning label with DOTween punch scale feedback on every integer second tick when the player strays outside the arena.
  - MortarTowerConfigSO: Designer-authored ScriptableObject defining health, enrage thresholds, arena radius, leash grace duration, camera offsets, attack timing/radii/damage, and cooldown windows.
- Key interfaces:
  - IMortarTowerBoss: Core interface for the mortar tower boss, providing access to Health, Config, Transform, TurretHousing, IsEnraged, aim methods, and OnBossDefeated.
  - IEncounterArenaController: Interface for arena boundary query, leash countdown state, and encounter lifecycle events (OnPlayerEnteredArena, OnPlayerExitedArena, OnLeashCountdownTick, OnEncounterReset, OnEncounterCompleted).
  - IMortarTowerState: State lifecycle contract (Enter, Exit, Update, FixedUpdate).
  - IMortarShellProjectile: Contract for launching parabolic shells, detonating, and returning to pool.
  - IMortarShellPool: Contract for retrieving, returning, and bulk-clearing mortar shells.
  - IMortarBoulderHitbox: Contract for initiating and canceling linear rolling boulder sweeps.
  - ICinemachineCombatFollowOffsetController: Camera service contract for transitioning and restoring Cinemachine follow offsets.
  - IArenaLeashWarningPresenter: UI contract for showing, updating, and hiding the leash warning banner.
  - IBossEncounterService: Central encounter coordination service contract for tracking tower progression, managing boss HUD/swarm freezing, and spawning GolemBoss.
- Runtime flow:
  - Detection & Engagement: As the player car approaches within 22 meters of the tower, EncounterArenaController detects player presence via squared distance. Alternatively, if the player damages the tower from outside, TakeDamage() calls NotifyDirectCombatEngaged().
  - Arena Lock-in: EncounterArenaController expands the visual perimeter border using DOScale (Ease.OutBack) and begins a continuous subtle pulse. It delegates swarm freezing and Boss HUD presentation to BossEncounterService, and instructs ICinemachineCombatFollowOffsetController to transition camera offset to combat framing.
  - Combat Loop: MortarTowerBoss transitions from MortarIdleState to the first attack. During active attacks and cooldown, UpdateTurretAim smoothly tracks the player car's position. Attacks rotate sequentially via MortarCooldownState: Cluster Burst -> Diagonal Bounce -> Rolling Boulder -> loop.
  - Leash & Reset Mechanics: If the player car leaves the 22m boundary during combat, EncounterArenaController begins a leash grace countdown (default 2.5s). ArenaLeashWarningPresenter displays the warning and punches scale every second. If the player returns before time expires, the warning hides and combat proceeds. If the countdown expires, EncounterArenaController resets the fight: boss health restores to full, enrage state clears, all active shells/telegraphs/hitboxes are canceled, camera restores to default, BossEncounterService restores swarms and hides HUD, arena border shrinks, and the boss returns to MortarIdleState.
  - Enrage Phase (< 40% Health): When health drops below 40%, the boss roars, plays enrage VFX, and shifts material base and emission colors to fiery orange/red using MaterialPropertyBlock. Attack 1 adds a second blast ring, Attack 2 fires 3 rapid salvos, Attack 3 boulder roll speed increases, and cooldown pauses shorten.
  - Defeat & Future Escalation: When boss health reaches zero, MortarDefeatedState halts all attacks, drops EXP particles, plays unparented death VFX, and fires OnBossDefeated. EncounterArenaController marks IsEncounterCompleted = true, notifies BossEncounterService, restores camera framing, and collapses the arena border.
  - Stage Progression Hook (Tower Climax): Defeating required Tower Bosses on the map signals BossEncounterService to trigger the main stage boss encounter: the Ancient Golem Boss spawns after a breathing delay to initiate the ultimate stage battle.

## Rules and Invariants

- Knockback immunity: MortarTowerBoss implements IKnockable but ignores all knockback impulses in ApplyKnockBack, preserving immovable architectural stability.
- Grid footprint cost: On Start, MarkFootprintImpassable() sets a 5x5 cell block centered on the tower to cost 255 (byte.MaxValue) in the navigation grid, forcing flow field paths around the structure.
- Boundary visual scaling: The perimeter border indicator scale must be calculated as _arenaRadius / IndicatorConstants.CIRCLE_MESH_RADIUS because the underlying CircleBorder.fbx mesh has a native radius of 1m.
- Material Property Blocks for enrage: Enrage color overrides must strictly use MaterialPropertyBlock to prevent runtime material instance cloning and memory leaks.
- Zero-GC projectile pooling: Artillery shells must always be leased from MortarShellPool and returned via ReturnToPool() or ReturnAll(). Never instantiate or destroy shell gameObjects during runtime combat.
- Telegraph tracking and auto-cleanup: All circular and rectangular ground telegraphs instantiated by the boss must be registered in _activeTelegraphs, pruned regularly, and dismissed cleanly on state exit, encounter reset, or boss defeat.
- Leash idempotency & completion gate: Once OnBossDefeated fires, IsEncounterCompleted must remain true so that player movement inside the arena perimeter does not re-trigger combat or leash logic.
- Delayed projectile pool return: MortarShellProjectile must hide its visual mesh and stop trail emission on detonation, but delay calling ReturnToPool() until impact particle effects and explosion audio have finished playing.
- Tween safety: All DOTween sequences across the arena border, camera offset, turret recoil, and projectiles must be explicitly killed in OnDisable and OnDestroy to avoid null reference exceptions on scene unloads.

## Extension Points

- Adding new Tower Boss types:
  - Create a new tower boss script (e.g. TeslaTowerBoss, FlamethrowerTowerBoss) implementing IMortarTowerBoss or a generalized ITowerBoss.
  - Reuse EncounterArenaController, IArenaLeashWarningPresenter, and ICinemachineCombatFollowOffsetController for arena containment, leash rules, and camera transitions.
  - Define custom attack states within a dedicated state machine folder.
- Unified Tower Boss Progression & Golem Boss Activation:
  - Place Tower Boss encounters across the gameplay map (e.g. 1 currently in RuinedBloodCity, scaling cleanly to 2).
  - Towers auto-register with BossEncounterService via EncounterArenaController.
  - Defeating the required count of towers triggers BossEncounterService.SpawnGolemBoss() after a 10s delay.
  - This establishes the core stage loop: Explore Map -> Defeat Tower Bosses -> Defeat Ancient Golem Boss -> Stage Portal.
- Designer tuning via ScriptableObject:
  - All balance values (MaxHealth, EnrageHealthPercent, ArenaRadius, LeashGracePeriodSeconds, attack damage, explosion radii, roll speeds, cooldowns) are exposed in MortarTowerConfigSO and can be tuned without recompiling code.
- Custom arena geometry & visuals:
  - EncounterArenaController supports custom perimeter border visuals and configurable arena radii per tower encounter.

## Integration Notes

- Upstream dependencies:
  - Reflex DI: Provides scene-level singletons (IPlayerManager, IGridManager, ICinemachineCombatFollowOffsetController, IArenaLeashWarningPresenter, IBossHUDPresenter, ISwarmFreezer, EXP spawner, Damage Numbers spawner) registered in DefaultGameplaySceneInstaller.
  - DOTween: Drives arena perimeter expansion/shrink/pulse, camera offset transitions, projectile flight/jump arcs, and recoil animations.
  - Cinemachine: CinemachineFollow on the active virtual camera is manipulated by CinemachineCombatFollowOffsetController.
  - Navigation Grid: GridManager supplies the WorldGrid used for footprint reservation and telegraph position snapping.
- Downstream consumers:
  - UI Presenters: BossHUDPresenter displays health bar; ArenaLeashWarningPresenter displays leash countdown.
  - Audio & VFX: AudioClipPlayer and VFXPlayer instances play combat feedback.
  - Progression Architecture: BossEncounterService monitors tower completion to initiate the Golem Boss fight after required towers are defeated.
- Cross-system coupling risks:
  - Camera Controller Binding: DefaultGameplaySceneInstaller must have CinemachineCombatFollowOffsetController assigned in its inspector fields; otherwise, camera zoom transitions will gracefully degrade to static follow.
  - Prefab Injection: If tower bosses are instantiated dynamically at runtime, ensure the Reflex container injects their dependencies or fallback resolution executes.

## Known Risks and Open Questions

- Multi-tower arena proximity:
  - If two tower boss arenas are placed too close to each other, their 22m boundary zones could overlap, creating conflicting camera offset tweens or dual Boss HUD activations.
  - Recommendation: Ensure map layout maintains at least 50 meters of separation between tower boss locations, or introduce arena arbitration logic.
- Progression state persistence:
  - Tracking the defeat of both tower bosses across session resumes or scene restarts will require storing defeated tower IDs in game progression storage once multi-scene or checkpoint systems are expanded.
- Golem spawn positioning:
  - When the 2nd tower boss is defeated, decide whether the Golem Boss spawns ahead of the player's current vehicle heading or emerges at a predefined central arena landmark.
