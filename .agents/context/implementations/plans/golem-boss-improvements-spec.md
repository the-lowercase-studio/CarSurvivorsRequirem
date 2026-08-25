# Specification: Golem Boss Combat & Animation Improvements

**Date:** 2026-08-25  
**Author:** Antigravity  
**Target Systems:** Assets/Scripts/Enemies/Bosses/Golem/, Assets/Scripts/StatusEffects/, Assets/Scripts/Skills/PlayerSkills/, Assets/Animations/Enemies/Bosses/Golem/  

---

## 1. Overview & Player Experience

- **Summary:** Comprehensive gameplay, animation, and combat fixes for the Golem Boss. This overhaul splits the leap slam into distinct takeoff and landing animations, ties hand-shot mechanics to synchronized animation triggers and pre-attack rotation, ensures all aerial circle-radius attacks deal accurate area-of-effect damage to the player, guarantees the Golem Boss reliably receives damage from all player weapons while maintaining non-blocking car passthrough, and enforces an atomic state/animation lock so attacks cannot interrupt one another mid-animation.
- **Player-Facing Goals:**
  - **Telegraphed Leap Slam:** The player clearly sees the Golem charge and jump on the ground, fly through the air, and upon ground impact trigger a heavy landing animation and circular shockwave damage.
  - **Fair & Readable Hand Attacks:** The Golem explicitly turns towards the player before firing forward fists, and arms only detach and shoot when the punch/launch animation reaches the release frame.
  - **Reliable Aerial AOE:** Standing inside any red/orange telegraph circle when a leap slam or falling sky arm impacts the ground deals damage to the player car.
  - **Smooth Arcade Driving & Combat:** The player car can drive through the Golem Boss without getting stuck or physically blocked, while all player weapons (projectiles, lasers, saws, mines) consistently register hits and deal damage to the boss.
  - **Clean Boss Pacing:** The Golem Boss never stutters, cancels, or abruptly switches between attacks mid-animation.
- **In-Scope vs. Out-of-Scope:**
  - **In-Scope:**
    - Animator controller and clip transition updates for Leap Takeoff (JumpAttack.anim) and Leap Land (JumpAttack2.anim).
    - Animation trigger integration (Call_OnLinearFistRelease, Call_OnSkyBarrageRelease, Call_OnLeapTakeoff, Call_OnLeapLandEnd).
    - State machine lifecycle and animation locking in GolemPursuitState, GolemLeapSlamState, GolemLinearFistState, GolemSkyBarrageState, and GolemStompState.
    - Damage reception fix via EntityManipulationHelper and SawBlade supporting GetComponentInParent<IDamageable>().
    - AOE radius damage verification for aerial attacks.
  - **Out-of-Scope:**
    - Introducing brand new attack archetypes or altering baseline phase health thresholds.
    - Altering unrelated enemies or player driving physics.

---

## 2. Open Questions & Resolved Decisions

### Resolved Decisions
- [x] **Leap Slam Flow:** The jump sequence is split into 3 distinct phases: Ground Takeoff Animation -> Parabolic Flight Tween -> Ground Impact Landing Animation. Boss only leaves the ground when takeoff completes, and landing animation begins exactly upon ground collision.
- [x] **Linear Fist Alignment:** Boss rotates smoothly to face the player before triggering the forward punch animation, and arm projectiles only detach when the animation reaches the release frame.
- [x] **Sky Barrage Release:** Boss plays the upward launch animation, releasing arms into the sky only at the designated animation frame trigger.
- [x] **Aerial Circle AOE Damage:** Both Leap Slam impact and Sky Arm drops perform circular area checks against player colliders, dealing damage to the player whenever the car is within the circle radius upon ground impact.
- [x] **Collider & Damage Reception:** EntityManipulationHelper and SawBlade will support resolving IDamageable via both TryGetComponent and GetComponentInParent so that all player weapons damage the Golem Boss reliably. Boss collider remains on layer Enemy with non-blocking physics interaction with layer Player.
- [x] **Attack Interruption Lock:** Attacks are strictly atomic. The stomp check inside GolemSkyBarrageState is removed, and GolemPursuitState will only transition to a new attack when the boss is in the walking/moving state and no attack animation is active.

### Open Questions (Hard Gate - Must be answered before full implementation)
- [ ] **Q1:** For animation event triggers on the Golem Animator (e.g. forward fist release, sky arm launch, jump takeoff), should we configure Animation Events directly on the .anim files in Unity, or use code-driven normalized time / duration fallback thresholds (e.g. in GolemAnimator / constants) to ensure deterministic execution even if clip events are not edited in the Unity Editor?
  - *Recommendation:* Support both: expose explicit public callback methods (Call_OnLinearFistRelease, Call_OnSkyBarrageRelease, etc.) for Unity Animation Events, and implement a fallback timer/coroutine based on clip lengths in GolemBossConstants so the system works immediately and reliably out of the box.
