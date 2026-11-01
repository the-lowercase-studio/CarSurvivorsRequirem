# Power-Ups System Documentation

## Purpose

The Power-Ups system coordinates rare, tactical drop pickups (Nuke, Freeze Timer, Bubble Shield) dropped by enemies, executes their timed or instant mechanical effects, controls global post-processing volumes and screen overlays, and synchronizes combat feedback across enemies, bosses, spawners, and player survivability.

It is responsible for:
- Providing pooled power-up pickup entities (Assets/Scripts/PowerUps/Collectibles/PowerUpCollectible.cs) identified by append-only enum values (Assets/Scripts/PowerUps/PowerUpType.cs).
- Supplying a shared scaled-time countdown helper for timed effects (Assets/Scripts/PowerUps/PowerUpTimer.cs).
- Coordinating deferred Nuke screen-clear damage against camera-visible regular enemies and engaged bosses while suppressing secondary collectible drops (Assets/Scripts/PowerUps/Nuke/NukeEffectService.cs).
- Enforcing global Time Freeze across enemy locomotion, attack controllers, animators, wave and swarm spawners, boss state machines, freezable DOTween sequences, and telegraph indicators (Assets/Scripts/PowerUps/Freeze/TimeFreezeService.cs, Assets/Scripts/PowerUps/Freeze/TimeFreezeExtensions.cs).
- Managing combat damage immunity, 3D Fresnel bubble presentation, and synchronized warning-window blinking on the bubble and player health bar via Bubble Shield (Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs, Assets/Scripts/PowerUps/Shield/BubbleShieldVisualController.cs, Assets/Shaders/BubbleShield.shader).
- Driving full-screen CanvasGroup overlay alpha fades for Nuke dirt and Freeze frost effects (Assets/Scripts/PowerUps/Presentation/ScreenOverlayPresenter.cs).
- Exposing Reflex DI contracts for cross-system queries: ITimeFreezeState and IBubbleShield.

It is not responsible for:
- Rolling enemy drop tables, resolving walkable grid landing cells, or pooling/scattering collectible GameObjects (handled by Assets/Scripts/Enemies/EnemyDropHandler.cs and Assets/Scripts/Enemies/CollectibleDropNotifier.cs).
- Managing skill upgrades or skill crates (handled by the collectibles and skills systems).
- Granting permanent player stats or experience (handled by the level and stats systems).
- Enforcing lethal arena boundary / void kills via TakeFullHpDamage, which intentionally bypass Bubble Shield (handled by Assets/Scripts/Player/PlayerDamagedHandler.cs and DeathVolume).
- Audio or VFX playback backends (handled by Assets/Scripts/Audio/AudioClipPlayer.cs and Assets/Scripts/VFX/VFXPlayer.cs).

## Reading Map

- Primary code locations:
  - Assets/Scripts/PowerUps/PowerUpType.cs
  - Assets/Scripts/PowerUps/PowerUpTimer.cs
  - Assets/Scripts/PowerUps/Collectibles/PowerUpCollectible.cs
  - Assets/Scripts/PowerUps/Config/NukeConfigSO.cs
  - Assets/Scripts/PowerUps/Config/FreezeTimerConfigSO.cs
  - Assets/Scripts/PowerUps/Config/BubbleShieldConfigSO.cs
  - Assets/Scripts/PowerUps/Constants/PowerUpConstants.cs
  - Assets/Scripts/PowerUps/Constants/TimeFreezeConstants.cs
  - Assets/Scripts/PowerUps/Constants/BubbleShieldShaderConstants.cs
  - Assets/Scripts/PowerUps/Nuke/NukeEffectService.cs
  - Assets/Scripts/PowerUps/Freeze/TimeFreezeService.cs
  - Assets/Scripts/PowerUps/Freeze/TimeFreezeExtensions.cs
  - Assets/Scripts/PowerUps/Presentation/ScreenOverlayPresenter.cs
  - Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs
  - Assets/Scripts/PowerUps/Shield/BubbleShieldVisualController.cs
  - Assets/Shaders/BubbleShield.shader
- Designer-authored data and prefabs:
  - Assets/ScriptableObjects/Collectibles/PowerUps/NukeConfig.asset
  - Assets/ScriptableObjects/Collectibles/PowerUps/FreezeTimerConfig.asset
  - Assets/ScriptableObjects/Collectibles/PowerUps/BubbleShieldConfig.asset
  - Assets/Prefabs/Collectibles/Nuke.prefab
  - Assets/Prefabs/Collectibles/FreezeTimer.prefab
  - Assets/Prefabs/Collectibles/BubbleShield.prefab
