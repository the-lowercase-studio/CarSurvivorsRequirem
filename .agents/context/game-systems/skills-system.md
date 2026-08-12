# Skills System Documentation

## Purpose

The Skills system owns player skill registration, skill initialization, upgradeable skill stats, and the runtime behavior of the current concrete player skills.

It is responsible for:
- Discovering child skill components under the player skill registry.
- Initializing starting skills, resetting runtime stat configs, and tracking uninitialized skill count.
- Queuing skill unlock or stat-upgrade requests through the skill upgrade flow service.
- Exposing upgradeable skill stats and stat icons to the skill upgrade UI.
- Applying stat upgrades through Assets/Scripts/Stats/UpgradeableStat.cs.
- Running concrete skill behavior for saw blades, minigun turrets, laser turrets, and landmines.

It is not responsible for:
- Player experience gain or level progression.
- Skill-crate grid spawning and collection lifecycle.
- Generic projectile damage, projectile lifetime, enemy health, knockback, stun, audio, or VFX implementations.
- Final balance values. Those live in ScriptableObject assets and inspector references.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Skills
  - Assets/Scripts/Skills/UpgradeFlow
  - Assets/Scripts/Skills/PlayerSkills
  - Assets/ScriptableObjects/Skills
- Related code:
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradeButton.cs
  - Assets/Scripts/UI/Skills/SkillsVisualPresenter.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Stats/UpgradeableStat.cs
  - Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md for Reflex binding changes.
  - .agents/skills/architecture-review/SKILL.md for ownership, event, and dependency review.
  - .agents/skills/check-optimalization/SKILL.md for projectile, physics query, pooling, and coroutine performance review.

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/Skills/ISkillBase.cs is the common player skill contract. It extends `IInitializable` and exposes Assets/ScriptableObjects/Skills/SkillInfoSO.cs.
  - Assets/Scripts/Skills/UpgradeableSkill.cs extends Assets/Scripts/Skills/ISkillBase.cs and exposes `CanBeUgraded()` plus an `ISkillUpgradeableStatsConfig` config.
  - Assets/Scripts/Skills/UpgradeableSkill.cs is the base `MonoBehaviour` for skills backed by a ScriptableObject upgrade config. It activates the skill GameObject during initialization when config exists.
  - Assets/Scripts/Skills/SkillsRegistry.cs discovers direct child components implementing Assets/Scripts/Skills/ISkillBase.cs in `Awake`, caches them in `Skills`, resets upgradeable skill configs and counts uninitialized skills in `Start`, and initializes `Skills[0]` as the starting skill.
  - Assets/Scripts/Skills/RandomUninitializedSkillsInitializator.cs picks and initializes a random uninitialized skill through the registry.
  - Assets/Scripts/Skills/RandomUpgradeableSkillFinder.cs filters to Assets/Scripts/Skills/UpgradeableSkill.cs, then picks a random candidate with at least one upgradeable stat still available. Its registry overload only considers initialized skills; Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs can pass a broader candidate set (initialized or queued for initialization).
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs owns skill reward queueing. It has separate APIs for new-skill rewards and stat-upgrade rewards, prevents the same locked skill from being queued twice for initialization, initializes queued new skills when dequeued, builds up to three randomized upgrade options, and falls back to `QueueRandomNewSkillRequest` if no upgrade candidate is found.
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeRequest.cs and Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeOption.cs carry the UI-facing request type, target skill, text, upgrade action, rarity, and optional stat icon (`Sprite`).
  - Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs uses reflection over public instance properties assignable to Assets/Scripts/Stats/UpgradeableStat.cs to expose upgrade choices and reads stat-level rarity overrides when `OverrideDefaultRarity` is enabled.
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeRarityCalculator.cs classifies non-overridden upgrade rolls as `Common`, `Rare`, or `UltraRare` from the rolled value's normalized position in the stat upgrade range.
  - Current concrete upgrade configs use Assets/Scripts/Stats/IntUpgradeableStat.cs for discrete damage, piercing, saw-count, turret-count, lasergun target-count, and landmine damage values, and Assets/Scripts/Stats/FloatUpgradeableStat.cs for cooldown, size, range, speed, radius, and knockback values.
  - Assets/ScriptableObjects/Skills/SkillInfoSO.cs provides UI name and description text for unlock and upgrade presentation.
