# Skills System Documentation

## Purpose

The Skills system owns player skill registration, discovery, initialization, upgradeable stat tracking, selection flow, HUD presentation, and the runtime behavior of concrete player skills.

It is responsible for:
- Discovering direct child skill components under the player skill registry.
- Enforcing the active skills limit of 3 skills (SkillConstants.MAX_ACTIVE_SKILLS = 3).
- Initializing the starting skill (direct child index 0) and deep-copying runtime stat configurations so ScriptableObject source assets remain unmutated.
- Tracking uninitialized and initialized skill counts and notifying listeners when a skill is initialized via OnSkillInitialized.
- Queuing reward requests via ISkillUpgradeFlow using lightweight tokens and lazy Just-In-Time (JIT) evaluation upon dequeue.
- Presenting dual-card new skill choices (SkillConstants.NEW_SKILL_CHOICE_COUNT = 2) with side-by-side preview cards and 1-2 hotkeys.
- Rendering dual-station 3D skill preview models with context-aware camera backgrounds (SkillVisualContext.StatUpgrade vs SkillVisualContext.NewSkillUnlocked).
- Presenting up to 3 randomized stat upgrade options (SkillConstants.MAX_SKILL_UPGRADE_OPTIONS = 3) with stat icons, rarity tiers, and 1-3 hotkeys.
- Displaying active player skills in the HUD (PlayerSkillsHUDPresenter) across 3 persistent slots with punch-scale acquisition animations.
- Applying runtime stat upgrades via UpgradeableStat<T> with in-memory cloning, unlimited max value support, and inclusive integer roll ranges.
- Running concrete skill behaviors for Saw Blades, Minigun Turrets, Lasergun Turrets, and Landmines.

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
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs
  - Assets/Scripts/Skills/ItemsWithScriptableConfigsActivator.cs
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
  - ISkillBase is the foundational player skill contract. It extends IInitializable and exposes SkillInfoSO.
  - IUpgradeableSkill extends ISkillBase, exposing CanBeUpgraded() and ISkillUpgradeableStatsConfig Config.
  - UpgradeableSkill<TConfig> is the abstract base MonoBehaviour for skills backed by a ScriptableObject upgrade config. It activates the skill GameObject upon initialization when a valid config exists.
  - SkillsRegistry discovers direct child components implementing ISkillBase in Awake, resets runtime config state and counts uninitialized skills in Start, initializes Skills[0] as the starting skill, and raises OnSkillInitialized whenever a skill is initialized.
  - SkillConstants defines global constraints: MAX_ACTIVE_SKILLS = 3, NEW_SKILL_CHOICE_COUNT = 2, MAX_SKILL_UPGRADE_OPTIONS = 3, RARE_THRESHOLD = 0.5f, ULTRA_RARE_THRESHOLD = 0.8f, TARGET_BUFFER_SIZE = 64, CAN_PLACE_MINE_RAY_DISTANCE = 5f, and TIME_TO_ARRIVE_AT_LOCATION_MULTIPLIER = 0.2f.
  - SkillUpgradeFlow owns reward queueing. Requests are stored as lightweight QueuedSkillRewardRequest tokens. When dequeued in TryGetNextRequest, tokens evaluate candidates lazily Just-In-Time against currently initialized skills:
    - New skill requests check if InitializedSkillsCount < MAX_ACTIVE_SKILLS, pick up to 2 uninitialized candidate skills, and emit a NewSkillChoice request. If the active skill cap is reached or no uninitialized skills remain, the flow falls back to a stat upgrade request.
    - Stat upgrade requests sample eligible initialized skills dynamically via RandomUpgradeableSkillFinder. If a candidate has no upgradeable stats remaining, it is skipped cleanly.
  - SkillUpgradeRequest is a readonly struct carrying RequestType (NewSkillChoice vs UpgradeSkill), SkillChoices, target UpgradeableSkill, and UpgradeOptions.
  - SkillUpgradeOption carries the display string, upgrade action callback, rarity tier, and stat Sprite icon for an upgrade button.
  - SkillUpgradeableStatsConfig reflects over public instance properties implementing IUpgradeableStat where CanBeUpgraded is true, returning NameUpgradableStatPair records.
  - UpgradeableStat<T>, FloatUpgradeableStat, and IntUpgradeableStat manage stat progression. They implement ICloneable and provide a strongly-typed Clone() method. They encapsulate Value, MinMaxRange, _rangeOfPossibleValuesForUpgrade, IsSubstractModeOn, HasUnlimitedMaxValue, AlwaysUseMinValueForUpgrade, Unit, Sprite Icon, and the OnUpgrade event.
  - DeepCopyUtility provides fast-path in-memory cloning via ICloneable, FloatUpgradeableStat, and IntUpgradeableStat before falling back to JSON serialization. This preserves UnityEngine.Object references (such as Sprite Icon) without modifying source ScriptableObject assets.
  - SkillUpgradeRarityCalculator evaluates rolled values against the stat upgrade range, classifying options as Common, Rare (>= 0.5), or UltraRare (>= 0.8). If OverrideDefaultRarity is enabled on the stat, the manual Rarity override takes precedence.
  - SkillUpgradePresenter orchestrates popup presentation, subscribing to level-up events, skill-crate collection, and queue notifications. It maps keys 1 and 2 to dual new-skill choices and keys 1, 2, and 3 to stat upgrades, protected by frame-debounce checks (_lastHandledInputFrame == Time.frameCount).
  - SkillsVisualPresenter manages dual 3D preview renderers (Slot 0 primary, Slot 1 secondary), changes camera background colors by context (SkillVisualContext.StatUpgrade vs SkillVisualContext.NewSkillUnlocked), and activates matching visual models by SkillInfoSO.Name.
  - PlayerSkillsHUDPresenter manages 3 persistent HUD slots showing empty frames or active skill 2D icons with punch-scale animations on acquisition. It registers with ISkillsRegistry.OnSkillInitialized.

