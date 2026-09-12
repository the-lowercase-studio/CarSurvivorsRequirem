# Implementation Summary - Mortar Tower Miniboss Encounter

Date: 2026-09-12

## Overview

Implemented the Mortar Tower Miniboss Encounter according to the staff-level technical specification in .agents/context/implementations/plans/mortar-tower-miniboss-spec.md, and refined following the architecture review. The Mortar Tower is the first stationary miniboss encounter in Car Survivors, featuring an impassable physical footprint, solid collision, a modular 22-meter combat arena with automatic leash detection, Cinemachine follow offset camera transition, Boss HUD integration, swarm spawner suppression, and three distinct geometric artillery attacks (Cluster Shrapnel Burst, Diagonal Line Bounce, and Rolling Boulder).

## Key Changes

### Constants & Configuration
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Constants/MortarTowerConstants.cs: Default health, enrage thresholds, camera offsets, pool capacities, audio clip keys, and material property identifiers.
- Assets/Scripts/Enemies/Bosses/Towers/Arena/Constants/ArenaConstants.cs: Expansion, shrink, and pulse timing constants for the arena perimeter visual plane.
- Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs: ScriptableObject holding all designer-authored parameters for health, arena leash, camera transitions, attacks 1 through 3, and cooldowns.

### Camera & UI Presentation
- Assets/Scripts/Camera/CinemachineCombatFollowOffsetController.cs: Implements ICinemachineCombatFollowOffsetController to smoothly tween CinemachineFollow.FollowOffset between default and combat offsets via DOTween, including inspector tooltips.
- Assets/Scripts/UI/HUD/ArenaLeashWarningPresenter.cs: Implements IArenaLeashWarningPresenter with dynamic RETURN TO ARENA countdown, scale punch feedback, and inspector tooltips.
- Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs: Added Reflex DI singleton bindings for ICinemachineCombatFollowOffsetController and IArenaLeashWarningPresenter.

### Arena & Leash Controller
- Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs: Implements IEncounterArenaController using Zero-GC squared distance checks, managing the 22m perimeter boundary, DOTween visual scaling and idle pulse, 2.5s leash countdown timer, Boss HUD activation/hiding, swarm freezer suppression, and camera transitions.
- Added IsEncounterCompleted gate to prevent encounter re-initialization when the boss is defeated while the player remains inside the arena.
- Ensured _isEncounterActive is cleanly reset to false on controller disable.

### Projectiles & Rolling Hitbox
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellProjectile.cs: Parabolic artillery shell with DOJump trajectory, OverlapSphereNonAlloc player damage, impact VFX, SFX, and delayed pool release allowing explosion visuals and sound to complete before entity deactivation.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Projectiles/MortarShellPool.cs: Dedicated object pool utilizing UnityEngine.Pool.IObjectPool for Zero-GC projectile management with safe while-loop tail release in ReturnAll().
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/Combat/MortarBoulderHitbox.cs: Rolling boulder hitbox with linear translation, procedural continuous rotation, and OverlapBoxNonAlloc collision damage with inspector tooltips.

### State Machine & Boss Controller
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/IMortarTowerState.cs: Core state interface.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/MortarTowerStateMachine.cs: State container and transition manager.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarIdleState.cs: Passive state awaiting arena activation.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarClusterBurstState.cs: Attack 1 - central impact shell plus 6-subshell hexagonal scatter; expands to 2 concentric blast rings when enraged.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarDiagonalBounceState.cs: Attack 2 - offset center landing with 4 diagonal rays executing 3 outward straight-line DOJump bounces; fires 3 rapid salvos when enraged; sub-coroutines tracked and stopped on state exit.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarRollingBoulderState.cs: Attack 3 - drop shell, directional aim telegraph, and linear rotating rolling boulder collision.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarCooldownState.cs: Inter-attack pause with enrage scaling and randomized duration rotating through attacks.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/StateMachine/States/MortarDefeatedState.cs: Halts active tweens, dismisses telegraphs, and cleans up entity.
- Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs: Implements IMortarTowerBoss, IDamageable, and IKnockable (knockback immune), manages health, MaterialPropertyBlock enrage emission, procedural recoil, grid footprint marking, auto-dismissing telegraph indicators with automatic inactive pruning, and unparented death VFX on defeat.

