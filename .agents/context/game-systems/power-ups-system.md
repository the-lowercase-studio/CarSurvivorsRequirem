# Power-Ups System Documentation

## Purpose

The Power-Ups system coordinates rare, tactical drop pickups (Nuke, Freeze Timer, Bubble Shield, Armor, Fire Tails) dropped by enemies, executes their timed, tiered, or instant mechanical effects, controls global post-processing volumes and screen overlays, and synchronizes combat feedback across enemies, bosses, spawners, vehicle VFX, and player survivability.

It is responsible for:
- Providing pooled power-up pickup entities (Assets/Scripts/PowerUps/Collectibles/PowerUpCollectible.cs) identified by append-only enum values (Assets/Scripts/PowerUps/PowerUpType.cs).
- Supplying a shared scaled-time countdown helper for timed effects (Assets/Scripts/PowerUps/PowerUpTimer.cs).
- Coordinating deferred Nuke screen-clear damage against camera-visible regular enemies and engaged bosses while suppressing secondary collectible drops (Assets/Scripts/PowerUps/Nuke/NukeEffectService.cs).
- Enforcing global Time Freeze across enemy locomotion, attack controllers, animators, wave and swarm spawners, boss state machines, freezable DOTween sequences, telegraph indicators, and active Fire Tails hazards/particles (Assets/Scripts/PowerUps/Freeze/TimeFreezeService.cs, Assets/Scripts/PowerUps/Freeze/TimeFreezeExtensions.cs).
- Managing combat damage immunity, 3D Fresnel bubble presentation, and synchronized warning-window blinking on the bubble and player health bar via Bubble Shield (Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs, Assets/Scripts/PowerUps/Shield/BubbleShieldVisualController.cs, Assets/Shaders/BubbleShield.shader).
- Managing tiered stackable Armor health plates (up to 3 levels: +20, +40, +60 HP = 120 total Armor HP), top-down multi-plate overflow damage absorption, pickup refills, and world-space health bar plate presentation (Assets/Scripts/PowerUps/Armor/ArmorService.cs, Assets/Scripts/HealthSystem/ArmorPlatesView.cs, Assets/Scripts/HealthSystem/ArmorPlateItemView.cs).
- Managing timed offensive Fire Tails area-denial drift tracks, preallocated FireHazardNode combat zones, throttled 10 Hz Physics.OverlapSphereNonAlloc scans, non-stacking burn DoT on enemies and bosses, regular-enemy movement speed slow, and dual rear-wheel flame/ribbon VFX switching (Assets/Scripts/PowerUps/FireTails/FireTailsService.cs, Assets/Shaders/FireTailsTrail.shader).
- Driving full-screen CanvasGroup overlay alpha fades for Nuke dirt and Freeze frost effects (Assets/Scripts/PowerUps/Presentation/ScreenOverlayPresenter.cs).
- Exposing Reflex DI contracts for cross-system queries: ITimeFreezeState, IBubbleShield, IArmorService, and IFireTailsService.

It is not responsible for:
- Rolling enemy drop tables, resolving walkable grid landing cells, or pooling/scattering collectible GameObjects (handled by Assets/Scripts/Enemies/EnemyDropHandler.cs and Assets/Scripts/Enemies/CollectibleDropNotifier.cs).
- Managing skill upgrades or skill crates (handled by the collectibles and skills systems).
- Granting permanent player stats or experience (handled by the level and stats systems).
- Passive health regeneration on Armor plates (Assets/Scripts/HealthSystem/RegenativeHealth.cs heals only base player IHealth).
- Enforcing lethal arena boundary / void kills via TakeFullHpDamage, which intentionally bypass Bubble Shield and clear Armor immediately (handled by Assets/Scripts/Player/PlayerDamagedHandler.cs and DeathVolume).
- Audio or VFX playback backends (handled by Assets/Scripts/Audio/AudioClipPlayer.cs and Assets/Scripts/VFX/VFXPlayer.cs).

## Reading Map

- Primary code locations:
  - Assets/Scripts/PowerUps/PowerUpType.cs
  - Assets/Scripts/PowerUps/PowerUpTimer.cs
  - Assets/Scripts/PowerUps/Collectibles/PowerUpCollectible.cs
  - Assets/Scripts/PowerUps/Config/NukeConfigSO.cs
  - Assets/Scripts/PowerUps/Config/FreezeTimerConfigSO.cs
  - Assets/Scripts/PowerUps/Config/BubbleShieldConfigSO.cs
  - Assets/Scripts/PowerUps/Config/ArmorConfigSO.cs
  - Assets/Scripts/PowerUps/Config/FireTailsConfigSO.cs
  - Assets/Scripts/PowerUps/Constants/PowerUpConstants.cs
  - Assets/Scripts/PowerUps/Constants/TimeFreezeConstants.cs
  - Assets/Scripts/PowerUps/Constants/BubbleShieldShaderConstants.cs
  - Assets/Scripts/PowerUps/Constants/ArmorConstants.cs
  - Assets/Scripts/PowerUps/Constants/FireTailsConstants.cs
  - Assets/Scripts/PowerUps/Nuke/NukeEffectService.cs
  - Assets/Scripts/PowerUps/Freeze/TimeFreezeService.cs
  - Assets/Scripts/PowerUps/Freeze/TimeFreezeExtensions.cs
  - Assets/Scripts/PowerUps/Presentation/ScreenOverlayPresenter.cs
  - Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs
  - Assets/Scripts/PowerUps/Shield/BubbleShieldVisualController.cs
  - Assets/Scripts/PowerUps/Armor/ArmorService.cs
  - Assets/Scripts/PowerUps/FireTails/FireTailsService.cs
  - Assets/Shaders/BubbleShield.shader
  - Assets/Shaders/FireTailsTrail.shader
- Editor tooling:
  - Assets/Scripts/Editor/Tools/ArmorWiringTool.cs
  - Assets/Scripts/Editor/Tools/FireTailsWiringTool.cs
