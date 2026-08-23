# Implementation Plan - Golem Boss Animation & Detachable Arm Switching

Date: 2026-08-23

## Overview

Integrate a dedicated animation controller (Mecanim Animator) into the Golem Boss system and implement distinct visual detachment switching for the separate Left and Right rocket arms. 
The boss will support a single moving locomotion animation and dedicated animation triggers for its 4 attacks (Leap Slam, Melee Foot Stomp, Linear Rocket Fists, Sky Arm Barrage). Enrage will remain purely a visual effect / material tint (no animation), and Death will not use a Mecanim animation.
Left and right detachable arm projectiles will be distinct components connected as children to their corresponding rig arms on the skeleton; when attacks are fired, their respective parent rig arm meshes will be visually disabled while the corresponding left/right arm projectiles become active in world space, smoothly restoring their respective rig arm meshes upon redocking.

## User Review Required

> [!IMPORTANT]
> - **Scope of Animations**:
>   - **Locomotion**: Single moving animation controlled via `IsMoving` bool / `Speed` float parameters.
>   - **Attacks**: 4 dedicated triggers (`LeapSlam`, `Stomp`, `LinearFist`, `SkyBarrage`).
>   - **Excluded**: No Mecanim animation for Death or Enrage (Enrage remains visual shader/VFX tinting only; Death remains state/VFX cleanup).
> - **Distinct Left & Right Detachable Arms**:
>   - Left and Right arm projectiles are separate entities and child components connected to the Left and Right rig arm bones respectively.
>   - `_leftRigArmVisual` and `_rightRigArmVisual` represent the attached skeletal arm meshes on the rig.
>   - When Left Arm launches $\rightarrow$ `_leftRigArmVisual` disables, Left Arm projectile enables and detaches.
>   - When Right Arm launches $\rightarrow$ `_rightRigArmVisual` disables, Right Arm projectile enables and detaches.
>   - When each arm docks $\rightarrow$ it re-parents to its socket, disables its projectile object, and re-enables its corresponding rig arm mesh.
> - **Audio, VFX, and Materials**: Untouched as requested.

## Open Questions

- None. Requirements clarified and aligned with user specifications.

## Proposed Changes

### Animation System Integration

#### [NEW] Assets/Scripts/Enemies/Bosses/Golem/Animation/GolemAnimator.cs
- Interface `IGolemAnimator` colocated above class.
- Methods:
  - `void SetMoving(bool isMoving, float speed = 0f);`
  - `void PlayLeapSlam();`
  - `void PlayStomp();`
  - `void PlayLinearFist();`
  - `void PlaySkyBarrage();`
- Class `GolemAnimator : MonoBehaviour, IGolemAnimator`:
  - `[SerializeField] private Animator _animator;`
  - Encapsulates safe parameter setting with null-safety fallbacks for `_animator`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/Constants/GolemBossConstants.cs
- Centralize animation parameter names:
  - `ANIM_PARAM_IS_MOVING = "IsMoving"`
  - `ANIM_PARAM_SPEED = "Speed"`
  - `ANIM_TRIGGER_LEAP_SLAM = "LeapSlam"`
  - `ANIM_TRIGGER_STOMP = "Stomp"`
  - `ANIM_TRIGGER_LINEAR_FIST = "LinearFist"`
  - `ANIM_TRIGGER_SKY_BARRAGE = "SkyBarrage"`

---

