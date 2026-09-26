# Implementation Summary - Mortar Shell Trajectory & Tangent Orientation Overhaul

Date: 2026-09-26

## Overview

Overhauled the mortar shell projectile flight calculation, curve traversal, and rotational heading in Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs. Resolved the issue where mortar projectiles with upright 3D models (such as MortarBullet.fbx) flew and landed sideways ("bokiem"). Implemented analytical parabolic and custom curve evaluation via DOTween, singularity-free pitch/yaw heading tracking, configurable model orientation offsets, and axial rifling roll.

## Key Changes

### Mortar Tower Projectile Flight Mechanics
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs:
  - Added serialized fields:
    - `_modelRotationOffset`: Euler offset (default `(90f, 0f, 0f)`) aligning the mesh tip (local +Y) with the forward flight velocity vector.
    - `_customHeightCurve`: Optional `AnimationCurve` providing designer-tunable ballistic profiles (e.g. apex hang and steep plunge).
    - `_riflingSpinSpeed`: Axial rotation speed (default `360f` deg/sec) around the forward flight axis.
  - Replaced finite difference `DOJump` with analytical progress evaluation using `DOVirtual.Float(0f, 1f, duration, progress => { ... })`.
  - Implemented `EvaluateTrajectory`: provides exact analytical position and tangent vector for both standard parabolas ($y(t) = 4h \cdot t(1-t)$) and custom curves.
  - Implemented `ComputeTrajectoryRotation`: builds an orthonormal frame from the horizontal trajectory azimuth to prevent gimbal lock / 180-degree flipping during steep downward descents, applies axial rifling spin deterministically ($t \cdot \text{duration} \cdot \text{speed}$), and multiplies by `_modelRotationOffset`.
  - Maintained zero runtime GC allocations in tween callbacks and clean unsubscription / tween cancellation in `KillTweens()`.

## Documentation & Standards

- Specification & Plan: .agents/context/implementations/plans/mortar-shell-trajectory-and-orientation-spec.md
- Coding Standards: Verified compliance with .agents/context/project-coding-standards.md (naming conventions, field order: serialized then private, no LINQ, explicit namespaces).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors (and 0 new warnings).

### Manual Verification Instructions
1. Open `Assets/Scenes/RuinedBloodCity.unity` in Unity Editor.
2. Approach the Mortar Tower to initiate the encounter.
3. Observe mortar shell launches during Attack 1 (Cluster Burst), Attack 2 (Diagonal Bounce), and Attack 3 (Boulder Drop):
   - At launch: shell nose points up along the launch angle.
   - At apex: shell smoothly transitions horizontally.
   - On descent: shell nose points down toward the ground target crater, without flipping or flying sideways.
   - In flight: shell smoothly spins axially (rifling spin).

## Follow-up / Unity Editor Steps

1. Select `Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortalShell.prefab` in the Unity Project window.
2. In the Inspector for `MortarShellProjectile`:
   - Verify `Model Rotation Offset` defaults to `(90, 0, 0)`.
   - (Optional) Assign a custom `AnimationCurve` to `Custom Height Curve` if a non-parabolic apex-hang or steep drop profile is desired.
   - (Optional) Adjust `Rifling Spin Speed` to tune the shell's axial spin rate.
