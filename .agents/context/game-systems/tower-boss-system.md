# Tower Boss System Documentation

## Purpose

The Tower Boss system manages stationary miniboss combat encounters and spatial arena mechanics in Car Survivors. It covers shared stationary boss lifecycle, finite state machines (Mortar Tower and Railgun Tower), circular arena boundary generation and pulse animations, distance-based leash enforcement with countdown warnings, combat camera framing transitions via Cinemachine, zero-allocation projectile and wave pooling, multi-stage attack patterns (ballistic bombardment, rolling hazards, sweeping/chasing lasers, and bidirectional ground energy waves), ground telegraph synchronization, impassable navigation grid reservation, and enrage phase transformations.

Additionally, this system serves as the prerequisite phase for the stage climax: defeating both Tower Bosses across the map (Mortar Tower and Railgun Tower) triggers the awakening and spawn of the Ancient Golem Boss.

It is not responsible for mobile pathfinding or pursuit steering (handled by mobile bosses like the Golem), global wave timing (handled by WaveManager), vehicle movement physics (handled by CarController), or stage progression portal activation (handled by BossEncounterService after the Golem Boss is defeated).

## Reading Map

- Shared Tower Boss core & Arena:
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/Enemies/Bosses/Constants/BossEncounterConstants.cs
  - Assets/Scripts/Enemies/Bosses/Towers/TowerBossBase.cs
  - Assets/Scripts/Enemies/Bosses/Towers/Constants/TowerBossConstants.cs
  - Assets/ScriptableObjects/Enemies/Bosses/TowerBossConfigSO.cs
  - Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs
  - Assets/Scripts/Enemies/Bosses/Towers/Arena/Constants/ArenaConstants.cs
  - Assets/Scripts/Camera/CinemachineCombatFollowOffsetController.cs
  - Assets/Scripts/UI/HUD/ArenaLeashWarningPresenter.cs
- Mortar Tower Boss:
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Constants/MortarTowerConstants.cs
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
- Railgun Tower Boss:
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/RailgunTowerBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Constants/RailgunTowerConstants.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Combat/RailgunArcLaserBeam.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Combat/RailgunLinearWaveHitbox.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Combat/RailgunWavePool.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Projectiles/RailgunOrbProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Projectiles/RailgunOrbPool.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/IRailgunTowerState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/RailgunTowerStateMachine.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/States/RailgunIdleState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/States/RailgunCooldownState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/States/RailgunLaserSweepState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/States/RailgunChasingLaserState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/States/RailgunExplosiveCrossBurstState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/StateMachine/States/RailgunDefeatedState.cs
- Designer-authored data and prefabs:
  - Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs
  - Assets/ScriptableObjects/Enemies/Bosses/RailgunTowerConfigSO.cs
  - Assets/ScriptableObjects/Enemy/Towers/MortarTowerConfig.asset
  - Assets/ScriptableObjects/Enemy/Towers/RailgunTowerConfig.asset
  - Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortalTower.prefab
  - Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortarBoulderHitbox.prefab
  - Assets/Prefabs/Enemies/Bosses/Towers/Railgun/RailgunTower.prefab
  - Assets/Prefabs/Enemies/Bosses/Towers/Railgun/RailgunOrb.prefab
  - Assets/Prefabs/Enemies/Bosses/Towers/Railgun/RailgunWaveHitbox.prefab
  - Assets/Materials/Laser/Laser.mat
- Related systems:
  - Boss progression & Golem boss: Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - UI HUD & Leash warning: Assets/Scripts/UI/HUD/BossHUDPresenter.cs, Assets/Scripts/UI/HUD/ArenaLeashWarningPresenter.cs
  - Combat camera: Assets/Scripts/Camera/CinemachineCombatFollowOffsetController.cs
  - Swarm suppression: Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs (ISwarmFreezer)
  - Navigation grid: Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Ground telegraphs: Assets/Scripts/Indicators/CircularTelegraphIndicator.cs, Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs, Assets/Scripts/Indicators/ArcTelegraphIndicator.cs, Assets/Scripts/Indicators/Constants/IndicatorConstants.cs
  - Health & floating damage numbers: Assets/Scripts/HealthSystem/Health.cs, Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Experience reward: Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
  - Dev console prefab discovery filter: Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs
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
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/interactables-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/unity-root-cause/SKILL.md

## Architecture and Data Flow