## Post-Review Fixes Applied

1. Encounter Completion Gate:
   - Added IsEncounterCompleted to IEncounterArenaController and EncounterArenaController.
   - Guarded Update() with if (_isEncounterCompleted) return;.
   - Prevents the encounter from re-triggering immediately when the boss is defeated and the player remains inside the 22m boundary.
2. Auto-Dismissing Telegraph Indicators:
   - Changed default autoContractOnFillComplete to true in ShowCircularTelegraph and ShowRectangularTelegraph.
   - Added PruneInactiveTelegraphs() to prevent accumulation of destroyed indicators in _activeTelegraphs.
3. Projectile Detonation Lifecycle & Audio/VFX Preservation:
   - Delayed ReturnToPool() in MortarShellProjectile.Detonate() using DOVirtual.DelayedCall for the duration of the impact particle effect.
   - Hid visual mesh and stopped trail emission on impact while keeping the GameObject active so particle systems and AudioSources play to completion.
4. Safe Pool Return Traversal:
   - Refactored ReturnAll() in MortarShellPool to pop from the tail via while (_activeShells.Count > 0), avoiding collection mutation during indexed traversal.
5. Coroutine Cleanup:
   - Tracked diagonal ray coroutines in MortarDiagonalBounceState and explicitly stopped them in Exit().
6. Death VFX Unparenting:
   - Unparented _deathVfxPlayer and configured with destroyOnEnd: true so the boss death explosion particle sequence plays completely when the boss GameObject is disabled.
7. Inspector Tooltips:
   - Added English tooltips across serialized fields in CinemachineCombatFollowOffsetController, ArenaLeashWarningPresenter, MortarBoulderHitbox, and EncounterArenaController.

## Verification Performed

### Automated Tests & Compilation
- Clean build verified on both assemblies:
```powershell
dotnet build Assembly-CSharp-firstpass.csproj
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 new warnings.

### Manual Verification Checklist (For Unity Editor)
1. In RuinedBloodCity.unity:
   - Place MortarTowerBoss prefab and EncounterArenaController within the scene.
   - Assign references in DefaultGameplaySceneInstaller (_cinemachineCombatFollowOffsetController, _arenaLeashWarningPresenter).
2. Enter the 22m arena radius with the player car:
   - Verify camera smoothly transitions to combat follow offset (0, 17, -13).
   - Verify perimeter circle mesh expands and pulses.
   - Verify Boss HUD appears and swarm spawning freezes.
3. Observe attack cycles:
   - Attack 1: Central impact followed by 6 hexagonal sub-shells; verify telegraphs cleanly contract and disappear on impact.
   - Attack 2: Offset center followed by 4 diagonal rays bouncing 3 times; verify sub-shells detonate with visible VFX and audible SFX.
   - Attack 3: Drop telegraph followed by linear rectangular indicator and rolling boulder; verify rectangular telegraph disappears as boulder rolls.
4. Test Leash Exit & Reset:
   - Exit perimeter during battle: verify warning banner appears with 2.5s countdown.
   - Re-enter within 2.5s: verify warning disappears and combat continues.
   - Stay outside: verify encounter resets, tower restores to full health, and camera/swarms normalize.
5. Test Enrage (< 40% HP):
   - Verify material shifts to emissive orange/red and attacks accelerate.
6. Test Defeat:
   - Deplete health to 0: verify exp particles drop, death explosion plays, Boss HUD hides, camera returns, swarms unfreeze, arena collapses, and the encounter does NOT re-trigger while standing in the arena.

## Follow-up / Unity Editor Steps

1. In the Unity Editor, configure the MortarTower prefab with MortarTowerConfigSO asset and assign references for muzzle, barrel, turret housing, and audio/VFX players.
2. In the scene hierarchy of RuinedBloodCity.unity, ensure the CinemachineCombatFollowOffsetController and ArenaLeashWarningPresenter are wired into the DefaultGameplaySceneInstaller inspector fields.