- [ ] **Q2:** For the Leap Slam landing recovery, should the boss remain stationary during the entire landing animation (~1.2s - 1.5s) to reward players who dodged the slam with a damage window?
  - *Recommendation:* Yes, pausing movement during the landing animation creates a satisfying combat cadence and gives the player a clear attack opening.

---

## 3. Data Model & Serialization

- **ScriptableObjects:**
  - File: Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs
  - Ensure serialized fields exist for timing tuning:
    - _leapTakeoffDuration (float, default 1.2f): Duration of ground takeoff windup before airborne launch.
    - _leapLandingDuration (float, default 1.2f): Duration of ground recovery animation after impact before returning to pursuit.
    - _linearFistReleaseDelay (float, default 0.8f): Fallback delay from punch animation start to arm rocket detachment.
    - _skyBarrageReleaseDelay (float, default 1.0f): Fallback delay from sky animation start to arm sky launch.
- **Serialized Fields & Inspector Setup:**
  - GolemAnimator.cs: Add serialized references / hashes for LeapTakeoff and LeapLand triggers.
  - All serialized fields strictly follow [SerializeField] private _camelCase naming conventions.

---

## 4. Architecture & Contracts

### Interfaces & Abstractions

- IGolemAnimator:
  - bool IsMovingAnimationPlaying { get; }
  - bool IsAttackAnimationPlaying { get; }
  - void SetMoving(bool isMoving, float speed = 0f);
  - void PlayLeapTakeoff();
  - void PlayLeapLand();
  - void PlayStomp();
  - void PlayLinearFist();
  - void PlaySkyBarrage();
  - event Action OnLinearFistRelease;
  - event Action OnSkyBarrageRelease;
  - event Action OnLeapTakeoffComplete;
  - event Action OnLeapLandComplete;

- IGolemBoss:
  - Retains all existing contracts while exposing updated animator events and damage trigger helpers.

### State Machine Lifecycle

```
[ GolemPursuitState ] 
       │ (Only when IsMovingAnimationPlaying == true & cooldowns ready)
       ├─────────────────┬─────────────────┬─────────────────┐
       ▼                 ▼                 ▼                 ▼
[ GolemLinearFistState ] [ GolemSkyBarrageState ] [ GolemStompState ] [ GolemLeapSlamState ]
  1. Face Target           1. Play HandsUp          1. Play Stomp       1. Play LeapTakeoff
  2. Play Forward Anim     2. Release to Sky        2. Damage at frame  2. Parabolic Fly
  3. Release Arms          3. Barrage Cycles        3. Wait anim end    3. Ground Impact & Land
  4. Wait Arms Return      4. Return & Dock                 │           4. Damage Circle AOE
  5. Wait Anim End         5. Wait Anim End                 │           5. Wait Land Anim End
       │                         │                          │                 │
       └─────────────────────────┴──────────────────────────┴─────────────────┘
                                 │
                                 ▼
                         [ GolemPursuitState ]
```

---

## 5. Subsystem Detailed Changes

### 5.1 GolemAnimator & Animation Controller
- Split JumpAttack into two animator states:
  - State LeapTakeoff playing JumpAttack.anim.
  - State LeapLand playing JumpAttack2.anim.
- Add triggers LeapTakeoff and LeapLand.
- Implement Call_OnLinearFistRelease(), Call_OnSkyBarrageRelease(), Call_OnLeapTakeoffComplete(), and Call_OnLeapLandComplete() in GolemAnimator for Unity Animation Events.
- Add animation lock property IsAttackAnimationPlaying that checks whether any attack state or transition is active.

### 5.2 GolemLeapSlamState
- **Enter:**
  - Stop movement, set kinematic.
  - Play PlayLeapTakeoff().
  - Display circular telegraph indicator at player's position.
  - Calculate target snapped ground landing position.
  - Wait for takeoff animation completion (via event or duration fallback).
- **Airborne Launch:**
  - Launch parabolic DOTween sequence to apex height and down to target ground location over LeapAirTime.
- **Ground Impact & Land:**
  - On arrival at target ground position, snap position and clear kinematic.
  - Play PlayLeapLand().
  - Trigger SLAM SFX and impact VFX.
  - Perform circle overlap check (Physics.OverlapSphere with _config.SlamRadius) on layer Player and apply _config.SlamDamage via IDamageable.
  - Wait for landing animation to complete before resetting cooldown and returning to GolemPursuitState.

### 5.3 GolemLinearFistState
- **Enter:**
  - Stop movement.
  - Calculate direction to player and rotate boss immediately to face the attack direction.
  - Display rectangular telegraph indicator.
  - Play PlayLinearFist().
  - Subscribe to OnLinearFistRelease (with fallback timer).
