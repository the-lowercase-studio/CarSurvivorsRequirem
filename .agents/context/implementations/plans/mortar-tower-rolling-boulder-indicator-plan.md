# Implementation Plan - Mortar Tower Rolling Boulder Telegraph Indicator Persistence

Date: 2026-09-26

Ensure the rectangular hazard telegraph indicator for Mortar Tower Boss Attack 3 (Rolling Boulder) remains active on the ground throughout the projectile's forward roll until impact/completion, rather than vanishing prematurely upon the expiration of the pre-attack aim duration.

## User Review Required

> [!NOTE]
> The telegraph indicator will fill up during the initial 0.6s aiming/windup phase, remain completely visible while the boulder projectile with its damage hitbox travels along the telegraphed lane, and gracefully contract and disappear via `ContractAndDismiss()` once the boulder reaches its destination or when the attack state exits.

## Open Questions

None. The expected behavior is explicitly defined: keep the rectangular hazard lane indicator visible during the entire forward movement of the rolling boulder hitbox.

## Proposed Changes

### Mortar Tower State Machine & Indicator Lifetime

#### Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs
- Retain a reference to the active indicator instance: `private RectangularTelegraphIndicator _activeTelegraph;`.
- Spawn the rectangular telegraph with `autoContractOnFillComplete: false` so that it stays fully rendered when the fill finishes.
- Keep the telegraph visible while waiting for `rollFinished`.
- Invoke `_activeTelegraph.ContractAndDismiss()` once `rollFinished` is true, resetting the reference to null.
- In `Exit()`, call `_activeTelegraph.Dismiss()` to ensure immediate cleanup if the state is interrupted or aborted early (e.g. boss defeat, leash reset).

#### Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
- Update `ShowRectangularTelegraph` parameter default value for `autoContractOnFillComplete` from `true` to `false`, matching `RectangularTelegraphIndicator.Show` and `GolemBoss.ShowRectangularTelegraph`.

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. Enter `RuinedBloodCity.unity` scene in the Unity Editor.
2. Trigger the Mortar Tower encounter.
3. Observe Attack 3 (Rolling Boulder):
   - Shell drops in front of the tower.
   - Rectangular telegraph appears on the ground, aiming towards the player, and fills up over 0.6s.
   - When the boulder projectile launches and rolls forward with its damage collider, the red rectangular indicator stays visible on the ground marking the danger corridor.
   - When the boulder reaches its maximum roll distance (or if the player leaves/resets), the indicator smoothly contracts and vanishes without leaving orphaned objects in the hierarchy.
