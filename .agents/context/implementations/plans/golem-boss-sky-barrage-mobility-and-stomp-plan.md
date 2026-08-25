# Implementation Plan - Golem Boss Sky Barrage Post-Launch Mobility & Melee Stomp

Date: 2026-08-25

Enables the Ancient Golem boss body to pursue the player and perform melee foot stomps while its detached rocket arms are airborne executing Sky Barrage bombardments.

## User Review Required

> [!IMPORTANT]
> - **Two-Phase Sky Barrage Lifecycle:**
>   1. **Launch Phase (Initial Stance):** The boss body stands completely stationary and kinematic (`SetKinematic(true)`) while playing the `SkyBarrage` animation trigger until the arms launch into the sky.
>   2. **Airborne Barrage Phase (Active Pursuit & Stomps):** Once arms are in flight, the body restores movement physics (`SetKinematic(false)`, `CanMove = true`) and pursues the player at current speed multiplier. If the player enters `StompRadius` and stomp cooldown is ready, the boss halts, grounds itself with `SetKinematic(true)`, executes a foot stomp attack, and resumes pursuit upon completion.
>   3. **Barrage Conclusion:** When both arms finish all bombardment cycles and dock back to sockets, the state machine transitions to `GolemPursuitState`.

## Open Questions

None.

## Proposed Changes

### Boss State Machine

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
- Introduce `_isLaunchingArms` (true during initial launch) and `_isStomping` (true during mid-barrage foot stomps).
- On `Enter()`:
  - Lock movement with `Movement.SetKinematic(true)`, `Movement.CanMove = false`, and `Movement.Stop()`.
  - Play `SkyBarrage` animation.
- On launch completion / arm release (`TriggerArmLaunch`):
  - Start arm aerial lifecycles.
  - Set `_isLaunchingArms = false`, `Movement.SetKinematic(false)`, `Movement.CanMove = true`.
- In `Update()`:
  - If `_isStomping`: handle stomp impact timer (calls `_boss.TriggerStompDamage()`) and stomp duration timer. On finish, reset stomp cooldown timer and restore `Movement.CanMove = true`, `Movement.SetKinematic(false)`.
  - If `!_isLaunchingArms && !_isStomping`: check if `_boss.DistanceToPlayer <= _boss.Config.StompRadius` and `_stateMachine.StompCooldownTimer <= 0f`. If so, trigger localized stomp execution (`Movement.SetKinematic(true)`, `Movement.Stop()`, `Animator.PlayStomp()`).
- In `FixedUpdate()`:
  - If `!_isLaunchingArms && !_isStomping && _boss.Movement.CanMove`: pursue player via `Movement.MoveTowards` and update `Animator.SetMoving(true, speed)`.
  - Otherwise: `Movement.Stop()` and `Animator.SetMoving(false, 0f)`.
- In `Exit()`:
  - Clean up sequences, telegraphs, animation events, restore non-kinematic physics, and reset flags.

---

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Verify 0 build errors and 0 new warnings.

### Manual Verification
1. **Launch Stance:** Trigger Sky Barrage. Confirm boss stands firmly grounded and stationary with hands raised without any hopping or displacement during the launch.
2. **Post-Launch Pursuit:** Confirm that as soon as the arms ascend into the air, the boss body starts walking toward the player while arms bombard the ground from above.
3. **Mid-Barrage Stomp:** Get close to the boss during the aerial barrage -> confirm the boss stops, stomps the ground (dealing stomp damage), and resumes pursuit while arms continue their independent drop cycles.
4. **Docking & Return:** Confirm that once all arm cycles complete, the arms return to sockets, dock, and the boss transitions cleanly into `GolemPursuitState`.