- Related runtime integration:
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs (ICollectibleDropNotifier.OnPowerUpCollected, ICollectibleDropSuppressor)
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs (IActiveEnemiesProvider)
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs (IBossEncounterService.CollectEngagedBosses)
  - Assets/Scripts/Enemies/Bosses/IBossCombatTarget.cs
  - Assets/Scripts/Enemies/Base/Enemy.cs
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
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Combat/MortarBoulderHitbox.cs
  - Assets/Scripts/Indicators/CircularTelegraphIndicator.cs
  - Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs
  - Assets/Scripts/Waves/WaveManager.cs
  - Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/HealthSystem/HealthBar.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Related docs:
  - .agents/context/game-systems/collectibles-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/game-systems/health-system.md
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
  - PowerUpType: Enum defining Nuke, FreezeTimer, and BubbleShield. Values are append-only to keep serialized prefab data stable.
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
  - ScreenOverlayPresenter: UI presenter requiring a CanvasGroup (with an optional Image reference) that clamps alpha via SetAlpha and resets to zero via ResetOverlay.
- Key interfaces:
  - IPowerUpCollectible: Extends ICollectible with PowerUpType Type { get; }. Colocated in Assets/Scripts/PowerUps/Collectibles/PowerUpCollectible.cs.
  - ITimeFreezeState: Exposes IsFrozen, UnfrozenTime, and event Action<bool> OnFrozenChanged. Colocated in Assets/Scripts/PowerUps/Freeze/TimeFreezeService.cs.
  - IBubbleShield: Exposes IsActive and NotifyDamageAbsorbed(). Colocated in Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs.
  - ICollectibleDropSuppressor: Exposes AreDropsSuppressed, BeginDropSuppression(), and EndDropSuppression() using a reference-counted depth integer. Colocated in Assets/Scripts/Enemies/CollectibleDropNotifier.cs.
  - IActiveEnemiesProvider: Exposes CopyActiveEnemies(List<Enemy> buffer) on EnemiesSpawner.
  - IBossCombatTarget: Unifies IBossHUDSource and IDamageable for engaged bosses (GolemBoss and MortarTowerBoss).
- Centralized constants:
  - PowerUpConstants (Assets/Scripts/PowerUps/Constants/PowerUpConstants.cs):
    - SFX_PICKUP_NUKE = "PowerUpPickupNuke"
    - SFX_PICKUP_FREEZE = "PowerUpPickupFreeze"
    - SFX_PICKUP_SHIELD = "PowerUpPickupShield"
    - SFX_NUKE_BLAST = "NukeBlast"
    - SFX_FREEZE_START = "FreezeStart"
    - SFX_FREEZE_END = "FreezeEnd"
    - SFX_SHIELD_START = "ShieldStart"
    - SFX_SHIELD_ABSORB = "ShieldAbsorb"
    - SFX_SHIELD_EXPIRE = "ShieldExpire"
    - MAX_EFFECT_WEIGHT = 1f
    - MIN_EFFECT_WEIGHT = 0f
  - TimeFreezeConstants (Assets/Scripts/PowerUps/Constants/TimeFreezeConstants.cs):
    - FREEZABLE_TWEEN_ID = 0x46525A31
  - BubbleShieldShaderConstants (Assets/Scripts/PowerUps/Constants/BubbleShieldShaderConstants.cs):
    - BASE_COLOR_ID = Shader.PropertyToID("_BaseColor")
    - ALPHA_ID = Shader.PropertyToID("_Alpha")
    - FRESNEL_POWER_ID = Shader.PropertyToID("_FresnelPower")
    - RIM_INTENSITY_ID = Shader.PropertyToID("_RimIntensity")
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
       - EnemyMovementController early-returns in FixedUpdate (zeroing _currentVelocity) and blocks CanBeTeleported.
       - Enemy.ApplyKnockBack ignores knockback impulses, and EnemyAttackController ignores player collision triggers.
       - EnemyAnimator sets Animator.speed = 0 and uses Enemy.UnfrozenTime for its 3.5s attack watchdog so frozen time does not trip premature cancellation.
       - BarrelEnemyExplosionController uses Enemy.UnfrozenTime for its 3.5s priming watchdog, while BarrelEnemyAttackFeedback and CircularTelegraphIndicator pause their freezable tweens.
       - WaveManager and SwarmSpawner pause countdowns and coroutine wait loops while _timeFreezeState.IsFrozen is true.
       - GolemBoss and MortarTowerBoss early-return in Update/FixedUpdate, GolemAnimator sets Animator.speed = 0, and boss attack/telegraph sequences tagged with AsFreezable() or FREEZABLE_TWEEN_ID pause in place.
    4. When _timer enters FadeOutDuration, StartFadeOutPresentation() tweens volume weight and frost overlay alpha toward 0 (Ease.InQuad).
    5. When _timer.Tick(Time.deltaTime) expires, EndFreeze(notifyPresentation: true) sets _isFrozen = false, adds elapsed freeze time to _accumulatedFrozenSeconds, stops the timer, fires OnFrozenChanged(false), resumes still-active tweens in _pausedTweens, resets presentation, and plays SFX_FREEZE_END.
  - Bubble Shield Execution Flow:
    1. On initial BubbleShield pickup, BubbleShieldService starts _timer with DurationSeconds, triggers _visualController.Show(AppearDuration, SteadyAlpha), locks the player health bar fill color via _playerHealthBar.SetColorOverride(ShieldColor, 1f), and plays SFX_SHIELD_START.
    2. On re-pickup while active, restarts _timer to DurationSeconds, calls _visualController.RestoreSteadyState(SteadyAlpha), and sets health bar color override blend to 1.
    3. When combat damage hits PlayerDamagedHandler.TakeDamage(damage), it checks _bubbleShield.IsActive; when true, it calls _bubbleShield.NotifyDamageAbsorbed() (playing SFX_SHIELD_ABSORB and _absorbVfxPlayer) and returns before decreasing health, playing damage SFX/VFX, or shaking the car mesh. PlayerDamagedHandler.TakeFullHpDamage() bypasses the shield check so DeathVolume kills remain lethal.
    4. Each Update while active: if the player is no longer alive, DeactivateImmediately() stops the timer, hides the bubble immediately, and clears the health bar override. Otherwise, during the final WarningWindowSeconds, UpdateShieldPresentation() computes blink01 = 0.5f * (1f + Mathf.Cos(cycle * 2 * PI)) and applies both _visualController.SetAlpha(Lerp(WarningMinAlpha, SteadyAlpha, blink01)) and _playerHealthBar.SetColorOverride(ShieldColor, blink01).
    5. When _timer expires, _visualController.Hide(DisappearDuration) scales and fades out the bubble, _playerHealthBar.ClearColorOverride() restores the health gradient, and SFX_SHIELD_EXPIRE plays.

