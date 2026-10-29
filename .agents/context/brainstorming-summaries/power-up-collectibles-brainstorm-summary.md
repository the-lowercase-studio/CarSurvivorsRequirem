# Brainstorm Brief: Power-Up Collectibles System (Nuke, Freeze Timer, Bubble Shield)

Date: 2026-10-29

## 1. Context & Motivation
- Feature / Idea: Introduce three rare combat power-up collectibles dropped by defeated enemies:
  1. **Nuke (Atomic Bomb)** (Confirmed): Instantly kills all regular enemies currently visible within the player's camera view, deals percentage-based maximum health damage to visible bosses, suppresses collectible drops from wiped regular enemies (yielding EXP only), and triggers an explosion VFX, post-processing blast sequence, and screen dirt/damage overlay.
  2. **Freeze Timer (Stopwatch)** (Confirmed): Freezes all active regular enemies, bosses, in-flight boss projectiles/detached arms, and active ground telegraphs in place for a timed window, accompanied by dedicated VFX, icy camera post-processing, and an optional UI frost screen overlay hook without mutating per-enemy materials.
  3. **Bubble Shield** (Confirmed): Surrounds the player vehicle with a car-sized translucent 3D protective sphere shader that grants combat damage immunity for a short window, locks the player health bar fill to a matching dark blue color, and synchronizes transparency/color blinking on both the 3D bubble and the health bar when about to expire.
- Player-Facing Goal: Create high-excitement tactical relief moments during dense swarms and boss encounters with strong visual/audio juice while preserving frame rate stability and inspector-driven balance.
- Impacted Game Systems:
  - Collectibles and Enemy Drops (Assets/Scripts/Enemies/EnemyDropHandler.cs, Assets/Scripts/Enemies/CollectibleDropNotifier.cs, Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs)
  - Regular Enemies and Death/Drop Lifecycle (Assets/Scripts/Enemies/Base/, Assets/Scripts/Enemies/Barrel/, Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs)
  - Boss Encounters, Projectiles, and Telegraphs (Assets/Scripts/Enemies/Bosses/, Assets/Scripts/Indicators/)
  - Wave and Swarm Spawning Pause Coordination (Assets/Scripts/Waves/WaveManager.cs, Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs)
  - Player Damage Reception and Health Bar Presentation (Assets/Scripts/Player/PlayerDamagedHandler.cs, Assets/Scripts/HealthSystem/HealthBar.cs)
  - Camera Post-Processing, Screen Overlays, Shaders, VFX, and Audio (Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs, Assets/Shaders/, Assets/Scripts/UI/HUD/)

---

## 2. Explored Alternatives & Trade-Offs

### A. Freeze Visual Presentation
#### Option 1 (Confirmed): URP Post-Processing Volume Blending + UI Frost Overlay Hook
- Pros:
  - Constant O(1) rendering cost regardless of whether 10 or 300 enemies are active on screen.
  - Zero runtime material cloning, zero GC allocations, and full preservation of URP SRP Batcher efficiency.
  - Supports immediate gameplay integration via URP Volume color adjustments/vignette while leaving a clean optional UI overlay slot ready for artist-authored frost sprites.
- Cons / Risks:
  - Tints the entire screen cold rather than isolating enemy meshes alone (accepted by user).

#### Option 2 (Rejected): Per-Enemy Material Replacement
- Pros:
  - Isolates frozen surface look strictly to enemy renderers.
- Cons / Risks:
  - Accessing renderer material instances across hundreds of pooled enemies clones materials, breaks SRP batching, spikes draw calls and GC allocations, and requires complex material restoration on pool release or death.

#### Option 3 (Not Selected): Global Shader Property or Custom URP Renderer Feature Pass
- Pros:
  - Cheap GPU-level tinting across enemy shaders or enemy layer without cloning materials.
- Cons / Risks:
  - Unnecessary shader/renderer maintenance overhead since camera post-processing plus a UI frost overlay already meets the visual goal.

### B. Collectible Drop Authoring
#### Option 1 (Confirmed): Per-Prefab Configuration in Existing EnemyDropHandler
- Pros:
  - Reuses the existing serialized CollectibleDropEntry workflow on enemy prefabs in Assets/Scripts/Enemies/EnemyDropHandler.cs (already used for SkillCrate across zombie tiers and barrel enemies).
  - Gives designers direct per-enemy and per-tier drop probability control in the Inspector without introducing a parallel drop table architecture.
- Cons / Risks:
  - Requires assigning the three new collectible prefabs across enemy base/variant prefabs in Assets/Prefabs/Enemies/.

#### Option 2 (Rejected): Centralized Global Loot Table ScriptableObject
- Pros:
  - Single-asset balancing across all enemies.
