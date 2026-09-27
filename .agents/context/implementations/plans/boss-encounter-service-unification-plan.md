# Implementation Plan - Boss Encounter Service Unification

Date: 2026-09-27

## Overview

Unify boss encounter management under a single centralized coordination service: `BossEncounterService` (`IBossEncounterService`). The game currently features two distinct boss categories:
1. **Static Pre-Placed Landmarks**: Immovable fortification bosses already present in the scene hierarchy (such as `MortarTowerBoss` in `RuinedBloodCity.unity` managed locally by `EncounterArenaController`).
2. **Conditional Dynamic Spawns**: Roaming stage-culmination bosses instantiated at runtime upon meeting specific game progression requirements (such as `GolemBoss`).

Under the new gameplay progression rule:
- `GolemBoss` cannot spawn while towers are active.
- Defeating all required towers (currently 1 placed in `RuinedBloodCity.unity`, planned for 2) is a mandatory prerequisite that triggers the spawn of `GolemBoss`.
- Centralize cross-cutting encounter concerns (Boss HUD presentation via `IBossHUDPresenter`, swarm wave suppression via `ISwarmFreezer`, encounter state tracking, and stage portal instantiation) inside `BossEncounterService`, removing duplicate HUD and freeze logic from individual encounter controllers.

---

## User Review Required

> [!IMPORTANT]
> - **Replacement of `BossManager` with `BossEncounterService`**: `BossManager.cs` currently has zero external consumers and acts solely as a hardcoded Golem spawner. We propose replacing `BossManager` with `BossEncounterService`, updating `DefaultGameplaySceneInstaller.cs` to bind `IBossEncounterService`, and migrating the scene GameObject in `RuinedBloodCity.unity`.
> - **Tower Defeat Condition & Adaptive Count**: By default, `_requiredTowersForGolemSpawn` will be serialized and configurable (defaulting to 2). An automatic clamp `Mathf.Min(_requiredTowersForGolemSpawn, registeredTowers.Count)` ensures Golem spawns once all placed towers in the scene are destroyed (supporting 1 tower currently in `RuinedBloodCity.unity` without softlocking, and scaling cleanly to 2 towers once the designer places the second tower).
> - **Spawn Delay**: As confirmed by user, after destroying the final required tower, an initial delay of 10 seconds (`_delayBeforeGolemSpawn = 10.0f`) will elapse before `GolemBoss` spawns, giving the player breathing room to collect EXP and recover.
> - **Debug Spawn Key**: KeyCode `P` remains active for QA/debugging to force-spawn `GolemBoss` immediately, logging an editor warning if towers were bypassed.

---

## Resolved Decisions

- [x] **Pacing Delay**: Confirmed by user. After defeating the final tower, `GolemBoss` will spawn after a 10.0-second delay (`_delayBeforeGolemSpawn = 10.0f`) to allow tower destruction VFX and EXP drop collection before boss engagement.
- [x] **Debug Spawn Key**: Confirmed by user. Retain KeyCode `P` for developer/QA testing to force-spawn `GolemBoss` immediately.
- [x] **Adaptive Tower Count**: `_requiredTowersForGolemSpawn` defaults to 2 in code, but dynamically clamps to the number of registered towers if fewer are placed in the scene (e.g. 1 in current `RuinedBloodCity.unity`).

## Open Questions

- *None.*

---

## Proposed Changes

### Boss Domain Core (`Assets/Scripts/Enemies/Bosses/`)

#### [NEW] Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
- Define interface `IBossEncounterService` colocated above `BossEncounterService`:
  - `bool IsAnyBossActive { get; }`
  - `int RequiredTowersCount { get; }`
  - `int DefeatedTowersCount { get; }`
  - `int RemainingTowersCount { get; }`
  - `void RegisterTower(IEncounterArenaController towerArena);`
  - `void UnregisterTower(IEncounterArenaController towerArena);`
  - `void NotifyEncounterEngaged(IHealth bossHealth, string displayName);`
  - `void NotifyEncounterDisengaged();`
  - `void NotifyTowerDefeated(IEncounterArenaController towerArena);`
  - `void SpawnGolemBoss(Vector3? spawnPosition = null);`
  - `event Action<int, int> OnTowerDefeated;`
  - `event Action<IGolemBoss> OnGolemSpawned;`
  - `event Action<IGolemBoss> OnGolemDefeated;`
- Class `BossEncounterService : MonoBehaviour, IBossEncounterService`:
  - `[Inject]` dependencies:
    - `[Inject] private readonly IBossHUDPresenter _bossHUDPresenter = null;`
    - `[Inject] private readonly ISwarmFreezer _swarmFreezer = null;`
    - `[Inject] private readonly IPlayerManager _playerManager = null;`
  - `[SerializeField]` fields:
    - `[SerializeField] private GolemBoss _golemBossPrefab;`
    - `[SerializeField] private GameObject _nextStagePortalPrefab;`
    - `[SerializeField] private string _golemDisplayName = "ANCIENT GOLEM";`
    - `[SerializeField] private float _spawnOffsetDistance = 15f;`
    - `[SerializeField] private int _requiredTowersForGolemSpawn = 2;`
    - `[SerializeField] private float _delayBeforeGolemSpawn = 10.0f;`
    - `[SerializeField] private KeyCode _debugSpawnKey = KeyCode.P;`
  - Responsibilities:
    - Centralize `_bossHUDPresenter.Show(health, name)` and `Hide()`.
    - Centralize `_swarmFreezer.IsSuppressed = true/false`.
    - Track list of registered tower arena controllers and defeated tower count.
    - Check condition: when `DefeatedTowersCount >= EffectiveRequiredTowers`, trigger `SpawnGolemBoss` after `_delayBeforeGolemSpawn` (10s).
    - Spawn stage progression portal upon `GolemBoss` defeat.
    - Safe cleanup on `OnDestroy` / `OnDisable`.

