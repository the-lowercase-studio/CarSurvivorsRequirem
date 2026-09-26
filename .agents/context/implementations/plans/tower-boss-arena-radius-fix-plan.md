# Implementation Plan - Tower Boss Arena Radius Alignment Fix

Date: 2026-09-26

Align the visual circular arena indicator of the Mortar Tower Boss with the logical combat boundary (22m), resolving the 2x scale mismatch where the visual ring was rendered at a 44m radius.

## User Review Required

> [!NOTE]
> The change corrects the visual indicator scale from 2x (44m) to 1:1 (22m) and synchronizes the arena radius directly with MortarTowerConfigSO, ensuring single source of truth.

## Open Questions

- None. User approved the diagnosed root cause and requested the fix implementation.

## Proposed Changes

### Tower Boss Arena System

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs
- Import Assets.Scripts.Indicators.Constants.
- Add SyncConfig() method to synchronize _arenaRadius and _leashGracePeriodSeconds with _boss.Config when available.
- In AnimatePerimeterExpansion(), change aseScale calculation from diameter = _arenaRadius * 2f to scale = _arenaRadius / IndicatorConstants.CIRCLE_MESH_RADIUS, rendering the circle mesh (unit radius 1m) at exactly _arenaRadius (22m).

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
- In Update(), synchronize the fallback arena trigger distance check with _config.ArenaRadius instead of hardcoding MortarTowerConstants.DEFAULT_ARENA_RADIUS.

---

## Verification Plan

### Automated Checks
- Project compilation check:
`powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
`

### Manual Verification
1. Enter play mode in RuinedBloodCity.unity.
2. Approach the Mortar Tower Boss.
3. Observe the circular boundary expanding upon combat engagement.
4. Verify that the visual perimeter boundary matches the 22m radius and that crossing the visual boundary immediately triggers the leash warning and countdown.
