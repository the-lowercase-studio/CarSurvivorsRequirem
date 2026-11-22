# Interactables System Documentation

## Purpose

The Interactables system manages interactive map landmarks and objectives in Car Survivors, including their placement or scene initialization, navigation grid reservation/release, player proximity checks, device-aware UI prompts, interaction and trigger mechanics, DOTween animation sequences, and progression or reward side-effects.

In Car Survivors, map interactables include:
1. Difficulty Totems (IncreaseDifficultyTotem): Proximity-triggered interactable objects that display device-aware PlayerInteract prompts through InputPromptView + IInputPromptProvider and accept input through IGameplayInput.WasInteractPressedThisFrame (E or Gamepad buttonSouth) to permanently boost enemy spawn chance redistribution speed and wave difficulty.
2. Capture Points (CapturePoint): Proximity-driven objective zones that track player car residence within a capture radius, animate expanding/outline ground planes via DOTween, decay progress when the player leaves, swap mesh materials upon acquisition, and reward a skill upgrade upon reaching 100% completion.
3. Golem Statue (GolemStatue): Central scene-placed landmark showing live tower progress ("Towers destroyed (0/2)" -> configurable tier challenge prompt once required towers are defeated), reserving an impassable WorldGrid footprint while dormant, shaking for 1.0s on PlayerInteract press, releasing its grid footprint, pushing the player car clear of the spawn radius if overlapping, and awakening the active tier's Golem Boss in place.
4. Next Stage Portal (NextStagePortal): Dynamic portal spawned at the defeated Golem's ground-snapped position that automatically pulls, spins, and shrinks the living player car into a 1.2s DOTween vortex while locking input and granting invulnerability, then either snapshots build/level/timer state and reloads into the next tier or opens the end-of-run DemoThankYouPresenter on the final tier.
5. Map Interactables Spawner (MapInteractablesSpawner): Scene component that evaluates spatial rules to place random interactables (totems, capture points) on walkable grid cells during scene initialization.

It does not own grid coordinate generation or cell walkability logic (owned by Grid System) or enemy/boss combat state machines.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs
  - Assets/Scripts/Interactables/CapturePoint/CapturePoint.cs
  - Assets/Scripts/Interactables/GolemStatue.cs
  - Assets/Scripts/Interactables/Constants/GolemStatueConstants.cs
  - Assets/Scripts/Interactables/NextStagePortal.cs
  - Assets/Scripts/Interactables/Constants/PortalConstants.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
  - Assets/Scripts/Spawners/Constants/MapInteractablesSpawnerConstants.cs
- Designer-authored prefabs:
  - Assets/Prefabs/Interactables/IncreaseDifficultyTotem.prefab
  - Assets/Prefabs/Interactables/GolemStatue.prefab
  - Assets/Prefabs/NextStagePortal.prefab
- Automated tests:
  - Assets/Scripts/Editor/Tests/IncreaseDifficultyTotemTests.cs
  - Assets/Tests/PlayMode/BootMainMenuGameplaySmokeTests.cs
- Related code:
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/GameFlow/RunSessionService.cs
  - Assets/Scripts/GameFlow/GameSceneLoader.cs
  - Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
