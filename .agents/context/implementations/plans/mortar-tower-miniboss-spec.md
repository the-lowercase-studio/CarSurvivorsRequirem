# Specification: Mortar Tower Miniboss Encounter

Date: 2026-09-12  
Author: Antigravity  
Target Systems:
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/
- Assets/Scripts/Enemies/Bosses/Towers/Arena/
- Assets/Scripts/Camera/
- Assets/Scripts/UI/HUD/
- Assets/Scripts/ReflexDI/
- Assets/ScriptableObjects/Enemies/Bosses/

---

## 1. Overview & Player Experience

### Summary
The **Mortar Tower** is the first stationary miniboss encounter in Car Survivors. Pre-placed as a prominent industrial combat landmark within a bounded 22-meter combat arena in RuinedBloodCity.unity, the encounter tests car handling, high-speed drifting, and aerial trajectory anticipation. Unlike roaming bosses, the tower remains stationary, defended by heavy chassis plating and an impassable physical footprint, launching aerial artillery clusters, bouncing diagonal straight-line barrages, and rolling explosive boulders.

Crossing into the 22-meter combat perimeter triggers an elevated wide-angle camera transition via Cinemachine follow offset tweening, activates the Boss HUD health bar, summons an expanding visual arena boundary, and suppresses standard swarm waves. If the player flees beyond the arena boundary, a 2.5-second leash countdown begins; failing to return within the grace period cleanly resets the encounter, restores the tower to 100% health, and returns the camera and swarm timers to normal.

### Player-Facing Goals
- **High-Intensity Arena Duel:** High-speed evasion of distinct geometric artillery telegraphs (radial cluster, diagonal cross bounces, linear rolling boulders) while circling the stationary fortress.
- **Cinematic Visual Presentation:** Smooth dynamic camera expansion gives full arena awareness without jarring snaps.
- **Fair & Readable Telegraphs:** Ground fill indicators synchronized with parabolic shell flight times give clear, unambiguous dodge cues.
- **Enrage Rush:** At < 40% HP, the tower vents fire, shifts materials to glowing orange/red emission, and accelerates attack rotations with double concentric blast waves and triple diagonal salvos.

### In-Scope vs. Out-of-Scope
- **In-Scope:**
  - Stationary boss controller (MortarTowerBoss) implementing IHealth, IDamageable, IKnockable (knockback immune).
  - Configurable data asset (MortarTowerConfigSO) for health, enrage thresholds, arena dimensions, attack parameters, and timing.
  - State machine with 3 primary attacks:
    - Attack 1: Cluster Shrapnel Burst (central impact + 6 radial hex sub-shells; enrage 2-wave expanding rings).
    - Attack 2: Diagonal Artillery Line Bounce (offset center + 4 diagonal rays with 3 straight bounces; enrage 3 rapid salvos).
    - Attack 3: Mortar Rolling Boulder (landing drop + linear telegraph + rotating rolling movement with LinearAttackHitbox).
  - Modular arena perimeter & leash controller (EncounterArenaController) managing radius detection, visual perimeter circle with DOTween pulse, and 2.5s return countdown.
  - Leash countdown warning HUD presenter (ArenaLeashWarningPresenter) with scale punch countdown.
  - Camera combat offset controller (CinemachineCombatFollowOffsetController) smoothly tweening CinemachineFollow.FollowOffset via DOTween.
  - Swarm suppression integration via ISwarmFreezer.IsSuppressed.
  - Reusing IBossHUDPresenter / BossHUDPresenter for boss health display.
  - Walkable cell snapping for artillery impacts via WorldGrid / CircularTelegraphIndicator.
  - Dedicated object pool (MortarShellPool) for artillery sub-shells and bounce projectiles to ensure Zero-GC performance.
  - Reflex DI bindings in DefaultGameplaySceneInstaller.
