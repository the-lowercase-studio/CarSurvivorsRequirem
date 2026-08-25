# Implementation Plan - Golem Boss Aerial Attacks Impact Radius Alignment

Date: 2026-08-25

Align Golem Boss aerial attack hitboxes (Leap Slam and Sky Arm Barrage) 1:1 with the full visual circular telegraph indicators by fixing the circle mesh scaling math in CircularTelegraphIndicator and configuring boss impact radius settings to match the target 13.0m (Leap Slam) and 4.8m (Sky Arm Barrage) areas.

## User Review Required

> [!IMPORTANT]
> - CircularTelegraphIndicator will now scale 1:1 with the requested radius parameter (targetScale = radius / CIRCLE_MESH_RADIUS), ensuring exact parity between visual indicators and collision radii.
> - _slamRadius will be updated from 6.5m to 13.0m in GolemBossConfigSO and GolemBossConfig.asset, matching the full visual area previously displayed for Leap Slam.
> - _skyArmImpactRadius will be updated from 2.4m to 4.8m in GolemBossConfigSO and GolemBossConfig.asset, matching the full visual area previously displayed for Sky Arm Barrage.

## Open Questions

- None. Requirements confirmed: adjust collision and visual telegraph to cover the larger 100% (1:1) area.

## Proposed Changes

### Indicators System

#### [MODIFY] Assets/Scripts/Indicators/Constants/IndicatorConstants.cs
- Add CIRCLE_MESH_RADIUS = 1f; constant documenting that CircleFacingUp and CircleBorder mesh assets have an unscaled radius of 1.0 unit (diameter of 2.0 units).

#### [MODIFY] Assets/Scripts/Indicators/CircularTelegraphIndicator.cs
- Fix targetScale calculation from radius * 2f to radius / IndicatorConstants.CIRCLE_MESH_RADIUS (radius), ensuring the visual circle on the ground matches the radius parameter 1:1 in world units.

---

### Golem Boss Configuration

#### [MODIFY] Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs
- Update default _slamRadius from 6.5f to 13.0f.
- Update default _skyArmImpactRadius from 1.8f to 4.8f.

#### [MODIFY] Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfig.asset
- Update serialized _slamRadius to 13.
- Update serialized _skyArmImpactRadius to 4.8.

---

## Verification Plan

### Automated Checks
- Compile the Unity C# solution to ensure zero errors and zero warnings:
`powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
`

### Manual Verification
1. Leap Slam Telegraph & Collision Verification:
   - In Unity Play Mode or scene test, trigger Golem Boss Leap Slam.
   - Verify that the red circle indicator spawns with 13.0m radius (26.0m diameter) and the damage check (Physics.OverlapCapsule) hits the player anywhere inside the full 13.0m radius of the circle indicator.
2. Sky Arm Barrage Telegraph & Collision Verification:
   - Trigger Golem Boss Sky Arm Barrage.
   - Verify that each falling arm indicator spawns with 4.8m radius (9.6m diameter) and deals damage anywhere inside the 4.8m circle when the arm hits the ground.