- Designer-authored data, materials, textures, and prefabs:
  - Assets/ScriptableObjects/Collectibles/PowerUps/NukeConfig.asset
  - Assets/ScriptableObjects/Collectibles/PowerUps/FreezeTimerConfig.asset
  - Assets/ScriptableObjects/Collectibles/PowerUps/BubbleShieldConfig.asset
  - Assets/ScriptableObjects/Collectibles/PowerUps/ArmorConfig.asset
  - Assets/ScriptableObjects/Collectibles/PowerUps/FireTailsConfig.asset
  - Assets/Materials/FireTailsTrail.mat
  - Assets/Textures/FlameNoise.png
  - Assets/Prefabs/Collectibles/Nuke.prefab
  - Assets/Prefabs/Collectibles/FreezeTimer.prefab
  - Assets/Prefabs/Collectibles/BubbleShield.prefab
  - Assets/Prefabs/Collectibles/Armor.prefab
  - Assets/Prefabs/Collectibles/FireTails.prefab
  - Assets/Prefabs/UI/HealthBarCanvas.prefab
  - Assets/Prefabs/Player/Player.prefab
- Related runtime integration:
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs (ICollectibleDropNotifier.OnPowerUpCollected, ICollectibleDropSuppressor)
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs (IActiveEnemiesProvider)
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs (IBossEncounterService.CollectEngagedBosses)
  - Assets/Scripts/Enemies/Bosses/IBossCombatTarget.cs
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/IMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Enemies/Base/EnemyAnimator.cs
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyExplosionController.cs
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyAttackFeedback.cs
  - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemLinearAttackHitbox.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs
  - Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
  - Assets/Scripts/Enemies/Bosses/Towers/TowerBossBase.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Combat/MortarBoulderHitbox.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/RailgunTowerBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Projectiles/RailgunOrbProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/Combat/RailgunLinearWaveHitbox.cs
  - Assets/Scripts/Indicators/CircularTelegraphIndicator.cs
  - Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs
  - Assets/Scripts/Indicators/ArcTelegraphIndicator.cs
  - Assets/Scripts/Waves/WaveManager.cs
  - Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Player/Car/CarVfxEffectsController.cs
  - Assets/Scripts/Player/Car/CarDriftTrailSegmentPool.cs
  - Assets/Scripts/HealthSystem/HealthBar.cs
  - Assets/Scripts/HealthSystem/ArmorPlatesView.cs
  - Assets/Scripts/HealthSystem/ArmorPlateItemView.cs
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/game-systems/vfx-system.md
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/dev-console-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/adr/ADR-001-reflex-di-architecture.md
  - .agents/context/adr/ADR-002-dotween-and-transform-extensions.md
  - .agents/context/adr/ADR-004-designer-authored-data-and-prefabs.md
  - .agents/context/adr/ADR-005-fail-fast-and-inspector-null-checks-policy.md
