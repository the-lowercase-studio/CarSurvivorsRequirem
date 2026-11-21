# Damage Numbers System Documentation

## Purpose

The Damage Numbers system displays short-lived world-space text feedback when enemies or bosses take damage. It owns popup creation, threshold-based visual selection, boss popup scale multiplication, immediate and continuous camera-facing orientation, sine/cosine movement and scale interpolation, lifetime release, pooling, and the player setting that enables or disables popups.

It does not calculate damage, change health values, choose combat targets, play blood VFX, or own enemy/boss death flow. Those behaviors remain with combat, health, enemy, boss, and VFX systems.

## Reading Map

- Primary code locations:
  - Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs
  - Assets/Scripts/DamageNumbers/DamageNumber.cs
  - Assets/Scripts/DamageNumbers/DamageNumberApearance.cs
  - Assets/Scripts/DamageNumbers/Constants/DamageNumberConstants.cs
  - Assets/Scripts/Effects/FaceMainCameraDirection.cs
- Integration points:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
  - Assets/Scripts/Enemies/Bosses/Towers/TowerBossBase.cs
  - Assets/Scripts/Enemies/Bosses/Towers/RailgunTower/RailgunTowerBoss.cs
  - Assets/Scripts/StatusEffects/IDamageable.cs
  - Assets/Scripts/StatusEffects/EntityManipulationHelper.cs
  - Assets/Scripts/ReflexDI/BootLoader.cs
  - Assets/Scripts/Settings/DamageNumbersSetting.cs
  - Assets/Scripts/UI/Settings/DamageNumbersOption.cs
  - Assets/Scripts/Spawners/WorldSpace/IInWorldSpaceSpawner.cs
  - Assets/Scripts/ObjectLifecycle/Actions/IEnableDisableFunctionalityTrigger.cs
- Automated tests:
  - Assets/Scripts/Editor/Tests/BossDamageNumbersTests.cs
- Related docs:
  - .agents/context/game-systems/settings-system.md
  - .agents/context/game-systems/status-effects-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related skills or instructions:
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md
  - .agents/skills/preserve-coding-standards/SKILL.md

## Architecture and Data Flow

- Core components:
  - DamageNumbersSpawner is a persistent Boot-scene MonoBehaviour registered through Reflex (BootLoader.InstallExtra) as both IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> and IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>.
  - DamageNubmersSpawnerConfig (note the preserved spelling typo DamageNubmersSpawnerConfig) carries the float Damage value, ShapeModes SpawnShapeMode, and optional bool IsBoss (default false, supporting both 2-argument and 3-argument Deconstruct).
  - DamageNumber is the pooled popup instance. It receives DamageNumberConfig, writes TextMeshPro text/color/font size, computes TargetScale = Vector3.one * growScaleMultiplier, interpolates world position and sine-curve localScale over _duration in Update(), and raises OnLifeEnd upon completion.
  - DamageNumberApearance (note the preserved spelling typo DamageNumberApearance) stores the visual values used by a popup: font size, grow scale multiplier (GrowFontSizeAnimationScaleMultiplier), and color.
  - FaceMainCameraDirection rotates popup child text toward the active Camera in LateUpdate(), and exposes Initialize(Camera) and SnapToTargetRotation() so newly spawned popups align immediately on frame 0 without starting 180 degrees mirrored.
  - DamageNumbersSetting stores and loads the enabled state through AppStorage.
  - DamageNumbersOption binds the setting to a UI Toggle.
- Key interfaces:
  - IInWorldSpaceSpawner<TSelf, TSpecificConfig> is the spawning contract used by enemy and boss damage handling.
  - IEnableDisableFunctionalityTrigger<T> is the settings contract used to enable or disable popup spawning.
  - IInitializable<DamageNumberConfig> is used by pooled DamageNumber instances.
  - IPositionalDamageable (extending IDamageable in Assets/Scripts/StatusEffects/IDamageable.cs) provides TakeDamage(float damage, Vector3 hitWorldPosition) so multi-collider bosses (such as RailgunTowerBoss) can spawn damage numbers at the attacked support leg rather than in mid-air at the root origin.
- Centralized constants:
  - DamageNumberConstants (Assets/Scripts/DamageNumbers/Constants/DamageNumberConstants.cs):
    - RESIZING_ANIMATION_SPEED = 0.6f
    - DEFAULT_POOL_CAPACITY = 20
    - MAX_POOL_SIZE = 100
    - DEFAULT_POPUPS_MOVEMENT_RANGE = 1f
    - DEFAULT_SCALE_MULTIPLIER = 1f
    - BOSS_SCALE_MULTIPLIER = 1.5f
