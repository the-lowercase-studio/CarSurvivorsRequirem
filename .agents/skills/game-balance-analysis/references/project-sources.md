# Car Survivors Balance Sources

This map identifies owners, not frozen balance values. Recheck current code, paths, active references, and prefab overrides on every analysis.

| Area | Primary sources | Extract or verify |
| --- | --- | --- |
| Levels | Assets/Scripts/LevelSystem/LevelController.cs | Evaluated EXP curve, starting level, carryover, cap, ordering |
| EXP | Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs; Assets/Scripts/LevelSystem/Exp/ExpParticle.cs; Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs | Emitted reward, splitting/multiplication, queue/collection delay, pickup movement |
| Waves | Assets/Scripts/Waves/WaveConfig.cs; Assets/Scripts/Waves/WaveManager.cs | Active config, delays, integer recurrence, clear/freezing behavior |
| Composition | Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs; Assets/Scripts/Spawners/Enemies/EnemySpawnInfo.cs; Assets/Scripts/Spawners/Enemies/EnemiesSpawnChanceRedistributionSystem.cs; Assets/Scripts/Spawners/SpawnChanceInfo.cs | Prefab mapping, weights/thresholds, redistribution, successful spawns |
| Swarm | Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs | Random intervals, warning/ticks, type sequence, clamp, suppression/restart |
| Enemies | Assets/ScriptableObjects/Enemy/EnemyConfigSO.cs; Assets/Scripts/Enemies/Base/Enemy.cs; Assets/Scripts/Enemies/Base/EnemyAttackController.cs | HP, damage, reward, movement, attacks/hit timing, reset |
| Boss objectives | Assets/Scripts/Enemies/Bosses/BossEncounterService.cs | Required towers, engagement, Golem delay, suppression, completion |
| Golem | Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs; Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs; Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/ | Phases, scheduling, telegraphs, damage/rewards |
| Tower | Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs; Assets/Scripts/Enemies/Bosses/Towers/MortarTower/; Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs | Route, attack cycles/phases, arena reset, rewards |
| Survival | Assets/Scripts/Player/PlayerDamagedHandler.cs; Assets/Scripts/Player/PlayerManager.cs; Assets/Scripts/HealthSystem/Health.cs; Assets/Scripts/HealthSystem/RegenativeHealth.cs | HP, damage acceptance, death, regeneration |
| Power | Assets/Scripts/Skills/PlayerSkills/; Assets/ScriptableObjects/Skills/; Assets/Scripts/Stats/; Assets/Scripts/Projectiles/ | Base/upgraded stats, attacks/reload, AoE, targets, uptime |
| Upgrades | Assets/Scripts/Skills/UpgradeFlow/; Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs; Assets/Scripts/UI/Level/PlayerLevelPresenter.cs | Offers, intervals, choice, application/visual timing |
| Modifiers | Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs; Assets/Scripts/Enemies/EnemyDropHandler.cs; Assets/Scripts/Skills/ObjectsImpactingSkills/ | Redistribution bonus, drop chance/rewards, collection |
| Space/time | Assets/Scripts/Navigation/GridSystem/; Assets/Scripts/Navigation/FlowFieldSystem/; Assets/Scripts/Player/Car/; Assets/Scripts/GameFlow/ | Placement/arrival, teleports, contact/range, pause/time scale |

## Semantics to Recheck

Verified at skill creation on 2026-10-02; verify again before using:

- LevelController evaluates a designer curve at the level and carries surplus EXP across multiple level-ups.
- WaveManager can bypass remaining delay when its count reaches zero after the first wave. Wave size converts to ushort: small starting counts can stall after truncation. Continuous exponential growth is not equivalent.
- Standard spawn batches trigger weight redistribution. Totems affect its factor rather than automatically scaling every enemy's HP/damage.
- Placement can yield fewer enemies than requested; swarm VFX can defer activation.
- Swarm warnings/spawn freeze ordinary waves. Active boss encounters suppress the next swarm countdown; this does not prove an ongoing swarm is canceled.
- Golem spawning follows required tower defeats and a delay, not a fixed elapsed-time schedule.
- ExpParticleSpawner calculates expPart but sends full exp to each particle. Positive integer dividers can multiply nominal kill rewards; report actual emitted EXP separately.
- Enemy count decreases on release; a dead entity awaiting effects can affect wave timing. Live combat population and pooled-active count may differ.

## Extraction Policy

Read serialized files only to resolve values/references. Include active scene configuration and prefab overrides. Field initializers indicate defaults/fallbacks, not current authored values.

Use existing editor exports for evaluated curves/stats if available; otherwise state missing inputs or use labeled scenarios. Creating an exporter is separate work, and no editor connector is mandatory.

Record values and behavior separately. If docs disagree with code, forecast current implementation and report the discrepancy; do not fix it during analysis-only work.
