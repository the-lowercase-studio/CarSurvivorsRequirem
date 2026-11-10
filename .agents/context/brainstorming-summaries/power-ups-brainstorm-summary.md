# Brainstorm Brief: Power-Ups System & Fire Tails Exploration

Date: 2026-11-02

Label choices and values as confirmed, recommended, tentative, or unresolved. Existing behavior requires source evidence. Omit irrelevant sections and leave unknown parameters unresolved; template examples are not approved design values.

---

## 1. Context & Motivation

- **Feature / Idea**: Comprehensive audit and expansion of the Power-Ups drop roster in *Car Survivors*, cataloging all current in-game power-ups, evaluating proposed concepts, and advancing **Fire Tails** (formerly explored as *Napalm Trail*) into active design exploration while recording rejected alternatives.
- **Player-Facing Goal**: Reinforce the core fantasy of being a combat vehicle by rewarding offensive driving, drifting, and swarm kiting with high-impact, temporary tactical power-ups dropped by enemies.
- **Impacted Game Systems**:
  - Assets/Scripts/PowerUps/: Extension of `PowerUpType` enum (`FireTails`), new `FireTailsService`, config ScriptableObject, and pickup collectible prefab.
  - Assets/Scripts/Player/Car/: Interfacing with `ICarController` and `CarVfxEffectsController` (`ICarVfxEffectsController`) for rear-wheel flame particles, dual fire `TrailRenderer` segment pools, and ground contact positions.
  - Assets/Scripts/Enemies/: Interaction with `IDamageable` for periodic burning damage and `EnemyMovementController` (`IMovementController`) for movement speed debuff (slow).
  - Assets/Scripts/ReflexDI/: Binding `IFireTailsService` in `DefaultGameplaySceneInstaller`.
  - Assets/Scripts/VFX/ & Assets/Scripts/Audio/: Fire `TrailRenderer` ribbons, World-Space drift fire `ParticleSystem`s, and ignition/burning audio feedback.

---

## 2. Power-Ups Status & Evaluation Matrix

The following table catalogs all currently implemented power-ups alongside newly brainstormed candidates. Per design direction, **Fire Tails** is selected for detailed exploration, while the remaining four brainstormed candidates are marked as rejected.

| Power-Up Name | Status | Mechanical Archetype | Core Gameplay Effect | Vehicle / Driving Synergy |
| :--- | :--- | :--- | :--- | :--- |
| **Nuke** | **Implemented** | Instant Screen Clear | Kills all camera-visible regular enemies instantly (`TakeFullHpDamage`) and deals 15% max HP damage to active bosses. Suppresses collectible drops while preserving EXP. | Emergency escape button when boxed in or blinded by excessive mob density. |
| **Freeze Timer** | **Implemented** | Global Crowd Control | Scaled-time freeze (5s) halting enemy locomotion, attack controllers, animators, swarm/wave spawners, boss state machines, and freezable DOTween sequences. | Grants breathing room to reposition the car, take tight turns, or clear congested bottlenecks. |
| **Bubble Shield** | **Implemented** | Defense / Immunity | Grants 8s of absolute combat damage immunity. Visualized by a 3D Fresnel bubble on the car with a 2s synchronized warning blink before expiry. | Encourages aggressive driving and ramming through enemy formations without taking hull damage. |
| **Armor** | **Implemented** | Defense / Absorption | Adds up to 3 stackable armor plates (+20 / +40 / +60 HP, up to 120 Armor HP total) that absorb incoming combat damage before the player car health bar is affected. | Forgives minor driving mistakes, small obstacle clips, or stray projectile hits during high-speed runs. |
| **Fire Tails** | **Selected for Exploration** | Area Denial / DoT & Slow | For 8s, rear tires burn with flames. While drifting, normal black tire skid marks are replaced by 2 burning fire tracks that deal non-stacking fire DoT (25 HP / 0.5s) and slow regular enemies by 40%. | **Deepest vehicle synergy:** rewards skillful handbrake drifting and donut maneuvers to trap pursuit swarms in rings of fire. |
| **Nitro Ram** | **Rejected** | Kinetic / Battering Ram | Temporary speed boost (+70%) turning the car into an invulnerable battering ram that instakills and flings regular enemies upon contact. | High vehicle fantasy, but overlaps heavily with Bubble Shield immunity and requires complex physics changes to enemy collision logic. |
| **Magnetic Vortex** | **Rejected** | Utility / Resource Vacuum | Escalating electromagnetic field pulling all scattered EXP particles and Skill Crates across the entire arena directly to the car. | Useful for large-arena car navigation, but overlaps with potential passive player stats (pickup radius) and lacks combat punch. |
| **EMP Overload** | **Rejected** | Anti-Projectile / Stun & Shock | Instant shockwave destroying active enemy projectiles in flight, followed by 6s of automatic Tesla chain lightning striking nearest targets. | Strong bullet-hell counter, but dilutes vehicle-specific gameplay by functioning essentially as an automated turret. |
| **Airstrike Flare** | **Rejected** | Automated Bombardment | Deploys a roof beacon calling down periodic artillery strikes with telegraph markers on nearby high-density enemy clusters. | Highly cinematic, but automated artillery duplicates existing weapon skills (like Mortar/Rocket skills) rather than feeling like a pickup boost. |