- Related docs:
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/enemy-spawning-and-waves-system.md
  - .agents/context/game-systems/spawners-system.md
  - .agents/context/game-systems/tower-boss-system.md
  - .agents/context/game-systems/golem-boss-system.md
  - .agents/context/game-systems/game-flow-system.md
  - .agents/context/game-systems/input-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - MapInteractablesSpawner: Scene-bound spawner that validates _gridManager/WorldGrid and _playerManager/GameObject (failing fast with InvalidOperationException when missing), optionally resolves IBossEncounterService, and reads InteractableSpawnRule configs to place interactables at scene start. Filters walkable grid cells against deterministic initial player chunk bounds (matching GridManager with playerInitialCell fallback), excludes cells inside registered tower encounter arenas plus _towerArenaExclusionBuffer (MapInteractablesSpawnerConstants.DEFAULT_TOWER_ARENA_EXCLUSION_BUFFER = 5f, negative-clamped), shuffles candidates, checks spatial distance rules (distance to impassable cells, distance to other interactables, distance to same type), instantiates prefabs, and injects dependencies via Reflex (GameObjectInjector.InjectRecursive).
  - InteractableSpawnRule: Serialized configuration holding prefab references, min/max spawn counts, and distance constraints.
  - IncreaseDifficultyTotem: Interactive totem component. Validates injected IGameplayInput, IInputPromptProvider, and _interactPromptView in Start() (after GameObjectInjector.InjectRecursive completes), binds InputActionId.PlayerInteract prompt updates, measures player distance, displays an interaction Canvas prompt when in range, polls IGameplayInput.WasInteractPressedThisFrame, calls IEnemySpawnDifficultyController.IncreaseSpawnChanceRedistributionFactor, triggers VFX, and deactivates itself.
  - CapturePoint: Proximity-based area objective. Tracks player presence inside _captureRadius using squared distance (sqrMagnitude), scales an expanding ground circle plane, triggers outline plane pop-in/pulse/shrink animations via DOTween, decays progress when the player exits, swaps target mesh materials upon 100% capture, plays capture VFX, and queues a random skill upgrade request via ISkillUpgradeFlow.
  - GolemStatue (implementing IGolemStatue): Scene-placed central landmark on Layer 17 (Interactables) with a blocking collider on TerrainLayers.Impassable. Validates injected IBossEncounterService, IGameplayInput, IInputPromptProvider, and serialized _visualRoot, _impassableCollider, _statusText, _interactionCanvas, and _interactPromptView fail-fast in Awake(). In Start(), binds InputActionId.PlayerInteract, reserves its padded WorldGrid footprint (Cost = byte.MaxValue), registers with IBossEncounterService.RegisterGolemStatue(this), and subscribes to IBossEncounterService.OnTowerDefeated to update _statusText from active tier StatueProgressFormat ("Towers destroyed ({0}/{1})") to StatueChallengePrompt ("Challenge first Titan" in Tier 1, "Challenge ascended Titan" in Tier 2) once DefeatedTowersCount >= RequiredTowersCount. When unlocked and the player is within _interactionRadius (default 6.0m), shows _interactionCanvas and polls IGameplayInput.WasInteractPressedThisFrame. On press, plays a 1.0s DOTween shake on _visualRoot (DOShakePosition + DOShakeRotation), disables _impassableCollider, calls cell.ResetCosts() on reserved cells, deactivates itself, pushes the player car outside _golemSpawnClearanceRadius (default 2.5m) if overlapping, and calls _bossEncounterService.SpawnGolemBoss(spawnPosition). Exposes DismissForExternalSpawn() so dev console golem spawns dismiss the statue and release its grid cells cleanly.
  - NextStagePortal (implementing INextStagePortal): Trigger-collider portal instantiated by BossEncounterService on Golem defeat via ReflexPrefabInstantiator.InstantiateInjectedObject. Injects IRunSessionService, IGameSceneLoader, IPlayerManager, ITimerPresenter, IInputContexts, IDemoThankYouPresenter, and ISkillUpgradePresenter. On player trigger enter (verifying EntityLayers.Player or parent IPlayerManager and _playerManager.Health.IsAlive()), calls _skillUpgradePresenter?.HideForStageTransition(), acquires InputContextScopeKind.Dying, pauses ITimerPresenter, enables _playerManager.DamagedHandler.SetPortalTransitionInvulnerable(true), deactivates the player SkillsRegistry GameObject, zeroes velocities and sets the player Rigidbody kinematic, and runs a joined DOTween vortex sequence (DOMove to portal center + PortalConstants.VORTEX_CENTER_Y_OFFSET, DORotate by PortalVortexSpins * 360 deg, DOScale to 0.01 over PortalVortexDuration = 1.2s). Upon completion, releases the input scope and branches on IRunSessionService.IsFinalTier: non-final tiers call CaptureSnapshotAndAdvanceTier and reload/load the next tier scene via IGameSceneLoader; final tier calls CaptureFinalRunTimer and opens IDemoThankYouPresenter.EnableThankYouScreen().