- Related agents or instructions:
  - AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - PowerUpType: Enum defining Nuke = 0, FreezeTimer = 1, BubbleShield = 2, Armor = 3, and FireTails = 4. Values are append-only to keep serialized prefab data stable.
  - PowerUpTimer: Plain C# countdown helper tracking Duration, Remaining, and IsRunning in scaled time. Exposes Start(duration), Stop(), Tick(deltaTime) (returning true strictly on the frame the timer reaches zero), and IsInWindow(window).
  - PowerUpCollectible: MonoBehaviour implementing IPowerUpCollectible and IPoolable. Serializes PowerUpType. On trigger contact with EntityLayers.Player, invokes OnCollected and ReturnToPool (raising OnCanBeReleased), and kills active transform tweens in OnRelease.
  - NukeConfigSO: ScriptableObject configuring BossDamagePercent (default 15%), EnemyVisibilityPadding (1m), BossVisibilityPadding (4m), SnapshotCapacity (512), FlashInDuration (0.1s), BlastTotalDuration (3.0s), and DirtOverlayPeakAlpha (1.0). Validates finite bounds in ValidateConfiguration().
  - NukeEffectService: Scene service that listens to ICollectibleDropNotifier.OnPowerUpCollected for PowerUpType.Nuke. Defers execution to the next Update frame via _isPendingExecution, computes camera frustum planes, suppresses enemy collectible drops via ICollectibleDropSuppressor, kills visible active enemies with TakeFullHpDamage(), deals percentage max-health damage to visible engaged bosses via IBossEncounterService.CollectEngagedBosses, and runs blast Volume and dirt overlay DOTween sequences.
  - FreezeTimerConfigSO: ScriptableObject configuring DurationSeconds (default 5s), FadeInDuration (0.25s), FadeOutDuration (0.5s), TweenBufferCapacity (256), and FrostOverlayPeakAlpha (1.0). Validates that FadeInDuration + FadeOutDuration <= DurationSeconds.
  - TimeFreezeService: Scene service implementing ITimeFreezeState. Listens for PowerUpType.FreezeTimer, manages freeze countdown via PowerUpTimer, tracks UnfrozenTime (Time.time minus accumulated and current frozen durations), pauses/resumes active DOTween tweens carrying TimeFreezeConstants.FREEZABLE_TWEEN_ID, raises OnFrozenChanged(bool), and drives cold post-processing Volume and frost ScreenOverlayPresenter fade-in/fade-out sequences.
  - TimeFreezeExtensions: Fluent DOTween extension AsFreezable<T>(this T tween) that assigns TimeFreezeConstants.FREEZABLE_TWEEN_ID (0x46525A31) via tween.SetId.
  - BubbleShieldConfigSO: ScriptableObject configuring DurationSeconds (default 8s), WarningWindowSeconds (2s), BlinkPeriodSeconds (0.25s), ShieldColor (dark blue), SteadyAlpha (0.35), WarningMinAlpha (0.05), AppearDuration (0.25s), and DisappearDuration (0.15s).
  - BubbleShieldService: Scene service implementing IBubbleShield. Listens for PowerUpType.BubbleShield, manages shield duration via PowerUpTimer, drives BubbleShieldVisualController and HealthBar.SetColorOverride, plays absorb VFX/SFX on NotifyDamageAbsorbed(), computes a cosine blink wave during the final WarningWindowSeconds to modulate bubble alpha and health bar color blend in lockstep, and deactivates immediately if the player dies.
  - BubbleShieldVisualController: Controls the 3D shield mesh attached to the player car. Uses MaterialPropertyBlock (setting _BaseColor and _Alpha via BubbleShieldShaderConstants) and DOTween scale/alpha transitions (OutBack/OutQuad on show, InBack/InQuad on hide) while keeping the GameObject inactive when hidden.
  - ArmorConfigSO: ScriptableObject configuring PlateCapacities (default [20f, 40f, 60f] for 3 levels), PlateColor (dark blue matching Bubble Shield: r 0.05, g 0.15, b 0.6, a 1), HorizontalOverlapRatio (0.2f), VerticalStepOffset (0f), ReferenceBaseHealthWidth (1.5f), and ReferenceBaseMaxHealth (100f). Validates exactly 3 positive finite capacities and finite layout bounds in ValidateConfiguration().
  - ArmorService: Scene service implementing IArmorService. Listens for PowerUpType.Armor and player OnNoHealth, tracks CurrentLevel (0..3) and preallocated _currentPlateHealth (float[3]), refills all unlocked plates up to CurrentLevel on pickup, absorbs incoming combat damage from highest active plate downward with immediate multi-plate overflow, drives ArmorPlatesView updates and shake feedback, plays armor SFX/VFX, and clears armor immediately on player death or disable.
  - FireTailsConfigSO: ScriptableObject configuring DurationSeconds (default 8.0s), HazardLifetimeSeconds (3.5s), TrailFadeOutDuration (0.5s), SpawnIntervalDistance (1.2m), HazardRadius (1.1m), DamagePerTick (25f), TickIntervalSeconds (0.5s), OverlapCheckIntervalSeconds (0.1s / 10 Hz), and SlowFactor (0.6f / 40% slow). Validates finite positive bounds and interval invariants in ValidateConfiguration().
  - FireTailsService: Scene service implementing IFireTailsService. Listens for PowerUpType.FireTails, manages the 8.0s duration via PowerUpTimer, toggles constant rear-wheel flames and drift fire trail pools on ICarVfxEffectsController, records ground-snapped FireHazardNode struct entries (capacity 256) along both rear tire tracks exclusively while grounded and drifting, evaluates Physics.OverlapSphereNonAlloc at 10 Hz (0.1s) against EntityLayers.Enemies, applies non-stacking burn DoT ticks (25 HP every 0.5s) to enemies and bosses, applies non-stacking 40% slow to regular enemies (EntityLayers.Enemy) while keeping bosses slow-immune, and pauses timers/particles when ITimeFreezeState.IsFrozen is true.
  - ScreenOverlayPresenter: UI presenter requiring a CanvasGroup (with an optional Image reference) that clamps alpha via SetAlpha and resets to zero via ResetOverlay.
- Key interfaces:
  - IPowerUpCollectible: Extends ICollectible with PowerUpType Type { get; }. Colocated in Assets/Scripts/PowerUps/Collectibles/PowerUpCollectible.cs.
  - ITimeFreezeState: Exposes IsFrozen, UnfrozenTime, and event Action<bool> OnFrozenChanged. Colocated in Assets/Scripts/PowerUps/Freeze/TimeFreezeService.cs.
  - IBubbleShield: Exposes IsActive and NotifyDamageAbsorbed(). Colocated in Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs.
  - IArmorService: Exposes CurrentLevel, CurrentArmorHealth, HasArmor, AbsorbDamage(float incomingDamage), and ClearArmorImmediate(). Colocated in Assets/Scripts/PowerUps/Armor/ArmorService.cs.
  - IFireTailsService: Exposes IsActive and RemainingSeconds. Colocated in Assets/Scripts/PowerUps/FireTails/FireTailsService.cs.
  - ICarVfxEffectsController: Exposes RearDriftEmitterCount, TryGetRearDriftEmitterWorldCandidate, ConfigureFireTailsTimings, SetFireTailsActive, and SetFireTailsFrozen. Colocated in Assets/Scripts/Player/Car/CarVfxEffectsController.cs.
  - ICollectibleDropSuppressor: Exposes AreDropsSuppressed, BeginDropSuppression(), and EndDropSuppression() using a reference-counted depth integer. Colocated in Assets/Scripts/Enemies/CollectibleDropNotifier.cs.
  - IActiveEnemiesProvider: Exposes CopyActiveEnemies(List<Enemy> buffer) on EnemiesSpawner.
  - IBossCombatTarget: Unifies IBossHUDSource and IDamageable for engaged bosses (GolemBoss, MortarTowerBoss, and RailgunTowerBoss).
