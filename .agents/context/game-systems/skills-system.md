# Skills System Documentation

## Purpose

The Skills system owns player skill registration, skill initialization, upgradeable skill stats, and the runtime behavior of the current concrete player skills.

It is responsible for:

- Discovering child skill components under the player skill registry.
- Initializing locked skills and tracking how many skills remain uninitialized.
- Queuing skill unlock or stat-upgrade requests through the skill upgrade flow service.
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
  - `Assets/Scripts/Skills/UpgradeFlow/`
  - `Assets/Scripts/Skills/PlayerSkills/`
  - `Assets/ScriptableObjects/Skills/`
- Related code:
  - `Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs`
  - `Assets/Scripts/UI/Skills/SkillUpgradeButton.cs`
  - `Assets/Scripts/UI/Skills/SkillsVisualPresenter.cs`
  - `Assets/Scripts/Player/PlayerManager.cs`
  - `Assets/Scripts/Stats/UpgradeableStat.cs`
  - `Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs`
  - `Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs`
  - `Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs`
- Related docs:
  - `.agents/context/game-systems/collectibles-system.md`
  - `.agents/context/game-systems/level-system.md`
  - `.agents/context/project-coding-standards.md`
  - `.agents/context/ai-game-dev-best-practices.md`
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
  - `SkillsRegistry` discovers direct child components implementing `ISkillBase` in `Awake`, caches them in `Skills`, counts uninitialized skills as an `int` in `Start`, and initializes `Skills[0]` as the starting skill.
  - `RandomUninitializedSkillsInitializator` picks and initializes a random uninitialized skill through the registry.
  - `RandomUpgradeableSkillFinder` filters to `IUpgradeableSkill`, then picks a random candidate with at least one upgradeable stat still available. Its registry overload only considers initialized skills; `SkillUpgradeFlow` can pass a broader candidate set.
  - `SkillUpgradeFlow` owns skill reward queueing. It has separate APIs for new-skill rewards and stat-upgrade rewards, prevents the same locked skill from being queued twice for initialization, initializes queued new skills when dequeued, and builds up to three randomized upgrade options.
  - `SkillUpgradeRequest` and `SkillUpgradeOption` carry the UI-facing request type, target skill, button text, and upgrade action.
  - `SkillUpgradeableStatsConfig` uses reflection over public instance properties assignable to `IUpgradeableStat` to expose upgrade choices.
  - Current concrete upgrade configs use `IntUpgradeableStat` for discrete damage, piercing, saw-count, turret-count, and landmine damage values, and `FloatUpgradeableStat` for cooldown, size, range, speed, radius, and knockback values.
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
  - `DefaultGameplaySceneInstaller` binds `PlayerManager` as `IPlayerManager`, `SkillsVisualPresenter` as `ISkillsVisualPresenter`, `SkillUpgradeFlow` as `ISkillUpgradeFlow`, and `CollectibleItemsSpawner` as `IOnRandomGridPosSpawner<CollectibleItemsSpawner>`.
  - `PlayerManager.Awake` gets the child `ISkillsRegistry`, making it available through `IPlayerManager.SkillsRegistry`.
  - `SkillsRegistry.Awake` registers all direct child skills implementing `ISkillBase`.
  - `SkillsRegistry.Start` counts uninitialized skills and initializes the first registered skill as the starting skill.
  - `SkillUpgradePresenter` listens to `IPlayerLevelPresenter.OnExpSliderVisualEndValueReached` and collectible spawner `OnSpawnedEntityReleased`.
  - Skill crates queue random stat-upgrade requests. Level rewards queue a random new-skill request every `_newSkillLevelInterval` levels after level 1 when locked skills remain; if no new skill is queued, the presenter queues a random stat-upgrade request instead.
  - While showing UI, the presenter hides all skill visuals, displays either a new-skill section or stat-upgrade buttons, and advances through queued requests as the player continues or selects an option.
  - Upgrade buttons call `IUpgradeableStat.Upgrade`, which updates the copied runtime stat value and raises `OnUpgrade`; skill configs and skill components subscribe to those events to update derived configs or activate more child items.

## Rules and Invariants