- Key interfaces:
  - IGolemStatue: Colocated in Assets/Scripts/Interactables/GolemStatue.cs. Exposes DismissForExternalSpawn().
  - INextStagePortal: Colocated marker interface in Assets/Scripts/Interactables/NextStagePortal.cs.
  - IPlayerManager: Injected dependency used by interactables to query player position, health liveness, Rigidbody, DamagedHandler, and SkillsRegistry.
  - IEnemySpawnDifficultyController: Injected into IncreaseDifficultyTotem to modify enemy spawn chance weights.
  - ISkillUpgradeFlow: Injected into CapturePoint to queue skill upgrades upon capture completion.
  - IGridManager: Injected into MapInteractablesSpawner and GolemStatue to fetch walkable cells, reserve/release footprint cells, and read player chunk geometry.
  - IBossEncounterService: Injected into GolemStatue (required) and resolved optionally by MapInteractablesSpawner to exclude tower boss encounter arenas via IsPositionWithinAnyTowerArena.
- Centralized constants:
  - GolemStatueConstants (Assets/Scripts/Interactables/Constants/GolemStatueConstants.cs):
    - AWAKEN_SHAKE_DURATION = 1.0f
    - AWAKEN_SHAKE_POSITION_STRENGTH = 0.25f
    - AWAKEN_SHAKE_ROTATION_STRENGTH = 6.0f
    - AWAKEN_SHAKE_VIBRATO = 18
    - DEFAULT_INTERACTION_RADIUS = 6.0f
    - DEFAULT_GOLEM_CLEARANCE_RADIUS = 2.5f
    - FOOTPRINT_PADDING = 0.25f
  - PortalConstants (Assets/Scripts/Interactables/Constants/PortalConstants.cs):
    - VORTEX_CENTER_Y_OFFSET = 0.5f
  - MapInteractablesSpawnerConstants (Assets/Scripts/Spawners/Constants/MapInteractablesSpawnerConstants.cs):
    - DEFAULT_TOWER_ARENA_EXCLUSION_BUFFER = 5f
- Runtime flow:
  - Spawning: On scene Start() (after EncounterArenaController.OnEnable() has registered tower arenas and IGridManager has built grid cost fields), MapInteractablesSpawner fetches walkable cells from IGridManager.WorldGrid, excludes the deterministic initial player chunk and any tower arena + _towerArenaExclusionBuffer, shuffles candidates, filters using rules, instantiates interactable prefabs, and recursively injects Reflex dependencies via GameObjectInjector.InjectRecursive.
  - Totem Interaction: IncreaseDifficultyTotem.Update measures distance to player. When within _interactionRadius, it displays _interactionCanvas. If IGameplayInput.WasInteractPressedThisFrame is true, it increases difficulty factor, hides canvas, triggers VFX, and sets enabled = false.
  - Capture Point Execution: CapturePoint.Update measures squared distance to player. While inside radius, _progress increases over _captureDurationSeconds and _expandingCirclePlane scales up. An outline circle plane pops in with Ease.OutBack. If the player leaves, progress decays based on _decaySpeedMultiplier and the outline scales down. Upon reaching 1.0 progress, _isCaptured locks, circle planes shrink to zero, target mesh materials swap, a skill upgrade request is queued via ISkillUpgradeFlow.QueueRandomSkillUpgradeRequest, capture VFX plays, and CapturePoint sets enabled = false.
  - Golem Statue Awakening & Portal Transition: GolemStatue tracks tower defeats via IBossEncounterService.OnTowerDefeated. Once all required towers are defeated, approaching within _interactionRadius reveals the device-aware PlayerInteract prompt. Pressing Interact runs the 1.0s shake, releases the impassable collider and grid cells, ensures player clearance, and spawns GolemBoss. Defeating GolemBoss spawns NextStagePortal, which executes the 1.2s vortex transition on player contact.

## Rules and Invariants

