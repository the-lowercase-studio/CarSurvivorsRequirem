# Skills System Documentation

## Purpose

The Skills system owns player skill registration, skill initialization, upgradeable skill stats, and the runtime behavior of the current concrete player skills.

It is responsible for:

- Discovering child skill components under the player skill registry.
- Initializing locked skills and tracking how many skills remain uninitialized.
- Exposing upgradeable skill stats to the skill upgrade UI.
- Applying stat upgrades through `IUpgradeableStat`.
- Running concrete skill behavior for saw blades, minigun turrets, laser turrets, and landmines.

It is not responsible for:

- Player experience gain or level progression.
- Skill-crate grid spawning and collection lifecycle.
- Generic projectile damage, projectile lifetime, enemy health, knockback, stun, audio, or VFX implementations.
- Final balance values. Those live in ScriptableObject assets and inspector references.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Skills/`
  - `Assets/Scripts/Skills/PlayerSkills/`
  - `Assets/ScriptableObjects/Skills/`
- Related code:
  - `Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs`
  - `Assets/Scripts/UI/Skills/SkillsVisualPresenter.cs`
  - `Assets/Scripts/Player/PlayerManager.cs`
  - `Assets/Scripts/Stats/UpgradeableStat.cs`
  - `Assets/Scripts/Activators/ItemsWithScriptableConfigsActivator.cs`
  - `Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs`
  - `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`
- Related docs:
  - `.agents/docs/collectibles-system.md`
  - `.agents/docs/level-system.md`
  - `.agents/docs/project-coding-standards.md`
  - `.agents/docs/ai-game-dev-best-practices.md`
- Related agents or instructions:
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/di-integration/SKILL.md` for Reflex binding changes.
  - `.agents/skills/architecture-review/SKILL.md` for ownership, event, and dependency review.
  - `.agents/skills/check-optimalization/SKILL.md` for projectile, physics query, pooling, and coroutine performance review.

## Architecture and Data Flow

- Core components:
  - `ISkillBase` is the common player skill contract. It extends `IInitializable` and exposes `SkillInfoSO`.
  - `IUpgradeableSkill` extends `ISkillBase` and exposes `CanBeUgraded()` plus an `ISkillUpgradeableStatsConfig` config.
  - `UpgradeableSkill<TUpgradeableConfig>` is the base `MonoBehaviour` for skills backed by a ScriptableObject upgrade config. It activates the skill GameObject during initialization when config exists.
  - `SkillsRegistry` discovers child components implementing `ISkillBase` in `Awake`, caches them in `Skills`, counts uninitialized skills in `Start`, and initializes `Skills[0]` as the starting skill.
  - `RandomUninitializedSkillsInitializator` picks and initializes a random uninitialized skill through the registry.
  - `RandomUpgradeableSkillFinder` picks a random initialized skill with at least one upgradeable stat still available.
  - `SkillUpgradeableStatsConfig` uses reflection over public instance properties assignable to `IUpgradeableStat` to expose upgrade choices.
  - `SkillInfoSO` provides UI name and description text for unlock and upgrade presentation.
- Concrete player skills:
  - `SawSkill` initializes saw blades from its serialized blade list. The first blade is initialized immediately; upgrading `NuberOfSaws` initializes more random inactive blades.
  - `SawBlade` damages enemies on trigger contact, applies knockback scaled by player movement speed, stuns enemies for the knockback duration, and plays attack audio.
  - `MinigunSkill` initializes random minigun turrets, initializes more turrets when `NumberOfTurrets` upgrades, and runs a coroutine that repeatedly tells initialized turrets to shoot.
  - `MinigunTurret` rotates its visual with DOTween, pools `Projectile` instances, initializes projectiles from `TurretConfigSO.ProjectileStatsSO`, plays muzzle VFX and shoot audio, and releases projectiles on life-end events.
  - `LasergunSkill` initializes laser turrets, initializes more turrets when `NumberOfTurrets` upgrades, and invokes repeated shooting based on `DelayBetweenShoots`.
  - `LasergunTurret` acquires visible enemy targets within range, rotates toward the current target, plays charge VFX, fires a line-renderer beam, and applies configured damage.
  - `LandmineSkill` periodically spawns landmine instances at the player position when a downward raycast confirms ground below.
  - `Landmine` explodes on enemy trigger contact, applies area damage, knockback, stun, explosion VFX, and destroys itself after death VFX completes.