- Centralized constants:
  - PowerUpConstants (Assets/Scripts/PowerUps/Constants/PowerUpConstants.cs):
    - SFX_PICKUP_NUKE = "PowerUpPickupNuke"
    - SFX_PICKUP_FREEZE = "PowerUpPickupFreeze"
    - SFX_PICKUP_SHIELD = "PowerUpPickupShield"
    - SFX_PICKUP_ARMOR = "PowerUpPickupArmor"
    - SFX_PICKUP_FIRE_TAILS = "PowerUpPickupFireTails"
    - SFX_NUKE_BLAST = "NukeBlast"
    - SFX_FREEZE_START = "FreezeStart"
    - SFX_FREEZE_END = "FreezeEnd"
    - SFX_SHIELD_START = "ShieldStart"
    - SFX_SHIELD_ABSORB = "ShieldAbsorb"
    - SFX_SHIELD_EXPIRE = "ShieldExpire"
    - SFX_ARMOR_START = "ArmorStart"
    - SFX_ARMOR_HIT = "ArmorHit"
    - SFX_ARMOR_BREAK = "ArmorBreak"
    - SFX_FIRE_TAILS_START = "FireTailsStart"
    - SFX_FIRE_TAILS_EXPIRE = "FireTailsExpire"
    - SFX_FIRE_TAILS_IGNITE = "FireTailsIgnite"
    - MAX_EFFECT_WEIGHT = 1f
    - MIN_EFFECT_WEIGHT = 0f
  - TimeFreezeConstants (Assets/Scripts/PowerUps/Constants/TimeFreezeConstants.cs):
    - FREEZABLE_TWEEN_ID = 0x46525A31
  - BubbleShieldShaderConstants (Assets/Scripts/PowerUps/Constants/BubbleShieldShaderConstants.cs):
    - BASE_COLOR_ID = Shader.PropertyToID("_BaseColor")
    - ALPHA_ID = Shader.PropertyToID("_Alpha")
    - FRESNEL_POWER_ID = Shader.PropertyToID("_FresnelPower")
    - RIM_INTENSITY_ID = Shader.PropertyToID("_RimIntensity")
  - ArmorConstants (Assets/Scripts/PowerUps/Constants/ArmorConstants.cs):
    - MAX_ARMOR_LEVEL = 3
    - DEFAULT_BASE_HEALTH_WIDTH = 1.5f
    - DEFAULT_BASE_MAX_HEALTH = 100f
    - DEFAULT_HORIZONTAL_OVERLAP_RATIO = 0.2f
    - DEFAULT_VERTICAL_STEP_OFFSET = 0f
  - FireTailsConstants (Assets/Scripts/PowerUps/Constants/FireTailsConstants.cs):
    - REAR_TIRE_TRACK_COUNT = 2
    - GROUND_SURFACE_OFFSET = 0.02f
    - MAX_HAZARD_NODES = 256
    - OVERLAP_COLLIDER_BUFFER_SIZE = 64
    - TARGET_TRACKING_INITIAL_CAPACITY = 128
    - NORMAL_SPEED_MULTIPLIER = 1f
