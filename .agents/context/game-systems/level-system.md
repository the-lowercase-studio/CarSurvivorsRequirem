# Level System Documentation

## Purpose

The Level System tracks the player's current level and experience, converts collected exp into level-ups through a designer-authored `AnimationCurve`, and exposes level/exp changes to UI and upgrade flows.

It is not responsible for enemy balance, choosing skill upgrades, score saving, or the visual/audio details of the death screen. Those systems consume the level state or trigger exp collection, but they own their own presentation and gameplay effects.

## Reading Map

- Primary code locations:
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticle.cs
  - Assets/Scripts/LevelSystem/Exp/ExpParticleSpawner.cs
- Related runtime integration:
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Enemies/EnemyDeathHandler.cs
  - Assets/Scripts/UI/Level/PlayerLevelPresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Editor tooling:
  - Assets/Scripts/Editor/GUI/LevelControllerEditor.cs
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related skills:
  - .agents/skills/di-integration/SKILL.md for Reflex binding changes.
  - .agents/skills/check-optimalization/SKILL.md for exp particle pooling or allocation review.
  - .agents/skills/unity-refactor-suggestions/SKILL.md for behavior-preserving cleanup.

## Architecture and Data Flow

- Core components:
  - `LevelData` is an immutable value struct containing `Lvl`, `Exp`, and `MaxExp`.
  - `ILevelController` exposes current `LevelData`, `OnExpChange`, `OnLvlUp`, and `AddExp(float value)`.
  - `LevelController` is a player-attached `MonoBehaviour` that owns level state and reads max exp from `_expCurve`.
  - `ExpParticleSpawner` queues world-space exp spawns, splits them by configured threshold/divider data, and uses a Unity `ObjectPool<ExpParticle>`.
  - `ExpParticle` moves along the flow field, detects the player trigger layer, plays collection audio and shrink tween, then awards exp through `IPlayerManager.LevelController`.
  - `PlayerLevelPresenter` animates exp slider and level text changes, then raises `OnExpSliderVisualEndValueReached` with the reached `LevelData`.
- Key interfaces:
  - `ILevelController` is owned by `LevelController` and reached through `IPlayerManager`.
  - `IInWorldSpaceSpawner<ExpParticleSpawner, float>` is bound in `DefaultGameplaySceneInstaller` for enemy death exp spawning.
  - `IPlayerLevelPresenter` is bound in `DefaultGameplaySceneInstaller` for skill-upgrade timing.
- Runtime flow:
  1. `PlayerManager.Awake` gets the player `ILevelController` from the same GameObject.
  2. `LevelController.Awake` initializes `LevelData.MaxExp` with `_expCurve.Evaluate(LevelData.Lvl)`.
  3. `EnemyDeathHandler` receives `Health.OnNoHealth`, plays death feedback, and calls `_expParticleSpawner.Spawn(transform.position, _enemy.Config.ExpForKill)`.
  4. `ExpParticleSpawner` periodically drains queued spawns and gets particles from its pool.
  5. Each spawned `ExpParticle` moves on the flow-field grid until it enters the player layer trigger.
  6. `ExpParticle.CollectExp` plays collection audio, shrinks the visual, and calls `_playerManager.LevelController.AddExp(_expAmount)`.
  7. `LevelController.AddExp` applies exp, emits one `OnLvlUp` per crossed level, then emits `OnExpChange` for the final level data.
  8. `PlayerLevelPresenter` queues level-up visuals and latest same-level exp changes. When the slider reaches a level-up endpoint, it raises `OnExpSliderVisualEndValueReached` with that level's `LevelData`.
  9. `SkillUpgradePresenter` listens to `IPlayerLevelPresenter.OnExpSliderVisualEndValueReached`, uses the reached level to queue a new-skill reward every configured interval when possible, otherwise queues an upgrade reward, and renders the returned new-skill or upgrade UI.

## Rules and Invariants

- Critical behavior rules:
  - `LevelController` is the single owner of level state. Consumers should read through `IPlayerManager.LevelController` or an injected interface where a binding exists.
  - `_expCurve` defines required exp for each level. Designer-authored curve behavior must remain visible and reviewable in the inspector.
  - Level is stored as `byte`; `AddExp` returns without changes when `LevelData.Lvl == byte.MaxValue`.
  - A single `AddExp` call can trigger multiple `OnLvlUp` events before the final `OnExpChange`.
  - `OnExpChange` is emitted after level-up processing even if one or more `OnLvlUp` events were emitted first.
