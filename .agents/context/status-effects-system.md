# Status Effects System Documentation

## Purpose

The Status Effects system defines shared combat capability interfaces for damage, stun, and knockback, plus the reusable stun timer controller.

It is responsible for:

- Exposing target capabilities through `IDamageable`, `IStunnable`, and `IKnockable`.
- Providing `IStunController` and `StunController` for timed stun state.
- Supporting helper-based effect application without depending on concrete enemy or player types.

It is not responsible for:

- Health value storage, regeneration, or health bar presentation.
- Enemy movement, attack logic, or death sequencing.
- Skill-specific effect timing, radius, target selection, or balance.
- Damage number spawning or VFX/audio feedback.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/StatusEffects/IDamageable.cs`
  - `Assets/Scripts/StatusEffects/IStunnable.cs`
  - `Assets/Scripts/StatusEffects/IKnockable.cs`
  - `Assets/Scripts/StatusEffects/StunController.cs`
- Current concrete users:
  - `Assets/Scripts/Enemies/Enemy.cs`
  - `Assets/Scripts/Enemies/EnemyMovementController.cs`
  - `Assets/Scripts/Enemies/EnemyAttackController.cs`
  - `Assets/Scripts/Player/PlayerDamagedHandler.cs`
  - `Assets/Scripts/StatusEffects/EntityManipulationHelper.cs`
  - `Assets/Scripts/Volumes/DeathVolume.cs`
  - `Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs`
  - `Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs`
  - `Assets/Scripts/Projectiles/Projectile.cs`
- Related docs:
  - `.agents/context/health-system.md`
  - `.agents/context/enemies-system.md`
  - `.agents/context/skills-system.md`
  - `.agents/context/projectiles-system.md`
  - `.agents/context/project-coding-standards.md`
- Related agents or instructions:
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/architecture-review/SKILL.md`

## Architecture and Data Flow

- Core components:
  - `IDamageable` exposes `TakeDamage(float damage)` and `TakeFullHpDamage()`.
  - `IStunnable` exposes `ApplyStun(float duration)`.
  - `IKnockable` exposes `ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)` and is currently `internal`.
  - `IStunController` exposes `IsStunned`, stun lifecycle events, and `PerformStun(float duration)`.
  - `StunController` is a `MonoBehaviour` that stores stun state and counts down in `Update`.
  - `EntityManipulationHelper` applies damage, knockback, and stun through collider `TryGetComponent` capability checks.
- Runtime flow:
  - Skills, projectiles, enemy attacks, or volumes detect a target collider.
  - Callers either use `EntityManipulationHelper` or directly query a capability interface.
  - Damage flows into the target's `IDamageable` implementation.
  - Knockback flows into the target's `IKnockable` implementation.
  - Stun flows into `IStunnable.ApplyStun`, which current enemies forward to `StunController.PerformStun`.
  - `StunController.PerformStun` starts stun and raises `OnStunStart`, or extends only when the new duration is longer than the remaining timer and raises `OnStunExtended`.
  - `StunController.Update` decrements the timer and raises `OnStunEnd` when the timer reaches zero.

## Rules and Invariants

- Critical behavior rules:
  - Effect callers should depend on capability interfaces, not concrete enemy or player classes.
  - Stun duration extension only replaces the timer when the new duration is longer than the current remaining stun.
  - `OnStunStart`, `OnStunExtended`, and `OnStunEnd` are synchronous state-change events from `StunController`.
  - `TakeFullHpDamage` represents an immediate lethal/full-health damage path and is used by `DeathVolume`.
  - Knockback direction should be flattened by helpers before application when using `EntityManipulationHelper.Knockback`.
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
  - Add a new skill effect by applying capabilities through `EntityManipulationHelper` or direct `TryGetComponent`.
  - Add a new status controller when the state has behavior beyond the current stun timer.
- Required dependencies and contracts:
  - Enemy prefabs that should be stunned need an `IStunController` implementation and an `IStunnable` forwarding path.
  - Knockback callers outside the current assembly may need visibility changes because `IKnockable` is internal.
  - Damage callers require colliders to be on objects that expose `IDamageable` or have the capability on a queried parent/object as implemented by the caller.
- Testing implications:
  - Compile after interface or visibility changes.
  - In Unity, validate damage, full HP damage, stun start/end/extension, knockback direction, and enemy movement/attack behavior while stunned.
  - For new targets, validate component placement matches the collider queried by effect callers.

## Integration Notes

- Upstream dependencies:
  - Skills and projectiles choose targets and effect values.
  - Enemy attack animation events apply player damage through `IDamageable`.
  - `DeathVolume` applies full HP damage or pool return.
- Downstream consumers:
  - Health systems receive damage through concrete `IDamageable` implementations.
  - Enemy movement can consume `IStunController.IsStunned`.
  - Skills rely on enemy capability interfaces for damage, knockback, and stun.
- Cross-system coupling risks:
  - Component placement matters because most effect application uses collider `TryGetComponent`.
  - Stun state only affects behavior when concrete consumers check `IStunController.IsStunned`.
  - Changing `IKnockable` visibility or signature affects helper and enemy implementation boundaries.

## Known Risks and Open Questions

- Known limitations:
  - `IKnockable` is `internal`, which limits use outside the current assembly.
  - `StunController` has no stacking model beyond replacing the timer with a longer duration.
  - Existing enemy movement has a documented issue where stun may not stop movement because the movement controller gates stun checks behind `_isStunable`.
  - Capability lookup currently uses `TryGetComponent` on the collider object, so child-collider setups may need explicit forwarding components.
- Open design questions:
  - Should stun affect attacking as well as movement for all enemies?
  - Should knockback be public API if future systems outside the assembly need to apply it?
  - Should status effects support immunity, resistance, stacking, or source tracking?
- Suggested follow-up tasks:
  - Review enemy stun movement blocking in a focused bugfix.
  - Add a prefab/component placement checklist for targetable entities if collider forwarding issues recur.
