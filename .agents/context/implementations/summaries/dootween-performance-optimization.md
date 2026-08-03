# Implementation Summary - DOTween Performance & Allocations Optimization

Date: 2026-07-22

## Overview

A project-wide audit of DOTween usage was conducted across runtime systems to eliminate high-risk GC allocations and CPU frame overhead in high-frequency execution paths (damage numbers, projectiles, EXP particles, enemy knockbacks, drops, and UI feedback).

## Files Modified

- Assets/Scripts/DamageNumbers/DamageNumber.cs
  - Replaced TextMeshPro.fontSize DOTween.To getter/setter property animation with a transform.localScale Sequence tween.
  - Eliminates per-frame TextMeshPro text layout recalculations, character glyph re-parsing, and mesh generation for active damage numbers.
  - Replaced anonymous completion lambdas with an explicit method callback to remove closure delegate allocations.
  - Added tween cleanup in OnDisable and OnDestroy.
- Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Added damageNumber.transform.DOKill() when damage numbers are released back to the object pool.
- Assets/Scripts/Projectiles/Projectile.cs
  - Replaced DOTween.Kill(transform) string/target search with an explicit Tween _shrinkTween handle.
  - Replaced completion closure lambda () => OnLifeEnd?.Invoke(...) with a cached HandleShrinkComplete method group callback.
  - Added proper tween cleanup in OnRelease and OnDestroy.
- Assets/Scripts/LevelSystem/Exp/ExpParticle.cs
  - Refactored CollectExp() to use a direct Tween _shrinkTween handle and HandleCollectShrinkComplete method callback instead of LifeEndingShrinkToZeroTween with dynamic lambdas.
  - Removed dangling AudioClipPlayer.OnAudioClipFinished event subscriptions that leaked delegate references across pooled EXP particles.
  - Added tween cleanup in OnRelease and OnDestroy.
- Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Added _movementUnrelatedToSpeedTween?.Kill() cleanup in OnDisable() and OnDestroy().
  - Prevents active knockback/force-movement tweens from continuing to move deactivated or pooled enemy GameObjects in pool storage.
- Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Added go.transform.DOKill() in actionOnRelease when returning drop prefabs (crates, EXP particles) to object pools.
- Assets/Scripts/Effects/XYZRotationLoop.cs
  - Removed reference-type Tuple<bool, bool, bool> heap allocation in SetMaxRotationTween on every OnEnable pass.
  - Added _rotationTween cleanup in OnDestroy().
- Assets/Scripts/HealthSystem/HealthBar.cs
  - Added _shakeTween handle tracking to complete/kill previous shake tweens on fast multi-hit damage.
  - Added tween cleanup in OnDisable() and OnDestroy().

## Key Optimization Decisions

- Scale Animation Over Text Layout: Animating transform.localScale leverages native transform matrix calculations instead of forcing TextMeshPro font layout rebuilds on every frame.
- Explicit Tween Handles Over Global Lookups: Storing private Tween fields allows direct _tween?.Kill() calls, bypassing expensive DOTween.Kill(transform) global target searches across all active engine tweens.
- Zero-Allocation Callbacks: Replaced inline () => ... closures in high-frequency loops with class method callbacks.
- Pool Safety & Hygiene: Guaranteed all active tweens on pooled objects are terminated upon disable/release to eliminate background execution bugs on recycled entities.

## Verification Performed

- Executed targeted C# compile: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
- Result: Build completed cleanly with 0 Errors.
