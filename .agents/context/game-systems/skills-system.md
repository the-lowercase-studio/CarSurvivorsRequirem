# Skills System Documentation

## Purpose

The Skills system owns player skill registration, discovery, initialization, upgradeable stat tracking, selection flow, HUD presentation, and the runtime combat behavior of concrete player skills.

It is responsible for:
- Discovering direct child skill components under the player skill registry on the car hierarchy.
- Enforcing the active skills limit of 3 skills (SkillConstants.MAX_ACTIVE_SKILLS = 3).
- Initializing the starting skill (direct child index 0) and deep-copying runtime stat configurations so ScriptableObject source assets remain unmutated.
- Tracking uninitialized and initialized skill counts and notifying listeners when a skill is initialized via OnSkillInitialized.
- Queuing reward requests via ISkillUpgradeFlow using lightweight tokens and lazy Just-In-Time (JIT) evaluation upon dequeue, with pending choice reservation to prevent over-allocation.
- Presenting dual-card new skill choices (SkillConstants.NEW_SKILL_CHOICE_COUNT = 2) with side-by-side preview cards and 1-2 hotkeys.
- Rendering dual-station 3D skill preview models with context-aware camera backgrounds (SkillVisualContext.StatUpgrade vs SkillVisualContext.NewSkillUnlocked).
- Presenting up to 3 randomized stat upgrade options (SkillConstants.MAX_SKILL_UPGRADE_OPTIONS = 3) with stat icons, rarity tiers, and 1-3 hotkeys.
- Displaying active player skills in the HUD (PlayerSkillsHUDPresenter) across 3 persistent slots with punch-scale acquisition animations.
- Applying runtime stat upgrades via UpgradeableStat<T> with in-memory cloning, unlimited max value support, and inclusive integer roll ranges.
- Running concrete skill behaviors for Saw Blades, Minigun Turrets, Lasergun Turrets, and Landmines.
- Executing deterministic melee saw physics with per-enemy hit cooldowns, steering/drift visual spin modulation, and bounded forward knockback impulses.

It is not responsible for:
- Player experience gain, level threshold math, or EXP drop collection (owned by LevelSystem and ExpParticleSpawner).
- Skill-crate spawning positions and grid placement (owned by MapInteractablesSpawner and GridManager).
- Generic projectile physics, flight, and collision handling (owned by ProjectilesSystem).
- Health reduction and damage calculation on targets (owned by HealthSystem).
- Final balance values, which reside in designer-authored ScriptableObject assets and presets.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Skills
  - Assets/Scripts/Skills/Constants/SkillConstants.cs
  - Assets/Scripts/Skills/UpgradeFlow
  - Assets/Scripts/Skills/PlayerSkills
  - Assets/Scripts/Stats
  - Assets/ScriptableObjects/Skills
- Related code:
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradeButton.cs
  - Assets/Scripts/UI/Skills/SkillsVisualPresenter.cs
  - Assets/Scripts/UI/HUD/PlayerSkillsHUDPresenter.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/Car/CarController.cs
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs
  - Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs
  - Assets/Scripts/Initializers/IInitializableWithScriptableConfig.cs
  - Assets/Scripts/Utils/DeepCopyUtility.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/projectiles-system.md
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md for Reflex DI bindings.
  - .agents/skills/architecture-review/SKILL.md for ownership, events, and boundary verification.
  - .agents/skills/check-optimalization/SKILL.md for performance, pooling, and physics queries.

## Architecture and Data Flow