- Concrete player skills:
  - SawSkill / SawBlade:
    - SawSkill initializes child SawBlade components using ItemsWithScriptableConfigsActivator. The first blade is active immediately; upgrading NuberOfSaws activates additional blades up to the configured value.
    - SawBlade rotates around the car and triggers collisions with EntityLayers.Enemies. On contact, it applies damage and knockback scaled by car speed via IPlayerManager.CarController.GetMovementSpeed(). Stun logic was removed.
  - MinigunSkill / MinigunTurret:
    - MinigunSkill initializes MinigunTurret instances up to NumberOfTurrets and runs a coroutine calling Shoot() at DelayBetweenShoots intervals.
    - MinigunTurret oscillates its visual horizontally using a mathematical cosine wave (avoiding heavy tween allocations). It spawns Projectile instances from an internal ObjectPool<Projectile> parented to _projectilesParent, applies projectile stats, triggers muzzle VFX, and plays audio.
  - LasergunSkill / LasergunTurret:
    - LasergunSkill initializes LasergunTurret instances up to NumberOfTurrets, propagates NumberOfTargets updates event-driven to all turrets, and invokes ShootFromTurrets() at DelayBetweenShoots intervals.
    - LasergunTurret searches for enemies using Physics.OverlapSphereNonAlloc on EntityLayers.Enemies, filters by line-of-sight against TerrainLayers.All, and sorts candidates by distance. It tracks closest targets up to NumberOfTargets, rotates toward the primary target, plays charge VFX, renders LineRenderer beams to all hit targets, and applies instant damage.
  - LandmineSkill / Landmine:
    - LandmineSkill periodically performs a downward raycast checking for TerrainLayers.Ground within CAN_PLACE_MINE_RAY_DISTANCE, instantiating a Landmine prefab when valid ground is detected.
    - Landmine scales to Size.Value and marks _isInitialized = true. When an enemy enters its trigger, it queries an overlap sphere against EntityLayers.Enemies, plays explosion VFX/audio, applies damage, and calculates radial knockback. Stun logic was removed.

- Runtime flow:
  1. Reflex DI Boot: DefaultGameplaySceneInstaller binds PlayerManager as IPlayerManager, SkillsVisualPresenter as ISkillsVisualPresenter, SkillUpgradeFlow as ISkillUpgradeFlow, PlayerSkillsHUDPresenter as IPlayerSkillsHUDPresenter, and CollectibleDropNotifier as ICollectibleDropNotifier.
  2. Registry Scan: In Awake, PlayerManager captures ISkillsRegistry. SkillsRegistry scans direct child transforms and caches all components implementing ISkillBase in Skills.
  3. Registry Start: SkillsRegistry calls ResetRuntimeState() on all upgradeable skill configs, deep-copying stats in memory. It counts uninitialized skills and calls InitializeSkill(Skills[0]), activating the starting skill and firing OnSkillInitialized.
  4. HUD Initialization: PlayerSkillsHUDPresenter initializes 3 empty slot frames and populates the first active skill icon from Skills[0].
  5. Reward Triggers:
     - Skill Crate: When a player collects a crate, CollectibleDropNotifier fires OnSkillUpgradeCollectibleCollected, triggering SkillUpgradeFlow.QueueRandomSkillUpgradeRequest.
     - Level Up: PlayerLevelPresenter fires OnExpSliderVisualEndValueReached. If the level satisfies the interval ((level - 1) % _newSkillLevelInterval == 0) and InitializedSkillsCount < MAX_ACTIVE_SKILLS, QueueRandomNewSkillRequest is called; otherwise, QueueRandomSkillUpgradeRequest is queued.
  6. Queue Notification: SkillUpgradeFlow raises OnRequestQueued, prompting SkillUpgradePresenter.TryShowQueuedRewardSection.
  7. Lazy Request Resolution: SkillUpgradePresenter calls TryGetNextRequest:
     - If NewSkillChoice: up to 2 uninitialized candidate skills are returned without initializing them. SkillUpgradePresenter displays the 2-card UI with dual 3D preview renderers. The player presses 1 or 2 to choose, and the chosen skill is initialized via SkillsRegistry.InitializeSkill, firing OnSkillInitialized and updating the HUD.
     - If UpgradeSkill: candidate initialized skills are sampled dynamically. 3 upgrade options are rolled with calculated rarities and icons. The player presses 1-3 or clicks a button, invoking Option.Apply() which synchronously executes IUpgradeableStat.Upgrade.
  8. Progression Updates: IUpgradeableStat.Upgrade updates the current stat value, clamps or limits if configured, updates CanBeUpgraded, and raises OnUpgrade. Concrete skill components listen to OnUpgrade to update active item counts, line renderers, or projectile properties.