- Cons / Risks:
  - Splits enemy drop authoring across two competing places (SkillCrate per-prefab vs power-ups global); rejected in favor of consistent per-prefab authoring.

### C. Runtime Power-Up Effect Orchestration
#### Option 1 (Recommended): Scene-Scoped Power-Up Effect Services Bound via Reflex DI
- Pros:
  - Keeps collectible prefabs lightweight pooled triggers implementing ICollectible and IPoolable, matching Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs.
  - Centralizes active timers (FreezeTimer, BubbleShield, Nuke screen effect), timer refresh on re-pickup, URP Volume weight tweening, and clean teardown on player death or scene unload.
  - Avoids FindAnyObjectByType lookups or static singletons in accordance with ADR-001.
- Cons / Risks:
  - Requires narrow interfaces in Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs for active enemy iteration and active boss lookup.

#### Option 2 (Baseline / Rejected): Self-Contained Logic Inside Collectible Prefabs
- Pros:
  - Fewer scene service classes initially.
- Cons / Risks:
  - Pooled pickup GameObjects return to their pool immediately upon trigger contact, making them unsafe owners for multi-second freeze/shield/post-process lifecycles and timer refreshes.

---

## 3. Unity & Architecture Considerations

- Existing Source Baseline (Verified):
  - Collectible pooling and scatter animations already exist in Assets/Scripts/Enemies/CollectibleDropNotifier.cs and Assets/Scripts/Enemies/DropAnimationConfiguration.cs.
  - Imported 3D models are already present at Assets/Imported/Models/Collectibles/Nuke.fbx and Assets/Imported/Models/Collectibles/FreezeTimer.fbx. BubbleShield has no imported 3D mesh yet and will use Unity 3D Sphere primitives with custom materials/shaders (Confirmed).
  - Assets/Prefabs/Collectibles/SkilCrate.prefab establishes the canonical collectible prefab hierarchy on Layer 12 (Collectibles): a root GameObject holding the collectible MonoBehaviour, Assets/Scripts/Effects/XYZRotationLoop.cs, Assets/Scripts/Effects/ScaleToAnimationOnEnable.cs, and a trigger Collider, paired with a dedicated *_Visual child GameObject (nesting the imported FBX or primitive mesh at local origin) and a child Point Light (URP Light with shadows disabled).
  - Assets/Scripts/Enemies/Base/Enemy.cs exposes TakeFullHpDamage() and TakeDamage(float), while Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs spawns EXP on Health.OnNoHealth and Assets/Scripts/Enemies/EnemyDropHandler.cs independently listens to Health.OnNoHealth to roll CollectibleDropEntry items.
  - Assets/Scripts/Volumes/DeathVolume.cs calls IDamageable.TakeFullHpDamage() for lethal out-of-bounds falls, whereas enemy/boss combat attacks call IDamageable.TakeDamage(float).
  - Assets/Scripts/HealthSystem/HealthBar.cs updates its fill color from a serialized Gradient whenever Health.OnHealthChanged fires (including ticks from Assets/Scripts/HealthSystem/RegenativeHealth.cs).
- Editor Prefab Structure & Authoring Conventions (Confirmed):
  - All newly created prefabs and scene hierarchies in Unity Editor must strictly follow established project prefab architecture rather than ad-hoc single-node setups or raw YAML hand-crafting (ADR-004, ADR-005).
  - Collectible pickup prefabs under Assets/Prefabs/Collectibles/ (Nuke, FreezeTimer, BubbleShield) must mirror Assets/Prefabs/Collectibles/SkilCrate.prefab: Layer 12 (Collectibles), root logic + trigger Collider + XYZRotationLoop + ScaleToAnimationOnEnable, a dedicated *_Visual child holding the FBX/Sphere mesh, and a subtle color-matched Point Light child with shadows disabled.
  - Active player-mounted BubbleShield, VFX, audio, and UI overlay hierarchies must follow existing Player/VFX/HUD conventions: proper project layer assignment from ProjectSettings/TagManager.asset (Collectibles, Skill, VFX, Audio, UI), root controller separated from child Visual, VFXPlayer, and AudioClipPlayer nodes, and explicit serialized Inspector references without Awake fallback searches.
- Active Enemy & Boss Resolution (Recommended):
  - Expose a zero-allocation read-only active enemy collection interface on Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs instead of scanning scene hierarchies.
  - Expose active living boss references (GolemBoss and engaged/registered MortarTowerBoss instances) via Assets/Scripts/Enemies/Bosses/BossEncounterService.cs so both Nuke frustum checks and FreezeTimer pauses operate strictly through Reflex DI.