- Ordering or sequencing guarantees:
  - Skill upgrade UI timing is tied to the visual slider reaching the level-up endpoint, not directly to `LevelController.OnLvlUp`; the raised event payload is the reached `LevelData`.
  - `PlayerLevelPresenter` only keeps the latest same-level exp event while level-up animations are pending.
  - Exp particle release is event-driven through `IPoolable.OnCanBeReleased` and `ObjectPool<ExpParticle>.Release`.
- Constraints contributors must preserve:
  - Do not bypass `LevelController.AddExp` when awarding player exp.
  - Do not change level-up event ordering without checking `PlayerLevelPresenter` and `SkillUpgradePresenter`.
  - Do not replace the existing exp curve with hidden constants unless the user explicitly requests a balance/data-shape change.
  - Do not directly edit scene, prefab, asset, or meta files for level setup unless the user explicitly asks.

## Extension Points

- Safe extension areas:
  - Add new consumers of `ILevelController` events for read-only presentation or analytics-like behavior.
  - Add editor/debug controls in `LevelControllerEditor` when they call public runtime APIs and stay editor-only.
  - Tune visual particle thresholds and exp curve values through Unity assets/inspector workflows, with user approval for balance changes.
- Required dependencies and contracts:
  - `PlayerManager` requires `LevelController` on the same GameObject.
  - `ExpParticle` requires `FlowFieldMovementController`, an injected `IPlayerManager`, a configured visual object, particle appearance thresholds, and an audio player child.
  - `ExpParticleSpawner` requires an exp particle prefab, parent transform, threshold divider data, and world-space spawn configuration.
  - `DefaultGameplaySceneInstaller` binds `ExpParticleSpawner` as `IInWorldSpaceSpawner<ExpParticleSpawner, float>` and `PlayerLevelPresenter` as `IPlayerLevelPresenter`.
- Testing implications:
  - Unit or edit-mode tests around `LevelController.AddExp` should cover exact-threshold exp, multiple level-ups in one call, exp carryover, final `OnExpChange`, and the `byte.MaxValue` cap.
  - Play-mode validation is needed for particle trigger collection, flow-field movement, audio/tween release timing, pooled object reuse, and UI skill-upgrade timing.

## Integration Notes

- Upstream dependencies:
  - Enemy exp value comes from `_enemy.Config.ExpForKill`.
  - Exp particle movement depends on the flow-field movement controller and grid target behavior.
  - Player detection depends on `EntityLayers.Player`.
  - Collection uses `TransformTweenExtensions.LifeEndingShrinkToZeroTween` and `IAudioClipPlayer`.
- Downstream consumers:
  - `PlayerLevelPresenter` consumes level events for slider and level text animation.
  - `SkillUpgradePresenter` consumes `IPlayerLevelPresenter.OnExpSliderVisualEndValueReached` to trigger level-based skill reward queueing through `ISkillUpgradeFlow`.
  - `PlayerDeathPresenter` reads final `LevelData.Lvl` for death-screen text.
  - `LevelControllerEditor` calls `AddExp` through editor debug buttons.
- Cross-system coupling risks:
  - Level state is exposed through `IPlayerManager`, so player composition changes can break level access.
  - The upgrade UI is coupled to level UI animation completion. Changing slider behavior can change when skill reward UI is shown.
  - Particle release waits on tween/audio callback paths, so callback ordering bugs can leak pooled particles or delay release.

## Known Risks and Open Questions

- Known limitations:
  - `ExpParticleSpawner.SpawnParticlesBasedOnExpAmount` calculates `expPart` but currently passes the original `exp` value into each spawned particle. If the divider is intended to split total exp across multiple particles, current behavior appears to multiply awarded exp.
  - Threshold/divider arrays are assumed to contain at least one usable entry. Empty arrays log an error but execution can continue into default values.
  - `ExpParticle.CollectExp` subscribes anonymous handlers to `OnAudioClipFinished`; repeated collection/reuse should be reviewed if audio callback behavior changes.
  - `PlayerLevelPresenter` subscribes to level events in `Start` but does not currently unsubscribe in `OnDisable` or `OnDestroy`.
- Open design questions:
  - Should exp particle divider data split a kill's exp across particles, or should every particle intentionally carry the full enemy exp value?
  - Should skill-upgrade selection remain tied to the slider animation endpoint and `_newSkillLevelInterval`, or should a separate level-up gameplay event drive rewards?
  - Should the required exp curve support non-integer or non-monotonic values, or should validation enforce safer progression data?
- Suggested follow-up tasks:
  - Add focused tests for `LevelController.AddExp` event ordering and carryover.
  - Review `ExpParticleSpawner` divider behavior with the designer before changing runtime exp awards.
  - Add lifecycle-safe unsubscribe handling to `PlayerLevelPresenter` if the presenter can be disabled or scene-reloaded without object destruction.