- Critical behavior rules:
  - Random interactable spawning occurs strictly on walkable cells outside the deterministic initial player chunk and outside any registered tower encounter arena (+ _towerArenaExclusionBuffer) during scene Start().
  - MapInteractablesSpawner.Start() and GolemStatue.Awake() must fail fast (InvalidOperationException) when required dependencies or serialized references are missing, while MapInteractablesSpawner keeps IBossEncounterService resolution optional for test scenes without towers.
  - GolemStatue's blocking collider must reside on TerrainLayers.Impassable (so FlowField.CreateCostField preserves impassability across chunk updates and player weapons querying EntityLayers.Enemies never target the dormant statue). When dismissing or awakening the statue, _impassableCollider.enabled = false MUST execute BEFORE calling cell.ResetCosts() on _reservedCells so the grid cells become immediately walkable (Cost = 1).
  - NextStagePortal.OnTriggerEnter must verify _playerManager.Health.IsAlive() before triggering so a destroyed car rolling on wheel colliders during the death VFX delay cannot trigger a stage transition, and must call ISkillUpgradePresenter.HideForStageTransition() so any live level-up/crate modal is dismissed before the vortex or DemoThankYouPresenter opens.
  - During the NextStagePortal vortex, the player is protected by InputContextScopeKind.Dying and IPlayerDamagedHandler.SetPortalTransitionInvulnerable(true), skills are deactivated, and Rigidbody.isKinematic is set true (guarded by CarController.FixedUpdate).
  - Distance checks in CapturePoint use squared magnitude (sqrMagnitude <= _captureRadius * _captureRadius) to avoid square root calculations.
  - Active DOTween sequences on CapturePoint, GolemStatue, and NextStagePortal must be killed in OnDisable and OnDestroy (and input scopes released) to avoid orphaned tweens or leaked input locks.
- Constraints contributors must preserve:
  - Do not use static singletons or FindAnyObjectByType to resolve dependencies; inject services via Reflex.
  - Clean up UI prompts, canvases, and input scopes when objects are deactivated or destroyed.

## Extension Points

- Adding Random Map Interactables: Create a new MonoBehaviour (or prefab), add an entry under _spawnRules in MapInteractablesSpawner in the Inspector, and configure spatial constraints.
- Customizing Tier Statue Prompts and Portal Vortex: Configure StatueProgressFormat and StatueChallengePrompt per LoopTierConfig, and PortalVortexDuration / PortalVortexSpins on GameLoopProgressionConfigSO in Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset.
- Capture Point Visuals: Customize _captureRadius, _captureDurationSeconds, _decaySpeedMultiplier, _maxCircleScale, _outlineCirclePlane, and captured materials on the CapturePoint Inspector fields.

## Integration Notes

- Upstream dependencies:
  - IGridManager: Supplies cell walkability, cost resetting, and chunk sizing for spawning and GolemStatue footprint management.
  - IPlayerManager: Supplies player position, liveness, Rigidbody, DamagedHandler, SkillsRegistry, and initial spawn chunk center.
  - IBossEncounterService: Supplies tower arena exclusion checks (IsPositionWithinAnyTowerArena), tower defeat events (OnTowerDefeated), statue registration, Golem spawning, and NextStagePortal instantiation.
  - IRunSessionService and IGameSceneLoader: Coordinate tier snapshot capture, scene reload, and final-tier routing for NextStagePortal and GolemStatue text.
  - Reflex DI: Injects scene-placed GolemStatue, recursively injects MapInteractablesSpawner instances, and injects NextStagePortal via ReflexPrefabInstantiator.InstantiateInjectedObject.
- Downstream consumers:
  - SkillUpgradePresenter: Displays skill upgrade modal UI when CapturePoint completes its reward flow, and hides via HideForStageTransition() when NextStagePortal triggers.
  - DemoThankYouPresenter: Opened by NextStagePortal on completing the final tier.
  - EnemiesSpawnChanceRedistributionSystem: Receives difficulty multiplier increases from totems.
- Known Behavior & Lifecycle Notes:
  - IncreaseDifficultyTotem validates [Inject] services and binds _interactPromptView in Start() rather than Awake() because MapInteractablesSpawner runs Instantiate(...) synchronously before calling Reflex.Injectors.GameObjectInjector.InjectRecursive.
  - GolemStatue is placed directly in Assets/Scenes/RuinedBloodCity.unity (injected before Awake() by Reflex SceneScope) and NextStagePortal is instantiated via ReflexPrefabInstantiator.InstantiateInjectedObject (injected before Awake() under an inactive staging parent).
  - IncreaseDifficultyTotem and GolemStatue world-space prompt elements reside on Layer 17 (Interactables) and use FaceMainCameraDirection.
