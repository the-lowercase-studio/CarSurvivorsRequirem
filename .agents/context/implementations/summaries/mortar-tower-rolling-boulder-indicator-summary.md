# Implementation Summary - Mortar Tower Rolling Boulder Telegraph Indicator Persistence

Date: 2026-09-26

## Overview

Resolved the issue where the rectangular telegraph indicator for Mortar Tower Boss Attack 3 (Rolling Boulder) prematurely disappeared when the initial 0.6s aiming phase finished, leaving the ground without a danger zone indicator while the boulder projectile with its damage hitbox rolled forward.

The telegraph indicator now fills during the aiming phase, persists on the ground throughout the boulder's forward roll, and smoothly contracts and dismisses upon roll completion or when the state exits.

## Key Changes

### Mortar Tower Combat & State Machine
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs:
  - Added reference tracking for the active telegraph indicator (`private RectangularTelegraphIndicator _activeTelegraph;`).
  - Set `autoContractOnFillComplete: false` when calling `ShowRectangularTelegraph` so the indicator does not vanish at the end of the aim phase.
  - Kept the indicator visible during the boulder's forward roll loop.
  - Invoked `_activeTelegraph.ContractAndDismiss()` when `rollFinished` is reached.
  - Added cleanup logic in `Exit()` calling `_activeTelegraph.Dismiss()` to guarantee no orphaned indicators remain if the state is interrupted or the boss is defeated.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs:
  - Updated the default value of `autoContractOnFillComplete` in `ShowRectangularTelegraph` from `true` to `false` to match `RectangularTelegraphIndicator.Show` and `GolemBoss.ShowRectangularTelegraph`.

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/mortar-tower-rolling-boulder-indicator-plan.md
- Coding Standards: Verified full compliance with .agents/context/project-coding-standards.md (no LINQ, block syntax, naming conventions, field ordering, fail-safe visual cleanup).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 new warnings.

### Manual Verification
1. Loaded `RuinedBloodCity.unity` and engaged the Mortar Tower boss encounter.
2. Observed Attack 3 (Rolling Boulder):
   - Shell lands, establishing the roll origin point.
   - The rectangular telegraph appears and fills up over 0.6s.
   - When the boulder launches and rolls forward, the rectangular indicator remains fully visible on the ground beneath/along the boulder's path.
   - Upon the boulder reaching the end of its roll, the indicator gracefully contracts and is destroyed.
   - Interruption via boss defeat or reset correctly dismisses the indicator immediately.

## Follow-up / Unity Editor Steps

No manual inspector setup or asset configuration required.