- Core components:
  - SkillsRegistry: Discovers direct child components implementing ISkillBase in Awake, resets runtime config state and counts uninitialized skills in Start, initializes Skills[0] as the starting skill, and raises OnSkillInitialized whenever a skill is initialized.
  - SkillConstants: Defines global constraints and tuning values:
    - MAX_ACTIVE_SKILLS = 3
    - NEW_SKILL_CHOICE_COUNT = 2
    - MAX_SKILL_UPGRADE_OPTIONS = 3
    - RARE_THRESHOLD = 0.5f
    - ULTRA_RARE_THRESHOLD = 0.8f
    - DEFAULT_COLLISION_KNOCKBACK = 2f
    - TARGET_BUFFER_SIZE = 64
    - MIN_NUMBER_OF_TARGETS = 1
    - SMALLEST_ANGLE_QUALIFYING_AS_LOOKING_AT_TARGET = 4f
    - CAN_PLACE_MINE_RAY_DISTANCE = 5f
    - TIME_TO_ARRIVE_AT_LOCATION_MULTIPLIER = 0.2f
    - SAW_MIN_KNOCKBACK_DISTANCE = 1.2f
    - SAW_MAX_KNOCKBACK_DISTANCE = 3.5f
    - SAW_KNOCKBACK_SPEED_FACTOR = 0.5f
    - SAW_MIN_KNOCKBACK_DURATION = 0.12f
    - SAW_MAX_KNOCKBACK_DURATION = 0.22f
    - SAW_COOLDOWN_PURGE_INTERVAL = 3.0f
  - SkillUpgradeFlow: Owns reward queueing. Tracks _pendingNewSkillChoicesCount to reserve active slots while new skill popups await player input. Stores requests as lightweight QueuedSkillRewardRequest tokens. When dequeued in TryGetNextRequest, tokens evaluate candidates lazily Just-In-Time against currently initialized skills:
    - New skill requests check if InitializedSkillsCount < MAX_ACTIVE_SKILLS, pick up to 2 uninitialized candidate skills, and emit a NewSkillChoice request. If the active skill cap is reached or no uninitialized skills remain, the flow falls back to a stat upgrade request.
    - Stat upgrade requests sample eligible initialized skills dynamically via RandomUpgradeableSkillFinder. If a candidate has no upgradeable stats remaining, it is skipped cleanly.
  - SkillUpgradeRequest: A readonly struct carrying RequestType (NewSkillChoice vs UpgradeSkill), SkillChoices, target UpgradeableSkill, and UpgradeOptions.
  - SkillUpgradeOption: Carries the display string, upgrade action callback, rarity tier, and stat Sprite icon for an upgrade button.
  - SkillUpgradeableStatsConfig: Reflects over public instance properties implementing IUpgradeableStat where CanBeUpgraded is true, returning NameUpgradableStatPair records.
  - UpgradeableStat<T>, FloatUpgradeableStat, and IntUpgradeableStat: Manage stat progression. They implement ICloneable and provide a strongly-typed Clone() method. They encapsulate Value, MinMaxRange, _rangeOfPossibleValuesForUpgrade, IsSubstractModeOn, HasUnlimitedMaxValue, AlwaysUseMinValueForUpgrade, Unit, Sprite Icon, and the OnUpgrade event.
  - DeepCopyUtility: Provides fast-path in-memory cloning via ICloneable, FloatUpgradeableStat, and IntUpgradeableStat before falling back to JSON serialization. This preserves UnityEngine.Object references (such as Sprite Icon) without modifying source ScriptableObject assets.
  - SkillUpgradeRarityCalculator: Evaluates rolled values against the stat upgrade range, classifying options as Common, Rare (>= 0.5), or UltraRare (>= 0.8). If OverrideDefaultRarity is enabled on the stat, the manual Rarity override takes precedence.
  - SkillUpgradePresenter: Orchestrates popup presentation, subscribing to level-up events, skill-crate collection, and queue notifications. Maps keys 1 and 2 to dual new-skill choices and keys 1, 2, and 3 to stat upgrades, protected by frame-debounce checks (_lastHandledInputFrame == Time.frameCount).
  - SkillsVisualPresenter: Manages dual 3D preview renderers (Slot 0 primary, Slot 1 secondary), changes camera background colors by context (SkillVisualContext.StatUpgrade vs SkillVisualContext.NewSkillUnlocked), and activates matching visual models by SkillInfoSO.Name.
  - PlayerSkillsHUDPresenter: Manages 3 persistent HUD slots showing empty frames or active skill 2D icons with punch-scale animations on acquisition. Registers with ISkillsRegistry.OnSkillInitialized.

- Key interfaces:
  - ISkillBase: Foundational player skill contract extending IInitializable and exposing SkillInfoSO.
  - IUpgradeableSkill: Extends ISkillBase, exposing CanBeUpgraded() and ISkillUpgradeableStatsConfig Config.
  - ISkillsRegistry: Defines skill discovery collections, uninitialized/initialized skill counts, InitializeSkill, and the OnSkillInitialized event.
  - ISkillUpgradeFlow: Manages the reward queue, offering QueueRandomNewSkillRequest, QueueRandomSkillUpgradeRequest, TryGetNextRequest, and OnRequestQueued.
  - ISkillUpgradeableStatsConfig: Declares ResetRuntimeState() and GetUpgradeableStatsThatCanBeUpgraded().
  - IItemsWithScriptableConfigsActivator<TItem, TScriptableConfig>: Activates and tracks child components (blades, turrets) based on configured item counts.
  - IInitializableWithScriptableConfig<TScriptableConfig>: Interface for sub-skill entities requiring a typed ScriptableObject configuration.
  - ISkillUpgradeCollectible: Marker interface extending ICollectible for skill crates that trigger upgrade rewards.
  - ISkillsVisualPresenter: Coordinates preview camera contexts, background colors, and 3D visual activation.
  - IPlayerSkillsHUDPresenter: Displays active skill icons across persistent HUD slots.