### Core Boss & State Machine Wiring

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs
- Expose `IGolemAnimator Animator { get; }`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs
- Add `[SerializeField] private GolemAnimator _animator;`
- Expose `public IGolemAnimator Animator => _animator;`
- (Enrage and Death retain existing VFX and state handling without invoking Mecanim triggers).

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemPursuitState.cs
- In `Enter()`: call `_boss.Animator?.SetMoving(true)`.
- In `FixedUpdate()`: update movement speed and moving status `_boss.Animator?.SetMoving(true, speed)`.
- In `Exit()`: call `_boss.Animator?.SetMoving(false, 0f)`.
- In `Update()`: trigger `_boss.Animator?.PlayStomp()` when melee foot stomp occurs.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLeapSlamState.cs
- In `Enter()`: call `_boss.Animator?.SetMoving(false)` and trigger `_boss.Animator?.PlayLeapSlam()`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs
- In `Enter()`: call `_boss.Animator?.SetMoving(false)` and trigger `_boss.Animator?.PlayLinearFist()`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs
- In `Enter()`: trigger `_boss.Animator?.PlaySkyBarrage()`.
- In `FixedUpdate()`: update movement status while body pursues player during sky barrage `_boss.Animator?.SetMoving(true, speed)`.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemDeathState.cs
- In `Enter()`: call `_boss.Animator?.SetMoving(false)`.

---

### Detachable Left & Right Arm Rig Visual Switching

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs
- Add `[SerializeField] private GameObject _rigArmVisual;`
- Provide `Initialize(Transform socketTransform, GameObject rigArmVisual)` to bind the arm to its specific socket and skeletal rig mesh.
- Update `DockToSocket()`:
  - Reparent to socket transform.
  - Reset local position and rotation.
  - Disable projectile `gameObject.SetActive(false)`.
  - Re-enable corresponding rig visual `_rigArmVisual?.SetActive(true)`.
- Update `FireLinear(...)` and `LaunchToSky(...)`:
  - Disable corresponding rig visual `_rigArmVisual?.SetActive(false)`.
  - Enable projectile `gameObject.SetActive(true)`.
  - Unparent `transform.SetParent(null)`.
  - Execute flight / launch sequence.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmSocketController.cs
- Distinct references for both arms:
  - `[SerializeField] private Transform _leftArmSocket;`
  - `[SerializeField] private Transform _rightArmSocket;`
  - `[SerializeField] private GameObject _leftRigArmVisual;`
  - `[SerializeField] private GameObject _rightRigArmVisual;`
  - `[SerializeField] private GolemArmProjectile _leftArmProjectile;`
  - `[SerializeField] private GolemArmProjectile _rightArmProjectile;`
- In `Initialize()`:
  - Initialize `_leftArmProjectile` with `_leftArmSocket` and `_leftRigArmVisual`.
  - Initialize `_rightArmProjectile` with `_rightArmSocket` and `_rightRigArmVisual`.
- In `ResetAllArms()`:
  - Resets and docks both `_leftArmProjectile` and `_rightArmProjectile`, restoring both rig visuals.

---

## Verification Plan

### Automated Checks
- Project compilation check with zero warnings:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Coding standards audit (field order `[Inject]` -> `[SerializeField]` -> private, `_camelCase` naming, `UPPER_SNAKE_CASE` constants).

### Manual Verification
1. **Prefab Setup in Unity Editor**:
   - Add `Animator` and `GolemAnimator` to `GolemBoss` prefab.
   - In `GolemArmSocketController`, assign `_leftArmSocket`, `_rightArmSocket`, `_leftRigArmVisual` (left arm mesh on skeleton), `_rightRigArmVisual` (right arm mesh on skeleton), `_leftArmProjectile`, and `_rightArmProjectile`.
2. **Animation Verification**:
   - Verify locomotion animation plays while moving and stops when stationary / charging attacks.
   - Verify Leap Slam triggers `LeapSlam` animation.
   - Verify Stomp triggers `Stomp` animation.
   - Verify Linear Rocket Fists triggers `LinearFist` animation.
   - Verify Sky Barrage triggers `SkyBarrage` animation.
   - Verify Enrage triggers visual shader/VFX color shift without throwing animation errors.
3. **Left & Right Arm Visual Detachment**:
   - In normal state, confirm both rig arm meshes are visible and projectile entities are hidden.
   - During Linear Rocket Fists, confirm both rig arm meshes vanish and left & right projectile arms fire forward and return.
   - During Sky Barrage, confirm left and right rig arm meshes vanish, projectiles launch into the sky and slam down sequentially with jitter, and both rig arm meshes reappear upon redocking.