- Concrete player skills:
  - Assets/Scripts/Skills/PlayerSkills/Saw/SawSkill.cs initializes saw blades from its serialized blade list. The first blade is initialized immediately; upgrading `NuberOfSaws` initializes more random inactive blades.
  - Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs damages enemies on trigger contact, applies knockback scaled by player movement speed, stuns enemies for the knockback duration, and plays attack audio.
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunSkill.cs initializes random minigun turrets, initializes more turrets when `NumberOfTurrets` upgrades, and runs a coroutine that repeatedly tells initialized turrets to shoot.
  - Assets/Scripts/Skills/PlayerSkills/Minigun/MinigunTurret.cs rotates its visual with DOTween, pools `Projectile` instances, initializes projectiles from `TurretConfigSO.ProjectileStatsSO`, plays muzzle VFX and shoot audio, and releases projectiles on life-end events.
  - Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunSkill.cs initializes laser turrets, initializes more turrets when `NumberOfTurrets` upgrades, applies `NumberOfTargets` to each initialized turret, and invokes repeated shooting based on `DelayBetweenShoots`.
  - Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs acquires the closest visible enemy targets within range up to its configured target count, rotates toward the closest primary target, plays charge VFX, fires one line-renderer beam per captured hit target, and applies configured damage once to each captured target.
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/LandmineSkill.cs periodically spawns landmine instances at the player position when a downward raycast confirms ground below.
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs explodes on enemy trigger contact, applies area damage, knockback, stun, explosion VFX, and destroys itself after death VFX completes.
- Runtime flow:
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds `PlayerManager` as `IPlayerManager`, `SkillsVisualPresenter` as `ISkillsVisualPresenter`, `SkillUpgradeFlow` as `ISkillUpgradeFlow`, and `CollectibleItemsSpawner` as `IOnRandomGridPosSpawner<CollectibleItemsSpawner>`.
  - `PlayerManager.Awake` gets the child `ISkillsRegistry`, making it available through `IPlayerManager.SkillsRegistry`.
  - `SkillsRegistry.Awake` registers all direct child skills implementing Assets/Scripts/Skills/ISkillBase.cs.
  - `SkillsRegistry.Start` calls `ResetUpgradeableSkillConfigs()`, counts uninitialized skills, and initializes the first registered skill (`Skills[0]`) as the starting skill.
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs listens to `IPlayerLevelPresenter.OnExpSliderVisualEndValueReached`, `ICollectibleDropNotifier.OnSkillUpgradeCollectibleCollected`, and `ISkillUpgradeFlow.OnRequestQueued`.
  - Skill crates queue random stat-upgrade requests. Level rewards queue a random new-skill request every `_newSkillLevelInterval` levels after level 1 when locked skills remain; if no new skill is queued, the presenter queues a random stat-upgrade request instead.
  - While showing UI, the presenter hides all skill visuals, displays either a new-skill section (supporting 'F' key continue) or stat-upgrade buttons (supporting keyboard 1-3 / numpad 1-3 hotkeys), and advances through queued requests as the player continues or selects an option.
  - Upgrade option creation rolls the upgrade value, uses the stat rarity override when present, otherwise calculates rarity from the upgrade range, attaches the stat icon (`Sprite`), then passes option data through to the generated `SkillUpgradeButton`.
  - Upgrade buttons call `IUpgradeableStat.Upgrade`, which updates the stat value and raises `OnUpgrade`; skill configs and skill components subscribe to those events to update derived configs or activate more child items.

## Rules and Invariants

- Critical behavior rules:
  - Skills must be registered as direct children of the Assets/Scripts/Skills/SkillsRegistry.cs transform if they should be discovered by the current registry.
  - A skill without a valid config logs a warning and remains uninitialized.
  - Upgradeable stats intended for UI must be public properties on the skill config and implement Assets/Scripts/Stats/UpgradeableStat.cs.
  - Runtime upgrade state uses deep-copied stats reset by `ResetRuntimeState()` in `SkillsRegistry.Start()` and config `OnEnable`, preventing permanent mutation of ScriptableObject starting values.
  - Skill visuals shown by `SkillsVisualPresenter` are matched by GameObject name against Assets/ScriptableObjects/Skills/SkillInfoSO.cs.Name.
  - Skill unlock and upgrade UI is driven by level presenter and skill-crate release events, not by skill components directly.
  - Upgrade UI selection and queueing belong in Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs; UI rendering, button hotkeys, and audio belong in Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs.
  - Rarity classification must stay presentation-only. Do not make gameplay strength depend on the visual rarity label.
  - Integer upgrade rarity accounts for Unity's exclusive integer `Random.Range` upper bound; `_alwaysUseMinValueForUpgrade` stats classify from the configured upgrade range minimum.
