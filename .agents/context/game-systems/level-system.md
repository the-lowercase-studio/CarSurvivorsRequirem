# Level System Documentation

## Purpose

The Level System tracks the player's current level and experience, restores saved level and experience across multi-tier stage transitions, converts collected exp into level-ups through a designer-authored AnimationCurve, and exposes level/exp changes to UI and upgrade flows.

It is not responsible for enemy balance, choosing skill upgrades, score saving, or the visual/audio details of the death or completion screens. Those systems consume the level state or trigger exp collection, but they own their own presentation and gameplay effects.

## Reading Map

- Primary code locations:
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticle.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
  - Assets/Scripts/LevelSystem/Constants/LevelConstants.cs
- Related runtime integration:
  - Assets/Scripts/GameFlow/RunSessionService.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs
  - Assets/Scripts/UI/Level/PlayerLevelPresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/ReflexDI/ReflexPrefabInstantiator.cs
- Editor tooling:
  - Assets/Scripts/Editor/GUI/LevelControllerEditor.cs
- Automated tests:
  - Assets/Scripts/Editor/Tests/LevelControllerTests.cs
  - Assets/Scripts/Editor/Tests/ExpParticleTests.cs
  - Assets/Scripts/Editor/Tests/ExpParticleSpawnerTests.cs
  - Assets/Scripts/Editor/Tests/PlayerLevelPresenterTests.cs
  - Assets/Tests/PlayMode/LevelAndExpPlayModeTests.cs
- Related docs:
  - .agents/context/game-systems/game-flow-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md
  - .agents/skills/unity-refactor-suggestions/SKILL.md

## Architecture and Data Flow

- Core components:
  - LevelData is an immutable value struct containing Lvl (default 1 in constructor), Exp, and MaxExp.
  - ILevelController exposes current LevelData, OnExpChange, OnLvlUp, and AddExp(float value).
  - LevelController is a player-attached MonoBehaviour that owns level state (initializing LevelData with lvl: 1), injects IRunSessionService to restore saved (Lvl, Exp) on multi-tier scene loads, reads max exp from _expCurve, and ignores non-positive, NaN, or infinite values in AddExp(float value).
  - ExpParticleSpawner queues world-space exp spawns, splits total exp across spawned particles via expPart = exp / expTresholdDevider.Divider, exposes public DrainQueuedSpawns() (invoking SpawnParticlesBasedOnExpAmount()), and uses a Unity ObjectPool<ExpParticle> whose createFunc instantiates and injects particles before Awake()/OnEnable() via ReflexPrefabInstantiator.InstantiateInjectedObject (using a reusable inactive staging parent created in Awake() and destroyed in OnDestroy() via DestroyImmediate in EditMode / Destroy in Play Mode).
  - ExpParticle captures initial _startScale in Awake() (for pre-Start() OnRelease() safety) and again in Start(), moves along the flow field when not collecting, detects the player trigger layer, plays collection audio and shrinks linearly toward Vector3.zero via public AdvanceCollection(float deltaTime) (called by Update(Time.deltaTime)), then awards _expAmount through IPlayerManager.LevelController.
  - PlayerLevelPresenter initializes slider values (setting _expSlider.maxValue before _expSlider.value so restored mid-run EXP is not clamped to 1 on frame 0) and level text in Start(), animates exp slider and level text changes, supports FastForward() to drain queued transitions, unsubscribes from LevelController events in OnDestroy(), and raises OnExpSliderVisualEndValueReached with the reached LevelData.
- Key interfaces:
  - ILevelController is owned by LevelController and reached through IPlayerManager.
  - IInWorldSpaceSpawner<ExpParticleSpawner, float> is bound in DefaultGameplaySceneInstaller for enemy and boss death exp spawning.
  - IPlayerLevelPresenter is bound in DefaultGameplaySceneInstaller for skill-upgrade timing and Dev Console exp --auto fast-forwarding.
