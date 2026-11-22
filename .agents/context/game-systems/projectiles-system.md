# Projectiles System Documentation

## Purpose

The Projectiles system retains the shared ScriptableObject configuration contract (`ProjectileConfigSO`) used by turret-based player skills (`TurretConfigSO` and `LasergunSkillSO` / `LasergunTurret`), after the legacy standalone player `Projectile` MonoBehaviour, `ProjectileSpawnConfig`, `ProjectileConstants`, and `Projectile.prefab` / `MinigunProjectile.prefab` assets were retired in the Phase 1 unused-code cleanup.

It is responsible for:

- Storing serialized starting projectile stats (`_startDamage`, `_startSize`, `_startSpeed`, `_startRange`, `_startMaxPiercing`, and `DisapearingDuration`) and copying them into mutable runtime properties (`Damage`, `Speed`, `Size`, `Range`, `MaxPiercing`) in `ProjectileConfigSO.OnEnable()`.
- Supplying runtime damage (and synced range on `LasergunSkillSO`) to `TurretConfigSO` and `LasergunTurret`.

It is not responsible for:

- Minigun hit resolution or ballistic tracer streams (owned by `MinigunShotResolver` and `MinigunTurret` in the Skills system).
- Lasergun target acquisition, line-of-sight checks, or `LineRenderer` beams (owned by `LasergunTurret` in the Skills system).
- Boss-specific projectiles (`GolemArmProjectile` in the Golem Boss system, `MortarShellProjectile` and `RailgunOrbProjectile` in the Tower Boss system).
- Enemy health reduction or floating damage number spawning (owned by the Health and Damage Numbers systems).

## Reading Map

- Primary code locations:
  - Assets/ScriptableObjects/ProjectileConfigSO.cs
  - Assets/ScriptableObjects/TurretConfigSO.cs
  - Assets/ScriptableObjects/Skills/PlayerSkills/LasergunSkill/LasergunSkillUpgradeableConfigSO.cs
  - Assets/Scripts/Skills/Turret.cs
  - Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs
- Authored configuration assets:
  - Assets/ScriptableObjects/Skills/PlayerSkills/LasergunSkill/LaserProjectileStats.asset
  - Assets/ScriptableObjects/Skills/PlayerSkills/MinigunSkill/MinigunProjectileConfig.asset
- Automated tests:
  - Assets/Scripts/Editor/Tests/SkillConfigRuntimeTests.cs
  - Assets/Scripts/Editor/Tests/SkillsTierCarryoverTests.cs
- Related docs:
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md

## Architecture and Data Flow

- Core components:
  - ProjectileConfigSO: ScriptableObject storing serialized starting damage, size, speed, range, max piercing, and disappearance duration. Damage and max piercing are int values. OnEnable copies starting values into mutable runtime properties (`Damage`, `Speed`, `Size`, `Range`, `MaxPiercing`).
  - TurretConfigSO: Aggregates a `ProjectileConfigSO` reference (`ProjectileStatsSO`) alongside turret range and rotation settings.
  - Turret<ConfigSO>: Abstract base MonoBehaviour implementing `IInitializableWithScriptableConfig<ConfigSO>` with serialized `_config` and `_gunTip`, exposing `Initialize(ConfigSO)`, `IsInitialized()`, and abstract `Shoot(float shootPreparingAnimationSpeed = 1f)`.
- Runtime flow:
  - In `LasergunSkillSO.ResetRuntimeState()` and `SyncDerivedRuntimeState()`, `_projectileConfig.Damage` and `_projectileConfig.Range` (alongside `_turretConfig.Range`) are synchronized from the deep-copied `UpgradeableStat` values (`Damage` and `Range`) and updated whenever `Damage.OnUpgrade` or `Range.OnUpgrade` fires or Tier 2 carryover state is restored.
  - When `LasergunTurret.Shoot` fires at acquired targets, it reads `_config.ProjectileStatsSO.Damage` and forwards the damage and hit position (`_hitTargetClosestPoints[i]`) to `EntityManipulationHelper.Damage`.
  - `MinigunSkill` and `MinigunTurret` resolve corridor hits directly through `MinigunShotResolver` and `MinigunSkillSO` stats without reading `ProjectileConfigSO`.

## Rules and Invariants

- Critical behavior rules:
  - Preserve serialized field names on `ProjectileConfigSO` (`_startDamage`, `_startSize`, `_startSpeed`, `_startRange`, `_startMaxPiercing`, `DisapearingDuration`) unless an ADR-004 asset cleanup is explicitly approved.
  - `LasergunSkillSO.ResetRuntimeState()` and `SyncDerivedRuntimeState()` must synchronize `_projectileConfig.Damage` from `Damage.Value` so runtime upgrades and multi-tier carryover apply immediately without mutating serialized `_startDamage` on disk.
- Constraints contributors must preserve:
  - Do not reintroduce the retired generic `Projectile` MonoBehaviour or prefab references on `Turret<ConfigSO>` unless a new pooled physical player projectile weapon is explicitly designed and approved.
  - Follow ADR-004 before editing or deleting `.asset` or `.prefab` files.

## Extension Points

- Safe extension areas:
  - If a future player weapon requires physical pooled projectiles, design a dedicated pooled projectile component implementing `IPoolable` and `IInitializableWithScriptableConfig<TConfig>` under the owning skill folder (similar to `MortarShellPool` / `RailgunOrbPool` in the Tower Boss system).
- Testing implications:
  - Compile after C# changes with `dotnet build CarSurvivors.Runtime.csproj -p:BuildProjectReferences=false` and `dotnet build CarSurvivors.Editor.Tests.csproj -p:BuildProjectReferences=false`.
  - Verify `LasergunSkillSO` derived stat synchronization in Assets/Scripts/Editor/Tests/SkillConfigRuntimeTests.cs and Assets/Scripts/Editor/Tests/SkillsTierCarryoverTests.cs.

## Integration Notes

- Upstream dependencies:
  - `LasergunSkillSO` updates `ProjectileConfigSO.Damage` and `ProjectileConfigSO.Range` from runtime `UpgradeableStat` instances.
- Downstream consumers:
  - `LasergunTurret` reads `_config.ProjectileStatsSO.Damage` when applying beam damage to enemies and bosses.
- Cross-system coupling risks:
  - `ProjectileConfigSO` runtime properties (`Damage`, `Range`, etc.) are mutable on the asset instance during play and rely on `OnEnable()` and `LasergunSkillSO.ResetRuntimeState()` / `SyncDerivedRuntimeState()` to reset cleanly at run start and tier transitions.

## Known Risks and Open Questions

- Known limitations:
  - `ProjectileConfigSO` still serializes legacy fields (`DisapearingDuration`, `_startSize`, `_startSpeed`, `_startRange`, `_startMaxPiercing`) and Assets/ScriptableObjects/Skills/PlayerSkills/MinigunSkill/MinigunProjectileConfig.asset remains on disk pending an ADR-004 Phase 2 asset cleanup decision.
- Suggested follow-up tasks:
  - If Phase 2 asset cleanup is approved under ADR-004, trim unused serialized properties on `ProjectileConfigSO` and remove the unreferenced `MinigunProjectileConfig.asset`.
