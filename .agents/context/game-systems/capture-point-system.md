# CapturePoint System Documentation

## Purpose

The CapturePoint system provides an interactive map objective in gameplay scenes. It tracks player car proximity within a capture radius, smoothly scales a visual ground circle indicator using DOTween, decays progress when the player leaves the area, quickly shrinks and hides the circle plane upon acquisition, swaps target mesh materials upon acquisition, and rewards a skill upgrade request upon reaching 100% completion.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Interactables/CapturePoint/CapturePoint.cs
  - Assets/Scripts/Spawners/MapInteractablesSpawner.cs
- Related code:
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
  - Assets/Scripts/Navigation/GridSystem/GridManager.cs
- Related docs:
  - .agents/context/game-systems/interactables-system.md
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/di-and-boot-flow-system.md
  - .agents/context/project-coding-standards.md
- Related agents or instructions:
  - AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - CapturePoint: MonoBehaviour attached to capture point scene prefabs. Handles player distance checks, progress calculation and decay, DOTween expanding circle plane scaling, post-capture shrink & deactivation, material swapping on completion, deactivation visual toggling, and reward triggering.
  - MapInteractablesSpawner: A scene-bound spawner that places interactable prefabs (including CapturePoint) on walkable grid cells during scene initialization, enforcing spawn count limits and spatial distance constraints.
- Key interfaces:
  - IPlayerManager: Injected dependency providing access to player position (GameObject) and SkillsRegistry.
  - ISkillUpgradeFlow: Injected dependency responsible for queuing random skill upgrades when capture completes.
  - IGridManager: Injected dependency used by MapInteractablesSpawner to locate walkable grid cells.
- Runtime flow:
  1. On scene Start(), MapInteractablesSpawner evaluates InteractableSpawnRule configurations and instantiates CapturePoint prefabs on walkable grid positions.
  2. MapInteractablesSpawner recursively injects dependencies into spawned game objects using Reflex DI (Reflex.Injectors.GameObjectInjector.InjectRecursive).
  3. Every Update(), active CapturePoint instances measure 3D squared distance (sqrMagnitude) between transform position and IPlayerManager.GameObject.
  4. If squared distance <= _captureRadius * _captureRadius, progress increases over _captureDurationSeconds ((1f / _captureDurationSeconds) * Time.deltaTime).
  5. If squared distance > _captureRadius * _captureRadius, progress decays based on _decaySpeedMultiplier (((1f / _captureDurationSeconds) * _decaySpeedMultiplier) * Time.deltaTime).
  6. Progress is clamped between 0.0 and 1.0 using Mathf.Clamp01(_progress).
  7. Visual progress updates via UpdateExpandingCircleScale(), setting _expandingCirclePlane.localScale to Vector3.one * (_progress * _maxCircleScale).
  8. Upon reaching 1.0 progress:
     - _isCaptured is set to true and progress is locked to 1.0.
     - Existing _scaleTween is killed and a new shrink tween (DOScale(Vector3.zero, _shrinkDurationSeconds).SetEase(Ease.InQuad)) scales down _expandingCirclePlane before disabling its GameObject via DisableExpandingCirclePlane.
     - SwapMaterialsOnCaptured() replaces element 0 and element 1 of _targetRenderer.materials with _capturedMaterial1 and _capturedMaterial2 if assigned.
     - Queues random skill upgrade via ISkillUpgradeFlow.QueueRandomSkillUpgradeRequest(_playerManager.SkillsRegistry).
     - Plays VFX via _capturedVfxPlayer (if assigned).
     - Disables _deactivationVisuals GameObject (if assigned).
     - Sets enabled = false to stop Update execution.
  9. On OnDestroy(), _scaleTween is killed via _scaleTween?.Kill() to prevent memory leaks or missing target warnings.

## Rules and Invariants

- Critical behavior rules:
  - Capture progress is strictly clamped between 0.0 and 1.0.
  - Distance checks use squared magnitude (sqrMagnitude vs _captureRadius * _captureRadius) to eliminate unnecessary square root calculations.
  - Visual circle plane scale directly mirrors current progress relative to _maxCircleScale while capturing.
  - Upon 100% capture, the ground circle plane quickly shrinks to zero scale over _shrinkDurationSeconds before its GameObject is set active to false.
  - Visuals & VFX fields (_deactivationVisuals and _capturedVfxPlayer) are strictly optional and safely handle null references.
  - Upon capture completion, materials on _targetRenderer (element 0 and element 1) are replaced with _capturedMaterial1 and _capturedMaterial2 if assigned.
  - Completed capture points become inactive (enabled = false) and will not re-trigger.
  - DOTween tweens are explicitly killed in OnDestroy to prevent memory leaks.
- Ordering or sequencing guarantees:
  - Material swapping and circle shrink tween initiate immediately upon completion before queuing skill upgrade requests.
- Constraints contributors must preserve:
  - Inject dependencies via Reflex ([Inject] private readonly).
  - Declare fields in standard order ([Inject] fields, then [SerializeField] fields, then private fields).
  - Do not use LINQ or expression-bodied method definitions in runtime logic.
  - Do not use FindAnyObjectByType or static singletons for service access.

## Extension Points

- Safe extension areas:
  - Prefab parameters customizable in Unity Inspector: _captureRadius, _captureDurationSeconds, _decaySpeedMultiplier, _maxCircleScale, and _shrinkDurationSeconds.
  - Visual feedback can be customized by assigning VFXPlayer, toggling _deactivationVisuals, or configuring _targetRenderer, _capturedMaterial1, and _capturedMaterial2.
- Required dependencies and contracts:
  - IPlayerManager and ISkillUpgradeFlow must be registered in the Reflex scene context.
- Testing implications:
  - Compile validation via `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Unity play mode checks are required to verify ground plane scaling, quick shrink animation, material swapping, VFX playback, and UI modal popups.

## Integration Notes

- Upstream dependencies:
  - Reflex DI Container, IPlayerManager, ISkillUpgradeFlow, DOTween, VFXPlayer, IGridManager.
- Downstream consumers:
  - SkillUpgradePresenter (listens to ISkillUpgradeFlow requests and presents modal UI).
- Cross-system coupling risks:
  - Requires MapInteractablesSpawner spawn rules setup in Unity scene/prefab for automatic map population.
  - Spawning depends on IGridManager walkable cell calculations.

## Known Risks and Open Questions

- Known limitations:
  - Spawning requires walkable grid cells generated by GridManager.
  - Frame-dependent distance calculation occurs every Update() per active CapturePoint.
- Open design questions:
  - None at present.
- Suggested follow-up tasks:
  - Ensure CapturePoint prefab is configured with appropriate renderer, circle plane, materials, and VFX references in Unity Editor and assigned in MapInteractablesSpawner spawn rules.