- Shared base components:
  - EncounterArenaController: Coordinates the spatial encounter circle around any TowerBossBase. Exposes `ArenaCenter` (`transform.position`), syncs config from `_boss.BaseConfig` and registers with `BossEncounterService` in `OnEnable()`, checks squared distance from the player car every frame against `BaseConfig.ArenaRadius` (default 22m), manages perimeter border expansion and continuous idle pulsing via DOTween, triggers `ICinemachineCombatFollowOffsetController` combat camera framing, enforces the leash countdown timer, and locks re-activation via `IsEncounterCompleted` once defeated.
  - TowerBossBase (implements ITowerBoss, IBossCombatTarget, IKnockable): Abstract base component owning Reflex DI (PlayerManager, GridManager, ExpParticleSpawner, DamageNumbersSpawner, TimeFreezeState), Health bindings, ground telegraph tracking/cleanup, MaterialPropertyBlock enrage color shifts, recoil tweens (including optional barrel punch suppression via PlayFireRecoil(sfxKey, includeBarrelRecoil) and non-oscillating single-kick burst recoil via PlayBurstFireRecoil), TimeFreezeState-gated turret yaw aiming (GetTurretAimRotation, SetTurretAimRotation, SnapTurretAim, UpdateTurretAim), barrel local pitch control (BarrelPivotHeight, GetBarrelPitchDegrees, SetBarrelPitch, ResetBarrelPitch), and shared death VFX/EXP/event emission.
  - TowerBossConfigSO: Base ScriptableObject defining shared MaxHealth, EnrageHealthPercent, ExpReward, EnrageColor, EnrageEmissionColor, ArenaRadius, LeashGracePeriodSeconds, CombatFollowOffset, CameraTransitionDuration, CooldownMin, CooldownMax, EnrageCooldownMin, and EnrageCooldownMax.
  - CinemachineCombatFollowOffsetController: Scene service implementing ICinemachineCombatFollowOffsetController. Smoothly tweens CinemachineFollow.FollowOffset between default exploration framing (0, 11, -7) and combat framing (0, 17, -13) over 0.8 seconds using DOTween.
  - ArenaLeashWarningPresenter: HUD presenter implementing IArenaLeashWarningPresenter. Displays RETURN TO ARENA: Xs warning text with DOTween punch scale feedback on every integer second tick while outside the boundary.

- Mortar Tower Boss components:
  - MortarTowerBoss: Concrete TowerBossBase implementing IMortarTowerBoss. Marks a 5x5 cell square footprint around its base as impassable (cost 255) on Start and rotates attacks through MortarTowerStateMachine (MortarIdleState -> MortarCooldownState -> MortarClusterBurstState / MortarDiagonalBounceState / MortarRollingBoulderState -> MortarDefeatedState).
  - MortarShellPool & MortarShellProjectile: Zero-GC parabolic artillery pool and shell with custom height curve support and delayed pool return for explosion feedback.
  - MortarBoulderHitbox: Swept OverlapBoxNonAlloc trigger volume for Attack 3 rolling boulder contact damage.
  - MortarTowerConfigSO: Extends TowerBossConfigSO with Attack 1-3 radii, shell flight times, bounce counts, and durations/speeds.