## Rules and Invariants

- Critical behavior rules:
  - Skill Discovery: Skills must be direct children of the SkillsRegistry GameObject to be discovered during Awake.
  - Active Skills Cap: The player cannot exceed SkillConstants.MAX_ACTIVE_SKILLS (3 skills). Any new skill request queued when 3 skills are active is rejected or falls back to a stat upgrade.
  - Dual Skill Choice: New skill unlocks offer exactly up to SkillConstants.NEW_SKILL_CHOICE_COUNT (2 choices), selectable via keyboard 1 or 2 (supporting Alpha and Numpad keys).
  - Just-In-Time Evaluation: Queued stat upgrade requests must not bind a target skill at enqueue time. They store a token and dynamically evaluate candidate skills upon dequeue, ensuring newly unlocked skills are immediately eligible for subsequent upgrades during multi-reward surges.
  - In-Memory Deep Copy: ScriptableObject configurations must never be mutated at runtime. All stats must be deep-copied in ResetRuntimeState() using DeepCopyUtility.DeepCopy, which leverages ICloneable fast-paths.
  - Stat Icon Preservation: Sprite Icon references must survive the deep-copy process. Bypassing the ICloneable pathway into JsonUtility will drop UnityEngine.Object references and cause blank stat icons in the UI.
  - Unlimited Max Stats: Stats configured with HasUnlimitedMaxValue = true must bypass maximum value clamping and must not set CanBeUpgraded = false, preventing upgrade starvation.
  - Visual Matching: 3D preview models under SkillsVisualPresenter must have GameObject names that exactly match SkillInfoSO.Name.
  - Input Debounce: All UI hotkey handlers and click handlers must check _lastHandledInputFrame == Time.frameCount to prevent duplicate input registration across consecutive frames.
  - Presentation-Only Rarity: SkillUpgradeRarity (Common, Rare, UltraRare) is exclusively visual for card borders and labels. Gameplay effects must not scale based on the visual rarity label.

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
    4. Add the skill component as a direct child under the player's Skills GameObject.
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
  - New skill configs must inherit SkillUpgradeableStatsConfig and implement ResetRuntimeState().
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

## Integration Notes

- Upstream dependencies:
  - Reflex DI Container provides IPlayerManager, IPlayerLevelPresenter, ICollectibleDropNotifier, ISkillUpgradeFlow, and ISkillsVisualPresenter.
  - PlayerManager provides access to ISkillsRegistry.
  - DeepCopyUtility provides in-memory cloning for all upgradeable stats.
  - EntityLayers and TerrainLayers gate collision detection, targeting queries, and landmine placement.
  - ProjectileConstants provides default pooling sizes for projectile-based turrets.

- Downstream consumers:
  - SkillUpgradePresenter consumes ISkillUpgradeFlow, ISkillsRegistry, and ISkillsVisualPresenter.
  - PlayerSkillsHUDPresenter consumes ISkillsRegistry.OnSkillInitialized.
  - SawBlade, MinigunTurret, LasergunTurret, and Landmine interact with IDamageable, IKnockable, and IVFXPlayer.

- Cross-system coupling risks:
  - Skill progression depends on events from LevelSystem (level rewards) and CollectiblesSystem (crates).
  - UI button text generation relies on converting PascalCase property names to display strings (statName.PascalCaseToWords()).
  - Dual-station preview rendering couples SkillsVisualPresenter with specific camera clear flags, solid background colors, and render texture wiring.

## Known Risks and Open Questions

- Known limitations:
  - SawSkillUpgradeableConfigSO.NuberOfSaws retains a legacy spelling typo in its serialized property name to preserve existing asset data compatibility.
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
  - Safely migrate SawSkillUpgradeableConfigSO.NuberOfSaws using FormerlySerializedAs when asset serialization updates are scheduled.