- **Release:**
  - When release event fires, trigger FireLinear on both arms.
- **Completion:**
  - Once arms return to sockets and dock, reset cooldown and return to GolemPursuitState.

### 5.4 GolemSkyBarrageState
- **Enter:**
  - Stop movement.
  - Play PlaySkyBarrage().
  - Subscribe to OnSkyBarrageRelease (with fallback timer).
- **Release:**
  - When release event fires, trigger LaunchToSky on arm projectiles.
  - Boss can resume pursuit movement while arms barrage from the sky (without triggering stomp or new attacks).
- **Impact & AOE:**
  - When each sky arm lands at its circular telegraph, perform Physics.OverlapSphere with _config.SkyArmImpactRadius and damage player.
- **Completion:**
  - Once all cycles finish and arms dock, reset cooldown and return to GolemPursuitState.

### 5.5 EntityManipulationHelper & Damage Reception
- Update EntityManipulationHelper.Damage(Collider target, float damage):
  - If target.TryGetComponent(out IDamageable damageable) is null, check target.GetComponentInParent<IDamageable>().
  - Apply the same parent fallback to Knockback and Stun methods.
- Update SawBlade.cs:
  - Check both other.TryGetComponent(out IDamageable d) and other.GetComponentInParent<IDamageable>().

### 5.6 Attack State Interruption Lock
- In GolemPursuitState:
  - Add explicit guard: check _boss.Animator.IsMovingAnimationPlaying and ensure no attack is currently executing before selecting any attack.
- In GolemSkyBarrageState:
  - Remove stomp interrupt check so sky barrage runs to completion without interruption.
- Ensure all attack states are atomic and self-contained.

---

## 6. Implementation Plan (Phases & Steps)

### Phase 1: Core Contracts, Animation Triggers & Collider Damage Routing
- [ ] **Step 1.1:** Update EntityManipulationHelper.cs and SawBlade.cs to resolve IDamageable on parent hierarchy.
  - Files:
    - Assets/Scripts/StatusEffects/EntityManipulationHelper.cs
    - Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs
  - Verification: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
- [ ] **Step 1.2:** Update IGolemAnimator.cs, GolemAnimator.cs, GolemBossConstants.cs, and GolemBossConfigSO.cs to define takeoff/landing triggers, release events, and fallback timings.
  - Files:
    - Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs
    - Assets/Scripts/Enemies/Bosses/Golem/Constants/GolemBossConstants.cs
    - Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs
  - Verification: Compile check.

### Phase 2: Leap Slam Split & Synchronized Hand Attacks
- [ ] **Step 2.1:** Refactor GolemLeapSlamState.cs into distinct Takeoff -> Parabolic Flight -> Landing -> Recovery flow with ground circular AOE damage.
  - Files:
    - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs
  - Verification: Compile check.
- [ ] **Step 2.2:** Refactor GolemLinearFistState.cs to enforce pre-rotation and animation trigger-based fist launch.
  - Files:
    - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs
  - Verification: Compile check.
- [ ] **Step 2.3:** Refactor GolemSkyBarrageState.cs to enforce animation trigger-based sky launch, atomic execution (remove stomp interrupt), and ground circular AOE damage.
  - Files:
    - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
    - Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
  - Verification: Compile check.

### Phase 3: State Machine Lock & Animator Controller Integration
- [ ] **Step 3.1:** Update GolemPursuitState.cs and GolemBoss.cs to enforce strict animation lock preventing mid-animation attack switches.
  - Files:
    - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemPursuitState.cs
    - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
  - Verification: Compile check.
- [ ] **Step 3.2:** Update GolemBossAnimationContoller.controller to wire LeapTakeoff and LeapLand states with JumpAttack.anim and JumpAttack2.anim.
  - Files:
    - Assets/Animations/Enemies/Bosses/Golem/GolemBossAnimationContoller.controller
  - Verification: Compile check and inspector validation.

### Phase 4: Verification & Polish
- [ ] **Step 4.1:** Verify compilation with zero warnings using dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
- [ ] **Step 4.2:** Perform Unity Play Mode testing with full verification checklist.

---

## 7. Verification & Acceptance Criteria

- [ ] Project compiles with zero warnings: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
- [ ] Golem Boss reliably takes damage from player Minigun, Lasergun, Landmines, and SawBlade.
- [ ] Player car can smoothly drive through Golem Boss without physical snagging.
- [ ] Leap Slam displays takeoff animation on ground -> launches into air -> impacts ground -> plays landing animation and deals circular damage.
- [ ] Forward Fist attack rotates boss toward player before punch, releasing arms only at the animation release frame.
- [ ] Vertical Sky Barrage releases arms only at the upward animation release frame.
- [ ] All aerial circle attacks damage player car anywhere inside the indicator radius upon ground impact.
- [ ] Golem Boss never interrupts or switches attacks while an attack animation is playing.
