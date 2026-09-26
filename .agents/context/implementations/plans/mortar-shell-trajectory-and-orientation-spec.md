# Specification: Mortar Shell Trajectory & Tangent Orientation Overhaul

Date: 2026-09-26  
Author: Antigravity  
Target Systems:
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/
- Assets/Prefabs/Enemies/Bosses/Towers/Mortal/
- Assets/ScriptableObjects/Enemies/Bosses/

---

## 1. Overview & Player Experience

### Summary
During the stationary Mortar Tower boss fight, attacks launch mortar shells (MortarShellProjectile) along parabolic arcs toward player-targeted positions on the arena ground. Previously, a placeholder sphere was used. With the replacement visual model (MortarBullet.fbx), two visual and mathematical defects emerged:
1. **Sideways Orientation ("Bokiem"):** The 3D model is authored with its tip/nose oriented along local +Y (or non-+Z), whereas Quaternion.LookRotation aligns local +Z with the trajectory. As a result, the shell flies sideways, and its nose points perpendicular to the flight path throughout ascent, apex, and descent.
2. **Trajectory & Tangent Flaws:** The flight is driven by transform.DOJump with per-frame finite difference calculations (transform.position - previousPosition). When falling steeply toward the ground, delta.normalized approaches Vector3.down, which creates a gimbal singularity against Vector3.up in LookRotation, causing jitter, snapping, or inaccurate pitch angles. Furthermore, DOJump lacks customizable ballistic shaping (e.g. apex hang time and steep plunge).

This specification overhauls the flight trajectory and rotation calculation so that the mortar shell's tip continuously and smoothly tracks the velocity tangent vector from muzzle exit to ground impact, with configurable model alignment offsets and a polished ballistic curve.

### Player-Facing Goals
- **Clear Visual Telegraphy & Threat Readability:** The mortar shell visually leads with its nose/warhead pointing along its flight path. When plunging from the sky into the ground telegraph circle, the player intuitively sees the angle and trajectory of incoming artillery fire.
- **Natural Artillery Arc:** The projectile exhibits authentic mortar ballistics (punchy muzzle launch, apex transition, steep plunge into the ground) rather than feeling like a rubber ball bouncing.
- **Zero Jitter & Zero Snapping:** Continuous mathematical trajectory evaluation eliminates one-frame lags and gimbal snapping near vertical descent.

### In-Scope vs. Out-of-Scope
- **In-Scope:**
  - Refactoring MortarShellProjectile flight calculation to use analytical trajectory and stable pitch/yaw heading computation.
  - Adding a configurable visual rotation offset (_visualRotationOffset or _modelRotationOffset, default (90, 0, 0)) to align the mesh tip with the velocity tangent.
  - Adding an optional customizable height/ballistic AnimationCurve (with fallback to analytical parabola) for fine-grained trajectory arc tuning.
  - Ensuring gimbal-free orientation calculation even for steep vertical descents (using trajectory plane lateral normals).
  - Updating MortalShell.prefab serialized fields.
  - Preserving existing pooling (MortarShellPool), detonation logic, impact VFX/SFX, and callback contracts (onLanded).
- **Out-of-Scope:**
  - Changing boss state machine timings or damage values (retaining existing Attack1, Attack2, Attack3 balance).
  - Adding physics-based rigidbodies or PhysX ballistic simulations (remains deterministic and procedural via DOTween / mathematical interpolation).
  - Modifying the raw FBX asset file (MortarBullet.fbx).

---

## 2. Open Questions & Resolved Decisions

### Resolved Decisions
- [x] **Decision 1 (Deterministic Procedural Flight):** Retain procedural tweening via DOTween rather than converting to dynamic physics / Rigidbodies to preserve exact arrival timing synchronization with ground telegraphs (warningDuration).
- [x] **Decision 2 (Gimbal Lock Immunity):** Use trajectory horizontal azimuth and trajectory plane cross-products to determine the rotation frame rather than naive Quaternion.LookRotation(dir, Vector3.up) which fails when dropping vertically (dir = Vector3.down).
- [x] **Decision 3 (Object Pool & Zero-GC Safety):** All trajectory evaluation in OnUpdate or tween callbacks must generate zero allocations (no LINQ, no heap closures across frames, pre-allocated vectors).
- [x] **Decision 4 (Q1 - Model Rotation Offset Ownership):** Option A selected. Use serialized `Vector3 _modelRotationOffset = new Vector3(90f, 0f, 0f)` in `MortarShellProjectile`, applied dynamically during flight heading calculation.
- [x] **Decision 5 (Q2 - Trajectory Arc Curve Formulation):** Option A selected. Hybrid trajectory: analytical parabola $y(t) = 4h \cdot t(1-t)$ by default, with optional `AnimationCurve _customHeightCurve` override in the Inspector.
- [x] **Decision 6 (Q3 - Axial Rifling Spin):** Option A selected. Add axial roll along the flight path forward axis parameterized by `_riflingSpinSpeed` (default 360 deg/sec) evaluated deterministically as $t \cdot \text{duration} \cdot \text{speed}$.