- Runtime flow:
  - BootLoader.InstallExtra calls _damageNumbersSpawner.Initialize(gameplaySceneInstaller.MainCamera) whenever a gameplay scene container builds and registers the DamageNumbersSpawner instance into each scene container.
  - DamageNumbersSpawner.Initialize(Camera mainCamera) stores _mainCamera and immediately updates all cached FaceMainCameraDirection[] arrays in _cameraFacingEffectsByDamageNumber so pooled and active popups receive the new scene Camera after scene reloads (such as NextStagePortal stage transitions or run restarts).
  - Regular enemies (Enemy.TakeDamage) spawn damage numbers at the blood VFX position with isBoss: false. Bosses (GolemBoss.TakeDamage and TowerBossBase.TakeDamage) pass isBoss: true. RailgunTowerBoss overrides TowerBossBase.GetDamageNumberSpawnPosition(Vector3? hitWorldPosition) to select the closest leg BoxCollider in _legColliders via oriented box distance (falling back to PlayerPosition when hitWorldPosition is omitted, such as Nuke) and anchor the popup to that support leg at DamageNumberYOffset height.
  - DamageNumbersSpawner.Spawn exits early when popups are disabled.
  - The spawner fetches a DamageNumber from _damageNumberPool, sets its world position, and calls OrientDamageNumberToCamera(damageNumber), which resets root rotation to Quaternion.identity, applies _mainCamera to the cached FaceMainCameraDirection components, and calls SnapToTargetRotation() so the popup faces the camera immediately before rendering its first frame.
  - The spawner picks the last serialized threshold entry whose Treshold (preserved spelling) is less than or equal to the incoming damage from _visualApearanceByDamageTresholds. If none matches, the popup is immediately released back to the pool.
  - When specificConfig.IsBoss is true, the spawner multiplies the copied DamageNumberApearance.GrowFontSizeAnimationScaleMultiplier by _bossDamagePopupScaleMultiplier (defaulting to DamageNumberConstants.BOSS_SCALE_MULTIPLIER = 1.5f).
  - DamageNumber.Initialize(config, pos, dest, _damagePopupVisibilityDuration) sets TextMeshPro text ("{0:0}"), color, and fontSize, sets TargetScale = Vector3.one * growScaleMultiplier, resets transform.localScale = Vector3.zero, and enables Update() interpolation.
  - Each frame in DamageNumber.Update(), normalized time t = Clamp01(_elapsedTime / duration) drives cosine-eased position interpolation (positionT = -(Cos(PI * t) - 1) * 0.5) and sine-curve scale (TargetScale * Sin(t * PI)). When t >= 1f, _isAnimating clears, localScale resets to Vector3.zero, and OnLifeEnd fires.
  - DamageNumbersSpawner.DamageNumber_OnLifeEnd unsubscribes, decrements CurrentlySpawnedObjectsCount, releases the popup back to _damageNumberPool, and raises OnSpawnedEntityReleased.

## Rules and Invariants

- The DI registration in BootLoader is the authoritative access path. Do not replace it with singleton access or scene searches.
- Damage numbers are spawned before health is reduced in Enemy.TakeDamage, GolemBoss.TakeDamage, and TowerBossBase.TakeDamage; preserve this ordering unless the gameplay meaning of hit feedback changes intentionally.
- _visualApearanceByDamageTresholds must contain at least one inspector entry or the spawner logs an error in Awake().
- Threshold ordering matters. FindCorrectVisualApearanceByTreshold iterates from the end of the serialized array backward, so designer-authored entries must be sorted from lowest to highest threshold for expected "highest matching threshold wins" behavior.
- If no threshold entry has Treshold <= damage, the fetched popup is released immediately without incrementing CurrentlySpawnedObjectsCount.
- Boss damage scaling mutates a local struct copy of DamageNumberApearance (appearance.GrowFontSizeAnimationScaleMultiplier *= bossScale) before passing it to DamageNumber.Initialize, leaving the serialized threshold table unmutated for subsequent normal enemy hits.
- Because DamageNumber.prefab's child Text has a local Y rotation of 180 degrees and FaceMainCameraDirection uses smoothed rotation in LateUpdate, DamageNumbersSpawner.Spawn must always call OrientDamageNumberToCamera (which calls SnapToTargetRotation()) so popups never spawn facing 180 degrees away from the camera and spin during early frames.
- DamageNumbersSpawner.Initialize(Camera) must propagate the new scene Camera reference to all existing entries in _cameraFacingEffectsByDamageNumber so pooled popups surviving across DontDestroyOnLoad scene reloads never retain a destroyed Camera reference.
- DamageNumbersSpawner.DisableFunctionality suppresses new popups only. It does not cancel or release already active popups.
- Pool release depends on DamageNumber.OnLifeEnd; any lifecycle change must still raise this event exactly once per successful spawn.