- Runtime flow:
  - Collectible Drop and Pickup Pipeline:
    1. When an enemy dies while ICollectibleDropSuppressor.AreDropsSuppressed is false, EnemyDropHandler rolls its configured CollectibleDropEntry table and spawns winning prefabs through ICollectibleDropNotifier.SpawnCollectible.
    2. CollectibleDropNotifier pools the prefab instance, animates its scatter jump onto a walkable grid cell, and subscribes to ICollectible.OnCollected.
    3. When the player car collider (EntityLayers.Player) enters PowerUpCollectible, it fires OnCollected and returns itself to the pool.
    4. CollectibleDropNotifier pattern-matches IPowerUpCollectible and invokes OnPowerUpCollected(powerUp.Type).
  - Nuke Execution Flow:
    1. NukeEffectService sets _isPendingExecution = true inside HandlePowerUpCollected, deferring execution out of the physics trigger callback to the next Update tick.
    2. ExecuteNuke() calculates the 6 camera frustum planes via GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes).
    3. Calls _dropSuppressor.BeginDropSuppression() inside a try/finally block, copies active enemies via _activeEnemiesProvider.CopyActiveEnemies(_activeEnemiesBuffer), tests each alive enemy's padded AABB (size = EnemyVisibilityPadding * 2) against the frustum planes, and calls enemy.TakeFullHpDamage() on matches. Killed enemies still spawn EXP particles via their death handlers, but EnemyDropHandler skips collectible rolls while suppression depth > 0.
    4. Queries _bossEncounterService.CollectEngagedBosses(_engagedBossesBuffer), tests each alive engaged boss's padded AABB (size = BossVisibilityPadding * 2) against the frustum planes, and applies boss.Health.MaxHealth * (BossDamagePercent / 100f) via boss.TakeDamage(damage). Dormant towers whose arena encounters are not active are excluded by CollectEngagedBosses.
    5. Plays SFX_NUKE_BLAST and _blastVfxPlayer, then runs a joined DOTween Sequence fading _blastVolume.weight and _dirtOverlayPresenter alpha up over FlashInDuration (Ease.OutQuad) and back to 0 over BlastTotalDuration - FlashInDuration (Ease.InQuad).
  - Time Freeze Execution Flow:
    1. On initial FreezeTimer pickup, TimeFreezeService sets _isFrozen = true, records _freezeStartedAt = Time.time, starts _timer with DurationSeconds, pauses all playing DOTween tweens carrying FREEZABLE_TWEEN_ID (storing them in _pausedTweens), invokes OnFrozenChanged(true), plays SFX_FREEZE_START and _freezeVfxPlayer, and starts fade-in tweens for _freezeVolume and _frostOverlayPresenter.
    2. If another FreezeTimer is collected while already frozen, StartOrResetFreeze() restarts _timer to DurationSeconds, clears _isFadingOut, kills active fade sequences, and snaps _freezeVolume.weight to 1 and _frostOverlayPresenter alpha to FrostOverlayPeakAlpha without re-pausing tweens or re-firing OnFrozenChanged.
    3. While frozen:
       - EnemyMovementController early-returns in FixedUpdate (zeroing _currentVelocity), blocks CanBeTeleported, and holds `_teleportAllowedUnfrozenTime` (`POST_RELOCATION_TELEPORT_GRACE_SECONDS = 3.0f` measured in `Enemy.UnfrozenTime`) paused across freeze frames.
       - Enemy.ApplyKnockBack ignores knockback impulses, and EnemyAttackController ignores player collision triggers.
       - EnemyAnimator sets Animator.speed = 0 and uses Enemy.UnfrozenTime for its 3.5s attack watchdog so frozen time does not trip premature cancellation.
       - BarrelEnemyExplosionController uses Enemy.UnfrozenTime for its 3.5s priming watchdog, while BarrelEnemyAttackFeedback and CircularTelegraphIndicator pause their freezable tweens.
       - WaveManager and SwarmSpawner pause countdowns and coroutine wait loops while _timeFreezeState.IsFrozen is true.
       - GolemBoss, MortarTowerBoss, and RailgunTowerBoss early-return in Update/FixedUpdate (and TowerBossBase skips frozen turret aiming), GolemAnimator sets Animator.speed = 0, and boss attack/telegraph sequences tagged with AsFreezable() or FREEZABLE_TWEEN_ID (including ArcTelegraphIndicator, RailgunOrbProjectile, and RailgunLinearWaveHitbox) pause in place.
       - FireTailsService pauses its active duration timer, _combatClock (freezing FireHazardNode expiration and burn tick timers), and calls ICarVfxEffectsController.SetFireTailsFrozen(true) to pause rear-wheel and lingering ground fire particle systems.
    4. When _timer enters FadeOutDuration, StartFadeOutPresentation() tweens volume weight and frost overlay alpha toward 0 (Ease.InQuad).
    5. When _timer.Tick(Time.deltaTime) expires, EndFreeze(notifyPresentation: true) sets _isFrozen = false, adds elapsed freeze time to _accumulatedFrozenSeconds, stops the timer, fires OnFrozenChanged(false), resumes still-active tweens in _pausedTweens, resets presentation, and plays SFX_FREEZE_END.
  - Bubble Shield Execution Flow:
    1. On initial BubbleShield pickup, BubbleShieldService starts _timer with DurationSeconds, triggers _visualController.Show(AppearDuration, SteadyAlpha), locks the player health bar fill color via _playerHealthBar.SetColorOverride(ShieldColor, 1f), and plays SFX_SHIELD_START.
    2. On re-pickup while active, restarts _timer to DurationSeconds, calls _visualController.RestoreSteadyState(SteadyAlpha), and sets health bar color override blend to 1.
    3. When combat damage hits PlayerDamagedHandler.TakeDamage(damage), it checks _bubbleShield.IsActive first (before Armor or base health); when true, it calls _bubbleShield.NotifyDamageAbsorbed() (playing SFX_SHIELD_ABSORB and _absorbVfxPlayer) and returns before decreasing armor or health, playing damage SFX/VFX, or shaking the car mesh. PlayerDamagedHandler.TakeFullHpDamage() bypasses the shield check so DeathVolume kills remain lethal.
    4. Each Update while active: if the player is no longer alive, DeactivateImmediately() stops the timer, hides the bubble immediately, and clears the health bar override. Otherwise, during the final WarningWindowSeconds, UpdateShieldPresentation() computes blink01 = 0.5f * (1f + Mathf.Cos(cycle * 2 * PI)) and applies both _visualController.SetAlpha(Lerp(WarningMinAlpha, SteadyAlpha, blink01)) and _playerHealthBar.SetColorOverride(ShieldColor, blink01).
    5. When _timer expires, _visualController.Hide(DisappearDuration) scales and fades out the bubble, _playerHealthBar.ClearColorOverride() restores the health gradient, and SFX_SHIELD_EXPIRE plays.
  - Armor Execution Flow:
    1. In Start(), ArmorService initializes _armorPlatesView with _config, hides all plates, and subscribes to ICollectibleDropNotifier.OnPowerUpCollected and _playerManager.Health.OnNoHealth.
    2. When PowerUpType.Armor is collected while the player is alive, ApplyArmorPickup() increments _currentLevel by 1 (clamped to _config.MaxLevel = 3), refills _currentPlateHealth[i] = _config.GetPlateCapacity(i) for all unlocked plates (0 <= i < _currentLevel), updates _armorPlatesView, and plays SFX_PICKUP_ARMOR.
    3. When combat damage reaches PlayerDamagedHandler.TakeDamage(damage) and Bubble Shield is not active, PlayerDamagedHandler checks _armorService.HasArmor and calls damage = _armorService.AbsorbDamage(damage).
    4. AbsorbDamage iterates downward from the highest active plate (index = _currentLevel - 1 down to 0). If a plate's health exceeds remainingDamage, it subtracts remainingDamage and sets remainingDamage = 0f. Otherwise, it subtracts the plate's remaining HP from remainingDamage, zeroes the plate, decrements _currentLevel = index, and marks plateBroken = true.
    5. ArmorService refreshes _armorPlatesView, triggers _armorPlatesView.PlayDamageShake(), plays _damageVfxPlayer, and plays SFX_ARMOR_BREAK (if any plate broke) or SFX_ARMOR_HIT. If remainingDamage > 0f after breaking all plates, PlayerDamagedHandler subtracts the remainder from _playerManager.Health.
    6. On PlayerDamagedHandler.TakeFullHpDamage(), player death (OnNoHealth), OnDisable, or OnDestroy, ClearArmorImmediate() zeroes all plates and hides plate UI immediately.
  - Fire Tails Execution Flow:
    1. In Start(), FireTailsService subscribes to OnPowerUpCollected and OnNoHealth, and configures fire trail lifetime and fade duration on _playerManager.CarVfxEffectsController.
    2. When PowerUpType.FireTails is collected, HandlePowerUpCollected starts or resets _timer to _config.DurationSeconds (8.0s), resets track continuity flags, calls SetFireTailsActive(true) on ICarVfxEffectsController (enabling constant rear-wheel flames and switching drift trails from black skid marks to fire ribbon pools), and plays SFX_FIRE_TAILS_START.
    3. Each Update while _timer.IsRunning and not frozen: if the player is grounded and drifting (car.IsGrounded && car.IsDrifting), UpdateDriftHazardEmission() plays SFX_FIRE_TAILS_IGNITE on drift start and samples both rear tire ground contacts via TryGetRearDriftEmitterWorldCandidate and TryGetTireGroundContact. Whenever a rear tire travels at least SpawnIntervalDistance (1.2m) from its last node on that track, AddHazardNode records a FireHazardNode at groundHit.point + Vector3.up * 0.02f with ExpireCombatTime = _combatClock + HazardLifetimeSeconds (3.5s), overwriting the oldest node if all 256 slots are full.
    4. Simultaneously in CarVfxEffectsController.LateUpdate(), drifting while _isFireTailsActive is true suppresses standard black skid marks (_driftEmitters), appends ground-snapped points to _fireDriftEmitters (rendered with Custom/FireTailsTrail shader), and emits lingering world-space flame particles (_rearDriftFireTrailParticles).
    5. When _timer expires, EndActiveEmission() calls SetFireTailsActive(false) and plays SFX_FIRE_TAILS_EXPIRE while already-spawned FireHazardNode entries, fire trail segments, and world-space flame particles continue decaying over their remaining 3.5s lifetimes.
    6. While _activeHazardNodeCount > 0 or _slowedMoversPreviousScan.Count > 0 (and not frozen), FireTailsService advances _combatClock, removes expired nodes via swap-back removal, and every OverlapCheckIntervalSeconds (0.1s / 10 Hz) runs EvaluateHazardCombatOverlaps():
       - Queries Physics.OverlapSphereNonAlloc(node.Position, HazardRadius, _overlapBuffer, EntityLayers.Enemies, QueryTriggerInteraction.Collide).
       - Collects unique IDamageable targets into _touchedDamageablesThisScan and deals DamagePerTick (25 HP) at most once per TickIntervalSeconds (0.5s) per target (non-stacking across overlapping nodes).
       - Applies _config.SlowFactor (0.6x speed) via IMovementController.SetMovementSpeedMultiplier to regular enemies on EntityLayers.Enemy while leaving Bosses (EntityLayers.Boss) slow-immune, and restores 1.0x speed to any mover that exited all hazard nodes since the previous scan.