#### [DELETE] Assets/Scripts/Enemies/Bosses/BossManager.cs
- Remove obsolete `BossManager.cs` whose duties are fully subsumed and expanded by `BossEncounterService.cs`.

---

### Tower Boss Domain (`Assets/Scripts/Enemies/Bosses/Towers/Arena/`)

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs
- Resolve/inject `IBossEncounterService`.
- On `Start()` / `OnEnable()`: Register itself via `_bossEncounterService.RegisterTower(this)`.
- On `OnDisable()` / `OnDestroy()`: Unregister itself via `_bossEncounterService.UnregisterTower(this)`.
- In `StartCombatEncounter()`:
  - Delegate HUD and swarm freezing to `_bossEncounterService.NotifyEncounterEngaged(_boss.Health, _bossDisplayName)`.
  - Remove direct calls to `_bossHUDPresenter.Show` and `_swarmFreezer.IsSuppressed = true`.
- In `ResetCombatEncounter()`:
  - Delegate HUD and swarm unfreezing to `_bossEncounterService.NotifyEncounterDisengaged()`.
  - Remove direct calls to `_bossHUDPresenter.Hide` and `_swarmFreezer.IsSuppressed = false`.
- In `Boss_OnBossDefeated()`:
  - Call `_bossEncounterService.NotifyTowerDefeated(this)`.
  - Remove direct calls to `_bossHUDPresenter.Hide` and `_swarmFreezer.IsSuppressed = false` (handled centrally).

---

### Dependency Injection & Scene Integration

#### [MODIFY] Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Replace `[SerializeField] private BossManager _bossManager;` with `[SerializeField] private BossEncounterService _bossEncounterService;`.
- Replace `builder.AddSingleton(_bossManager, typeof(IBossManager));` with `builder.AddSingleton(_bossEncounterService, typeof(IBossEncounterService));`.

#### [MODIFY] Assets/Scenes/RuinedBloodCity.unity
- Update GameObject `BossManager` (rename to `BossEncounterService`).
- Swap script component to `BossEncounterService`.
- Wire `_bossEncounterService` reference into `DefaultGameplaySceneInstaller`.
- Set `_requiredTowersForGolemSpawn = 1` (or 2 with adaptive clamp).
- Set `_delayBeforeGolemSpawn = 10`.

---

## Edge Cases, Performance & Lifecycle Invariants

1. **Zero GC in Update**:
   - Encounter registration occurs in `Awake`/`Start`.
   - Update loop only polls optional debug key `_debugSpawnKey`.
2. **Deterministic Swarm State**:
   - `_swarmFreezer.IsSuppressed` is managed by an integer ref-count or active encounter state in `BossEncounterService` so that disengaging from one fight or resetting does not corrupt swarm timers.
3. **Leash Reset Safety**:
   - If player flees a tower, the leash timer resets the tower to 100% HP and calls `NotifyEncounterDisengaged()`, cleanly restoring normal exploration and swarm spawning.
4. **Tower Destruction Tracking**:
   - Destroyed towers are tracked by instance reference in a `HashSet<IEncounterArenaController>` to prevent duplicate defeat invocations.
5. **Event Unsubscription**:
   - `OnBossDefeated` on Golem and Tower boss instances are cleanly unsubscribed in `OnDisable`/`OnDestroy`.

---

## Verification Plan

### Automated Checks
- Solution compilation without warnings:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. **Tower Combat & HUD Flow**:
   - Enter Mortar Tower arena in `RuinedBloodCity.unity`.
   - Verify `BossHUDPresenter` shows "MORTAR TOWER" and swarm spawns freeze.
   - Exit arena and let 2.5s leash expire; verify HUD hides, camera returns, and swarms resume.
2. **Single Tower Destruction & Golem Spawn Pacing**:
   - Engage and defeat the Mortar Tower.
   - Verify Mortar Tower plays defeat flow and EXP drops.
   - Verify a 10-second delay occurs before `Ancient Golem` spawns.
   - Verify `BossHUDPresenter` updates to "ANCIENT GOLEM" and swarm spawns freeze for Golem fight.
3. **Golem Defeat & Stage Progression**:
   - Defeat the Golem.
   - Verify next stage portal spawns at Golem location, HUD closes, and swarms unfreeze.
4. **Debug Spawn Key**:
   - Press `P` key at any point; verify Golem spawns cleanly for testing.