## Extension Points

- Safe extension areas:
  - Add new visual threshold entries through the serialized DamageNumbersSpawner array.
  - Tune popup visibility duration, movement radius, boss scale multiplier (_bossDamagePopupScaleMultiplier), prefab, font size, grow multiplier, and color through inspector data.
  - Implement IPositionalDamageable on any new multi-collider or large targetable entity that needs hit-location-aware damage number anchoring.
  - Add new ShapeModes only if DamageNumbersSpawner.GetDestinationBasedOnSpawnShapeMode is updated with explicit behavior for the new mode.
  - Add new consumers through IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> instead of referencing the concrete spawner directly.
- Required dependencies and contracts:
  - Scene setup must provide a DamageNumbersSpawner reference in BootLoader.
  - The popup prefab must contain a DamageNumber component with a valid TextMeshPro reference and child FaceMainCameraDirection component.
  - Settings UI requires ISetting<DamageNumbersSetting, bool> and the spawner's enable/disable interface to be bound in the active container.
- Testing implications:
  - Compile after C# edits with dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false and dotnet build Assembly-CSharp-Editor.csproj -p:BuildProjectReferences=false.
  - Run EditMode unit tests in Assets/Scripts/Editor/Tests/BossDamageNumbersTests.cs covering normal vs 1.5x boss scaling, pooled popup reuse across normal and boss hits, RailgunTowerBoss left/right leg hit positioning (including rotated tower transforms and PlayerPosition fallback), MortarTowerBoss and GolemBoss boss flag propagation, immediate camera-facing orientation on spawn without mirroring, and pooled camera reference updates across scene reloads.

## Integration Notes

- Upstream dependencies:
  - Enemy.TakeDamage, GolemBoss.TakeDamage, and TowerBossBase.TakeDamage (including MortarTowerBoss and RailgunTowerBoss) are the gameplay producers of damage number requests.
  - EntityManipulationHelper.Damage, Projectile, MinigunTurret, LasergunTurret, SawBlade, and FireTailsService forward world hit positions to IPositionalDamageable targets.
  - DamageNumbersOption and DamageNumbersSetting control the enabled state from player settings.
  - BootLoader provides the spawner instance and active scene MainCamera to Reflex scene containers.
- Downstream consumers:
  - Pooling/counting consumers can observe OnSpawnedEntityReleased through the IObjectReleaseNotifier part of IInWorldSpaceSpawner.
  - Designers interact with the serialized spawner configuration and popup prefab.
- Cross-system coupling risks:
  - Moving DamageNumbersSpawner registration out of BootLoader can break enemy/boss injection and settings injection together.
  - Changing the popup event lifecycle can leak event subscriptions or leave pooled objects active.
  - The serialized type and field names currently include preserved spelling mistakes such as DamageNubmersSpawnerConfig, DamageNumberApearance, Treshold, and _visualApearanceByDamageTresholds; renaming them affects code references and serialized data compatibility.

## Known Risks and Open Questions

- Known limitations:
  - DamageNumber.IsInitialized() remains true after the first initialization and is not reset on pool release (_isAnimating in Update() controls active interpolation).
  - _popupsSpeedRange is serialized on DamageNumbersSpawner but not used by the current Update() movement interpolation, which uses _damagePopupVisibilityDuration directly.
- Open design questions:
  - Should critical hits, healing, shields, or resisted damage use separate appearance rules?
  - Should popup placement be deterministic for tests or replay-like features?
- Suggested follow-up tasks:
  - Decide whether to migrate misspelled type and field names with Unity serialization compatibility safeguards.
  - Either use _popupsSpeedRange for movement timing/randomization or remove it in a dedicated cleanup.