- Runtime flow:
  - `DefaultGameplaySceneInstaller` binds `PlayerManager` as `IPlayerManager` and `CollectibleItemsSpawner` as `IOnRandomGridPosSpawner<CollectibleItemsSpawner>`.
  - `PlayerManager.Awake` gets the child `ISkillsRegistry`, making it available through `IPlayerManager.SkillsRegistry`.
  - `SkillsRegistry.Awake` registers all direct child skills implementing `ISkillBase`.
  - `SkillsRegistry.Start` counts uninitialized skills and initializes the first registered skill as the starting skill.
  - `SkillUpgradePresenter` listens to `IPlayerLevelPresenter.OnExpSliderVisualEndValueReached` and collectible spawner `OnSpawnedEntityReleased`.
  - When triggered, `SkillUpgradePresenter` queues either a random uninitialized skill or a random upgradeable skill.
  - While showing UI, the presenter pauses game time, shows either a new-skill section or stat-upgrade buttons, and resumes game time when moving to the next queued item.
  - Upgrade buttons call `IUpgradeableStat.Upgrade`, which updates the copied runtime stat value and raises `OnUpgrade`; skill configs and skill components subscribe to those events to update derived configs or activate more child items.

## Rules and Invariants

- Critical behavior rules:
  - Skills must be registered as direct children of the `SkillsRegistry` transform if they should be discovered by the current registry.
  - A skill without a valid config logs a warning and remains uninitialized.
  - Upgradeable stats intended for UI must be public properties on the skill config and implement `IUpgradeableStat`.
  - Runtime upgrade state should use deep-copied stats from `OnEnable`, not mutate serialized starting stat objects directly.
  - Skill visuals shown by `SkillsVisualPresenter` are matched by GameObject name against `SkillInfoSO.Name`.
  - Skill unlock and upgrade UI is driven by level presenter and skill-crate release events, not by skill components directly.
- Ordering or sequencing guarantees:
  - Registry discovery happens in `Awake`; initial skill activation and uninitialized count setup happen in `Start`.
  - New skill initialization is queued before the UI is shown, then `SkillUpgradePresenter.HandleUpgradeableOrInitializableSkillsShowing` calls `InitializeSkill` again before displaying the new-skill section. The second call is currently a no-op because initialized skills return `null` from the registry path.
  - Skill stat upgrade events fire synchronously from `IUpgradeableStat.Upgrade`.
  - Turret and child-item activation is event-driven from stat upgrades such as `NumberOfTurrets` and `NuberOfSaws`.
- Constraints contributors must preserve:
  - Preserve inspector-assigned skill info, config, child turret/blade lists, VFX, audio, projectile, and parent references.
  - Treat changes to skill stat ranges, cooldowns, damage, range, projectile stats, target selection, spawn cadence, and activation counts as player-facing balance changes.
  - Keep DI-facing dependencies explicit where already established. For example, `SawBlade` depends on `IPlayerManager`, and skill UI receives player and collectible dependencies through Reflex.
  - Avoid replacing registry, UI, or turret dependencies with singleton access or broad scene searches.
  - Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly unless the user explicitly asks and the text change is safe to review.

## Extension Points

- Safe extension areas:
  - Add a new player skill by implementing `ISkillBase` or deriving from `UpgradeableSkill<TConfig>`, assigning `SkillInfoSO` and config references, and placing the component under `SkillsRegistry`.
  - Add upgradeable stats by adding serialized starting stat fields, deep-copying them in the config `OnEnable`, and exposing public `IUpgradeableStat` properties.
  - Add child activatable items by using `ItemsWithScriptableConfigsActivator<TItem, TConfig>` when each child implements `IInitializableWithScriptableConfig<TConfig>`.
  - Add new unlock or upgrade triggers by queuing through `SkillUpgradePresenter` or a narrow event/contract rather than making skills own UI presentation.
- Required dependencies and contracts:
  - New upgrade configs should inherit `SkillUpgradeableStatsConfig` if their stats should appear in the current UI.
  - New child skill items must correctly implement `Initialize(config)` and `IsInitialized()`, because activators use `IsInitialized()` to avoid double activation.
  - New projectile-based skills should use the existing projectile config and pooling patterns unless a different lifecycle is intentionally reviewed.
  - New UI skill visuals must have names matching `SkillInfoSO.Name`.