- Wave & Swarm Freeze Arbitration (Recommended Guardrail):
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs already sets ISwarmFreezer.IsSuppressed = true while a boss encounter is active. FreezeTimer must coordinate pause state (for example via composite suppression flags or reference counting on WaveManager and SwarmSpawner) so that unfreezing time mid-boss-fight never accidentally re-enables swarm spawning while a boss remains active.
- Full Freeze-Frame Safety for Enemies, Watchdogs, and Boss Projectiles (Confirmed Goal / Recommended Mechanism):
  - Regular Enemies: Pause locomotion in EnemyMovementController, set Animator.speed = 0f, pause attack cooldowns, and freeze watchdog elapsed timers in Assets/Scripts/Enemies/Base/EnemyAnimator.cs (MAX_ATTACK_ANIMATION_DURATION = 3.5s) and Assets/Scripts/Enemies/Barrel/BarrelEnemyExplosionController.cs (_primingStartedAt) as well as tween sequences in Assets/Scripts/Enemies/Barrel/BarrelEnemyAttackFeedback.cs so primed enemies do not erroneously cancel or detonate while frozen.
  - Bosses & Mid-Air Attacks: Pause GolemStateMachine and MortarTowerStateMachine ticks, pause boss Animator playback and turret rotation, and pause active DOTween sequences/timers on GolemArmProjectile, GolemLinearAttackHitbox, GolemLeapSlamState, MortarShellProjectile, MortarBoulderHitbox, CircularTelegraphIndicator, and RectangularTelegraphIndicator via DOPause() / DOPlay() and frozen delta-time gating.
  - Newly spawned or dying entities: Enemies already in their death presentation sequence when FreezeTimer or Nuke triggers should still complete pool recycling cleanly without orphaning pooled instances.
- Post-Processing & Optional UI Screen Overlays (Confirmed Goal / Recommended Mechanism):
  - Use dedicated child/secondary URP Volume components (with higher priority and animated weight from 0 to 1) for Nuke blast post-processing and FreezeTimer cold post-processing so both effects can overlap cleanly without mutating the shared DefaultVolumeProfile asset or clashing with each other.
  - Provide optional serialized UI Image / CanvasGroup overlay references for both the FreezeTimer frost border and the Nuke dirty/damaged lens overlay (following ADR-005 cosmetic null-check allowance until final 2D sprites are assigned by the artist).
- Performance, Allocations & Lifecycle Invariants:
  - Pre-allocate camera frustum Plane[6] and temporary enemy snapshot buffers so Nuke execution causes zero GC allocations even when killing hundreds of enemies in one frame (iterating over a snapshot buffer because TakeFullHpDamage can trigger synchronous death handlers).
  - Kill all active DOTween sequences on OnDisable / OnDestroy across collectibles, shield visuals, health bar blinking, and post-processing controllers.

---

## 4. Key Decisions & Detailed Specifications

### A. Collectible Mechanics

1. **Nuke (Atomic Bomb)**:
   - **Pickup Model**: Uses Assets/Imported/Models/Collectibles/Nuke.fbx (Confirmed).
   - **Target Selection**: Checks camera visibility at collection time against the injected main Camera frustum (Confirmed).
   - **Regular Enemies**: Instantly kills all visible living regular Enemy instances (Confirmed).
   - **Drop Suppression on Nuke Kills**: Wiped regular enemies spawn EXP particles as normal, but must NOT roll or drop any collectibles from EnemyDropHandler (zero SkillCrates and zero rare power-ups from Nuke kills) (Confirmed).
   - **Boss Interaction**: Deals 15% of MaxHealth damage to any active boss (GolemBoss, MortarTowerBoss) currently within the camera view (Confirmed).
   - **Sensory Feedback**: Plays nuclear explosion VFX and audio, triggers timed post-processing representing the blast flash and aftermath, and supports a dirty/damaged screen overlay over a configured duration (Confirmed).

2. **Freeze Timer (Stopwatch)**:
   - **Pickup Model**: Uses Assets/Imported/Models/Collectibles/FreezeTimer.fbx (Confirmed).
   - **Scope of Freeze**: Full freeze-frame across all active regular enemies AND bosses, explicitly including already airborne/moving boss attacks (MortarShellProjectile flights, MortarBoulderHitbox sweeps, GolemArmProjectile flights/drops, GolemLinearAttackHitbox sweeps, Golem leap arcs) and their ground telegraph indicators, plus pausing wave/swarm spawners for the freeze duration (Confirmed).
   - **Re-Collection Behavior**: Collecting another FreezeTimer while already frozen resets the remaining freeze timer back to full maximum duration (no additive stacking) (Confirmed).
   - **HUD & Visual Feedback**: No dedicated HUD buff icon; visual feedback relies on URP post-processing cold color grading, active freeze VFX, and an optional UI frost overlay hook to be populated once artwork arrives (Confirmed).