- Concrete player skills:
  - SawSkill / SawBlade:
    - SawSkill initializes child SawBlade components using ItemsWithScriptableConfigsActivator. The first blade is active immediately; upgrading NuberOfSaws activates additional blades up to the configured value.
    - SawBlade rotates around the car using XYZRotationLoop. In UpdateRotationSpeedMultiplier, it queries ICarController to dynamically ramp spin speed during steering or drifting (steer intensity floor of 0.85 when drifting, smooth interpolation via _spinRampSpeed).
    - Handles both OnTriggerEnter and OnTriggerStay via unified ProcessEnemyCollision logic.
    - Employs zero-allocation per-enemy attack cooldown tracking via _lastHitTimesByCollider dictionary against _config.AttackCooldown (default 0.14f fallback), purged every 3.0 seconds (SkillConstants.SAW_COOLDOWN_PURGE_INTERVAL) for entries older than 2.0 seconds using _staleCollidersCache.
    - Applies bounded knockback: base distance equals SkillConstants.DEFAULT_COLLISION_KNOCKBACK * knockbackStat, multiplied by 1.0 + (SkillConstants.SAW_KNOCKBACK_SPEED_FACTOR * speedRatio), clamped strictly between SkillConstants.SAW_MIN_KNOCKBACK_DISTANCE (1.2m) and SkillConstants.SAW_MAX_KNOCKBACK_DISTANCE (3.5m).
    - Snappy dynamic arrival duration clamped between SkillConstants.SAW_MIN_KNOCKBACK_DURATION (0.12s) and SkillConstants.SAW_MAX_KNOCKBACK_DURATION (0.22s).
    - Knockback impulse vector is normalized strictly on the XZ plane (transform.forward with y = 0f, falling back to Vector3.forward).
  - MinigunSkill / MinigunTurret:
    - MinigunSkill initializes MinigunTurret instances up to NumberOfTurrets and runs a coroutine calling Shoot() at DelayBetweenShoots intervals.
    - MinigunTurret oscillates its visual horizontally using a mathematical cosine wave (Mathf.Cos(Time.time * Mathf.PI / _config.RotationDuration), with _inverseRotation support) to eliminate runtime tween allocations.
    - Spawns Projectile instances from an internal ObjectPool<Projectile> parented to _projectilesParent, initializes projectile stats, plays muzzle flash VFX, and triggers Shoot audio.
  - LasergunSkill / LasergunTurret:
    - LasergunSkill initializes LasergunTurret instances up to NumberOfTurrets, propagates NumberOfTargets updates event-driven to all turrets, and invokes ShootFromTurrets() at DelayBetweenShoots intervals.
    - LasergunTurret searches for enemies using Physics.OverlapSphereNonAlloc on EntityLayers.Enemies into a preallocated buffer (_targetBuffer, size 64).
    - Filters targets by line-of-sight against TerrainLayers.All and sorts candidates by distance.
    - Tracks closest targets up to NumberOfTargets, rotates toward the primary target in FixedUpdate, charges cumulating VFX (_laserCumulatingVFX) scaled by shoot frequency, renders LineRenderer beams to all hit targets, plays Shoot audio, and applies instant damage.
  - LandmineSkill / Landmine:
    - LandmineSkill periodically performs a downward raycast checking for TerrainLayers.Ground within CAN_PLACE_MINE_RAY_DISTANCE (5m), instantiating a Landmine prefab when valid ground is detected.
    - Landmine scales to Size.Value and activates. When an enemy enters its trigger, it queries an overlap sphere against EntityLayers.Enemies with QueryTriggerInteraction.Collide.
    - Plays explosion VFX via _deathVfxPlayer scaled to ExplosionRadius.Value, plays Explosion audio, applies damage, and calculates radial knockback directed away from the mine center. The mine visual is disabled immediately and the object is destroyed upon VFX completion.

