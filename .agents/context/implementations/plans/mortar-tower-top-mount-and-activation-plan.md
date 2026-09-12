# Implementation Plan - Mortar Tower Top-Mount Integration & Encounter Activation

Date: 2026-09-12

Investigation and architectural fixes for the Mortar Tower miniboss to address two critical defects:
1. **Fight Not Starting / Boss Not Responding**: The boss currently does not engage the player when approached or attacked.
2. **Top-Mounted Mortar Turret (`MortalHolder` at y = 7.92m)**: The implementation did not account for the mortar turret being mounted on top of the stone tower, causing recoil animations to teleport it 8 meters down into the tower base and failing to provide horizontal aiming/tracking and correct artillery height physics.

## User Review Required

> [!IMPORTANT]
> - **Dependency Injection Crash Resolved**: In `MortarTowerBoss.cs`, `_damageNumbersSpawner` was marked with `[Inject]`, but `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>` is not registered in `DefaultGameplaySceneInstaller` (it was only bound in `BootLoader` which is destroyed upon scene load). Because `MortalTower` is statically placed in `RuinedBloodCity.unity`, Reflex threw an unhandled `FieldInjectorException` during scene load, breaking the injection pipeline and leaving `EncounterArenaController._playerManager` null (preventing `Update()` from running proximity checks). We will make spawner/HUD/camera dependencies safely optional via `Container.HasBinding` / fallback resolution.
> - **Damage-Triggered Encounter Activation**: In addition to proximity (22m), `MortarTowerBoss.TakeDamage` will immediately trigger `StartEncounter()` if the tower is still in `MortarIdleState`, ensuring that players attacking from long range or outside the arena perimeter will reliably engage the boss.
> - **Turret Yaw Aiming (`MortalHolder`)**: The mortar turret mounted on `MortalHolder` (local position `y = 7.92m`) will now rotate around its vertical Y axis to aim at the player horizontally during combat and before firing attacks.
> - **Recoil Position Fix**: `PlayFireRecoil()` will store `_initialHousingLocalPos` and tween relative to that elevated position rather than resetting to `Vector3.zero` (which previously submerged the mortar into the stone tower).
> - **Rolling Boulder Prefab Instantiation**: `MortarTowerBoss` will instantiate `_boulderHitbox` if assigned a project prefab asset rather than a scene instance, ensuring attack 3 executes safely.

## Open Questions

- None. All requirements are clarified by the scene setup, prefab hierarchy, and user directives.

## Proposed Changes

### Boss Controller & Turret Mounting

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs
- **Safe DI Injection**: Replace strict `[Inject]` on `_damageNumbersSpawner` with `[Inject] private readonly Reflex.Core.Container _container = null;`. In `Awake()`, safely resolve `_damageNumbersSpawner` if `_container != null && _container.HasBinding<IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>>()`. This guarantees Reflex will never throw an unhandled `FieldInjectorException` during scene load.
- **Turret Initial Position Preservation**: In `Awake()`, cache `_initialHousingLocalPosition = _turretHousing != null ? _turretHousing.localPosition : Vector3.zero;`.
- **Turret Recoil Fix**: In `PlayFireRecoil()`, tween `_turretHousing` from its current position to `_initialHousingLocalPosition - _turretHousing.forward * 0.35f` and back to `_initialHousingLocalPosition` (never `Vector3.zero`).
- **Turret Horizontal Tracking**: Add `UpdateTurretAim(float deltaTime, float turnSpeed = 5f)` to smoothly rotate `_turretHousing` (`MortalHolder`) on the Y-axis to face `PlayerPosition`. Call `UpdateTurretAim` during active combat states.
- **Damage-Triggered Encounter Activation**: In `TakeDamage()`, if `_stateMachine.CurrentState == _idleState`, call `StartEncounter()` and notify `EncounterArenaController` so any player attack immediately starts combat.
- **Prefab Instantiation Safety for Boulder Hitbox**: In `Awake()`, if `_boulderHitbox != null && !_boulderHitbox.gameObject.scene.IsValid()`, instantiate it as a child of the boss and deactivate it until needed.
- **Autonomous Proximity Fallback**: In `Update()`, if in `_idleState` and `_playerManager != null && _playerManager.GameObject != null`, perform a fallback distance check (22m) to initiate combat if `EncounterArenaController` is not present.

---

### Arena Encounter Controller

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs
- **Resilient DI Resolution**: In `Awake()`, ensure optional Reflex dependencies (`_cameraController`, `_leashWarningPresenter`, `_bossHUDPresenter`, `_swarmFreezer`) are resolved safely without throwing if absent from the scene installer.
- **Boss Auto-Assignment**: In `Awake()`, if `_boss == null`, auto-bind `GetComponent<MortarTowerBoss>()`.
- **Player Manager Fallback**: If `_playerManager == null`, attempt resolution via `_container` or runtime player lookup.
- **Expose Public Wakeup Method**: Add `NotifyDirectCombatEngaged()` so when `MortarTowerBoss.TakeDamage` triggers combat, the arena perimeter, HUD, camera, and leash are properly engaged.

---

### Attack States & Projectiles

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs
- **Aim Turret Towards Drop Position**: Before launching the drop shell, rotate `_turretHousing` to face `dropPos`.
- **Ground Snapping for Drop & Roll**: Explicitly ensure `dropPos.y = 0f` and `endPos.y = 0f`, so the boulder launches from `MuzzlePosition` (`y = 8m`) down to ground level and rolls along the ground.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarClusterBurstState.cs
- **Aim Turret Towards Target**: In `Enter()`, snap/rotate `_turretHousing` to face `targetPosition` before firing.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarDiagonalBounceState.cs
- **Aim Turret Towards Salvo Center**: In `RunSingleSalvo()`, snap/rotate `_turretHousing` to face `snappedCenter` before firing.

#### [MODIFY] Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs
- **Trajectory Orientation**: During `LaunchParabolic`, dynamically orient the projectile mesh along its parabolic velocity trajectory rather than maintaining a static orientation.

---

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. Open `Assets/Scenes/RuinedBloodCity.unity` and press Play.
2. Verify in the Console that no Reflex `FieldInjectorException` or null reference warnings are thrown.
3. Drive towards `MortalTower`:
   - At 22 meters, verify the combat arena border animates in, the Boss HUD appears with "MORTAR TOWER", camera transitions to combat follow offset, and normal swarm enemies freeze.
   - Verify `MortalHolder` remains mounted at height `y = 7.92m` atop the tower and rotates to track the player car.
   - Verify attack 1 (cluster burst) fires from the elevated `MortalTip` with muzzle recoil that does not sink into the tower.
   - Verify attack 2 (diagonal bounce) salvos track the player and bounce diagonally across the arena.
   - Verify attack 3 launches a shell from the top of the tower down to the ground, followed by the rolling boulder rolling along the ground towards the player.
4. Test damage wakeup: Restart, shoot the tower from >25m distance (outside perimeter), verify combat starts immediately.