- **Out-of-Scope:**
  - Complex skeletal animator controllers (all animations are purely procedural transform/recoil tweens via DOTween).
  - Wreckage destruction physics mesh fracturing (deferred to a subsequent art/visual pass; minimal clean disable on defeat).
  - Custom stage transition portal or victory drop items (relying on existing exp drop).
  - Secondary virtual camera asset churn (leveraging single CinemachineCamera follow offset tweening).
  - Spawner logic or debug spawn keys in BossManager (the tower is pre-placed directly in the scene as confirmed by design).

---

## 2. Open Questions & Resolved Decisions

### Resolved Decisions
- [x] **Decision 1 (Activation & World Placement):** The Mortar Tower is pre-placed directly into the scene hierarchy as a persistent combat landmark. No spawner logic or runtime debug spawn key is required.
- [x] **Decision 2 (Leash Reset Penalty):** Immediate 100% health restore upon leash timeout (2.5s) to eliminate hit-and-run burst cheesing.
- [x] **Decision 3 (Boss HUD Constraint):** Single active boss encounter is guaranteed; uses existing IBossHUDPresenter directly without multi-boss architectural extensions.
- [x] **Decision 4 (Projectile Pooling):** Dedicated IObjectPool<MortarShellProjectile> pooling for artillery shells and bounce sub-shells to maintain strict zero-allocation performance during high-density barrages.
- [x] **Decision 5 (Procedural Animation):** Pure DOTween procedural transform kickback, DOJump trajectories, and rolling boulder rotation; zero animator controller overhead.
- [x] **Decision 6 (Swarm Suppression):** Automatically freezes swarm spawning via ISwarmFreezer.IsSuppressed = true while encounter is active.
- [x] **Decision 7 (Camera Dynamic Framing):** Standalone CinemachineCombatFollowOffsetController tweening CinemachineFollow.FollowOffset from (0, 11, -7) to (0, 17, -13) over 0.8s with Ease.InOutSine.
- [x] **Decision 8 (Solid Physical Footprint):** Boss collision layer with impassable obstacle collision; player car cannot drive through the tower. Ground grid cells beneath the tower marked impassable.

### Open Questions
*None. All architectural and gameplay questions have been resolved and approved.*

---

## 3. Data Model & Serialization

### Constants

#### MortarTowerConstants
Path: Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Constants/MortarTowerConstants.cs
- `DEFAULT_MAX_HEALTH = 5000f`
- `DEFAULT_ENRAGE_PERCENT = 0.40f`
- `DEFAULT_ARENA_RADIUS = 22f`
- `DEFAULT_LEASH_TIME = 2.5f`
- `NORMAL_FOLLOW_OFFSET = new Vector3(0f, 11f, -7f)`
- `COMBAT_FOLLOW_OFFSET = new Vector3(0f, 17f, -13f)`
- `CAMERA_TWEEN_DURATION = 0.8f`
- `PROJECTILE_POOL_CAPACITY = 24`
- `PROJECTILE_POOL_MAX_SIZE = 64`
- Audio key constants:
  - `SFX_MORTAR_FIRE = "MortarFire"`
  - `SFX_MORTAR_IMPACT = "MortarImpact"`
  - `SFX_ENRAGE_ROAR = "MortarEnrage"`
  - `SFX_LEASH_TICK = "LeashTick"`
- Material property constants:
  - `BASE_COLOR_PROPERTY = "_BaseColor"`
  - `EMISSION_COLOR_PROPERTY = "_EmissionColor"`

#### ArenaConstants
Path: Assets/Scripts/Enemies/Bosses/Towers/Arena/Constants/ArenaConstants.cs
- `DEFAULT_EXPAND_DURATION = 0.5f`
- `DEFAULT_SHRINK_DURATION = 0.35f`
- `DEFAULT_PULSE_DURATION = 1.2f`
- `DEFAULT_PULSE_SCALE_DELTA = 0.04f`

### ScriptableObjects

