# Status Effects System Documentation

## Purpose

The Status Effects system defines shared combat capability interfaces for damage and knockback, along with helper utilities for effect application.

It is responsible for:
- Exposing target capabilities through Assets/Scripts/StatusEffects/IDamageable.cs and Assets/Scripts/StatusEffects/IKnockable.cs.
- Supporting helper-based effect application without depending on concrete enemy or player types via Assets/Scripts/StatusEffects/EntityManipulationHelper.cs.

It is not responsible for:
- Health value storage, regeneration, or health bar presentation.
- Enemy movement, attack logic, or death sequencing.
- Skill-specific effect timing, radius, target selection, or balance.
- Damage number spawning or VFX/audio feedback.

## Reading Map

- Primary code locations:
  - Assets/Scripts/StatusEffects/IDamageable.cs
  - Assets/Scripts/StatusEffects/IKnockable.cs
  - Assets/Scripts/StatusEffects/EntityManipulationHelper.cs
- Current concrete users:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Volumes/DeathVolume.cs
  - Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs
  - Assets/Scripts/Projectiles/Projectile.cs
- Related docs:
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/projectiles-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/StatusEffects/IDamageable.cs exposes `TakeDamage(float damage)` and `TakeFullHpDamage()`.
  - Assets/Scripts/StatusEffects/IKnockable.cs exposes `ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)` and is currently `internal`.
  - Assets/Scripts/StatusEffects/EntityManipulationHelper.cs applies damage and knockback through collider `TryGetComponent` and `GetComponentInParent` capability checks.
- Runtime flow:
  - Skills, projectiles, enemy attacks, or volumes detect a target collider.
  - Callers either use Assets/Scripts/StatusEffects/EntityManipulationHelper.cs or directly query a capability interface.
  - Damage flows into the target's Assets/Scripts/StatusEffects/IDamageable.cs implementation.
  - Knockback flows into the target's Assets/Scripts/StatusEffects/IKnockable.cs implementation.

## Rules and Invariants

- Critical behavior rules:
  - Effect callers should depend on capability interfaces, not concrete enemy or player classes.
  - `TakeFullHpDamage` represents an immediate lethal/full-health damage path and is used by Assets/Scripts/Volumes/DeathVolume.cs.
  - Knockback direction should be flattened (`dir.y = 0`) by helpers before application when using Assets/Scripts/StatusEffects/EntityManipulationHelper.cs.Knockback.
- Ordering or sequencing guarantees:
  - Knockback calculation precedes movement updates in enemy movement handlers.
- Constraints contributors must preserve:
  - Keep effect APIs narrow and capability-based.
  - Treat damage, knockback range, and knockback travel time as player-facing balance.
  - Preserve target layer/collider assumptions in callers before changing capability lookup.
  - Do not move health storage or visual feedback into status capability interfaces.

## Extension Points

- Safe extension areas:
  - Add a new targetable entity by implementing the relevant capability interfaces.
  - Add a new skill effect by applying capabilities through Assets/Scripts/StatusEffects/EntityManipulationHelper.cs or direct `TryGetComponent`.
- Required dependencies and contracts:
  - Knockback callers outside the current assembly may need visibility changes because Assets/Scripts/StatusEffects/IKnockable.cs is internal.
  - Damage callers require colliders to be on objects that expose Assets/Scripts/StatusEffects/IDamageable.cs or have the capability on a queried parent/object as implemented by the caller.
- Testing implications:
  - Compile after interface or visibility changes.
  - In Unity, validate damage, full HP damage, knockback direction, and enemy movement behavior.
  - For new targets, validate component placement matches the collider queried by effect callers.

## Integration Notes

- Upstream dependencies:
  - Skills and projectiles choose targets and effect values.
  - Enemy attack animation events apply player damage through Assets/Scripts/StatusEffects/IDamageable.cs.
  - Assets/Scripts/Volumes/DeathVolume.cs applies full HP damage or pool return.
- Downstream consumers:
  - Health systems receive damage through concrete Assets/Scripts/StatusEffects/IDamageable.cs implementations.
  - Enemy movement handles knockback through concrete Assets/Scripts/StatusEffects/IKnockable.cs implementations.
  - Skills rely on enemy capability interfaces for damage and knockback.
- Cross-system coupling risks:
  - Component placement matters because most effect application uses collider `TryGetComponent` with parent fallback.
  - Changing Assets/Scripts/StatusEffects/IKnockable.cs visibility or signature affects helper and enemy implementation boundaries.

## Known Risks and Open Questions

- Known limitations:
  - Assets/Scripts/StatusEffects/IKnockable.cs is `internal`, which limits use outside the current assembly.
  - Capability lookup currently uses `TryGetComponent` on the collider object with a single parent fallback.
- Open design questions:
  - Should knockback be public API if future systems outside the assembly need to apply it?
  - Should status effects support damage-over-time (DOT), slow, or debuffs in the future?
- Suggested follow-up tasks:
  - Add a prefab/component placement checklist for targetable entities if collider forwarding issues recur.