## Rules and Invariants

- Critical behavior rules:
  - PowerUpType enum values (Nuke = 0, FreezeTimer = 1, BubbleShield = 2, Armor = 3, FireTails = 4) are serialized on pickup prefabs and must remain append-only.
  - Required configurations (NukeConfigSO, FreezeTimerConfigSO, BubbleShieldConfigSO, ArmorConfigSO, FireTailsConfigSO) and mechanical scene references (BubbleShieldVisualController, player HealthBar, ArmorPlatesView, and all five service references on DefaultGameplaySceneInstaller) must fail fast in Awake() or InstallBindings() with InvalidOperationException if unassigned or invalid (per ADR-005). Optional sensory references (Volume, ScreenOverlayPresenter, VFXPlayer, AudioClipPlayer) may be null-guarded.
  - Nuke kills must always wrap the enemy kill loop in ICollectibleDropSuppressor.BeginDropSuppression() and EndDropSuppression() within a try/finally block so Nuke kills award EXP without triggering chain-reaction collectible drops.
  - Nuke boss damage must only affect bosses returned by IBossEncounterService.CollectEngagedBosses (the active GolemBoss or towers whose EncounterArenaController has IsEncounterActive && !IsEncounterCompleted) and whose padded bounds intersect the camera frustum.
  - Time Freeze must never mutate Unity's global Time.timeScale (which would freeze the player car, player weapons, and UI); instead, it coordinates freeze state through ITimeFreezeState and TimeFreezeConstants.FREEZABLE_TWEEN_ID.
  - Watchdog timers on enemies and bosses that measure elapsed Time.time (such as EnemyAnimator and BarrelEnemyExplosionController 3.5s attack watchdogs) must sample ITimeFreezeState.UnfrozenTime instead of raw Time.time so a 5-second freeze does not trip false watchdog cancellations.
  - Combat damage priority in PlayerDamagedHandler.TakeDamage(float) is strictly: (1) Bubble Shield (blocks 100% of combat damage without depleting Armor), (2) Armor plates (depletes from highest active plate downward with immediate overflow across plates), (3) Base player IHealth. Environmental instakills via PlayerDamagedHandler.TakeFullHpDamage() bypass Bubble Shield and call IArmorService.ClearArmorImmediate() before killing the player.
  - Armor plates do not passively regenerate; RegenativeHealth heals only base player IHealth. Collecting an Armor pickup increments CurrentLevel (up to 3) and restores all unlocked plates (1..CurrentLevel) to full capacity.
  - Fire Tails ground hazards and fire drift trails emit exclusively while the car is both grounded and drifting (ICarController.IsGrounded && ICarController.IsDrifting). Driving straight displays only the constant rear-wheel hub flames.
  - Fire Tails burn DoT and movement slow never stack across multiple overlapping FireHazardNode spheres; bosses take full burn DoT but are immune to movement slow.
  - All five services must clean up active state, DOTween sequences, post-processing weights, overlays, speed multipliers, and health bar overrides/plates in OnDisable() and OnDestroy().
- Ordering or sequencing guarantees:
  - NukeEffectService, TimeFreezeService, BubbleShieldService, ArmorService, and FireTailsService validate configs and initialize buffers/visuals in Awake(), and subscribe to ICollectibleDropNotifier.OnPowerUpCollected in Start().
  - NukeEffectService defers ExecuteNuke() from the OnTriggerEnter pickup callback to the next Update() frame.
  - On Time Freeze end, TimeFreezeService sets _isFrozen = false and updates _accumulatedFrozenSeconds before invoking OnFrozenChanged(false) and resuming paused tweens, ensuring listeners querying IsFrozen or UnfrozenTime inside the callback observe the updated unfrozen state.