## Rules and Invariants

- Critical behavior rules:
  - PowerUpType enum values (Nuke = 0, FreezeTimer = 1, BubbleShield = 2) are serialized on pickup prefabs and must remain append-only.
  - Required configurations (NukeConfigSO, FreezeTimerConfigSO, BubbleShieldConfigSO) and mechanical scene references (BubbleShieldVisualController, player HealthBar, and all three service references on DefaultGameplaySceneInstaller) must fail fast in Awake() or InstallBindings() with InvalidOperationException if unassigned or invalid (per ADR-005). Optional sensory references (Volume, ScreenOverlayPresenter, VFXPlayer, AudioClipPlayer) may be null-guarded.
  - Nuke kills must always wrap the enemy kill loop in ICollectibleDropSuppressor.BeginDropSuppression() and EndDropSuppression() within a try/finally block so Nuke kills award EXP without triggering chain-reaction collectible drops.
  - Nuke boss damage must only affect bosses returned by IBossEncounterService.CollectEngagedBosses (the active GolemBoss or towers whose EncounterArenaController has IsEncounterActive && !IsEncounterCompleted) and whose padded bounds intersect the camera frustum.
  - Time Freeze must never mutate Unity's global Time.timeScale (which would freeze the player car, player weapons, and UI); instead, it coordinates freeze state through ITimeFreezeState and TimeFreezeConstants.FREEZABLE_TWEEN_ID.
  - Watchdog timers on enemies and bosses that measure elapsed Time.time (such as EnemyAnimator and BarrelEnemyExplosionController 3.5s attack watchdogs) must sample ITimeFreezeState.UnfrozenTime instead of raw Time.time so a 5-second freeze does not trip false watchdog cancellations.
  - Bubble Shield must block all combat damage routed through PlayerDamagedHandler.TakeDamage(float) while allowing environmental instakills routed through PlayerDamagedHandler.TakeFullHpDamage() to kill the player.
  - All three services must clean up active state, DOTween sequences, post-processing weights, overlays, and health bar overrides in OnDisable() and OnDestroy().
- Ordering or sequencing guarantees:
  - NukeEffectService, TimeFreezeService, and BubbleShieldService validate configs and initialize buffers/visuals in Awake(), and subscribe to ICollectibleDropNotifier.OnPowerUpCollected in Start().
  - NukeEffectService defers ExecuteNuke() from the OnTriggerEnter pickup callback to the next Update() frame.
  - On Time Freeze end, TimeFreezeService sets _isFrozen = false and updates _accumulatedFrozenSeconds before invoking OnFrozenChanged(false) and resuming paused tweens, ensuring listeners querying IsFrozen or UnfrozenTime inside the callback observe the updated unfrozen state.