3. **Bubble Shield**:
   - **Pickup & Active Shield Models**: Uses temporary 3D Sphere primitives for both the collectible drop and the active car-sized shield bubble until custom 3D assets are imported (Confirmed).
   - **Defensive Semantics**: Purely defensive protection—prevents all combat health loss in PlayerDamagedHandler.TakeDamage while active, with zero offensive contact damage or knockback (Confirmed). Lethal environmental out-of-bounds triggers calling TakeFullHpDamage (DeathVolume) bypass combat shield protection to prevent out-of-world soft-locks (Recommended).
   - **3D Bubble Visual**: Translucent sphere scaled to enclose the player car with a bubble/shield shader that keeps the car body and mounted weapons/skills clearly visible inside (Confirmed).
   - **Synchronized HealthBar Visual**: While the shield is steady, the player car HealthBar fill locks to a dark blue shield color matching the bubble (ignoring passive health-change gradient overrides). When entering the final expiration warning window, both the 3D bubble transparency and the HealthBar fill blink synchronously—with the HealthBar alternating/interpolating between dark shield blue and the normal health gradient color (Confirmed).
   - **Re-Collection Behavior**: Collecting another BubbleShield while active (including during the expiration blinking phase) resets the shield duration back to full maximum duration and immediately restores solid steady bubble visuals and solid dark blue HealthBar color (Confirmed). No separate HUD buff icon is added (Confirmed).

### B. Rarity & Drop Distribution
- **Authoring Location**: Configured per enemy prefab inside EnemyDropHandler._dropEntries alongside SkillCrate (Confirmed).
- **Relative Rarity Tiering**:
  - Nuke: Very rare drop chance (Confirmed).
  - Freeze Timer: Very rare drop chance (Confirmed).
  - Bubble Shield: Rare drop chance, strictly higher than Nuke and Freeze Timer (Confirmed).

### C. Design Parameters and Decision State
- Boss Nuke Damage Ratio: 15% of boss MaxHealth when inside camera view (Confirmed).
- Exact Freeze Timer Duration (seconds): Unresolved (Tentative starting value for spec: 5.0s – 6.0s, designer-configurable via ScriptableObject).
- Exact Bubble Shield Duration & Warning Blink Threshold (seconds): Unresolved (Tentative starting values for spec: 6.0s – 8.0s total duration with 1.5s – 2.0s warning blink window, designer-configurable via ScriptableObject).
- Exact Nuke Post-Processing / Dirty Screen Duration (seconds): Unresolved (Tentative starting value for spec: 2.5s – 3.5s total fade-out, designer-configurable via ScriptableObject).
- Exact Per-Prefab Drop Percentages across Zombie Levels 1–4 and BarrelEnemy: Unresolved (To be proposed relative to existing SkillCrate 0.3% – 10.0% baseline during gameplay-spec-writing).
- Dirty Screen Overlay Sprite (Nuke) & Frost Screen Overlay Sprite (Freeze Timer): Unresolved external art dependency (Optional UI presenter image slots to be wired now so sprites can be dropped in later without code edits).

---

## 5. Next Step & Implementation Scope

- Recommended Next Skill: gameplay-spec-writing (to draft the canonical technical specification under .agents/context/implementations/plans/power-up-collectibles-spec.md, lock initial ScriptableObject defaults and prefab drop table numbers, and break implementation into atomic compilable phases).
- Target Implementation Scope:
  - Assets/Scripts/Collectibles/ (or Assets/Scripts/Skills/ObjectsImpactingSkills/ expansion for power-up collectibles, effect controllers, and Constants)
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/EnemyMovementController.cs
  - Assets/Scripts/Enemies/Base/EnemyAnimator.cs
  - Assets/Scripts/Enemies/Base/EnemyAttackController.cs
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyExplosionController.cs
  - Assets/Scripts/Enemies/Barrel/BarrelEnemyAttackFeedback.cs
  - Assets/Scripts/Spawners/Enemies/EnemiesSpawner.cs
  - Assets/Scripts/Waves/WaveManager.cs
  - Assets/Scripts/Spawners/Swarm/SwarmSpawner.cs
  - Assets/Scripts/Enemies/Bosses/BossEncounterService.cs
  - Assets/Scripts/Enemies/Bosses/Golem/
  - Assets/Scripts/Enemies/Bosses/Towers/MortarTower/
  - Assets/Scripts/Indicators/CircularTelegraphIndicator.cs
  - Assets/Scripts/Indicators/RectangularTelegraphIndicator.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/HealthSystem/HealthBar.cs
  - Assets/Scripts/UI/HUD/ (optional screen overlay presenter hooks for frost and blast dirt)
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Shaders/ (translucent Bubble Shield shader)
  - Assets/ScriptableObjects/Collectibles/
  - Assets/Prefabs/Collectibles/
  - Assets/Prefabs/Enemies/