- Railgun Tower Boss components:
  - RailgunTowerBoss: Concrete TowerBossBase implementing IRailgunTowerBoss. Validates required config, 2 leg colliders, `TurretTransform`, `MuzzleTransform`, `_orbPool`, `_wavePool`, beam LineRenderers, telegraph prefabs, and reusable indicators (`ReusableSweepIndicator`, `ReusableSweepArcIndicator`, `ReusableChaseIndicator`) via fail-fast `InvalidOperationException` checks in `InitializeTower()`. Computes dual oriented bounding box (OBB) footprints (padded by `RailgunTowerConstants.FOOTPRINT_CELL_PADDING = 0.25f`) from its two `LegCollider` BoxColliders so the left and right gantry legs block flow-field cells while leaving the central arch open for the player car to drive underneath. Exposes `ICarController.MaxForwardSpeed` (falling back to `DEFAULT_PLAYER_MAX_FORWARD_SPEED = 16f`) and applies `FALLBACK_MUZZLE_HEIGHT = 6.75f` and `DAMAGE_NUMBER_Y_OFFSET = 3.5f`.
  - RailgunTowerStateMachine & States: Manages RailgunIdleState, RailgunCooldownState (turret tracks player during cooldown and cycles attacks), RailgunLaserSweepState, RailgunChasingLaserState, RailgunExplosiveCrossBurstState, and RailgunDefeatedState.
  - RailgunLaserSweepState (Attack 1): Telegraphs the entire sweep arc (90 deg normal, 110 deg enraged, 22m radius) with `ReusableSweepArcIndicator` (`ArcTelegraphIndicator`, built with `IndicatorConstants.DEFAULT_ARC_BORDER_WIDTH = 0.25f` and `DEFAULT_ARC_SEGMENTS = 32`) for 1.5s in normal phase or 0.75s on enrage pass 1 (0.45s re-aim warning before enrage pass 2) while aiming the turret at the sector centerline and smoothly tilting `RailgunHead` downward toward the arc base, then keeps `ReusableSweepArcIndicator` visible and drives `ArcLaserBeam` (`RailgunArcLaserBeam`, 32-segment dual-mesh aerial fan from `MuzzlePosition` plus ground band using `ARC_LASER_MIN_RADIUS = 0.25f`, `ARC_LASER_MUZZLE_HALF_WIDTH = 0.3f`, `ARC_LASER_BAND_VISUAL_HALF_DEPTH = 0.65f`) radially outward from 0m to 22m over 2.4s (1 normal pass) or 1.7s per pass (2 enraged passes) while pitching the barrel upward synchronously (suppressing barrel recoil punch so pitch motion stays smooth, gated by `SWEEP_PITCH_MIN_HORIZ_DISTANCE = 4.5f`), dealing 28 damage once per pass along the advancing annular sector (width 2.6m, `BEAM_HITBOX_HEIGHT = 2.5f`, `BEAM_GROUND_Y_OFFSET = 0.1f`, `ARC_LASER_HITBOX_TOLERANCE_SQR = 0.0025f`) via `IDamageable` deduplication and smoothly blending barrel pitch back to neutral over `BARREL_PITCH_RESET_DURATION = 0.35f` on completion.
  - RailgunChasingLaserState (Attack 2): Locks `ChaseAnchor` onto the player's clamped XZ position for 0.6s, then moves `ReusableChaseIndicator` + `ChaseBeamLine` toward the player for 4.0s at `Attack2NormalSpeedRatio` (0.58x authored in RailgunTowerConfig.asset, 0.85x SO default) or `Attack2EnrageSpeedRatio` (0.68x authored, 1.15x SO default) of `PlayerMaxForwardSpeed`, pausing in place whenever the center reaches within `CHASE_CENTER_HOLD_DISTANCE = 0.35f` (`0.1225f` squared) of the player's XZ center until the player collider exits the circle radius, pulsing 14 damage every 0.35s within 2.24m (normal) or 4.0m (enraged) radius.
  - RailgunExplosiveCrossBurstState (Attack 3): Pre-generates staggered parabolic orb targets (4 orbs normal / 7 orbs enraged authored in RailgunTowerConfig.asset; 2 / 4 SO defaults) inside an invisible scatter circle (`Attack3TargetOffsetMax = 11.5m`, `Attack3TargetOffsetMin = 0m`) initially centered on the player (pulled gently inward near arena borders via `CROSS_BURST_ZONE_CENTER_EDGE_PULL_FACTOR = 0.45f` so corner shots retain spread) and clamped within `ArenaRadius - ARENA_EDGE_SAFETY_MARGIN` (`2m`), sampling up to `CROSS_BURST_TARGET_SAMPLE_ATTEMPTS = 48` (with `CROSS_BURST_BEST_CANDIDATE_ATTEMPTS = 32` best-candidate fallback) to enforce `Attack3MinTargetSpacing` (`6.2m`) XZ separation between landing centers before sorting monotonically by tower-relative azimuth for a smooth one-way sweep. Smoothly pre-aims turret yaw (`CROSS_BURST_PRE_AIM_DEG_PER_SEC = 240f`, clamped `0.12s-0.35s`) and elevates the barrel to `CROSS_BURST_BARREL_PITCH_DEGREES = 10f`, fires via `RailgunOrbPool` with non-oscillating burst recoil (`4.5 deg` barrel / `0.16m` housing, max `0.17s`) while smoothly panning yaw across the 0.2s stagger interval, and resets barrel pitch to neutral (`0.35s`) on completion. Each detonation checks a 2.4m center impact sphere: if the player is hit, deals 38 center damage and suppresses wave splitting for that orb; if avoided, launches a bidirectional 0/180 deg wave pair via `RailgunWavePool` (12m half-length, 2.2m width, `WAVE_FRONT_DEPTH = 1.2f`, 26 m/s, 24 damage with shared per-detonation `IDamageable` deduplication) along randomized intersecting angles.
  - RailgunDefeatedState: Stops visuals/indicators and returns all active orbs and waves to pools upon defeat (while TowerBossBase.Health_OnNoHealth handles death VFX, EXP drop, OnBossDefeated emission, and deactivation, and state transition calls Exit() to stop active coroutines).
  - RailgunOrbPool / RailgunOrbProjectile & RailgunWavePool / RailgunLinearWaveHitbox: Zero-GC pools and pooled combat objects. RailgunLinearWaveHitbox synchronizes paired positive/negative RectangularTelegraphIndicators with swept non-tunneling OverlapBoxNonAlloc damage checks and single-hit HashSet deduplication.
  - RailgunTowerConfigSO: Extends TowerBossConfigSO with Attack 1-3 sweep, chase, orb volley, and bidirectional wave tuning parameters.