#### MortarTowerConfigSO
Path: Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs
- **Health & Phase Configuration:**
  - `[SerializeField] private float _maxHealth = 5000f;`
  - `[SerializeField] private float _enrageHealthPercent = 0.40f;`
  - `[SerializeField] private float _expReward = 500f;`
  - `[SerializeField] private Color _enrageColor = new Color(1f, 0.2f, 0.1f);`
  - `[SerializeField] private Color _enrageEmissionColor = new Color(2f, 0.4f, 0.1f);`
- **Arena & Leash Settings:**
  - `[SerializeField] private float _arenaRadius = 22f;`
  - `[SerializeField] private float _leashGracePeriodSeconds = 2.5f;`
- **Camera Settings:**
  - `[SerializeField] private Vector3 _combatFollowOffset = new Vector3(0f, 17f, -13f);`
  - `[SerializeField] private float _cameraTransitionDuration = 0.8f;`
- **Attack 1 (Cluster Shrapnel Burst):**
  - `[SerializeField] private float _attack1InitialWarning = 1.2f;`
  - `[SerializeField] private float _attack1CenterExplosionRadius = 2.5f;`
  - `[SerializeField] private float _attack1CenterDamage = 30f;`
  - `[SerializeField] private int _attack1SubShellCount = 6;`
  - `[SerializeField] private float _attack1SubShellScatterRadius = 6.0f;`
  - `[SerializeField] private float _attack1SubShellJumpPower = 4.0f;`
  - `[SerializeField] private float _attack1SubShellDuration = 0.6f;`
  - `[SerializeField] private float _attack1SubShellExplosionRadius = 2.0f;`
  - `[SerializeField] private float _attack1SubShellDamage = 20f;`
  - `[SerializeField] private float _attack1EnrageSecondWaveRadius = 11.0f;`
- **Attack 2 (Diagonal Line Bounce):**
  - `[SerializeField] private float _attack2CenterMinOffset = 6.0f;`
  - `[SerializeField] private float _attack2CenterMaxOffset = 10.0f;`
  - `[SerializeField] private float _attack2InitialWarning = 0.9f;`
  - `[SerializeField] private int _attack2BounceStepCount = 3;`
  - `[SerializeField] private float _attack2BounceStepDistance = 3.5f;`
  - `[SerializeField] private float _attack2BounceJumpPower = 3.0f;`
  - `[SerializeField] private float _attack2BounceDurationPerStep = 0.45f;`
  - `[SerializeField] private float _attack2BounceExplosionRadius = 1.8f;`
  - `[SerializeField] private float _attack2BounceDamage = 22f;`
  - `[SerializeField] private int _attack2EnrageSalvoCount = 3;`
  - `[SerializeField] private float _attack2EnrageSalvoInterval = 0.4f;`
- **Attack 3 (Rolling Boulder):**
  - `[SerializeField] private float _attack3DropWarningDuration = 1.0f;`
  - `[SerializeField] private float _attack3RollTelegraphLength = 20.0f;`
  - `[SerializeField] private float _attack3RollTelegraphWidth = 2.5f;`
  - `[SerializeField] private float _attack3RollSpeed = 16.0f;`
  - `[SerializeField] private float _attack3EnrageRollSpeed = 22.0f;`
  - `[SerializeField] private float _attack3RollDamage = 35f;`
- **Cooldown & Phase Modifiers:**
  - `[SerializeField] private float _cooldownMin = 3.5f;`
  - `[SerializeField] private float _cooldownMax = 4.5f;`
  - `[SerializeField] private float _enrageCooldownMin = 2.0f;`
  - `[SerializeField] private float _enrageCooldownMax = 2.5f;`

---

## 4. Architecture & Reflex DI Contracts