### Open Questions (Hard Gate - All Resolved)
*(None remaining. Proceeding to implementation.)*

---

## 3. Data Model & Serialization

### Component Fields on MortarShellProjectile
- [SerializeField] private Vector3 _modelRotationOffset = new Vector3(90f, 0f, 0f);
- [SerializeField] private AnimationCurve _customHeightCurve;
- [SerializeField] private float _riflingSpinSpeed = 360f;

### Prefab Inspector Setup
- Target Prefab: Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortalShell.prefab
- Verify _visualModel points to the Visual child GameObject.
- Set _modelRotationOffset = (90, 0, 0).

---

## 4. Architecture & Flight Mechanics Design

### Analytical Flight Trajectory Math
Let t in [0, 1] be normalized flight progress (t = elapsed / duration):

1. **Horizontal Position:**
   P_xz(t) = Vector3.Lerp(P_start, P_target, t)_xz

2. **Vertical Position:**
   - Parabolic formula:
     y(t) = Mathf.Lerp(P_start.y, P_target.y, t) + 4 * jumpPower * t * (1 - t)
   - Curve formula (if _customHeightCurve != null && _customHeightCurve.length > 0):
     y(t) = Mathf.Lerp(P_start.y, P_target.y, t) + jumpPower * _customHeightCurve.Evaluate(t)

3. **Tangent Velocity Vector V(t):**
   V(t) = P(t + delta) - P(t) with a small forward sample delta = 0.001f (or analytical derivative)

4. **Singularity-Free Tangent Orientation:**
   - Compute horizontal heading H = (V.x, 0, V.z).
   - If |H|^2 > 10^-6:
     - Yaw: theta_yaw = atan2(V.x, V.z) * Rad2Deg
     - Pitch: theta_pitch = -atan2(V.y, |H|) * Rad2Deg
     - Flight Rotation: Q_flight = Quaternion.Euler(theta_pitch, theta_yaw, currentRoll)
   - If falling strictly vertical (|H| ~ 0):
     - Maintain the last valid yaw heading and pitch down to -90 deg.
   - Final Rotation:
     Q_final = Q_flight * Quaternion.Euler(_modelRotationOffset)

---

## 5. Visual, Audio & Tweening Integration

- **DOTween Flow:**
  - Driven by DOVirtual.Float(0f, 1f, duration, UpdateFlightProgress).SetEase(Ease.Linear).
  - Calling KillTweens() cleanly terminates any active flight tween on OnDisable and before returning to pool.
- **Trail Renderer:**
  - Emits smoothly from the projectile transform. Because the projectile is oriented along velocity, the tail correctly trails backward.
- **Audio & Impact VFX:**
  - Existing AudioClipPlayer (SFX_MORTAR_IMPACT) and VFXPlayer trigger on Detonate().

---

## 6. Edge Cases, Performance & Lifecycle Invariants

- **Zero Allocation Invariant:** Trajectory position and quaternion updates in UpdateFlightProgress allocate 0 bytes on the heap.
- **Gimbal Lock Safety:** The explicit yaw/pitch calculation prevents Unity LookRotation zero-vector warnings and gimbal snapping when dropping vertically at t -> 1.
- **Pool Recycling:** When returned to MortarShellPool, tweens are killed, trail renderer cleared, and rotation reset.

---

## 7. Implementation Plan (Phases & Steps)

### Phase 1: Analytical Trajectory & Orientation Math
- [ ] **Step 1.1:** Update MortarShellProjectile.cs with _modelRotationOffset, _customHeightCurve, and _riflingSpinSpeed serialized fields.
- [ ] **Step 1.2:** Implement CalculatePosition(float t, Vector3 start, Vector3 target, float jumpPower) and CalculateTangent(float t, Vector3 start, Vector3 target, float jumpPower).
- [ ] **Step 1.3:** Replace DOJump with DOVirtual.Float evaluating exact analytical position and singularity-free tangent rotation with model offset.

### Phase 2: Prefab & Visual Integration
- [ ] **Step 2.1:** Inspect and configure MortalShell.prefab inspector values for _modelRotationOffset.
- [ ] **Step 2.2:** Verify trail renderer emits behind the shell and visual model turns off cleanly on detonation.

### Phase 3: Validation & Gate
- [ ] **Step 3.1:** Run dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false and verify zero compilation errors/warnings.
- [ ] **Step 3.2:** Execute pre-commit checks and verify coding standards compliance.

---

## 8. Verification & Acceptance Criteria
- [ ] Compilation succeeds with zero warnings: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
- [ ] Mortar projectile nose points forward along the parabolic trajectory at all times (ascending, peak, descending).
- [ ] Projectile does NOT fly sideways or land broadside.
- [ ] No gimbal lock or sudden 180-degree flip occurs when descending steeply onto the target.
- [ ] Inspector serialized fields allow easy orientation offset tuning without recompilation.