- Key interfaces:
  - ITowerBoss: Shared tower boss contract extending IBossCombatTarget and IKnockable, exposing BaseConfig, TurretHousing, BarrelPivotHeight, PlayerPosition, StartEncounter, ResetEncounter, GetTurretAimRotation, SetTurretAimRotation, SnapTurretAim, UpdateTurretAim, GetBarrelPitchDegrees, SetBarrelPitch, ResetBarrelPitch, and OnBossDefeated.
  - IMortarTowerBoss & IRailgunTowerBoss: Tower-specific extensions of ITowerBoss for configs, pools, indicators, beams, coroutines, and state transitions.
  - IEncounterArenaController: Contract for arena center (`ArenaCenter`), bounds, leash state, direct engagement notification, and encounter lifecycle events.
  - IMortarTowerState & IRailgunTowerState: State lifecycle contracts (Enter, Exit, Update, FixedUpdate).
  - IMortarShellProjectile / IMortarShellPool / IMortarBoulderHitbox: Mortar combat and pooling contracts.
  - IRailgunOrbProjectile / IRailgunOrbPool / IRailgunLinearWaveHitbox / IRailgunWavePool: Railgun combat and pooling contracts.
  - IBossEncounterService: Central encounter coordination service tracking tower count (RequiredTowersCount = 2 in RuinedBloodCity.unity, matching `BossEncounterConstants.DEFAULT_REQUIRED_TOWERS`), spawning GolemBoss after both towers are defeated, and exposing `IsPositionWithinAnyTowerArena(Vector3 worldPosition, float extraBuffer = 0f)` for scene interactable exclusion queries.

- Runtime flow:
  - Detection & Engagement: Player enters the 22m arena radius (EncounterArenaController) or damages the tower directly outside the arena (TakeDamage -> NotifyDirectCombatEngaged).
  - Arena Lock-in: Arena border expands and pulses, BossHUDPresenter shows the active tower display name (MORTAR TOWER or RAILGUN TOWER) and health/enrage bar, swarms freeze via ISwarmFreezer, and Cinemachine offsets ease into combat framing.
  - Combat & Enrage (<= 40% Health): Each tower cycles its 3 signature attacks separated by CooldownState. Crossing EnrageHealthPercent plays enrage SFX/VFX and applies MaterialPropertyBlock HDR emissive tints while escalating attack parameters (faster cooldowns, 2-pass sweep on Railgun with 0.75s initial + 0.45s re-aim warning, increased chase speed (`0.68x` authored in RailgunTowerConfig.asset / `1.15x` SO default) and `4.0m` radius, and expanded cross burst (`7` orbs authored / `4` SO default)).
  - Leash & Reset: Leaving the 22m circle triggers the 2.5s countdown warning. Returning clears the warning; timing out calls ResetEncounter(), restoring full HP, clearing enrage, returning all pooled projectiles/waves, dismissing indicators/beams, unfreezing swarms, restoring camera framing, and returning to Idle.
  - Defeat & Stage Escalation: Reaching 0 HP enters DefeatedState, drops EXP, fires OnBossDefeated, marks IsEncounterCompleted = true, and increments BossEncounterService.DefeatedTowersCount. Defeating both towers in RuinedBloodCity initiates the Ancient Golem Boss encounter.

## Rules and Invariants

