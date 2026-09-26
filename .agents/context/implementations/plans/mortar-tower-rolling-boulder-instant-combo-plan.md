# Implementation Plan - Mortar Tower Rolling Boulder Instant Combo Timing

Date: 2026-09-26

Synchronize the telegraph indicators and remove the post-landing delay in Mortar Tower Boss Attack 3 (Rolling Boulder). Both the circular drop telegraph and the rectangular roll telegraph will appear simultaneously at the onset of the attack, and the horizontal rolling boulder will launch instantly upon the drop shell impacting the ground.

## User Review Required

> [!NOTE]
> At attack start, both telegraphs will render immediately:
> 1. The circular telegraph marks the drop point.
> 2. The rectangular telegraph marks the horizontal roll corridor extending from the drop point towards the player.
> Both indicators fill during the shell's parabolic flight duration (`dropWarning`).
> When the shell impacts the ground, the circular indicator detonates, and the rolling boulder launches immediately with zero artificial delay. The rectangular indicator remains visible until the boulder finishes rolling.

## Open Questions

None. The user's specification is exact: both indicators must appear right away, and the horizontal projectile must launch instantly when the drop projectile hits the ground.

## Proposed Changes

### Mortar Tower State Machine

#### Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs
- Move the calculation of `rollDir`, `endPos`, and the call to `_boss.ShowRectangularTelegraph` up to the beginning of `RunBoulderAttack()`, alongside `ShowCircularTelegraph`.
- Set the rectangular telegraph fill duration to `dropWarning` (`_boss.Config.Attack3DropWarningDuration`), keeping `autoContractOnFillComplete: false`.
- Launch the parabolic drop shell.
- When `dropLanded` becomes true (upon shell impact), immediately launch the rolling boulder via `_boss.BoulderHitbox.Roll` without waiting for any secondary aim timer.
- Remove the redundant `AIM_TELEGRAPH_DURATION` (0.6s) wait loop.
- Keep the rectangular indicator visible while the boulder rolls, contracting it via `ContractAndDismiss()` when `rollFinished` is true.
- Maintain existing cleanup in `Exit()` (`_activeTelegraph.Dismiss()`).

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. Launch `RuinedBloodCity.unity` in Unity Editor.
2. Engage Mortar Tower encounter and await Attack 3 (Rolling Boulder).
3. Verify:
   - Both the circular drop telegraph and the rectangular hazard lane telegraph appear simultaneously right at the beginning of the attack.
   - Both fill synchronously during the 1.0s drop warning duration as the mortar shell flies through the air.
   - The instant the shell hits the ground, the boulder immediately begins rolling down the lane.
   - The rectangular telegraph remains visible along the lane until the boulder reaches the end of its roll.