- Runtime flow:
  1. Reflex DI Boot: DefaultGameplaySceneInstaller binds PlayerManager as IPlayerManager, SkillsVisualPresenter as ISkillsVisualPresenter, SkillUpgradeFlow as ISkillUpgradeFlow, PlayerSkillsHUDPresenter as IPlayerSkillsHUDPresenter, and CollectibleDropNotifier as ICollectibleDropNotifier.
  2. Registry Scan: In Awake, PlayerManager captures ISkillsRegistry. SkillsRegistry scans direct child transforms and caches all components implementing ISkillBase in Skills.
  3. Registry Start: SkillsRegistry calls ResetRuntimeState() on all upgradeable skill configs, deep-copying stats in memory. It counts uninitialized skills and calls InitializeSkill(Skills[0]), activating the starting skill and firing OnSkillInitialized.
  4. HUD Initialization: PlayerSkillsHUDPresenter initializes 3 empty slot frames and populates the first active skill icon from Skills[0].
  5. Reward Triggers:
     - Skill Crate: When a player collects a crate, CollectibleDropNotifier fires OnSkillUpgradeCollectibleCollected, triggering SkillUpgradeFlow.QueueRandomSkillUpgradeRequest.
     - Level Up: PlayerLevelPresenter fires OnExpSliderVisualEndValueReached. If the level satisfies the interval ((level - 1) % _newSkillLevelInterval == 0) and (InitializedSkillsCount + _pendingNewSkillChoicesCount < MAX_ACTIVE_SKILLS), QueueRandomNewSkillRequest is called; otherwise, QueueRandomSkillUpgradeRequest is queued.
  6. Queue Notification: SkillUpgradeFlow raises OnRequestQueued, prompting SkillUpgradePresenter.TryShowQueuedRewardSection.
  7. Lazy Request Resolution: SkillUpgradePresenter calls TryGetNextRequest:
     - If NewSkillChoice: Decrements _pendingNewSkillChoicesCount. Up to 2 uninitialized candidate skills are returned without initializing them. SkillUpgradePresenter displays the 2-card UI with dual 3D preview renderers. The player presses 1 or 2 to choose, and the chosen skill is initialized via SkillsRegistry.InitializeSkill, firing OnSkillInitialized and updating the HUD.
     - If UpgradeSkill: Candidate initialized skills are sampled dynamically. 3 upgrade options are rolled with calculated rarities and icons. The player presses 1-3 or clicks a button, invoking Option.Apply() which synchronously executes IUpgradeableStat.Upgrade.
  8. Progression Updates: IUpgradeableStat.Upgrade updates the current stat value, clamps or limits if configured, updates CanBeUpgraded, and raises OnUpgrade. Concrete skill components listen to OnUpgrade to update active item counts, line renderers, or projectile properties.

## Rules and Invariants

- Critical behavior rules:
  - Skill Discovery: Skills must be direct children of the SkillsRegistry GameObject to be discovered during Awake.
  - Active Skills Cap: The player cannot exceed SkillConstants.MAX_ACTIVE_SKILLS (3 skills). Any new skill request queued when 3 skills are active or reserved is rejected or falls back to a stat upgrade.
  - Dual Skill Choice: New skill unlocks offer exactly up to SkillConstants.NEW_SKILL_CHOICE_COUNT (2 choices), selectable via keyboard 1 or 2 (supporting Alpha and Numpad keys).
  - Just-In-Time Evaluation: Queued stat upgrade requests must not bind a target skill at enqueue time. They store a token and dynamically evaluate candidate skills upon dequeue, ensuring newly unlocked skills are immediately eligible for subsequent upgrades during multi-reward surges.
  - Pending Choices Reservation: SkillUpgradeFlow tracks _pendingNewSkillChoicesCount upon enqueueing new skill choices, preventing over-queueing beyond the 3-skill maximum during rapid multi-level surges.
  - In-Memory Deep Copy: ScriptableObject configurations must never be mutated at runtime. All stats must be deep-copied in ResetRuntimeState() using DeepCopyUtility.DeepCopy, which leverages ICloneable fast-paths.
  - Stat Icon Preservation: Sprite Icon references must survive the deep-copy process. Bypassing the ICloneable pathway into JsonUtility will drop UnityEngine.Object references and cause blank stat icons in the UI.
  - Unlimited Max Stats: Stats configured with HasUnlimitedMaxValue = true must bypass maximum value clamping and must not set CanBeUpgraded = false, preventing upgrade starvation.
  - Visual Matching: 3D preview models under SkillsVisualPresenter must have GameObject names that exactly match SkillInfoSO.Name.
  - Input Debounce: All UI hotkey handlers and click handlers must check _lastHandledInputFrame == Time.frameCount to prevent duplicate input registration across consecutive frames.
  - Presentation-Only Rarity: SkillUpgradeRarity (Common, Rare, UltraRare) is exclusively visual for card borders and labels. Gameplay effects must not scale based on the visual rarity label.
  - Zero-Allocation Saw Hit Tracking: SawBlade must track enemy hit timestamps in a preallocated dictionary and clear stale entries via a pooled/cached list without runtime GC allocations.
  - Bounded Melee Impulse: Saw melee knockback must always clamp between SAW_MIN_KNOCKBACK_DISTANCE (1.2m) and SAW_MAX_KNOCKBACK_DISTANCE (3.5m), and arrival duration must clamp between 0.12s and 0.22s.

