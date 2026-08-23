# Implementation Plan - Golem Boss System

Date: 2026-08-23

## Overview & Player Experience

Introduce the first boss encounter into Car Survivors: a massive, multi-phase Golem Boss with 4 distinct attack patterns, direct pursuit navigation with obstacle sliding, detachable rocket fist projectiles, visual telegraphing with walkable cell snapping, dynamic enrage scaling, a dedicated screen-top boss health bar, swarm suppression during the encounter, and a Next Stage Portal spawning upon victory.

## User Review Required

> [!IMPORTANT]
> - **Navigation Model**: Golem uses direct pursuit towards the player car with multi-ray/spherecast obstacle sliding against TerrainLayers.Impassable (rather than tile-by-tile FlowField stepping), giving it heavyweight momentum while preventing phasing through buildings.
> - **Detachable Arm Lifecycles**: Arms function as independent projectile entities during attacks (linear rocket launch and sky barrage) and smoothly redock to body sockets via DOTween upon completion or boss state transitions.
> - **Swarm Suppression**: During the boss encounter, regular wave spawns continue, but Swarm events from SwarmSpawner are paused/suppressed until boss defeat.
> - **Portal Spawning**: Defeating the Golem Boss instantiates the NextStagePortal prefab at its death coordinates (plain visual GameObject, non-interactive for now).

## Open Questions & Resolved Decisions

### Resolved Decisions
- [x] **Q1 (Audio Assets)**: Audio clips will be configured later; use constant string keys in `GolemBossConstants` with safe fallback handling so missing audio clips in `AudioClipPlayer` do not cause runtime errors.
- [x] **Q2 (Next Stage Portal Interaction)**: `NextStagePortal` is instantiated upon boss defeat as a plain non-interactive visual GameObject marking the victory spot.

## Proposed Changes

### Data Model & ScriptableObjects

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/Config/GolemBossConfigSO.cs
- ScriptableObject configuration defining:
  - Base stats: MaxHealth, MoveSpeed, RotationSpeed, BodyContactDamage, StompDamage.
  - Phase thresholds: Phase 2 HP percentage (0.6f), Phase 3 Enrage HP percentage (0.3f).
  - Phase multipliers: Cooldown multiplier, movement speed multiplier, projectile speed multiplier.
  - Attack 1 (Leap Slam): LeapTriggerMaxDistance (anti-kiting trigger), LeapAirTime, LeapMaxHeight, SlamRadius, SlamDamage, WarningDuration.
  - Attack 2 (Melee Foot Stomp): StompRadius, StompCooldown, StompDamage.
  - Attack 3 (Linear Rocket Fists): ChargeDuration, ProjectileSpeed, MaxDistance, FistDamage, FistWidth, WarningDuration.
  - Attack 4 (Sky Arm Barrage): BarrageCyclesPerPhase (array [1, 2, 4]), LaunchAirTime, FallSpeed, ImpactRadius, ImpactDamage, JitterMinDelay (0.15f), JitterMaxDelay (0.4f), WarningDuration.
  - Enrage settings: Visual tint color shift, VFX emission rate multiplier.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/Constants/GolemBossConstants.cs
- String keys for audio clips (SLAM_SFX, ROCKET_SFX, ROAR_SFX, DEATH_SFX), animation triggers, and shader property IDs (_EmissionColor, _BaseColor).

---

### Telegraph Indicators

#### [NEW] Assets/Scripts/Indicators/ITelegraphIndicator.cs
- Interface defining lifecycle and initialization for telegraph projections:
  - `void ShowCircular(Vector3 worldPosition, float radius, float duration);`
  - `void ShowRectangular(Vector3 origin, Vector3 forwardDirection, float length, float width, float duration);`
  - `void Dismiss();`

#### [NEW] Assets/Scripts/Indicators/CircularTelegraphIndicator.cs
- Spawns at target coordinates, snaps to the nearest walkable cell center using WorldPosToCellConverter and CellStatusDescriber.IsWalkable.
- Scales up from 0 to full radius with Ease.OutQuad, displays filled warning progress, and rapidly contracts to 0 on impact with Ease.InQuad.

#### [NEW] Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs
- Projects a directional warning rectangle forward from arm sockets or boss front, showing linear rocket fist trajectory and charge progress.

---

