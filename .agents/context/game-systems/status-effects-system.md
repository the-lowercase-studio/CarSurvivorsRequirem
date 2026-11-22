# Status Effects System Documentation

## Purpose

The Status Effects system defines shared combat capability interfaces for standard damage, hit-position-aware damage, and knockback, along with helper utilities for effect application.

It is responsible for:
- Exposing target damage capabilities through Assets/Scripts/StatusEffects/IDamageable.cs (IDamageable and IPositionalDamageable) and knockback capabilities through Assets/Scripts/StatusEffects/IKnockable.cs.
- Supporting helper-based effect application without depending on concrete enemy, boss, or player types via Assets/Scripts/StatusEffects/EntityManipulationHelper.cs.

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
- Concrete capability implementations:
  - Assets/Scripts/Enemies/Base/Enemy.cs (IDamageable, IKnockable)
  - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs (IDamageable, IKnockable)
  - Assets/Scripts/Enemies/Bosses/Towers/TowerBossBase.cs (IPositionalDamageable, IKnockable)
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs (IPositionalDamageable, IKnockable)
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/RailgunTowerBoss.cs (IPositionalDamageable, IKnockable)
  - Assets/Scripts/Player/PlayerDamagedHandler.cs (IDamageable)
- Concrete effect producers and callers:
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Volumes/DeathVolume.cs
  - Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs
  - Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs
  - Assets/Scripts/Projectiles/Projectile.cs
  - Assets/Scripts/PowerUps/FireTails/FireTailsService.cs
  - Assets/Scripts/PowerUps/Nuke/NukeEffectService.cs
- Related docs:
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/damage-numbers-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/projectiles-system.md
  - .agents/context/game-systems/power-ups-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/StatusEffects/IDamageable.cs defines:
    - IDamageable: TakeDamage(float damage) and TakeFullHpDamage().
    - IPositionalDamageable : IDamageable: TakeDamage(float damage, Vector3 hitWorldPosition) for targets (such as TowerBossBase and RailgunTowerBoss) that anchor hit feedback or damage numbers to the specific collider or world location struck.
  - Assets/Scripts/StatusEffects/IKnockable.cs exposes ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation) and is currently internal.
  - Assets/Scripts/StatusEffects/EntityManipulationHelper.cs applies damage and knockback through collider TryGetComponent and GetComponentInParent capability checks:
    - Damage(Collider target, float damage) guards null/disabled colliders and delegates to Damage(target, damage, target.bounds.center).
    - Damage(Collider target, float damage, Vector3 hitWorldPosition) resolves IDamageable on the collider or its parent; if the target implements IPositionalDamageable, it invokes positionalDamageable.TakeDamage(damage, hitWorldPosition), otherwise it calls damageable.TakeDamage(damage).
    - Knockback(Collider target, Vector3 dir, float range, float timeToArriveAtLocation) resolves IKnockable on the collider or its parent, flattens dir.y = 0, and calls knockable.ApplyKnockBack(dir, range, timeToArriveAtLocation).
- Runtime flow:
  - Skills, projectiles, power-ups, enemy attacks, or volumes detect a target collider.
  - Callers either invoke EntityManipulationHelper.Damage (SawBlade passing other.ClosestPoint(transform.position), LasergunTurret passing _hitTargetClosestPoints[i], Landmine using target.bounds.center) or dispatch directly to IPositionalDamageable / IDamageable (Projectile passing collider.ClosestPoint(sphereCenter), MinigunTurret passing hit.ImpactPosition, FireTailsService passing closest point to the overlapping FireHazardNode).
  - Damage flows into the target's IPositionalDamageable or IDamageable implementation.
  - Knockback flows into the target's IKnockable implementation.

## Rules and Invariants

- Critical behavior rules:
  - Effect callers should depend on capability interfaces (IDamageable, IPositionalDamageable, IKnockable), not concrete enemy, boss, or player classes.
  - When a weapon or hazard knows the world contact or closest point on the hit collider, it should pass that position via EntityManipulationHelper.Damage(collider, damage, hitWorldPosition) or IPositionalDamageable.TakeDamage(damage, hitWorldPosition) so multi-collider targets like RailgunTowerBoss anchor damage numbers to the attacked support leg.
  - TakeFullHpDamage represents an immediate lethal/full-health damage path and is used by Assets/Scripts/Volumes/DeathVolume.cs and NukeEffectService (on regular enemies).
  - Knockback direction is flattened (dir.y = 0) by EntityManipulationHelper.Knockback before application.
- Ordering or sequencing guarantees:
  - Knockback calculation precedes movement updates in enemy movement handlers.
- Constraints contributors must preserve:
  - Keep effect APIs narrow and capability-based.
  - Treat damage, knockback range, and knockback travel time as player-facing balance.
  - Preserve target layer/collider assumptions in callers before changing capability lookup.
  - Do not move health storage or visual feedback into status capability interfaces.

## Extension Points

- Safe extension areas:
  - Add a new targetable entity by implementing IDamageable (or IPositionalDamageable if hit-position anchoring is needed) and optionally IKnockable.
  - Add a new skill or hazard effect by applying capabilities through Assets/Scripts/StatusEffects/EntityManipulationHelper.cs or direct TryGetComponent / GetComponentInParent lookup.
- Required dependencies and contracts:
  - Knockback callers outside the current assembly may need visibility changes because Assets/Scripts/StatusEffects/IKnockable.cs is internal.
  - Damage callers require colliders to be on objects that expose IDamageable or have the capability on a parent object as resolved by TryGetComponent + GetComponentInParent.
- Testing implications:
  - Compile after interface or visibility changes.
  - Run EditMode tests in Assets/Scripts/Editor/Tests/BossDamageNumbersTests.cs and Assets/Scripts/Editor/Tests/ArmorPowerUpAndDamagePipelineTests.cs.
  - In Unity Play Mode, validate damage, positional damage number anchoring on RailgunTowerBoss legs, full HP damage, knockback direction, and enemy movement behavior.

## Integration Notes

- Upstream dependencies:
  - Skills, projectiles, and power-ups choose targets, hit positions, and effect values.
  - Enemy and boss attacks apply player damage through IDamageable.
  - Assets/Scripts/Volumes/DeathVolume.cs applies full HP damage or pool return.
- Downstream consumers:
  - Enemy, GolemBoss, TowerBossBase, and PlayerDamagedHandler receive damage through IDamageable / IPositionalDamageable implementations.
  - EnemyMovementController handles knockback through Enemy's IKnockable implementation (while GolemBoss and TowerBossBase implement IKnockable as a no-op for knockback immunity).
- Cross-system coupling risks:
  - Component placement matters because effect application uses collider TryGetComponent with GetComponentInParent fallback (for example, RailgunTowerBoss places its two leg BoxColliders on child transforms while RailgunTowerBoss sits on the root).
  - Changing Assets/Scripts/StatusEffects/IKnockable.cs visibility or signature affects helper and enemy implementation boundaries.

## Known Risks and Open Questions

- Known limitations:
  - Assets/Scripts/StatusEffects/IKnockable.cs is internal, which limits use outside Assembly-CSharp (including Assembly-CSharp-Editor test fakes unless accessed indirectly).
- Open design questions:
  - Should IKnockable be promoted to public if future Editor test suites need direct knockback fakes?
- Suggested follow-up tasks:
  - Add a prefab/component placement checklist for multi-collider targetable entities if collider forwarding issues recur.