- Knockback immunity: TowerBossBase implements IKnockable but ignores all knockback impulses in ApplyKnockBack, preserving immovable architectural stability.
- Grid footprint reservation: MortarTowerBoss blocks a 5x5 cell square; RailgunTowerBoss uses dual-leg OBB bounds from its two LegCollider BoxColliders so the central arch remains navigable in GridManager.
- Boundary visual scaling: The perimeter border indicator scale is calculated as arenaRadius / IndicatorConstants.CIRCLE_MESH_RADIUS because CircleBorder.fbx has a native radius of 1m.
- Material Property Blocks for enrage: Enrage color overrides must use MaterialPropertyBlock to avoid runtime material cloning and leaks.
- Zero-GC pooling & swept detection: Mortar shells, Railgun orbs, and Railgun linear waves must always be leased from their pools and returned cleanly on reset/defeat. Wave hitboxes must perform swept segment OverlapBoxNonAlloc checks between previous and current distances so fast waves cannot tunnel over stationary or fast-moving cars.
- Time-freeze and tween cleanup: All projectile, wave, indicator, arena, camera, and recoil DOTween sequences must call .AsFreezable() (or SetFreezable(true) on indicators), respect ITimeFreezeState, and be killed in OnDisable, OnDestroy, and ResetEncounter. Reusable indicators (`CircularTelegraphIndicator.HideReusable`) must restore parent transform after `gameObject.SetActive(false)` (never inside `OnDisable`), and `ArcTelegraphIndicator` must destroy its procedural mesh in `OnDestroy`.
- Boss prefab exclusion in DevConsole: Any new stationary boss prefab added under Assets/Prefabs/ must be excluded in GameplayDevCommandsRegistrar.HandleSpawnCommand so dev enemy spawn commands do not spawn bosses at the car.

## Extension Points

- Adding new Tower Boss types:
  - Inherit TowerBossConfigSO for shared config and TowerBossBase (plus a narrow interface extending ITowerBoss) for shared DI, health, enrage, recoil, telegraphs, and death handling.
  - Reuse EncounterArenaController, IArenaLeashWarningPresenter, and ICinemachineCombatFollowOffsetController unchanged.
- Scene tower count scaling:
  - Place additional Tower Bosses at least 50 meters apart (for 22m arena radii) and set BossEncounterService._requiredTowersForGolemSpawn to match the authored tower count in the scene.

## Integration Notes

- Upstream dependencies:
  - Reflex DI provides IPlayerManager, IGridManager, ICinemachineCombatFollowOffsetController, IArenaLeashWarningPresenter, IBossHUDPresenter, ISwarmFreezer, ITimeFreezeState, EXP spawner, and Damage Numbers spawner via DefaultGameplaySceneInstaller.
  - DOTween drives arena borders, camera transitions, recoil animations, parabolic orb flight, and linear wave travel.
  - CinemachineVirtualCamera / CinemachineFollow is offset by CinemachineCombatFollowOffsetController.
- Downstream consumers:
  - BossHUDPresenter consumes ITowerBoss via IBossHUDSource and OnPresentationChanged.
  - AudioClipPlayer and VFXPlayer instances on tower roots and pooled projectiles play configured SFX/VFX through MainMixer Master.
  - BossEncounterService tracks DefeatedTowersCount / RequiredTowersCount (2 in RuinedBloodCity.unity) to spawn GolemBoss, and answers `MapInteractablesSpawner` `IsPositionWithinAnyTowerArena` queries at scene start.
- Cross-system coupling risks:
  - Prefab fail-fast checks: RailgunTowerBoss.InitializeTower() and RailgunLinearWaveHitbox.Awake() throw InvalidOperationException if required serialized fields (config, 2 leg colliders, pools, beam LineRenderers, telegraph prefabs, reusable indicators) are unassigned.

## Known Risks and Open Questions

- Multi-tower arena proximity:
  - Arenas must remain at least 50 meters apart for 22m radii so engagement circles and HUD/camera tweens do not overlap (currently MortarTower at (35.56, 0, 173.92) and RailgunTower at (168.50, 0, 35.76) in RuinedBloodCity.unity are 191.73m apart).
- Progression state persistence:
  - Tracking defeated towers across session restarts or scene reloads remains in-memory for the active scene run.
- Manual feel verification:
  - Full interactive Play Mode verification from Boot.unity through gameplay feel tuning (chase beam speed vs car drift, orb wave dodging inside gantry arch, and 2-tower defeat -> GolemBoss transition) requires interactive Play Mode playtesting in Unity Editor.