### Golem Boss Core & State Machine

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs
- Interface exposing boss lifecycle, current phase, enrage state, and socket references.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
- Main boss controller implementing IGolemBoss, IHealthy, IDamageable, IKnockable.
- Owns health component, state machine, arm controller, movement controller, audio player, and visual effects.
- Monitors health percentage to trigger Phase 2 and Phase 3 (Enrage) transitions.
- Enrage triggers material color/emission shift and spawns enraged aura VFX.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/Movement/GolemMovementController.cs
- Handles pursuit of player car position with configurable acceleration and turn rate.
- Performs multi-ray spherecasts against TerrainLayers.Impassable and slides along surface normals to prevent clipping through obstacles.
- Features an impassable physical collider layer so the player vehicle cannot clip or drive through the Golem body.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
- Detachable arm entity with trigger collider and trail renderer.
- Modes: Docked (attached to body socket), LinearThrust (flying forward and retracting), SkyBarrage (launching upward into sky and slamming down onto telegraph target).
- Deals damage strictly to EntityLayers.Player via IDamageable.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmSocketController.cs
- Manages Left and Right arm sockets (LeftArmSocket, RightArmSocket), tracking docked/detached states and initiating launch/dock animations.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/IGolemState.cs
- Interface for Golem combat states: Enter(), Update(), FixedUpdate(), Exit().

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/GolemStateMachine.cs
- Manages active state, state transitions, attack cooldown queues, and priority overrides (such as anti-kiting Leap Slam).

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemPursuitState.cs
- Default pursuit state: moves toward player, evaluating attack conditions (melee stomp range, linear rocket range, barrage cooldown, or leap slam distance trigger).

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs
- Leap Slam state: jumps upward out of camera frame, creates circular telegraph on player position (passable cell snapped), crashes down with AOE impact.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs
- Linear Rocket Fist state: faces target, projects rectangular telegraph, fires left/right arm forward, retracts and redocks.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
- Sky Arm Barrage state: launches arms into sky, drops them sequentially with randomized jitter delays onto snapped telegraph markers, repeats for N cycles based on current phase.

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemDeathState.cs
- Death state: disables colliders, triggers boss death animation/VFX, docks any flying arms, spawns NextStagePortal, and notifies spawner/UI.

---

### UI & Boss HUD

#### [NEW] Assets/Scripts/UI/HUD/BossHUDPresenter.cs
- Implements IBossHUDPresenter.
- Screen-top UI canvas with Boss Name, animated health slider, and phase warning indicators.
- Smoothly fades in via CanvasGroup alpha on boss spawn and fades out upon boss defeat.
- Subscribes to boss IHealth events (OnHealthDecreased, OnHealthChanged, OnNoHealth).

---

### Boss Management, Spawning & Swarm Suppression

#### [NEW] Assets/Scripts/Enemies/Bosses/BossManager.cs
- Implements IBossManager.
- Spawns Golem Boss at designated coordinates or upon pressing debug key P.
- Suppresses SwarmSpawner during boss encounter by setting SwarmSpawner.IsSuppressed = true (or freezing swarm timer).
- Instantiates NextStagePortal prefab at boss death position upon defeat.

#### [MODIFY] Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
- Add `public bool IsSuppressed { get; set; }` to allow external suppression during boss encounters without resetting normal wave pacing.

#### [MODIFY] Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Bind BossHUDPresenter and BossManager in Reflex container.

---

## Verification Plan

### Automated Checks
- Project compilation check with zero warnings:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Validate coding standards compliance (naming conventions, field ordering [Inject] -> [SerializeField] -> private, constant placement).

### Manual Verification
1. **Boss Spawn (Debug Key P)**:
   - Press P during gameplay to verify Golem Boss instantiates with full HP and screen-top Boss HUD smoothly fades in.
2. **Navigation & Collision**:
   - Drive car around obstacles; verify Golem pursues smoothly, slides against walls without clipping, and vehicle cannot drive through Golem's impassable body collider.
3. **Combat Pattern Execution**:
   - Verify Melee Foot Stomp triggers when driving close to Golem.
   - Verify Linear Rocket Fists show rectangular telegraph, launch, deal damage to car, and retract smoothly.
   - Verify Sky Arm Barrage launches arms upward, displays circular telegraphs with walkable cell snapping, drops arms with jitter delays, and completes phase-appropriate cycles.
   - Drive far away (> LeapTriggerMaxDistance) and verify Golem immediately executes anti-kiting Leap Slam.
4. **Phase & Enrage Progression**:
   - Damage boss below 60% HP: verify attack cooldowns speed up and 2-cycle barrages start.
   - Damage boss below 30% HP: verify visual Enrage tint/emission activates, cooldowns drop to ~2.0s, and 4-cycle barrages trigger.
5. **Boss Defeat & Portal Flow**:
   - Deplete boss health: verify Golem enters death sequence, Boss HUD smoothly fades out, SwarmSpawner resumes, and NextStagePortal spawns at death location.
