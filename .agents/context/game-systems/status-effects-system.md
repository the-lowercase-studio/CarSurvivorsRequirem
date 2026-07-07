# Status Effects System Documentation

## Purpose

The Status Effects system defines shared combat capability interfaces for damage, stun, and knockback, plus the reusable stun timer controller.

It is responsible for:
- Exposing target capabilities through [IDamageable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs), [IStunnable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IStunnable.cs), and [IKnockable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs).
- Providing `IStunController` and [StunController](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/StunController.cs) for timed stun state.
- Supporting helper-based effect application without depending on concrete enemy or player types.

It is not responsible for:
- Health value storage, regeneration, or health bar presentation.
- Enemy movement, attack logic, or death sequencing.
- Skill-specific effect timing, radius, target selection, or balance.
- Damage number spawning or VFX/audio feedback.

## Reading Map

- Primary code locations:
  - [IDamageable.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs)
  - [IStunnable.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IStunnable.cs)
  - [IKnockable.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs)
  - [StunController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/StunController.cs)
- Current concrete users:
  - [Enemy.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Enemy.cs)
  - [EnemyMovementController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/EnemyMovementController.cs)
  - [EnemyAttackController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/EnemyAttackController.cs)
  - [PlayerDamagedHandler.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Player/PlayerDamagedHandler.cs)
  - [EntityManipulationHelper.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/EntityManipulationHelper.cs)
  - [DeathVolume.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Volumes/DeathVolume.cs)
  - [SawBlade.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs)
  - [Landmine.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs)
  - [Projectile.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Projectiles/Projectile.cs)
- Related docs:
  - [health-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/health-system.md)
  - [enemies-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/enemies-system.md)
  - [skills-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/skills-system.md)
  - [projectiles-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/projectiles-system.md)
  - [project-coding-standards.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/project-coding-standards.md)
- Related agents or instructions:
  - [document-system SKILL.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/skills/document-system/SKILL.md)
  - [architecture-review SKILL.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/skills/architecture-review/SKILL.md)

## Architecture and Data Flow

- Core components:
  - [IDamageable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs) exposes `TakeDamage(float damage)` and `TakeFullHpDamage()`.
  - [IStunnable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IStunnable.cs) exposes `ApplyStun(float duration)`.
  - [IKnockable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs) exposes `ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)` and is currently `internal`.
  - `IStunController` exposes `IsStunned`, stun lifecycle events, and `PerformStun(float duration)`.
  - [StunController](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/StunController.cs) is a `MonoBehaviour` that stores stun state and counts down in `Update`.
  - [EntityManipulationHelper](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/EntityManipulationHelper.cs) applies damage, knockback, and stun through collider `TryGetComponent` capability checks.
- Runtime flow:
  - Skills, projectiles, enemy attacks, or volumes detect a target collider.
  - Callers either use [EntityManipulationHelper](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/EntityManipulationHelper.cs) or directly query a capability interface.
  - Damage flows into the target's [IDamageable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs) implementation.
  - Knockback flows into the target's [IKnockable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs) implementation.
  - Stun flows into [IStunnable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IStunnable.cs).ApplyStun, which current enemies forward to `StunController.PerformStun`.
  - `StunController.PerformStun` starts stun and raises `OnStunStart`, or extends only when the new duration is longer than the remaining timer and raises `OnStunExtended`.
  - `StunController.Update` decrements the timer and raises `OnStunEnd` when the timer reaches zero.

## Rules and Invariants

- Critical behavior rules:
  - Effect callers should depend on capability interfaces, not concrete enemy or player classes.
  - Stun duration extension only replaces the timer when the new duration is longer than the current remaining stun.
  - `OnStunStart`, `OnStunExtended`, and `OnStunEnd` are synchronous state-change events from [StunController](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/StunController.cs).
  - `TakeFullHpDamage` represents an immediate lethal/full-health damage path and is used by [DeathVolume](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Volumes/DeathVolume.cs).
  - Knockback direction should be flattened by helpers before application when using [EntityManipulationHelper](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/EntityManipulationHelper.cs).Knockback.
- Ordering or sequencing guarantees:
  - `OnStunStart` fires after `IsStunned` becomes true.
  - `OnStunEnd` fires after `IsStunned` becomes false.
  - `OnStunExtended` fires only when an active stun receives a longer remaining duration.
- Constraints contributors must preserve:
  - Keep effect APIs narrow and capability-based.
  - Treat damage, stun duration, knockback range, and knockback travel time as player-facing balance.
  - Preserve target layer/collider assumptions in callers before changing capability lookup.
  - Do not move health storage or visual feedback into status capability interfaces.

## Extension Points

- Safe extension areas:
  - Add a new targetable entity by implementing the relevant capability interfaces.
  - Add a new skill effect by applying capabilities through [EntityManipulationHelper](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/EntityManipulationHelper.cs) or direct `TryGetComponent`.
  - Add a new status controller when the state has behavior beyond the current stun timer.
- Required dependencies and contracts:
  - Enemy prefabs that should be stunned need an `IStunController` implementation and an [IStunnable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IStunnable.cs) forwarding path.
  - Knockback callers outside the current assembly may need visibility changes because [IKnockable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs) is internal.
  - Damage callers require colliders to be on objects that expose [IDamageable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs) or have the capability on a queried parent/object as implemented by the caller.
- Testing implications:
  - Compile after interface or visibility changes.
  - In Unity, validate damage, full HP damage, stun start/end/extension, knockback direction, and enemy movement/attack behavior while stunned.
  - For new targets, validate component placement matches the collider queried by effect callers.

## Integration Notes

- Upstream dependencies:
  - Skills and projectiles choose targets and effect values.
  - Enemy attack animation events apply player damage through [IDamageable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs).
  - [DeathVolume](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Volumes/DeathVolume.cs) applies full HP damage or pool return.
- Downstream consumers:
  - Health systems receive damage through concrete [IDamageable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IDamageable.cs) implementations.
  - Enemy movement can consume `IStunController.IsStunned`.
  - Skills rely on enemy capability interfaces for damage, knockback, and stun.
- Cross-system coupling risks:
  - Component placement matters because most effect application uses collider `TryGetComponent`.
  - Stun state only affects behavior when concrete consumers check `IStunController.IsStunned`.
  - Changing [IKnockable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs) visibility or signature affects helper and enemy implementation boundaries.

## Known Risks and Open Questions

- Known limitations:
  - [IKnockable](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/IKnockable.cs) is `internal`, which limits use outside the current assembly.
  - [StunController](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/StatusEffects/StunController.cs) has no stacking model beyond replacing the timer with a longer duration.
  - Existing enemy movement has a documented issue where stun may not stop movement because the movement controller gates stun checks behind `_isStunable`.
  - Capability lookup currently uses `TryGetComponent` on the collider object, so child-collider setups may need explicit forwarding components.
- Open design questions:
  - Should stun affect attacking as well as movement for all enemies?
  - Should knockback be public API if future systems outside the assembly need to apply it?
  - Should status effects support immunity, resistance, stacking, or source tracking?
- Suggested follow-up tasks:
  - Review enemy stun movement blocking in a focused bugfix.
  - Add a prefab/component placement checklist for targetable entities if collider forwarding issues recur.
