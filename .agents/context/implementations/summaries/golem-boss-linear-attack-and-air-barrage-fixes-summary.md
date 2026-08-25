# Implementation Summary - Golem Boss Combat Fixes: Stationary Attack Grounding, 2-Way Linear Attack Hitbox, and Vertical Capsule Area Overlaps

Date: 2026-08-25

## Overview

Resolved three combat issues identified during Ancient Golem boss testing:
1. Eliminated boss jumping/hopping/sliding during stationary attacks (`SkyBarrageState`, `LinearFistState`, `StompState`) by enforcing kinematic grounding (`SetKinematic(true)`).
2. Resolved collision misses on `GolemLinearAttackHitbox` by unparenting the hitbox from the boss Rigidbody during flight, and implemented a 2-way damage sweep (forward punch pass + return flight pass with hit cache reset).
3. Resolved aerial and stomp damage misses near indicator edges by upgrading ground-level `OverlapSphere` checks to vertical `Physics.OverlapCapsule` checks ($3.5\text{m}-4.0\text{m}$ height).

## Key Changes

### State Machine & Grounding
- Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs: Removed the mid-attack movement resumption timer (`_launchTimer`). Enforced kinematic grounding (`Movement.SetKinematic(true)`) on `Enter()` and restored normal movement physics on `Exit()`.
- Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs: Added `Movement.SetKinematic(true)` on `Enter()` and `Movement.SetKinematic(false)` on `Exit()` to prevent any physics depenetration impulses from launching the boss into the air.
- Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemStompState.cs: Added `Movement.SetKinematic(true)` on `Enter()` and `Movement.SetKinematic(false)` on `Exit()`.

### Hitbox Subsystem
- Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemLinearAttackHitbox.cs:
  - Added hierarchy detachment (`transform.SetParent(null)`) on `Activate()` and restored parent on `Deactivate()`.
  - Implemented a 2-phase DOTween sequence: Phase 1 sweeps outward to `maxDistance`, clears `_hitColliders` cache at apex, and Phase 2 sweeps inward back to origin.
  - Added continuous zero-allocation `CheckOverlap()` on tween update and `OnTriggerEnter` to ensure zero possibility of high-velocity vehicle tunneling.

### Aerial & Ground Slam Collision Volumes
- Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs: Replaced `Physics.OverlapSphere` in `DropFromSky` with `Physics.OverlapCapsule` covering ground to $3.5\text{m}$ height.
- Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs: Replaced `Physics.OverlapSphere` in `ApplyAreaImpactDamage` with `Physics.OverlapCapsule` covering ground to $4.0\text{m}$ height.
- Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs: Replaced `Physics.OverlapSphere` in `TriggerStompDamage` with `Physics.OverlapCapsule`.
- Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemStompTrigger.cs: Corrected `Physics.OverlapBox` rotation parameter to use `Quaternion.identity` with world AABB bounds.

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/golem-boss-linear-attack-and-air-barrage-fixes-plan.md
- System Documentation: .agents/context/game-systems/golem-boss-system.md
- Coding Standards: Verified compliance with .agents/context/project-coding-standards.md (naming conventions, explicit field ordering, no LINQ, standard method body syntax).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 new warnings.

### Manual Verification Steps
1. **Stationary Stance Verification:**
   - Engage the boss in combat. Trigger Linear Rocket Fists, Sky Barrage, and Stomp attacks.
   - Confirm the boss stays 100% stationary and planted on the ground throughout all three animations without jumping or jittering.
2. **2-Way Linear Attack Verification:**
   - Stand in the rectangular lane during forward fist launch -> verify damage is dealt once.
   - Enter or remain in the lane as fists return -> verify damage is dealt a 2nd time on the return pass.
3. **Aerial Damage Area Verification:**
   - Stand near the outer rim of the circular ground telegraph during Sky Barrage arm drops and Leap Slam landings -> verify damage is registered with 100% reliability.

## Follow-up / Unity Editor Steps
No additional manual inspector setup required.