### Component Boundaries & Ownership
The encounter is partitioned into distinct single-responsibility modules:
1. **EncounterArenaController:** Detects player entry/exit via radius math, controls the visual perimeter circle, runs the leash timer, and fires high-level arena lifecycle events.
2. **MortarTowerBoss:** Owns health, combat state machine, enrage visuals, and coordinates attacks. Responds to arena reset/defeat events.
3. **CinemachineCombatFollowOffsetController:** Owns tweening the Cinemachine follow offset. Decoupled from boss logic; driven via interface.
4. **ArenaLeashWarningPresenter:** Owns the screen HUD warning text and tick animation when outside the arena.
5. **MortarShellPool:** Manages `IObjectPool<MortarShellProjectile>` for zero GC allocation.

```
       +------------------------------------+
       |       EncounterArenaController     |
       |  - Radius check (Distance squared) |
       |  - Visual perimeter circle mesh    |
       |  - 2.5s Leash Countdown FSM       |
       +-----------------+------------------+
                         |
        +----------------+----------------+----------------+
        |                                 |                |
        v                                 v                v
+------------------+             +-----------------+ +-------------------+
| MortarTowerBoss  |             | ICinemachine... | | IArenaLeash...    |
| - Health / Damage|             | OffsetController| | WarningPresenter  |
| - State Machine  |             +-----------------+ +-------------------+
| - Enrage Visuals |
+--------+---------+
         |
         +----------------------------------+
         |                                  |
         v                                  v
+--------------------+            +--------------------+
| MortarShellPool    |            | LinearAttackHitbox |
| (Pooled Shells)    |            | (Rolling Boulder)  |
+--------------------+            +--------------------+
```

### Interfaces

#### IMortarTowerBoss
Path: Assets/Scripts/Enemies/Bosses/Towers/MortarTower/IMortarTowerBoss.cs
```csharp
namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower
{
    public interface IMortarTowerBoss : IDamageable, IKnockable
    {
        IHealth Health { get; }
        MortarTowerConfigSO Config { get; }
        Transform Transform { get; }
        bool IsEnraged { get; }
        event Action<IMortarTowerBoss> OnBossDefeated;
        void StartEncounter();
        void ResetEncounter();
    }
}
```

#### IEncounterArenaController
Path: Assets/Scripts/Enemies/Bosses/Towers/Arena/IEncounterArenaController.cs
```csharp
namespace Assets.Scripts.Enemies.Bosses.Towers.Arena
{
    public interface IEncounterArenaController
    {
        bool IsEncounterActive { get; }
        bool IsLeashCountdownActive { get; }
        float RemainingLeashSeconds { get; }
        float ArenaRadius { get; }
        event Action OnPlayerEnteredArena;
        event Action OnPlayerExitedArena;
        event Action<float> OnLeashCountdownTick;
        event Action OnEncounterReset;
        event Action OnEncounterCompleted;
    }
}
```

#### ICinemachineCombatFollowOffsetController
Path: Assets/Scripts/Camera/ICinemachineCombatFollowOffsetController.cs
```csharp
namespace Assets.Scripts.Camera
{
    public interface ICinemachineCombatFollowOffsetController
    {
        void TransitionToCombatOffset(Vector3 combatOffset, float duration, Ease ease = Ease.InOutSine);
        void RestoreDefaultOffset(float duration, Ease ease = Ease.InOutSine);
    }
}
```

#### IArenaLeashWarningPresenter
Path: Assets/Scripts/UI/HUD/IArenaLeashWarningPresenter.cs
```csharp
namespace Assets.Scripts.UI.HUD
{
    public interface IArenaLeashWarningPresenter
    {
        void ShowWarning(float remainingSeconds);
        void UpdateCountdown(float remainingSeconds);
        void Hide();
    }
}
```

#### IMortarShellProjectile
Path: Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/IMortarShellProjectile.cs
```csharp
namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles
{
    public interface IMortarShellProjectile
    {
        void LaunchParabolic(Vector3 startPosition, Vector3 targetPosition, float jumpPower, float duration, Action onLanded);
        void Detonate(float radius, float damage);
        void ReturnToPool();
    }
}
```

