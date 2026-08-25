# Implementation Summary - Golem Boss Linear Attack Hitbox System & Arm Collider Decoupling

Date: 2026-08-25

## Overview

Implemented an indicator-aligned attack hitbox system for the Ancient Golem boss and decoupled physical collisions from detachable arm projectile meshes. The linear punch attack ("Linear Rocket Fists") is now driven by a dedicated wavefront trigger collider (GolemLinearAttackHitbox) matching the full rectangular telegraph lane (LinearFistWidth), while arm projectiles act as pure visual representations. Single-hit deduplication per attack pass and zero per-frame heap allocations guarantee 100% fair and consistent combat behavior.

## Key Changes

### Hitbox & Combat Subsystems
- Assets/Scripts/Enemies/Bosses/Golem/Combat/GolemLinearAttackHitbox.cs: Created IGolemLinearAttackHitbox interface and GolemLinearAttackHitbox component. Implements dynamic BoxCollider sizing to match width, height, and depth, DOTween forward movement synchronized with flying fists, zero-allocation OverlapBoxNonAlloc check on activation, and HashSet collider hit deduplication per thrust pass.
- Assets/Scripts/Enemies/Bosses/Golem/Arms/GolemArmProjectile.cs: Removed RequireComponent(typeof(Collider)), _damageCollider, and OnTriggerEnter. Streamlined FireLinear to act as a visual motion controller (animations, trails, socket docking). Retained OverlapSphere damage in DropFromSky for circular landing impacts.
- Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs: Exposed IGolemLinearAttackHitbox LinearAttackHitbox { get; } on the boss contract.
- Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs: Serialized _linearAttackHitbox reference with auto-discovery in Awake() and automatic deactivation in OnDisable() and OnDestroy().

### State Machine & Configuration
- Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs: Added _linearFistHitboxHeight (2.5f), _linearFistHitboxDepth (1.5f), and _linearFistHitboxVerticalOffset (1.0f) fields and public property getters.
- Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemLinearFistState.cs: Updated FireFists to activate LinearAttackHitbox with configured dimensions and speed multipliers synchronously with arm launching, and updated Exit to deactivate the hitbox.

### Project & System Documentation
- Assembly-CSharp.csproj: Registered GolemLinearAttackHitbox.cs for msbuild compilation.
- .agents/context/game-systems/golem-boss-system.md: Updated architecture documentation to reflect GolemLinearAttackHitbox, decoupled arm projectiles, and indicator-aligned attack flows.

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/golem-boss-linear-attack-hitbox-spec.md
- Coding Standards: Verified compliance with .agents/context/project-coding-standards.md (naming conventions, explicit field ordering, no LINQ, standard method body syntax).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 new warnings.

### Manual Verification Instructions
1. Run RuinedBloodCity scene or spawn GolemBoss via BossManager debug spawn key (`P`).
2. When Golem charges Linear Rocket Fists, observe the rectangular red telegraph on the ground.
3. Position the vehicle anywhere inside the rectangular telegraph boundary (including the center lane between fists or near the outer edges).
4. Verify damage is applied strictly when the flying arms reach the vehicle position.
5. Verify vehicle takes damage exactly once during the forward punch pass and takes no damage when fists return.
6. Verify sky barrage and stomp attacks continue functioning smoothly with indicator-aligned damage.

## Follow-up / Unity Editor Steps

1. In Assets/Prefabs/Enemies/Bosses/Golem/GolemBoss.prefab:
   - Add a child GameObject named `LinearAttackHitbox` with `GolemLinearAttackHitbox`, `BoxCollider` (isTrigger = true), and `Rigidbody` (isKinematic = true).
   - Drag this child into the `_linearAttackHitbox` field on the `GolemBoss` component (or let `Awake()` auto-discover via `GetComponentInChildren`).
2. In Assets/Prefabs/Enemies/Bosses/Golem/Golem_L_Arm_Projectile.prefab and Golem_R_Arm_Projectile.prefab:
   - Optionally remove the unused `Collider` component from the arm projectile prefabs.