---

## 3. Explored Alternatives & Trade-Offs for Fire Tails

### Option 1 (Selected — High-Performance Hybrid): `FireHazardNode` Struct Buffer + Dual Fire `TrailRenderer` Pools & World-Space `ParticleSystem`s
- **Architecture**:
  - **Visuals**: Reuses `CarDriftTrailSegmentPool` on `CarVfxEffectsController` with 2 fire `TrailRenderer` emitters (`_rearFireDriftTrailRenderers`) and 2 World-Space `ParticleSystem` emitters (`_rearDriftFireTrailParticles`), plus 2 local rear-wheel flame `ParticleSystem`s (`_rearWheelConstantFireParticles`).
  - **Combat**: `FireTailsService` records lightweight `FireHazardNode` structs (`Position`, `ExpireCombatTime`) in a preallocated `FireHazardNode[256]` array at `1.2m` intervals behind the two drifting rear wheels and evaluates `Physics.OverlapSphereNonAlloc` at a throttled `0.1s` (`10 Hz`) interval.
- **Pros**:
  - Zero spawned `GameObject`s at runtime (eliminates ~140 active puddle `GameObject`s, `Transform` updates, and `MaterialPropertyBlock` SRP Batcher breaks).
  - Only 2 `TrailRenderer` draw calls and 2 World-Space `ParticleSystem`s for the entire ground fire trail.
  - ~85–90% lower physics CPU cost via `10 Hz` overlap throttling.

### Option 2: Dedicated Pooled Hazard Puddle GameObjects (`FireTailPuddle`)
- **Architecture**: Spawning pooled `MonoBehaviour` `GameObject`s with individual quad meshes and particle systems every `1.2m`.
- **Cons / Risks**:
  - At max drift speed (`24 m/s`) over `3.5s` across 2 wheels, up to `140` active `GameObject`s and `140` `ParticleSystem`s coexist simultaneously, causing high transparency overdraw and SRP Batcher overhead.

### Option 3: Baseline / Minimal: Reusing Existing Skills (Landmine Trap)
- **Cons / Risks**:
  - Landmines detonate once and disappear; they do not provide continuous area denial, fire DoT, slow, or drift synergy.

---

## 4. Confirmed Decisions & Target Scope

| Parameter / Decision | State | Value / Rationale |
| :--- | :--- | :--- |
| Power-Up Name & Enum | **Confirmed** | Renamed from *Napalm Trail* to **Fire Tails**; add `FireTails = 4` to Assets/Scripts/PowerUps/PowerUpType.cs. |
| Power-Up Active Duration | **Confirmed** | 8.0 seconds (scaled time via `PowerUpTimer`, refreshed on re-pickup). |
| Emission Mode | **Confirmed** | Constant flames on rear wheels for 8.0s; ground fire tracks spawn **exclusively while drifting**, replacing normal black tire skid marks with 2 fire tracks. |
| Hazard Lifetime & Fade | **Confirmed** | 3.5 seconds per segment with a 0.5-second fade-out. |
| Non-Stacking DoT & Slow | **Confirmed** | 25 HP every 0.5s (`50 DPS`) and 40% movement speed reduction (`SlowFactor = 0.6`), non-stacking across overlapping nodes. |
| Boss Interaction | **Confirmed** | Bosses take full non-stacking fire DoT (25 HP / 0.5s) and are immune to movement slow. |
| Visual & Combat Architecture | **Confirmed** | 2 fire `TrailRenderer` pools + 2 World-Space `ParticleSystem`s + `FireHazardNode[256]` struct buffer scanned every `0.1s`. |

- **Authoritative Technical Specification**:
  - .agents/context/implementations/plans/fire-tails-power-up-spec.md
