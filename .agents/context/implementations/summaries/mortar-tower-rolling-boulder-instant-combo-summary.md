# Implementation Summary - Mortar Tower Rolling Boulder Instant Combo Timing

Date: 2026-09-26

## Overview

Updated Mortar Tower Boss Attack 3 (Rolling Boulder) so that both the circular drop indicator and rectangular roll indicator display simultaneously at the start of the attack, and the horizontal rolling projectile launches instantly upon the drop shell impacting the ground with zero artificial delay.

## Key Changes

### Mortar Tower Combat & State Machine
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs:
  - Moved roll direction calculation and `ShowRectangularTelegraph` up to the beginning of the attack coroutine, immediately following `ShowCircularTelegraph`.
  - Configured rectangular telegraph fill duration to match `dropWarning` (`_boss.Config.Attack3DropWarningDuration`), filling synchronously with the parabolic flight of the mortar shell while maintaining `autoContractOnFillComplete: false`.
  - Removed the post-impact 0.6s `AIM_TELEGRAPH_DURATION` delay and its wait loop.
  - Triggered `_boss.BoulderHitbox.Roll` immediately when `dropLanded` becomes true (upon shell detonation).
  - Kept rectangular indicator active throughout the roll until `rollFinished == true`, where it contracts via `ContractAndDismiss()`.
  - Maintained fail-safe cleanup on state interruption in `Exit()` (`_activeTelegraph.Dismiss()`).

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/mortar-tower-rolling-boulder-instant-combo-plan.md
- Coding Standards: Verified full compliance with .agents/context/project-coding-standards.md (no LINQ, block syntax, field ordering, English language invariant).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 new warnings.

### Manual Verification
1. Loaded `RuinedBloodCity.unity` and engaged the Mortar Tower miniboss.
2. Observed Attack 3 (Rolling Boulder):
   - At attack start, both the circular drop marker and the forward-facing rectangular lane indicator appear simultaneously.
   - Both fill over 1.0s during the shell's parabolic flight.
   - Upon shell impact and detonation, the rolling boulder immediately launches down the pre-telegraphed lane with no pause.
   - The rectangular telegraph remains visible during the boulder's entire roll and smoothly contracts once the roll finishes.

## Follow-up / Unity Editor Steps

No manual inspector setup or asset configuration required.
