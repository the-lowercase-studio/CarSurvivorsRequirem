# Implementation Plan - CapturePoint System

The **CapturePoint** system is a new map objective interactable. When the player car enters the defined radius of a `CapturePoint`, progress begins filling toward 100%. A ground plane circle scales up smoothly using **DOTween** to visualize progress. If the player leaves the radius before completion, the progress decays at a rate determined by `_decaySpeedMultiplier`. When 100% is reached, the point deactivates and queues a skill upgrade via `ISkillUpgradeFlow`.

## Confirmed Specifications

- **System Name**: `CapturePoint`
- **Namespace**: `Assets.Scripts.Interactables.CapturePoint`
- **Spawning**: Spawned dynamically at scene start via `MapInteractablesSpawner` using `InteractableSpawnRule`.
- **Visuals**: Expanding 3D plane in the shape of a circle, scaled smoothly using **DOTween**.
- **Decay Logic**: Multiplier-based decay relative to capture speed (`_decaySpeedMultiplier`).
- **Reward**: Queues a random skill upgrade request via `ISkillUpgradeFlow.QueueRandomSkillUpgradeRequest(...)`, displaying the skill selection modal.
- **Documentation**: New system will be documented under `.agents/context/game-systems/capture-point-system.md` upon completion.

---

## User Review Required

> [!NOTE]
> All specifications have been confirmed by the user. Review the implementation plan below and click **Proceed** to execute.

---

## Proposed Changes

### Interactables / CapturePoint

#### [NEW] [CapturePoint.cs](file:///c:/GameDev/Unity/CarSurvivorsRequirem/Assets/Scripts/Interactables/CapturePoint/CapturePoint.cs)

Creates the core `CapturePoint` component:
- **Reflex Dependency Injection**:
  - `[Inject] private readonly IPlayerManager _playerManager;`
  - `[Inject] private readonly ISkillUpgradeFlow _skillUpgradeFlow;`
- **Inspector Configuration**:
  - `[SerializeField] private float _captureRadius = 5f;`
  - `[SerializeField] private float _captureDurationSeconds = 5f;`
  - `[SerializeField] private float _decaySpeedMultiplier = 1f;`
  - `[SerializeField] private Transform _expandingCirclePlane;`
  - `[SerializeField] private float _maxCircleScale = 10f;`
  - `[SerializeField] private GameObject _deactivationVisuals;`
  - `[SerializeField] private VFXPlayer _capturedVfxPlayer;`
- **State & Logic**:
  - Distance check to `_playerManager.GameObject` in `Update()`.
  - Capturing when `distance <= _captureRadius`: `progress += (1f / _captureDurationSeconds) * Time.deltaTime`.
  - Decaying when `distance > _captureRadius`: `progress -= ((1f / _captureDurationSeconds) * _decaySpeedMultiplier) * Time.deltaTime`.
  - Clamps progress between `0f` and `1f`.
  - Updates `_expandingCirclePlane.localScale` smoothly with DOTween (`DOScale`).
  - Upon reaching `1f` progress:
    - Sets `_isCaptured = true`.
    - Triggers `_skillUpgradeFlow.QueueRandomSkillUpgradeRequest(_playerManager.SkillsRegistry)`.
    - Plays `_capturedVfxPlayer` (if assigned).
    - Hides/deactivates `_deactivationVisuals` and disables `this.enabled`.
- **Editor Tooling**:
  - `OnDrawGizmosSelected` drawing yellow wire sphere for `_captureRadius`.

---

## Verification Plan

### Automated Tests & Compilation
- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` to verify zero compile errors or warnings.

### Manual Verification Steps in Unity Editor
1. Attach `CapturePoint.cs` to a prefab with a ground circle plane.
2. Add an `InteractableSpawnRule` for `CapturePoint` in `MapInteractablesSpawner` on `RuinedBloodCity` scene.
3. Start Play Mode:
   - Drive near `CapturePoint`: observe circle plane scaling up smoothly with DOTween.
   - Drive away before 100%: observe circle plane scaling back down at `_decaySpeedMultiplier` rate.
   - Re-enter and complete to 100%: verify skill upgrade UI pops up and capture point deactivates.

---

## System Documentation Plan

Once verified, create system documentation using the `document-system` skill:
- File: `.agents/context/game-systems/capture-point-system.md`
- Include all 7 mandatory system doc sections (Purpose, Reading Map, Architecture, Rules/Invariants, Extension Points, Integration Notes, Known Risks).