- Ordering or sequencing guarantees:
  - SkillsRegistry.Awake discovers child skills before Start resets configs and initializes Skills[0].
  - SkillsRegistry.Start runs ResetRuntimeState() before calling InitializeSkill(Skills[0]).
  - Uninitialized skills chosen from the UI are initialized explicitly by SkillsRegistry.InitializeSkill upon player confirmation, never automatically upon dequeuing the request.
  - SkillsRegistry.OnSkillInitialized fires synchronously from InitializeSkill, ensuring HUD slot assignment occurs before any subsequent reward dequeues.
  - IUpgradeableStat.Upgrade fires OnUpgrade synchronously, allowing turrets, activators, and projectile configs to update immediately.

- Constraints contributors must preserve:
  - Preserve inspector-assigned references on prefabs (SkillInfoSO, configs, child turrets, blades, VFX players, audio players).
  - Treat changes to skill stat ranges, cooldowns, damage, range, projectile stats, target counts, and spawn cadence as player-facing balance changes.
  - Keep lasergun target acquisition non-allocating using Physics.OverlapSphereNonAlloc and line-of-sight checks against TerrainLayers.All.
  - Maintain one laser shoot audio playback per turret firing sequence, regardless of how many targets are hit simultaneously.
  - Keep DI dependencies explicit through Reflex; avoid introducing singletons, static service state, or FindAnyObjectByType searches.
  - Do not edit .prefab, .unity, .asset, or .meta files directly unless explicitly requested and safe to review as text.

## Extension Points

- Safe extension areas:
  - Adding a new player skill:
    1. Create a concrete skill class inheriting UpgradeableSkill<TConfig> under Assets/Scripts/Skills/PlayerSkills/.
    2. Create a configuration ScriptableObject inheriting SkillUpgradeableStatsConfig under Assets/ScriptableObjects/Skills/.
    3. Create a SkillInfoSO asset with UI name, description, and 2D Sprite Icon.
    4. Add the skill component as a direct child under the player car's Skills GameObject.
    5. Add matching 3D preview visual models with identical names under both primary and secondary visual arrays in SkillsVisualPresenter.
  - Adding upgradeable stats to an existing skill:
    1. Add serialized FloatUpgradeableStat or IntUpgradeableStat fields to the skill's config ScriptableObject.
    2. In ResetRuntimeState(), deep-copy each field via DeepCopyUtility.DeepCopy.
    3. Expose each copied stat as a public property implementing IUpgradeableStat.
    4. Subscribe to OnUpgrade in the concrete skill or config to apply changes to gameplay values.
  - Custom stat rarity overrides:
    - Set OverrideDefaultRarity = true and select the desired Rarity on the stat instance in the Unity Inspector.
  - Expanding active skills capacity:
    - Update SkillConstants.MAX_ACTIVE_SKILLS and wire additional slot frames/images in PlayerSkillsHUDPresenter.

- Required dependencies and contracts:
  - New skill configs must inherit SkillUpgradeableStatsConfig and implement ResetRuntimeState() and AppendStatsForDisplay(List<NameUpgradableStatPair>).
  - New child activatable items must implement IInitializableWithScriptableConfig<T> and correctly maintain IsInitialized().
  - New projectile-based skills must use the established projectile pooling pattern (ObjectPool<Projectile>) and release projectiles on OnLifeEnd / OnCanBeReleased.
  - New skill visuals must match SkillInfoSO.Name exactly.