### State Machine Architecture
Path: Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/
- `MortarTowerStateMachine`: Lightweight state container with `Initialize`, `ChangeState`, `Update`, and `FixedUpdate`.
- `IMortarTowerState`: `Enter()`, `Exit()`, `Update()`, `FixedUpdate()`.
- Concrete States:
  - `MortarIdleState`: Waits for player entry trigger via `StartEncounter()`.
  - `MortarClusterBurstState`: Implements Attack 1 (central player impact + 6 radial hex sub-shells with enrage double ring).
  - `MortarDiagonalBounceState`: Implements Attack 2 (random offset center + 4 diagonal rays with 3 straight-line DOJump bounces, enrage 3-salvo repeat).
  - `MortarRollingBoulderState`: Implements Attack 3 (aerial drop + linear telegraph + rotating rolling boulder via `LinearAttackHitbox`).
  - `MortarCooldownState`: Inter-attack pause with randomized duration derived from config (adjusted for enrage phase).
  - `MortarDefeatedState`: Disables hitbox, halts active tweens, dismisses telegraphs, and notifies subscribers.

### Reflex DI Wiring
In Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs:
- Serialized fields:
  - `[SerializeField] private CinemachineCombatFollowOffsetController _cinemachineCombatFollowOffsetController;`
  - `[SerializeField] private ArenaLeashWarningPresenter _arenaLeashWarningPresenter;`
- Installer bindings:
  - `builder.AddSingleton(_cinemachineCombatFollowOffsetController, typeof(ICinemachineCombatFollowOffsetController));`
  - `builder.AddSingleton(_arenaLeashWarningPresenter, typeof(IArenaLeashWarningPresenter));`

---

## 5. Visual, Audio & Tweening Integration

### Tower Barrel Recoil & Idle Animations
- Procedural transform tweening on artillery fire:
  - Barrel pitches upward using `DOPunchRotation(new Vector3(-15f, 0f, 0f), 0.35f, 4, 0.5f)`.
  - Turret housing kicks backward using `DOLocalMove(recoilOffset, 0.1f).SetEase(Ease.OutQuad)` followed by smooth return `DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.InOutSine)`.

### Visual Perimeter Border
- Reuses circular plane mesh at `IndicatorConstants.GROUND_Y_OFFSET`:
  - When player crosses into radius: `DOScale(new Vector3(arenaDiameter, 1f, arenaDiameter), 0.5f).SetEase(Ease.OutBack)`.
  - Ongoing idle: subtle breathing pulse via `DOScale(targetScale * 1.04f, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)`.
  - On reset or defeat: `DOScale(Vector3.zero, 0.35f).SetEase(Ease.InQuad)`.

### Projectile Parabolic Flight & Rolling Physics
- Parabolic shells:
  - Driven via `transform.DOJump(targetLandingPos, jumpPower, 1, duration).SetEase(Ease.Linear)`.
  - Synchronized with `CircularTelegraphIndicator` fill duration to guarantee telegraph fill reaches 100% precisely at impact.
- Rolling boulder:
  - Rotates continuously along horizontal pitch axis via `transform.DORotate(new Vector3(360f, 0f, 0f), 0.5f, RotateMode.LocalAxisAdd).SetLoops(-1, LoopType.Incremental)`.
  - Linear translation along target trajectory via `transform.DOMove(endPos, duration).SetEase(Ease.Linear)`.

### Material & VFX Enrage Transition
- When health drops below 40%:
  - Iterates through registered renderers using `MaterialPropertyBlock`.
  - Sets `_BaseColor` to glowing orange/red.
  - Sets `_EmissionColor` to high-intensity HDR emission value.
  - Triggers smoke/flame particle burst via `VFXPlayer`.
  - Plays enrage siren/roar audio clip.

---