- Testing implications:
  - Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, validate initial skill activation, skill-crate unlock flow, level-up upgrade flow, game pause/resume around the skill UI, and visual matching by skill name.
  - For a new skill, test first initialization, repeated upgrade choices, max-stat behavior, scene reload/runtime reset behavior, audio/VFX references, and enemy interaction layers.
  - For physics skills, validate enemy layer filtering, terrain obstruction checks, collider trigger setup, and knockback/stun interactions.

## Integration Notes

- Upstream dependencies:
  - Reflex injects `IPlayerManager`, `IPlayerLevelPresenter`, `IOnRandomGridPosSpawner<CollectibleItemsSpawner>`, and `IGridManager` into skill-adjacent components.
  - `PlayerManager` provides the registry to UI through `IPlayerManager.SkillsRegistry`.
  - `UpgradeableStat<T>` owns upgrade range, current value, max detection, subtract-mode behavior, unit display, and `OnUpgrade` events.
  - `DeepCopyUtility` protects ScriptableObject-authored starting values from direct runtime mutation.
  - Unity layers in `EntityLayers` and `TerrainLayers` gate collision, target acquisition, and landmine placement.
- Downstream consumers:
  - `SkillUpgradePresenter` consumes `ISkillsRegistry`, `ISkillBase`, `IUpgradeableSkill`, `ISkillUpgradeableStatsConfig`, `SkillInfoSO`, and `IUpgradeableStat`.
  - `SkillsVisualPresenter` consumes `SkillInfoSO.Name` for visual lookup.
  - Projectile, health, status, audio, VFX, grid, collectible, and level systems react to skill behavior but do not own skill progression state.
- Cross-system coupling risks:
  - Skill progression is coupled to collectible release and level UI events.
  - Skill UI assumes upgradeable stat property names can be converted from PascalCase to words for button text.
  - Concrete skills rely heavily on serialized scene/prefab references, so code-only changes may compile while scene wiring remains broken.
  - `Turret<TConfig>.Awake` searches for the `ProjectilesHolder` tag, which is a scene convention outside the type contract.

## Known Risks and Open Questions

- Known limitations:
  - `IUpgradeableSkill.CanBeUgraded` and `SawSkillUpgradeableConfigSO.NuberOfSaws` contain spelling errors that are now part of the code contract.
  - `SkillsRegistry.Start` initializes `Skills[0]`, so starting skill depends on direct child order and fails if no skills are registered.
  - `RandomUpgradeableSkillFinder` casts skills to `IUpgradeableSkill` and then calls `CanBeUgraded()` without filtering nulls, so non-upgradeable registered skills would throw.
  - `SkillUpgradePresenter` calls `RandomUpgradeableSkillFinder.Find` twice in the upgrade branch, which can enqueue a different skill from the one checked for null.
  - `Landmine.Initialize` does not set `_isInitialized`, so `Landmine.IsInitialized()` remains false after initialization.
  - `LandmineSkill` has a serialized `_cooldown` field that is not used; active spawn cadence comes from `_config.SpawnCooldown.Value`.
  - `LasergunSkill` iterates all serialized turrets when shooting, so uninitialized inactive turrets are asked to shoot unless their inactive GameObjects prevent invocation side effects in the current setup.
  - Some current skill code uses direct scene/tag lookup patterns that do not match the preferred DI direction for new code.
- Open design questions:
  - Should starting skill be explicitly configured instead of relying on registry child order?
  - Should skill unlock and upgrade selection be deterministic, weighted, or player-choice based rather than random?
  - Should generic collectible spawning remain under `Skills/ObjectsImpactingSkills/Crate` as more pickups are added?
  - Should skill configs be reset per run through a central run-state service instead of relying on ScriptableObject `OnEnable` cloning?
- Suggested follow-up tasks:
  - Add guardrails in `SkillsRegistry` and `RandomUpgradeableSkillFinder` for empty registries and non-upgradeable skills.
  - Fix the landmine initialization flag and remove or wire the unused cooldown field in a focused behavior-preserving cleanup.
  - Review `SkillUpgradePresenter` queueing for duplicate initialization and double random upgrade selection.
  - Consider a dedicated skill progression service if more systems need to trigger, observe, save, or restore skill state.