- Ordering or sequencing guarantees:
  - Registry discovery happens in `Awake`; config runtime state resetting, initial skill activation, and uninitialized count setup happen in `Start`.
  - New skills are queued without initialization, tracked in `_skillsQueuedForInitialization`, then `SkillUpgradeFlow.TryGetNextRequest` removes the queued marker and initializes the skill immediately before returning a new-skill request to the presenter.
  - Upgrade requests can target initialized skills or skills already queued for initialization, but cannot target locked skills that are not already queued.
  - Queued upgrade requests are rechecked with `CanBeUgraded()` when dequeued, so stale maxed-out upgrade requests are skipped.
  - Skill stat upgrade events fire synchronously from `IUpgradeableStat.Upgrade`.
  - Turret and child-item activation is event-driven from stat upgrades such as `NumberOfTurrets` and `NuberOfSaws`.
  - Lasergun target-count changes are event-driven from `NumberOfTargets.OnUpgrade`; existing initialized turrets receive the new target capacity, and newly initialized turrets receive the current capacity after activation.
- Constraints contributors must preserve:
  - Preserve inspector-assigned skill info, config, child turret/blade lists, VFX, audio, projectile, and parent references.
  - Treat changes to skill stat ranges, cooldowns, damage, range, projectile stats, target selection, spawn cadence, and activation counts as player-facing balance changes.
  - Preserve lasergun target selection as closest visible enemies in range unless a design change explicitly approves different targeting.
  - Preserve one lasergun shoot sound per turret shot, even when multiple targets are hit.
  - Keep DI-facing dependencies explicit where already established. For example, Assets/Scripts/Skills/PlayerSkills/Saw/SawBlade.cs depends on `IPlayerManager`, and skill UI receives player and collectible dependencies through Reflex.
  - Avoid replacing registry, UI, or turret dependencies with singleton access or broad scene searches.
  - Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly unless the user explicitly asks and the text change is safe to review.

## Extension Points

- Safe extension areas:
  - Add a new player skill by implementing Assets/Scripts/Skills/ISkillBase.cs or deriving from Assets/Scripts/Skills/UpgradeableSkill.cs, assigning Assets/ScriptableObjects/Skills/SkillInfoSO.cs and config references, and placing the component under Assets/Scripts/Skills/SkillsRegistry.cs.
  - Add upgradeable stats by adding serialized starting stat fields, deep-copying them in config `ResetRuntimeState()`, and exposing public Assets/Scripts/Stats/UpgradeableStat.cs properties.
  - Mark finite or high-impact stats with `OverrideDefaultRarity` and `Rarity` on the stat itself when random range position is not the desired rarity signal.
  - Add child activatable items by using Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs when each child implements `IInitializableWithScriptableConfig`.
  - Add new unlock or upgrade triggers through Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs or a narrow event consumed by Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs, rather than making skills own UI presentation.
- Required dependencies and contracts:
  - New upgrade configs should inherit Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs if their stats should appear in the current UI.
  - New upgrade flow behavior should stay behind Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs unless another system explicitly needs a different contract.
  - New child skill items must correctly implement `Initialize(config)` and `IsInitialized()`, because activators use `IsInitialized()` to avoid double activation.
  - New projectile-based skills should use the existing projectile config and pooling patterns unless a different lifecycle is intentionally reviewed.
  - New UI skill visuals must have names matching Assets/ScriptableObjects/Skills/SkillInfoSO.cs.Name.
- Testing implications:
  - Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, validate initial skill activation, skill-crate unlock flow, level-up upgrade flow, upgrade button clicks/hotkeys (1-3, numpad 1-3, F key), continue input, and visual matching by skill name.
  - For a new skill, test first initialization, repeated upgrade choices, max-stat behavior, rarity labeling/backgrounds, scene reload/runtime reset behavior, audio/VFX references, and enemy interaction layers.
  - For physics skills, validate enemy layer filtering, terrain obstruction checks, collider trigger setup, and knockback/stun interactions.
  - For lasergun target-count changes, validate `NumberOfTargets = 1` against the previous single-target behavior, values `2..5` against closest-visible-enemy selection, terrain occlusion, one beam per hit target, beam cleanup after the laser effect, and multiple turret shots stacking as separate hits.

## Integration Notes