## 6. Edge Cases, Performance & Lifecycle Invariants

### Zero-GC Frame Budget
- Distance evaluations in `EncounterArenaController.Update()` use `(transform.position - playerPos).sqrMagnitude <= sqrRadius` (no `Vector3.Distance` square root, zero heap allocations).
- Shell projectiles are drawn from and returned to a pre-allocated `IObjectPool<MortarShellProjectile>`.
- Collider overlap queries in explosion damage use pre-allocated buffers (`Physics.OverlapSphereNonAlloc`).
- Warning countdown UI formats integer seconds without string interpolation allocations where feasible, or caches string ticks.

### Safe Unity Lifecycle & Tween Management
- All DOTween sequences and tweens (`Sequence`, `Tween`) are explicitly killed in `OnDisable()` and `OnDestroy()`.
- Active telegraph indicators are tracked in a list and dismissed via `DismissAllTelegraphs()` whenever combat resets or ends.
- Leash countdown coroutine/timer is killed immediately upon re-entering the perimeter.
- Swarm spawner suppression flag (`ISwarmFreezer.IsSuppressed`) is guaranteed to be restored to `false` on encounter reset, tower defeat, or when the tower GameObject is disabled.

### Grid Snapping Invariant
- Artillery landing points (central impact and radial/diagonal offsets) are converted to grid cells via `WorldGrid` and snapped to the nearest walkable cell using the established spiral search algorithm in `CircularTelegraphIndicator.SnapToWalkableCell()`. This ensures no shells detonate inside impassable map geometry.

---

## 7. Implementation Plan (Phases & Steps)

### Phase 1: Constants, Config & Core Infrastructure
- [ ] **Step 1.1:** Create constants in Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Constants/MortarTowerConstants.cs and Assets/Scripts/Enemies/Bosses/Towers/Arena/Constants/ArenaConstants.cs.
  - Verification: Compile via `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- [ ] **Step 1.2:** Implement `MortarTowerConfigSO` under Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs with all serialized balance parameters.
  - Verification: Compile check.
- [ ] **Step 1.3:** Implement `ICinemachineCombatFollowOffsetController` and `CinemachineCombatFollowOffsetController` under Assets/Scripts/Camera/.
  - Verification: Compile check.
- [ ] **Step 1.4:** Implement `IArenaLeashWarningPresenter` and `ArenaLeashWarningPresenter` under Assets/Scripts/UI/HUD/.
  - Verification: Compile check.
- [ ] **Step 1.5:** Register camera controller and leash warning presenter in Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs.
  - Verification: Compile check; verify Reflex DI bindings.

### Phase 2: Encounter Arena & Leash Management
- [ ] **Step 2.1:** Implement `IEncounterArenaController` and `EncounterArenaController` under Assets/Scripts/Enemies/Bosses/Towers/Arena/.
  - Includes distance math, perimeter circle plane scaling, breathing pulse tween, and 2.5s leash countdown timer.
  - Verification: Compile check.
- [ ] **Step 2.2:** Wire arena events to camera offset transition, swarm suppression (`ISwarmFreezer.IsSuppressed`), Boss HUD (`IBossHUDPresenter.Show/Hide`), and leash warning presenter.
  - Verification: Compile check; lifecycle inspection.

### Phase 3: Projectiles & Artillery Attack Pipeline
- [ ] **Step 3.1:** Implement `IMortarShellProjectile` and `MortarShellProjectile` under Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/ with parabolic `DOJump` movement and impact damage.
  - Verification: Compile check.
- [ ] **Step 3.2:** Implement `MortarShellPool` utilizing `UnityEngine.Pool.IObjectPool` for zero-allocation shell management.
  - Verification: Compile check.
- [ ] **Step 3.3:** Implement Attack 1 (Cluster Shrapnel Burst) with hexagonal sub-shell scatter and enrage double rings.
  - Verification: Compile check.
- [ ] **Step 3.4:** Implement Attack 2 (Diagonal Line Bounce) with 4-ray 3-step straight-line bounces and enrage triple salvos.
  - Verification: Compile check.
- [ ] **Step 3.5:** Implement Attack 3 (Rolling Boulder) with linear telegraph aiming and `LinearAttackHitbox` rolling collision.
  - Verification: Compile check.

### Phase 4: Mortar Tower Boss Controller, Enrage & Scene Integration
- [ ] **Step 4.1:** Implement `IMortarTowerBoss` and `MortarTowerBoss` under Assets/Scripts/Enemies/Bosses/Towers/MortarTower/ with health management, damage receiving, knockback immunity, and state machine transitions.
  - Verification: Compile check.
- [ ] **Step 4.2:** Integrate enrage material swap (`MaterialPropertyBlock`), VFX sparks/smoke, and audio clips.
  - Verification: Compile check.
- [ ] **Step 4.3:** Integrate defeat flow: arena border collapse, camera restoration, swarm unfreezing, exp particle drop, and entity disable.
  - Verification: Compile check.
- [ ] **Step 4.4:** Run full project compilation check and coding standards verification.
  - Verification: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` exits with 0 errors.