- Runtime flow:
  1. PlayerManager.Awake gets the player ILevelController from the same GameObject.
  2. LevelController.Awake checks IRunSessionService.TryGetRestoredLevel(out byte restoredLevel, out float restoredExp); if a tier transition snapshot exists, it restores Lvl = Mathf.Max(1, restoredLevel), Exp = Mathf.Max(0f, restoredExp), and MaxExp = _expCurve.Evaluate(safeLevel); otherwise it initializes Lvl = 1, Exp = 0, and MaxExp = _expCurve.Evaluate(1).
  3. PlayerLevelPresenter.Start reads _playerLevelController.LevelData, sets _expSlider.maxValue and then _expSlider.value, and immediately calls UpdateLevelText() so restored levels display on frame 0 without clamping.
  4. EnemyDeathHandler, BarrelEnemyDeathHandler, GolemBoss, and TowerBossBase receive Health.OnNoHealth, play death feedback, and call _expParticleSpawner.Spawn(transform.position, expForKill).
  5. ExpParticleSpawner periodically drains queued spawns and gets particles from its pool (instantiated and injected via ReflexPrefabInstantiator).
  6. Each spawned ExpParticle moves on the flow-field grid until it enters the player layer trigger.
  7. ExpParticle.CollectExp plays collection audio, shrinks the visual, and calls _playerManager.LevelController.AddExp(_expAmount).
  8. LevelController.AddExp applies exp, emits one OnLvlUp per crossed level, then emits OnExpChange for the final level data.
  9. PlayerLevelPresenter queues level-up visuals and latest same-level exp changes. When the slider reaches a level-up endpoint, it raises OnExpSliderVisualEndValueReached with that level's LevelData.
  10. SkillUpgradePresenter listens to IPlayerLevelPresenter.OnExpSliderVisualEndValueReached, uses the reached level to queue a new-skill reward every configured interval when possible, otherwise queues an upgrade reward, and renders the returned new-skill or upgrade UI.

## Rules and Invariants

- Critical behavior rules:
  - LevelController is the single owner of level state. Consumers should read through IPlayerManager.LevelController or an injected interface where a binding exists.
  - _expCurve defines required exp for each level. Designer-authored curve behavior must remain visible and reviewable in the inspector.
  - Level is stored as byte; AddExp returns without changes when LevelData.Lvl == byte.MaxValue.
  - LevelController.Awake must restore saved (Lvl, Exp) from IRunSessionService before PlayerLevelPresenter.Start reads LevelData so tier transitions do not emit false level-up events or reset the HUD to Level 1.
  - ExpParticle.prefab must not carry GameObjectSelfInjector; ExpParticleSpawner must instantiate pooled instances through ReflexPrefabInstantiator.InstantiateInjectedObject with _container and _injectionStagingParent.
  - A single AddExp call can trigger multiple OnLvlUp events before the final OnExpChange.
  - OnExpChange is emitted after level-up processing even if one or more OnLvlUp events were emitted first.
- Ordering or sequencing guarantees:
  - Skill upgrade UI timing is tied to the visual slider reaching the level-up endpoint, not directly to LevelController.OnLvlUp; the raised event payload is the reached LevelData.
  - PlayerLevelPresenter only keeps the latest same-level exp event while level-up animations are pending.
  - Exp particle release is event-driven through IPoolable.OnCanBeReleased and ObjectPool<ExpParticle>.Release.
- Constraints contributors must preserve:
  - Do not bypass LevelController.AddExp when awarding player exp during active gameplay.
  - Do not change level-up event ordering without checking PlayerLevelPresenter and SkillUpgradePresenter.
  - Do not replace the existing exp curve with hidden constants unless the user explicitly requests a balance/data-shape change.
  - Follow ADR-004 before editing scene, prefab, asset, or meta files directly.

## Extension Points