- Upstream dependencies:
  - Reflex injects `IPlayerManager`, `IPlayerLevelPresenter`, `ICollectibleDropNotifier`, `ISkillUpgradeFlow`, and `ISkillsVisualPresenter` into `SkillUpgradePresenter`.
  - `PlayerManager` provides the registry to UI through `IPlayerManager.SkillsRegistry`.
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs depends on Assets/Scripts/Skills/SkillsRegistry.cs, Assets/Scripts/Skills/RandomUpgradeableSkillFinder.cs, queued reward request state, and Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs to choose and construct requests.
  - `UpgradeableStat<T>` owns upgrade range, current value, max detection, subtract-mode behavior, unlimited max behavior, unit display, icon (`Sprite`), rarity override metadata, and `OnUpgrade` events.
  - Assets/Scripts/Utils/DeepCopyUtility.cs protects ScriptableObject-authored starting values from direct runtime mutation.
  - Unity layers in `EntityLayers` and `TerrainLayers` gate collision, target acquisition, and landmine placement.
  - Lasergun target selection uses non-allocating physics overlap buffers, enemy layer filtering, and terrain line-of-sight checks before targets are accepted.
- Downstream consumers:
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs consumes Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs, Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeRequest.cs, Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeOption.cs, Assets/Scripts/Skills/SkillsRegistry.cs, Assets/Scripts/Skills/ISkillBase.cs, Assets/Scripts/Skills/UpgradeableSkill.cs, and Assets/ScriptableObjects/Skills/SkillInfoSO.cs.
  - Assets/Scripts/UI/Skills/SkillsVisualPresenter.cs consumes Assets/ScriptableObjects/Skills/SkillInfoSO.cs.Name for visual lookup.
  - Projectile, health, status, audio, VFX, grid, collectible, and level systems react to skill behavior but do not own skill progression state.
- Cross-system coupling risks:
  - Skill progression is coupled to collectible release and level UI events.
  - Skill UI assumes upgradeable stat property names can be converted from PascalCase to words for button text.
  - Skill UI rarity depends on stat upgrade ranges and optional stat-level rarity overrides; bad range metadata or missing overrides can make high-impact upgrades look too common.
  - Concrete skills rely heavily on serialized scene/prefab references, so code-only changes may compile while scene wiring remains broken.
  - Assets/Scripts/Skills/Turret.cs searches for the "ProjectilesHolder" tag, which is a scene convention outside the type contract.

## Known Risks and Open Questions

- Known limitations:
  - `IUpgradeableSkill.CanBeUgraded` and `SawSkillUpgradeableConfigSO.NuberOfSaws` contain spelling errors that are now part of the code contract.
  - `SkillsRegistry.Start` initializes `Skills[0]`, so starting skill depends on direct child order and fails if no skills are registered.
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/Landmine.cs.Initialize sets config and scale but does not set `_isInitialized = true`, so `Landmine.IsInitialized()` remains false after initialization.
  - Assets/Scripts/Skills/PlayerSkills/LandmineTrap/LandmineSkill.cs has a serialized `_cooldown` field that is not used; active spawn cadence comes from `_config.SpawnCooldown.Value`.
  - Assets/Scripts/Skills/PlayerSkills/Lasergun/LasergunTurret.cs creates additional line-renderer instances at runtime when `NumberOfTargets` grows above one; those clones depend on the serialized first `LineRenderer` being a valid beam template.
  - Some current skill code uses direct scene/tag lookup patterns that do not match the preferred DI direction for new code.
- Open design questions:
  - Should starting skill be explicitly configured instead of relying on registry child order?
  - Should skill unlock and upgrade selection be deterministic, weighted, or player-choice based rather than random?
  - Should generic collectible spawning remain under Assets/Scripts/Skills/ObjectsImpactingSkills/Crate as more pickups are added?
  - Should skill configs be reset per run through a central run-state service instead of ScriptableObject `ResetRuntimeState()` copies?
- Suggested follow-up tasks:
  - Add guardrails in Assets/Scripts/Skills/SkillsRegistry.cs for empty registries before `Skills[0]` is initialized.
  - Fix the landmine initialization flag (`_isInitialized = true`) and remove or wire the unused cooldown field in a focused behavior-preserving cleanup.
  - Review Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs queueing for repeated upgrade requests against the same skill and stale requests after stats reach max values.
  - Consider a dedicated skill progression service if more systems need to trigger, observe, save, or restore skill state.