- Constraints contributors must preserve:
  - Do not use LINQ or allocate per-frame collections in Nuke frustum checks, Time Freeze tween pausing, or Bubble Shield update loops; reuse preallocated buffers (_frustumPlanes, _activeEnemiesBuffer, _engagedBossesBuffer, _pausedTweens, _tweenScratch).
  - Do not use DOTween integer ID 0x46525A31 (TimeFreezeConstants.FREEZABLE_TWEEN_ID) for non-freezable tweens, and do not overwrite the ID of a tween after calling .AsFreezable().

## Extension Points

- Safe extension areas:
  - Adding a new power-up type:
    1. Append the new identifier to the end of PowerUpType in Assets/Scripts/PowerUps/PowerUpType.cs.
    2. Author a dedicated ConfigSO under Assets/Scripts/PowerUps/Config/ with OnValidate clamping and a fail-fast ValidateConfiguration() method.
    3. Create a scene service under a new subfolder in Assets/Scripts/PowerUps/ that injects ICollectibleDropNotifier and subscribes to OnPowerUpCollected(PowerUpType).
    4. If other systems need to query the new power-up's state, colocate a narrow interface above the service and bind it in DefaultGameplaySceneInstaller.InstallBindings.
    5. Create a PowerUpCollectible prefab assigned with the new PowerUpType and add it to EnemyDropHandler drop tables on enemy prefabs.
  - Making a new enemy or boss tween pause during Time Freeze:
    1. Import Assets.Scripts.PowerUps.Freeze and chain .AsFreezable() onto the Tween or Sequence creation call (provided the caller does not overwrite tween.id for target-based DOKill).
  - Tuning power-up balance and presentation:
    1. Adjust Nuke damage percentage, frustum padding, and flash timings in Assets/ScriptableObjects/Collectibles/PowerUps/NukeConfig.asset.
    2. Adjust Freeze duration and fade windows in Assets/ScriptableObjects/Collectibles/PowerUps/FreezeTimerConfig.asset.
    3. Adjust Bubble Shield duration, warning window, blink period, alpha levels, and color in Assets/ScriptableObjects/Collectibles/PowerUps/BubbleShieldConfig.asset.
- Required dependencies and contracts:
  - DefaultGameplaySceneInstaller requires serialized references to NukeEffectService, TimeFreezeService, and BubbleShieldService, binding TimeFreezeService as ITimeFreezeState and BubbleShieldService as IBubbleShield, alongside CollectibleDropNotifier (bound as both ICollectibleDropNotifier and ICollectibleDropSuppressor), EnemiesSpawner (bound as IActiveEnemiesProvider), BossEncounterService (bound as IBossEncounterService), PlayerManager (bound as IPlayerManager), and MainCamera (bound as Camera).
  - PowerUpCollectible prefabs require a trigger Collider, a PowerUpCollectible component with _type set, and registration in EnemyDropHandler._dropEntries.
- Testing implications:
  - Verify C# compilation:
    dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  - Play Mode verification checklist (editor-dependent checks; record as unverified unless run in Unity Editor):
    - Picking up Nuke kills only camera-visible enemies, spawns EXP particles without spawning secondary collectible drops, damages visible engaged bosses by 15% MaxHealth, leaves dormant towers unharmed, and plays the blast Volume and dirt overlay fade.
    - Picking up FreezeTimer stops regular enemies, barrel priming timers/telegraphs, wave/swarm countdowns, and Golem/Mortar Tower state machines for 5s; re-picking while frozen resets the timer to full duration and restores peak frost presentation.
    - Picking up BubbleShield displays the Fresnel shield sphere, overrides the player health bar to dark blue, absorbs enemy and boss combat hits with absorb SFX/VFX, blinks both the bubble and health bar in the final 2s, and still allows DeathVolume out-of-bounds falls to kill the player.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs supplies pickup notifications (ICollectibleDropNotifier.OnPowerUpCollected) and drop suppression state (ICollectibleDropSuppressor).
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs supplies the active enemy snapshot via IActiveEnemiesProvider.CopyActiveEnemies.
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs supplies currently engaged bosses via IBossEncounterService.CollectEngagedBosses.
  - Assets/Scripts/Player/PlayerManager.cs supplies player liveness state (IPlayerManager.Health.IsAlive()) to BubbleShieldService.
  - UnityEngine.Camera (MainCamera) supplies frustum planes for Nuke visibility culling.
