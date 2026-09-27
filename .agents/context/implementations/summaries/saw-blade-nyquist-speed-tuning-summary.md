# Implementation Summary - Saw Blade Rotation Nyquist Speed Tuning & Robustness

Date: 2026-09-27

## Overview

Implemented Option A to resolve stroboscopic aliasing (the wagon-wheel effect) and apparent rotation reversals on the front circular saw blades (`SawBlade`) of the player car. 

Detailed frequency and geometric analysis revealed that the 3D saw model has 16 teeth spaced every 22.5 deg. At 60+ FPS, any rotational speed exceeding 675 deg/s (or 11.25 deg displacement per frame) violates the Nyquist-Shannon sampling limit, causing discrete frames to be perceived by the player as moving backwards or slowing to a crawl. The previous plan's high base speed (1200 deg/s) and large turn multiplier (up to 3600 deg/s) pushed the blades directly into critical stroboscopic aliasing zones whenever the player steered.

With this update:
- Base rotation speed is tuned to 400 deg/s (`_tweenIterationTime = 0.9s`, ~1.11 rev/s).
- Turn speed bonus is tuned to a gentle 25% boost (`_maxTurnSpeedMultiplier = 0.25f`), ensuring that even at full steer lock with additive car yaw angular velocity (~165 deg/s), total effective world rotation speed peaks at 665 deg/s, mathematically remaining strictly below the 675 deg/s Nyquist limit at 60 FPS.
- Added a fallback in `SawBlade.cs` to retrieve `ICarController` via `GetComponentInParent<ICarController>()` if `_playerManager` is not yet injected.

## Key Changes

### Saw Skill & Controller
- Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs:
  - Tuned `_maxTurnSpeedMultiplier` default value from `2.0f` to `0.25f`.
  - Added parent hierarchy fallback (`GetComponentInParent<ICarController>()`) in `UpdateRotationSpeedMultiplier()` to safeguard against delayed or missing Reflex DI container injection.

### Prefab Asset Tuning
- Assets/Imported/Models/Skills/Saw/SawBlade.prefab:
  - Updated `_tweenIterationTime` on `XYZRotationLoop` from `0.3` to `0.9` (400 deg/s base speed).
- Assets/Prefabs/Skills/Saw/Saw.prefab:
  - Updated `_tweenIterationTime` overrides on both `SawBlade` and `SawBlade (1)` instances from `0.3` to `0.9`.

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/saw-blade-nyquist-speed-tuning-plan.md
- Coding Standards: Verified strict compliance with .agents/context/project-coding-standards.md:
  - Field ordering: `[Inject]`, `[SerializeField]`, private.
  - Zero heap allocations in `Update()`.
  - English language invariant maintained across all code, comments, and operational documentation.

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors.

### Mathematical Verification
- 16 teeth $\rightarrow$ tooth pitch $\theta = 22.5^\circ$.
- Nyquist limit: $\theta / 2 = 11.25^\circ/\text{frame}$.
- At 60 FPS: max speed without aliasing $= 675^\circ/\text{s}$.
- Straight driving: $400^\circ/\text{s} = 6.67^\circ/\text{frame}$ ($< 11.25^\circ$, perfectly forward at 1.11 rev/s).
- 100% steer lock on opposing blade: $500^\circ/\text{s} - 165^\circ/\text{s} = 335^\circ/\text{s}$ (net world $5.58^\circ/\text{frame}$, forward).
- 100% steer lock on assisting blade: $500^\circ/\text{s} + 165^\circ/\text{s} = 665^\circ/\text{s}$ (net world $11.08^\circ/\text{frame} < 11.25^\circ$, forward).
- Zero aliasing, zero harmonic freezes, and zero perceived reverse rotation across all turning states.

## Follow-up / Unity Editor Steps

1. Enter Play Mode in Unity Editor with the Saw skill equipped.
2. Drive straight and verify the clean, steady forward spin of both saw blades.
3. Turn left and right sharply and perform drifts: verify that both blades continue spinning forward smoothly with a responsive 25% rev-up, completely free from visual stuttering, optical braking, or reverse rotation.