---

## 8. Verification & Acceptance Criteria

### Automated Checks
- Solution compiles cleanly with zero errors:
  ```powershell
  dotnet build Assembly-CSharp-firstpass.csproj
  dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  ```
- No compiler warnings introduced in new scripts.

### Manual Verification
1. **Perimeter Entry & Combat Initialization:**
   - Drive the car across the 22m perimeter boundary.
   - Verify visual circular border plane expands from 0 to 22m and begins gentle pulse.
   - Verify Cinemachine camera smoothly elevates to `(0, 17, -13)` over 0.8s.
   - Verify Boss HUD appears displaying "MORTAR TOWER" and 5000 HP.
   - Verify swarm spawner timers freeze (`ISwarmFreezer.IsSuppressed = true`).
2. **Attack 1 (Cluster Shrapnel Burst):**
   - Verify central circular telegraph forms on player vehicle position.
   - Verify mortar barrel recoils and shell impacts center.
   - Verify 6 sub-shells launch simultaneously via `DOJump` into a regular hexagon at 6m radius.
   - Verify sub-shells detonate with secondary explosions.
3. **Attack 2 (Diagonal Line Bounce):**
   - Verify initial landing occurs at a random offset point (6–10m away from player).
   - Verify 4 diagonal rays (45°, 135°, 225°, 315°) spawn 3 successive outward bounces along straight lines.
   - Verify telegraph indicators lead each bounce impact.
4. **Attack 3 (Rolling Boulder):**
   - Verify landing telegraph appears in arena.
   - Verify rectangular telegraph projects towards player vehicle.
   - Verify boulder lands, rotates continuously, and rolls along the linear track at 16m/s dealing collision damage.
5. **Leash Exit & Reset Flow:**
   - Drive car outside the 22m perimeter during combat.
   - Verify `"RETURN TO ARENA: 2.5s"` UI banner appears and counts down with scale punch.
   - Re-enter within 2.5s: warning hides, combat continues seamlessly.
   - Remain outside until 0s: encounter resets completely, tower HP restores to 5000 (100%), all telegraphs/shells despawn, camera returns to `(0, 11, -7)`, arena border collapses, and swarm timer unfreezes.
6. **Enrage Phase:**
   - Damage tower below 2000 HP (40%).
   - Verify tower switches to emissive red/orange material, plays enrage audio, and speeds up cooldowns.
   - Verify Attack 1 produces 2 concentric blast rings.
   - Verify Attack 2 fires 3 rapid salvos in succession.
7. **Defeat Sequence:**
   - Deplete tower health to 0.
   - Verify Boss HUD hides, camera smoothly returns, arena perimeter collapses, swarms resume, exp particles spawn, and tower deactivates cleanly.