- Constraints contributors must preserve:
  - Do not use LINQ or allocate per-frame collections in Nuke frustum checks, Time Freeze tween pausing, Bubble Shield updates, Armor absorption, or Fire Tails hazard scans; reuse preallocated buffers (_frustumPlanes, _activeEnemiesBuffer, _engagedBossesBuffer, _pausedTweens, _tweenScratch, _currentPlateHealth, _hazardNodes, _overlapBuffer, _touchedDamageablesThisScan, _slowedMoversThisScan).
  - Do not use DOTween integer ID 0x46525A31 (TimeFreezeConstants.FREEZABLE_TWEEN_ID) for non-freezable tweens, and do not overwrite the ID of a tween after calling .AsFreezable().

## Extension Points

- Safe extension areas:
  - Adding a new power-up type:
    1. Append the new identifier to the end of PowerUpType in Assets/Scripts/PowerUps/PowerUpType.cs.
    2. Author a dedicated ConfigSO under Assets/Scripts/PowerUps/Config/ with OnValidate clamping and a fail-fast ValidateConfiguration() method.
    3. Create a scene service under a new subfolder in Assets/Scripts/PowerUps/ that injects ICollectibleDropNotifier and subscribes to OnPowerUpCollected(PowerUpType).
    4. If other systems need to query the new power-up's state, colocate a narrow interface above the service and bind it in DefaultGameplaySceneInstaller.InstallBindings.
    5. Create a PowerUpCollectible prefab assigned with the new PowerUpType, register it in GameplayDevCommandsRegistrar for dev console testing, and add it to EnemyDropHandler drop tables on enemy prefabs.
  - Making a new enemy or boss tween pause during Time Freeze:
    1. Import Assets.Scripts.PowerUps.Freeze and chain .AsFreezable() onto the Tween or Sequence creation call (provided the caller does not overwrite tween.id for target-based DOKill).
  - Tuning power-up balance and presentation:
    1. Adjust Nuke damage percentage, frustum padding, and flash timings in Assets/ScriptableObjects/Collectibles/PowerUps/NukeConfig.asset.
    2. Adjust Freeze duration and fade windows in Assets/ScriptableObjects/Collectibles/PowerUps/FreezeTimerConfig.asset.
    3. Adjust Bubble Shield duration, warning window, blink period, alpha levels, and color in Assets/ScriptableObjects/Collectibles/PowerUps/BubbleShieldConfig.asset.
    4. Adjust Armor plate capacities ([20, 40, 60]), plate color, and horizontal overlap ratio in Assets/ScriptableObjects/Collectibles/PowerUps/ArmorConfig.asset.
    5. Adjust Fire Tails active duration, hazard lifetime, spawn spacing, radius, burn damage/tick interval, overlap scan interval, and enemy slow factor in Assets/ScriptableObjects/Collectibles/PowerUps/FireTailsConfig.asset, or shader/material properties in Assets/Materials/FireTailsTrail.mat.
- Required dependencies and contracts:
  - DefaultGameplaySceneInstaller requires serialized references to NukeEffectService, TimeFreezeService, BubbleShieldService, ArmorService, and FireTailsService, binding TimeFreezeService as ITimeFreezeState, BubbleShieldService as IBubbleShield, ArmorService as IArmorService, and FireTailsService as IFireTailsService, alongside CollectibleDropNotifier (bound as both ICollectibleDropNotifier and ICollectibleDropSuppressor), EnemiesSpawner (bound as IActiveEnemiesProvider), BossEncounterService (bound as IBossEncounterService), PlayerManager (bound as IPlayerManager), and MainCamera (bound as Camera).
  - PowerUpCollectible prefabs require a trigger Collider, a PowerUpCollectible component with _type set, and registration in EnemyDropHandler._dropEntries.
- Testing implications:
  - Verify C# compilation:
    dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  - In-game Dev Console commands for spawning power-ups ahead of the player:
    powerup <nuke|freeze|shield|armor|firetails|crate>
  - Play Mode verification checklist (editor-dependent checks; record as unverified unless run in Unity Editor):
    - Picking up Nuke kills only camera-visible enemies, spawns EXP particles without spawning secondary collectible drops, damages visible engaged bosses by 15% MaxHealth, leaves dormant towers unharmed, and plays the blast Volume and dirt overlay fade.
    - Picking up FreezeTimer stops regular enemies, enemy post-spawn/post-relocation teleport grace timers, barrel priming timers/telegraphs, wave/swarm countdowns, Golem/Mortar Tower/Railgun Tower state machines, and active Fire Tails timers/particles for 5s; re-picking while frozen resets the timer to full duration and restores peak frost presentation.
    - Picking up BubbleShield displays the Fresnel shield sphere, overrides the player health bar to dark blue, absorbs enemy and boss combat hits with absorb SFX/VFX without depleting Armor plates, blinks both the bubble and health bar in the final 2s, and still allows DeathVolume out-of-bounds falls to kill the player.
    - Picking up Armor stacks up to 3 plates (+20, +40, +60 HP) extending to the screen-right of the player's world-space health bar at the same vertical height with newer plates layered over older plates; taking combat damage depletes plates from outer right inward and overflows excess damage across lower plates and into base HP.
    - Picking up FireTails ignites constant rear-wheel flames for 8s; drifting suppresses black tire skid marks and emits dual glowing molten fire ribbons (Custom/FireTailsTrail shader) and lingering world-space flames that deal non-stacking 25 HP burn ticks every 0.5s to enemies/bosses and slow regular enemies by 40%.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs supplies pickup notifications (ICollectibleDropNotifier.OnPowerUpCollected) and drop suppression state (ICollectibleDropSuppressor).
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs supplies the active enemy snapshot via IActiveEnemiesProvider.CopyActiveEnemies.
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs supplies currently engaged bosses via IBossEncounterService.CollectEngagedBosses.
  - Assets/Scripts/Player/PlayerManager.cs supplies player liveness state (IPlayerManager.Health), driving/drift state (IPlayerManager.CarController), and rear wheel trail/particle VFX control (IPlayerManager.CarVfxEffectsController).
  - UnityEngine.Camera (MainCamera) supplies frustum planes for Nuke visibility culling.