- Safe extension areas:
  - Add new consumers of ILevelController events for read-only presentation or analytics-like behavior.
  - Add editor/debug controls in LevelControllerEditor when they call public runtime APIs and stay editor-only.
  - Tune visual particle thresholds and exp curve values through Unity assets/inspector workflows, with user approval for balance changes.
- Required dependencies and contracts:
  - PlayerManager requires LevelController on the same GameObject.
  - ExpParticle requires FlowFieldMovementController, an injected IPlayerManager, a configured visual object, particle appearance thresholds, and an audio player child.
  - ExpParticleSpawner requires an injected Reflex Container, an exp particle prefab, parent transform, threshold divider data, and world-space spawn configuration.
  - DefaultGameplaySceneInstaller binds ExpParticleSpawner as IInWorldSpaceSpawner<ExpParticleSpawner, float> and PlayerLevelPresenter as IPlayerLevelPresenter.
- Testing implications:
  - Compile after C# edits with `dotnet build CarSurvivors.Runtime.csproj -p:BuildProjectReferences=false`, `dotnet build CarSurvivors.Editor.Tests.csproj -p:BuildProjectReferences=false`, and `dotnet build CarSurvivors.PlayMode.Tests.csproj -p:BuildProjectReferences=false`.
  - Automated EditMode tests in Assets/Scripts/Editor/Tests/LevelControllerTests.cs, ExpParticleTests.cs, ExpParticleSpawnerTests.cs, and PlayerLevelPresenterTests.cs cover exact-threshold exp, multiple level-ups in one call, exp carryover, final OnExpChange, byte.MaxValue cap, invalid AddExp input guards, IRunSessionService level/exp restoration, divided expPart conservation, AdvanceCollection shrink progression, and PlayerLevelPresenter frame-0 slider initialization and FastForward queueing.
  - Automated PlayMode tests in Assets/Tests/PlayMode/LevelAndExpPlayModeTests.cs cover live InvokeRepeating queue draining, flow-field attraction, trigger collection shrink-to-payout, and coroutine multi-level-up UI transitions.

## Integration Notes

- Upstream dependencies:
  - IRunSessionService provides saved level and exp across tier transitions.
  - Enemy and boss exp values come from EnemyConfigSO.ExpForKill, GolemBossConfigSO.ExpForKill, and TowerBossConfigSO.ExpReward.
  - Exp particle movement depends on the flow-field movement controller and grid target behavior.
  - Player detection depends on EntityLayers.Player.
  - Collection uses linear scale interpolation in ExpParticle.AdvanceCollection and IAudioClipPlayer.
- Downstream consumers:
  - PlayerLevelPresenter consumes level events for slider and level text animation.
  - SkillUpgradePresenter consumes IPlayerLevelPresenter.OnExpSliderVisualEndValueReached to trigger level-based skill reward queueing through ISkillUpgradeFlow.
  - PlayerDeathPresenter and DemoThankYouPresenter read final LevelData.Lvl for end-of-run level text.
  - RunSessionService reads LevelData.Lvl and LevelData.Exp when capturing tier transition snapshots.
  - LevelControllerEditor and GameplayDevCommandsRegistrar (exp command) call AddExp.
- Cross-system coupling risks:
  - Level state is exposed through IPlayerManager, so player composition changes can break level access.
  - The upgrade UI is coupled to level UI animation completion. Changing slider behavior can change when skill reward UI is shown.
  - Particle release waits on tween/audio callback paths, so callback ordering bugs can leak pooled particles or delay release.

## Known Risks and Open Questions

- Known limitations:
  - Threshold/divider arrays are assumed to contain at least one usable entry. Empty arrays log an error in Awake() or SetSizeAndMaterialBasedOnExpAmount and return early.
- Open design questions:
  - Should skill-upgrade selection remain tied to the slider animation endpoint and _newSkillLevelInterval, or should a separate level-up gameplay event drive rewards?
- Suggested follow-up tasks:
  - Monitor high-count EXP particle bursts during late-tier swarms to ensure threshold dividers keep active particle counts well below pool ceilings.