- Critical behavior rules:
  - Skills must be registered as direct children of the `SkillsRegistry` transform if they should be discovered by the current registry.
  - A skill without a valid config logs a warning and remains uninitialized.
  - Upgradeable stats intended for UI must be public properties on the skill config and implement `IUpgradeableStat`.
  - Runtime upgrade state should use deep-copied stats from `OnEnable`, not mutate serialized starting stat objects directly.
  - Skill visuals shown by `SkillsVisualPresenter` are matched by GameObject name against `SkillInfoSO.Name`.
  - Skill unlock and upgrade UI is driven by level presenter and skill-crate release events, not by skill components directly.
  - Upgrade UI selection and queueing belong in `ISkillUpgradeFlow`; UI rendering and hotkey handling belong in `SkillUpgradePresenter`.
- Ordering or sequencing guarantees:
  - Registry discovery happens in `Awake`; initial skill activation and uninitialized count setup happen in `Start`.
  - New skills are queued without initialization, tracked in `_skillsQueuedForInitialization`, then `SkillUpgradeFlow.TryGetNextRequest` removes the queued marker and initializes the skill immediately before returning a new-skill request to the presenter.
  - Upgrade requests can target initialized skills or skills already queued for initialization, but cannot target locked skills that are not already queued.
  - Queued upgrade requests are rechecked with `CanBeUgraded()` when dequeued, so stale maxed-out upgrade requests are skipped.
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
  - Add new unlock or upgrade triggers through `ISkillUpgradeFlow` or a narrow event consumed by `SkillUpgradePresenter`, rather than making skills own UI presentation.
- Required dependencies and contracts:
  - New upgrade configs should inherit `SkillUpgradeableStatsConfig` if their stats should appear in the current UI.
  - New upgrade flow behavior should stay behind `ISkillUpgradeFlow` unless another system explicitly needs a different contract.
  - New child skill items must correctly implement `Initialize(config)` and `IsInitialized()`, because activators use `IsInitialized()` to avoid double activation.
  - New projectile-based skills should use the existing projectile config and pooling patterns unless a different lifecycle is intentionally reviewed.
  - New UI skill visuals must have names matching `SkillInfoSO.Name`.
- Testing implications:
  - Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, validate initial skill activation, skill-crate unlock flow, level-up upgrade flow, upgrade button clicks/hotkeys, continue input, and visual matching by skill name.
  - For a new skill, test first initialization, repeated upgrade choices, max-stat behavior, scene reload/runtime reset behavior, audio/VFX references, and enemy interaction layers.
  - For physics skills, validate enemy layer filtering, terrain obstruction checks, collider trigger setup, and knockback/stun interactions.

## Integration Notes

- Upstream dependencies:
  - Reflex injects `IPlayerManager`, `IPlayerLevelPresenter`, `IOnRandomGridPosSpawner<CollectibleItemsSpawner>`, and `IGridManager` into skill-adjacent components.
  - `PlayerManager` provides the registry to UI through `IPlayerManager.SkillsRegistry`.
  - `SkillUpgradeFlow` depends on `ISkillsRegistry`, `RandomUpgradeableSkillFinder`, queued reward request state, and `SkillUpgradeableStatsConfig` to choose and construct requests.
  - `UpgradeableStat<T>` owns upgrade range, current value, max detection, subtract-mode behavior, unit display, and `OnUpgrade` events.
  - `DeepCopyUtility` protects ScriptableObject-authored starting values from direct runtime mutation.
  - Unity layers in `EntityLayers` and `TerrainLayers` gate collision, target acquisition, and landmine placement.
- Downstream consumers:
  - `SkillUpgradePresenter` consumes `ISkillUpgradeFlow`, `SkillUpgradeRequest`, `SkillUpgradeOption`, `ISkillsRegistry`, `ISkillBase`, `IUpgradeableSkill`, `SkillInfoSO`, and `IUpgradeableStat`.
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
  - `SkillUpgradeFlow.QueueRandomSkillUpgradeRequest` does nothing when no candidate upgradeable skill is found, so reward triggers can silently produce no visible section if every available stat is maxed or no upgradeable skills are eligible.
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
  - Add guardrails in `SkillsRegistry` for empty registries before `Skills[0]` is initialized.
  - Fix the landmine initialization flag and remove or wire the unused cooldown field in a focused behavior-preserving cleanup.
  - Review `SkillUpgradeFlow` queueing for repeated upgrade requests against the same skill and stale requests after stats reach max values.
  - Consider a dedicated skill progression service if more systems need to trigger, observe, save, or restore skill state.