- Downstream consumers:
  - Assets/Scripts/Enemies/EnemyDropHandler.cs checks ICollectibleDropSuppressor.AreDropsSuppressed before rolling drops on enemy death.
  - Assets/Scripts/Player/PlayerDamagedHandler.cs checks IBubbleShield.IsActive and IArmorService.HasArmor / AbsorbDamage before applying combat damage, and calls IArmorService.ClearArmorImmediate on TakeFullHpDamage.
  - Assets/Scripts/HealthSystem/HealthBar.cs renders the shield color override via SetColorOverride and ClearColorOverride.
  - Assets/Scripts/HealthSystem/ArmorPlatesView.cs and Assets/Scripts/HealthSystem/ArmorPlateItemView.cs render the 3 cascading armor plates next to the player's HealthBar.
  - Assets/Scripts/Player/Car/CarVfxEffectsController.cs switches drift trail rendering between standard tire tracks and fire trail pools and controls rear-wheel / world-space fire particle emission via ICarVfxEffectsController.
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs applies Fire Tails movement speed slow via IMovementController.SetMovementSpeedMultiplier and resets it to 1f on enable/disable/relocation.
  - Assets/Scripts/Enemies/Base/Enemy.cs, EnemyMovementController.cs, EnemyAttackController.cs, EnemyAnimator.cs, Assets/Scripts/Enemies/Barrel/BarrelEnemyExplosionController.cs, Assets/Scripts/Waves/WaveManager.cs, Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs, Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs, Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs, Assets/Scripts/Enemies/Bosses/Towers/TowerBossBase.cs, Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs, Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/RailgunTowerBoss.cs, and Assets/Scripts/PowerUps/FireTails/FireTailsService.cs consume ITimeFreezeState.
  - Barrel feedback, Golem attack states/projectiles/hitboxes, TowerBossBase recoil tweens, Mortar Tower shells/boulder hitboxes, Railgun Tower orbs/linear waves, and circular/rectangular/arc telegraph indicators consume TimeFreezeExtensions.AsFreezable or TimeFreezeConstants.FREEZABLE_TWEEN_ID.
- Cross-system coupling risks:
  - DOTween allows only a single object ID (tween.id) per tween. Any system that chains .AsFreezable() cannot also assign a custom tween.SetId(...) without overwriting FREEZABLE_TWEEN_ID and breaking Time Freeze pause/resume.
  - NukeEffectService relies on EnemiesSpawner._activeEnemies and BossEncounterService._registeredTowers / _activeGolemInstance staying accurately synchronized with entity lifecycles.
  - ArmorPlatesView relies on HealthBarCanvas using FaceMainCameraDirection (where local -X maps to screen-right); changing HealthBarCanvas rotation conventions would invert plate layout direction.

## Known Risks and Open Questions

- Known limitations:
  - MortarShellProjectile.LaunchParabolic (Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs) imports Assets.Scripts.PowerUps.Freeze, but its _flightTween and _releaseTween do not call .AsFreezable(). In addition, MortarDiagonalBounceState uses WaitForSeconds in its salvo coroutines without gating on ITimeFreezeState.IsFrozen. As a result, already-launched mortar shells continue flying/detonating and diagonal bounce salvo intervals continue ticking during Time Freeze even while their ground telegraph indicators are paused.
  - TimeFreezeService.PauseFreezableTweens() snapshots and pauses playing freezable tweens only at the moment Time Freeze transitions from unfrozen to frozen. If any tween tagged with .AsFreezable() is started while IsFrozen is already true (for example from an un-gated coroutine or callback), it will not be paused automatically.
  - GolemLinearAttackHitbox and MortarBoulderHitbox pause their movement sequences (.AsFreezable()) and OnUpdate overlap polling during Time Freeze, but their trigger BoxColliders remain enabled and their OnTriggerEnter methods do not check ITimeFreezeState.IsFrozen. If the player car drives into a stationary frozen boulder or linear fist hitbox, contact damage is still applied. Additionally, MortarBoulderHitbox._rotationTween (visual spin loop) is not marked .AsFreezable().
  - PowerUpConstants defines SFX_PICKUP_NUKE, SFX_PICKUP_FREEZE, SFX_PICKUP_SHIELD, SFX_PICKUP_FIRE_TAILS, SFX_ARMOR_START, MAX_EFFECT_WEIGHT, and MIN_EFFECT_WEIGHT, which are currently unused in runtime code (each service plays its active start/pickup/blast SFX key directly).
  - NukeEffectService tracks pending execution with a single boolean (_isPendingExecution); collecting two Nuke pickups within the same frame executes only a single Nuke pass on the next Update.
- Open design questions:
  - Should in-flight MortarShellProjectile trajectories and MortarDiagonalBounceState salvo coroutines pause during Time Freeze to stay synchronized with their paused CircularTelegraphIndicator visuals?
  - Should stationary boss hitboxes (MortarBoulderHitbox, GolemLinearAttackHitbox) suppress OnTriggerEnter damage while ITimeFreezeState.IsFrozen is true, or remain contact hazards if the player drives into them?
  - Should HUD UI display active countdown icons/bars for Freeze Timer, Bubble Shield, and Fire Tails durations?
- Suggested follow-up tasks:
  - Chain .AsFreezable() onto MortarShellProjectile._flightTween and _releaseTween, and gate MortarDiagonalBounceState coroutine waits on ITimeFreezeState.IsFrozen if full projectile freeze parity is desired.
  - Chain .AsFreezable() onto MortarBoulderHitbox._rotationTween and optionally guard OnTriggerEnter with ITimeFreezeState.IsFrozen in MortarBoulderHitbox and GolemLinearAttackHitbox.
  - Pause newly created freezable tweens immediately inside TimeFreezeExtensions.AsFreezable if TimeFreezeService is currently frozen, or provide a registration helper.
  - Tune enemy drop table percentages on enemy prefabs in the Unity Editor to include Armor.prefab and FireTails.prefab alongside existing power-up drops.