- Downstream consumers:
  - Assets/Scripts/Enemies/EnemyDropHandler.cs checks ICollectibleDropSuppressor.AreDropsSuppressed before rolling drops on enemy death.
  - Assets/Scripts/Player/PlayerDamagedHandler.cs checks IBubbleShield.IsActive before applying combat damage.
  - Assets/Scripts/HealthSystem/HealthBar.cs renders the shield color override via SetColorOverride and ClearColorOverride.
  - Assets/Scripts/Enemies/Base/Enemy.cs, EnemyMovementController.cs, EnemyAttackController.cs, EnemyAnimator.cs, Assets/Scripts/Enemies/Barrel/BarrelEnemyExplosionController.cs, Assets/Scripts/Waves/WaveManager.cs, Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs, Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs, Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs, and Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs consume ITimeFreezeState.
  - Barrel feedback, Golem attack states/projectiles/hitboxes, Mortar Tower recoil/boulder hitboxes, and telegraph indicators consume TimeFreezeExtensions.AsFreezable or TimeFreezeConstants.FREEZABLE_TWEEN_ID.
- Cross-system coupling risks:
  - DOTween allows only a single object ID (tween.id) per tween. Any system that chains .AsFreezable() cannot also assign a custom tween.SetId(...) without overwriting FREEZABLE_TWEEN_ID and breaking Time Freeze pause/resume.
  - NukeEffectService relies on EnemiesSpawner._activeEnemies and BossEncounterService._registeredTowers / _activeGolemInstance staying accurately synchronized with entity lifecycles.

## Known Risks and Open Questions

- Known limitations:
  - MortarShellProjectile.LaunchParabolic (Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs) imports Assets.Scripts.PowerUps.Freeze, but its _flightTween and _releaseTween do not call .AsFreezable(). In addition, MortarDiagonalBounceState uses WaitForSeconds in its salvo coroutines without gating on ITimeFreezeState.IsFrozen. As a result, already-launched mortar shells continue flying/detonating and diagonal bounce salvo intervals continue ticking during Time Freeze even while their ground telegraph indicators are paused.
  - TimeFreezeService.PauseFreezableTweens() snapshots and pauses playing freezable tweens only at the moment Time Freeze transitions from unfrozen to frozen. If any tween tagged with .AsFreezable() is started while IsFrozen is already true (for example from an un-gated coroutine or callback), it will not be paused automatically.
  - GolemLinearAttackHitbox and MortarBoulderHitbox pause their movement sequences (.AsFreezable()) and OnUpdate overlap polling during Time Freeze, but their trigger BoxColliders remain enabled and their OnTriggerEnter methods do not check ITimeFreezeState.IsFrozen. If the player car drives into a stationary frozen boulder or linear fist hitbox, contact damage is still applied. Additionally, MortarBoulderHitbox._rotationTween (visual spin loop) is not marked .AsFreezable().
  - PowerUpConstants defines SFX_PICKUP_NUKE, SFX_PICKUP_FREEZE, SFX_PICKUP_SHIELD, MAX_EFFECT_WEIGHT, and MIN_EFFECT_WEIGHT, which are currently unused in runtime code (each service plays its effect start/blast SFX key directly).
  - NukeEffectService tracks pending execution with a single boolean (_isPendingExecution); collecting two Nuke pickups within the same frame executes only a single Nuke pass on the next Update.
- Open design questions:
  - Should in-flight MortarShellProjectile trajectories and MortarDiagonalBounceState salvo coroutines pause during Time Freeze to stay synchronized with their paused CircularTelegraphIndicator visuals?
  - Should stationary boss hitboxes (MortarBoulderHitbox, GolemLinearAttackHitbox) suppress OnTriggerEnter damage while ITimeFreezeState.IsFrozen is true, or remain contact hazards if the player drives into them?
  - Should HUD UI display active countdown icons/bars for Freeze Timer and Bubble Shield durations?
- Suggested follow-up tasks:
  - Chain .AsFreezable() onto MortarShellProjectile._flightTween and _releaseTween, and gate MortarDiagonalBounceState coroutine waits on ITimeFreezeState.IsFrozen if full projectile freeze parity is desired.
  - Chain .AsFreezable() onto MortarBoulderHitbox._rotationTween and optionally guard OnTriggerEnter with ITimeFreezeState.IsFrozen in MortarBoulderHitbox and GolemLinearAttackHitbox.
  - Pause newly created freezable tweens immediately inside TimeFreezeExtensions.AsFreezable if TimeFreezeService is currently frozen, or provide a registration helper.
