# Implementation Plan - Golem Boss Combat Fixes: Stationary Attack Stances, 2-Way Linear Attack Hitbox, and Vertical Capsule Area Overlaps

Date: 2026-08-25

Addresses combat issues identified during boss testing:
1. Golem moving/jumping during Sky Barrage and Linear Rocket Fists instead of standing firmly planted on the ground.
2. Linear Rocket Fist hitbox collision reliability issues, child Rigidbody hierarchy detachment, and implementing the 2-way damage pass (forward thrust + return flight).
3. Air attack (Sky Barrage arm drops and Leap Slam) collision detection misses on circular indicator edges due to 3D ground sphere truncation, replaced with vertical capsule volume queries.

## User Review Required

> [!IMPORTANT]
> - **Stationary Attack Stance (Kinematic Grounding):** The boss body will now stand completely stationary and kinematic (`SetKinematic(true)`) for the entire duration of `GolemSkyBarrageState`, `GolemLinearFistState`, and `GolemStompState`. This prevents any physical bouncing, jumping, car ramming pushback, or depenetration impulses from popping the boss into the air. Normal physics is restored upon transitioning back to `GolemPursuitState`.
> - **2-Way Linear Attack Damage:** `GolemLinearAttackHitbox` will sweep forward with the rocket fists, reset its hit deduplication cache at max range, and sweep back with the returning fists, allowing players to be damaged once going out and once coming back.
> - **Indicator Height Decoupling:** All circular telegraph impacts (`DropFromSky`, `LeapSlam`, `Stomp`) will use `Physics.OverlapCapsule` covering ground to 3.5m–4.0m height to guarantee 100% reliable hits across the full visual indicator radius regardless of vehicle suspension height or terrain elevation.

## Open Questions

None. All issues and expected behaviors have been identified and mapped to explicit physics solutions.

## Proposed Changes

### 1. Boss State Machine & Stationary Attack Grounding

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
- Lock boss movement and walking animations throughout the entire `GolemSkyBarrageState` duration.
- Set `_boss.Movement.SetKinematic(true)` on `Enter()` and `_boss.Movement.SetKinematic(false)` on `Exit()` to prevent any jumping or displacement.
- Remove the `_launchTimer` movement-unlock logic so the body remains stationary until `FinishAttack()` transitions back to `_pursuitState`.
- Keep `FixedUpdate()` calling `_boss.Movement.Stop()` and `_boss.Animator?.SetMoving(false, 0f)`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs
- Set `_boss.Movement.SetKinematic(true)` on `Enter()` and `_boss.Movement.SetKinematic(false)` on `Exit()` to eliminate any physics hopping or depenetration impulses during the attack.
- Ensure rotation is locked to `_attackDirection` without any vertical displacement.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemStompState.cs
- Set `_boss.Movement.SetKinematic(true)` on `Enter()` and `_boss.Movement.SetKinematic(false)` on `Exit()`.

---

### 2. Linear Attack Hitbox (2-Way Sweep & Hierarchy Detachment)

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemLinearAttackHitbox.cs
- Cache `_initialParent` in `Awake()`.
- On `Activate()`, unparent `transform.SetParent(null)` so the hitbox kinematic Rigidbody is completely detached from the parent boss Rigidbody in PhysX.
- Structure `_activeSequence` as a 2-phase sequence matching `GolemArmProjectile`:
  - **Forward Phase:** Sweeps from `origin` to `targetPosition` over `travelTime` (`Ease.OutQuad`). Continuous overlap check (`CheckOverlap`) and `OnTriggerEnter` deduplicate hits during outward travel.
  - **Return Phase:** At max range, clears `_hitColliders` cache, then sweeps from `targetPosition` back to `origin` (or socket position) over `returnTime` (`Ease.InQuad`). Continuous overlap check and `OnTriggerEnter` deduplicate hits during return travel.
  - **Completion:** Calls `Deactivate()`, restores `transform.SetParent(_initialParent)`, resets local position, and invokes `onComplete`.
- Add a periodic zero-allocation `CheckOverlap()` on tween update in addition to `OnTriggerEnter` to ensure zero possibility of high-speed vehicle tunneling.

---

### 3. Aerial & Ground Slam Collision Volume Fixes

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
- In `DropFromSky()`, replace `Physics.OverlapSphere(targetSlamPosition, impactRadius, ...)` with `Physics.OverlapCapsule(targetSlamPosition, targetSlamPosition + Vector3.up * 3.5f, impactRadius, EntityLayers.Player, QueryTriggerInteraction.Collide)`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs
- In `ApplyAreaImpactDamage()`, replace `Physics.OverlapSphere(center, radius, ...)` with `Physics.OverlapCapsule(center, center + Vector3.up * 4.0f, radius, EntityLayers.Player, QueryTriggerInteraction.Collide)`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
- In `TriggerStompDamage()`, replace fallback `Physics.OverlapSphere(transform.position, _config.StompRadius, ...)` with `Physics.OverlapCapsule(transform.position, transform.position + Vector3.up * 3.5f, _config.StompRadius, EntityLayers.Player, QueryTriggerInteraction.Collide)`.

---

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Verify 0 build errors and 0 new warnings.

### Manual Verification
1. **Stationary Stances Test:**
   - Trigger Sky Barrage, Linear Rocket Fists, and Foot Stomp. Confirm boss stays 100% grounded and stationary without popping, jumping, or jittering.
2. **Linear Attack Test:**
   - Trigger Linear Rocket Fists. Stand in the telegraph lane during outward punch -> verify damage taken once.
   - Stay or enter the lane as fists return -> verify damage taken a 2nd time on the return pass.
3. **Aerial Damage Area Test:**
   - Stand on the outer edge of the circular telegraph during Sky Barrage arm drop -> verify damage registers reliably.
   - Stand on the outer edge of Leap Slam circular telegraph -> verify damage registers reliably.