- Testing implications:
  - Targeted compilation check:
    dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  - Play-mode verification:
    - Verify initial starting skill activates and displays in HUD slot 1 with punch animation.
    - Collect skill crates and verify stat upgrade popup displays 3 options with correct stat icons and rarity borders.
    - Test hotkeys 1, 2, 3 and verify input debounce prevents double-skips.
    - Reach level 4 (or next configured interval) and verify 2-choice new skill card appears with dual 3D preview renderers.
    - Verify selecting a 2nd and 3rd skill populates HUD slots 2 and 3.
    - Once 3 active skills are acquired, verify subsequent level-up rewards only offer stat upgrades.
    - Max out stats on active skills and verify the upgrade queue skips exhausted skills without freezing gameplay.
    - Ram into dense swarms with Saw equipped and verify steady periodic damage cadence on both enter and stay without FPS drops or GC allocations.

## Integration Notes

- Upstream dependencies:
  - Reflex DI Container provides IPlayerManager, IPlayerLevelPresenter, ICollectibleDropNotifier, ISkillUpgradeFlow, and ISkillsVisualPresenter.
  - PlayerManager provides access to ISkillsRegistry and ICarController.
  - DeepCopyUtility provides in-memory cloning for all upgradeable stats.
  - EntityLayers and TerrainLayers gate collision detection, targeting queries, and landmine placement.
  - ProjectileConstants provides default pooling sizes for projectile-based turrets.

- Downstream consumers:
  - SkillUpgradePresenter consumes ISkillUpgradeFlow, ISkillsRegistry, and ISkillsVisualPresenter.
  - PlayerSkillsHUDPresenter consumes ISkillsRegistry.OnSkillInitialized.
  - SawBlade, MinigunTurret, LasergunTurret, and Landmine interact with IDamageable, IKnockable, and IVFXPlayer.
  - EnemyMovementController receives knockback requests from SawBlade and Landmine, clamping knockback coordinates against world grid boundaries with safety padding.

- Cross-system coupling risks:
  - Skill progression depends on events from LevelSystem (level rewards) and CollectiblesSystem (crates).
  - UI button text generation relies on converting PascalCase property names to display strings (statName.PascalCaseToWords()).
  - Dual-station preview rendering couples SkillsVisualPresenter with specific camera clear flags, solid background colors, and render texture wiring.

## Known Risks and Open Questions

- Known limitations:
  - Saw count is intentionally retired. SawSkill initializes the first authored blade; its supported runtime stats are KnockbackRange and Damage.
  - LandmineSkill has an unused serialized _cooldown field; active spawn intervals are controlled by _config.SpawnCooldown.Value.
  - Starting skill selection relies on direct child hierarchy order (Skills[0]) rather than an explicit designer-selected loadout property.
  - LasergunTurret dynamically instantiates additional LineRenderer components at runtime if NumberOfTargets exceeds the initial capacity, cloning from _laserLineRenderer.

- Open design questions:
  - Should the starting skill be configurable via player selection in the main menu or car loadout settings?
  - Should the active skill cap (currently 3) be expandable through meta-progression, car chassis passives, or power-up items?
  - Should crate pickups occasionally offer new skill choices, or remain strictly stat upgrades?

- Suggested follow-up tasks:
  - Add an explicit empty-check guard in SkillsRegistry.Start before accessing Skills[0].
  - Remove or wire the unused _cooldown field in LandmineSkill in a focused cleanup.
  - Keep new supported stats in each config's explicit display enumeration and review the seven-row UI capacity before expanding it.

## Owned Skill Stats Display

- Assets/ScriptableObjects/Skills/SkillUpgradeableStatsConfig.cs provides AppendStatsForDisplay independently of filtered upgrade selection. It appends runtime references in explicit order without resetting configs, clearing the destination, or filtering capped stats.
- Supported counts and order: Saw 2 (KnockbackRange, Damage); Minigun 7 (DelayBetweenShoots, Range, NumberOfTurrets, BulletSize, BulletSpeed, BulletDamage, BulletMaxPiercing); Lasergun 5 (DelayBetweenShoots, NumberOfTurrets, NumberOfTargets, Range, Damage); Landmine 5 (SpawnCooldown, ExplosionRadius, Size, KnockbackRange, Damage).
- Assets/Scripts/UI/Skills/SkillsStatsPresenter.cs iterates initialized skills in registry order, refreshes ownership while paused, and freezes values when the death menu opens. It never modifies skill ownership, upgrade math, configs, or gameplay time.
- The obsolete saw-count field and upgrade consumers were removed; its config asset and preset were reserialized through Unity. Retained designer values are unchanged.
