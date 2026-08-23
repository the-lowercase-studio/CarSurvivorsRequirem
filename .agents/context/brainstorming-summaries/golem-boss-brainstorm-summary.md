# Brainstorm Brief: Golem Boss System

Date: 2026-08-23

## 1. Context & Motivation
- Feature / Idea: Introduce the first boss encounter into Car Survivors: a massive, multi-phase Golem Boss with 4 distinct attack patterns, direct pursuit navigation, detachable rocket fists, visual telegraphing, and dynamic enrage scaling.
- Player-Facing Goal: Provide a thrilling, high-stakes combat encounter testing the player car's mobility and positioning, complete with a dedicated screen-top health bar, telegraph indicators, and a portal spawning upon victory.
- Impacted Game Systems:
  - Enemies and Boss logic
  - Navigation and Grid System (passable cell snapping and obstacle avoidance)
  - UI and HUD (top-screen boss health bar presenter)
  - Waves and Swarm Spawning (regular waves continue, swarms suppressed)
  - VFX, Materials, and Audio

---

## 2. Explored Alternatives & Trade-Offs

### Option 1 (Selected): Modular State Machine + Detachable Arm Projectiles
- Pros:
  - Clean separation of concerns between Boss Core, Movement, Combat State Machine, Arm Management, and Phase Management.
  - Arms function as independent modular projectile entities during detached attacks while allowing the body to pursue and stomp.
  - Zero garbage collection overhead during combat cycles using DOTween and event subscriptions.
  - Highly tunable via a dedicated ScriptableObject (GolemBossConfigSO).
- Cons / Risks:
  - Requires careful lifecycle management for detached arms (returning to body sockets if the boss transitions states or dies).

### Option 2: Monolithic Enemy Subclass + Coroutines
- Pros:
  - Single script implementation, fewer files created initially.
- Cons / Risks:
  - High coupling, difficult to debug multi-cycle recursive attacks (e.g. Sky Arm Barrage), messy coroutine cancellation on boss death or state interruption.

### Option 3: Behavior Tree Engine Integration
- Pros:
  - Visual node editing for designer workflows.
- Cons / Risks:
  - Massive boilerplate and runtime overhead unnecessary for a survivor game with 4 deterministic attack patterns.

---

## 3. Unity & Architecture Considerations

- Data Authoring:
  - GolemBossConfigSO stores health thresholds, phase multipliers, attack cooldowns, telegraph warning durations, projectile speeds, damage values, and leap anti-kiting range.
- Navigation & Movement:
  - Direct pursuit vector towards player position, bypassing tile-by-tile FlowField stepping.
  - Physics sphere casting / multi-ray checks against TerrainLayers.Impassable with a surface sliding vector to prevent passing through obstacles.
  - Boss has a physical impassable collider so the player vehicle cannot drive through it.
- Telegraph Indicator System:
  - Circular Telegraph: Spawns at target position -> DOTween scales up from zero to full radius (Ease.OutQuad) -> Holds at max size during warning -> Boss/Arm impacts -> DOTween rapidly contracts to zero (Ease.InQuad) and recycles.
  - Grid Passability Check: Center position is converted to grid coordinates and verified via CellStatusDescriber.IsWalkable. If impassable, snaps to the nearest walkable neighboring cell center.
  - Rectangular Telegraph: Directional plane projecting forward from arm positions for linear rocket attacks.
- Detachable Arms:
  - Left and right arms mounted on bone sockets (LeftArmSocket, RightArmSocket).
  - Capable of detaching for linear thrusts or shooting into the sky, then flying back and docking smoothly.
- Boss HUD Presenter:
  - Dedicated screen-top canvas group with animated health slider and boss title.
  - Subscribes to IHealth events on spawn, smoothly fades in on appearance and fades out on boss defeat.
- Spawn & Defeat Flow:
  - Debug key P triggers boss spawning for testing.
  - Wave Manager continues normal enemy waves, but swarm events are suppressed during boss fight.
  - Defeat spawns a visual NextStagePortal GameObject at the death location.

---

## 4. Key Decisions & Specifications

### A. Combat Patterns
1. **Attack 1: Leap Slam (AOE)**:
   - Boss leaps high out of camera view.
   - Circular telegraph appears on player location (passable cell snapped).
   - Slams down dealing heavy AOE damage. Rapid contract scale down. No camera shake.
   - **Anti-Kiting Priority Trigger**: If the player distance exceeds LeapTriggerMaxDistance, this attack triggers immediately outside the normal rotation queue.
2. **Attack 2: Melee Foot Stomp**:
   - Triggered when player is in close proximity to the boss's feet/chassis.
   - Deals contact damage; can execute even when arms are detached during Attack 4.
3. **Attack 3: Linear Rocket Fists**:
   - Rectangular telegraph projected forward -> brief charge pause -> arms detach and fire forward as triggers (damaging player only) -> arms retract and redock.
4. **Attack 4: Sky Arm Barrage**:
   - Arms launch upward into the sky.
   - Arms drop down with randomized staggered delays within a jitter interval (e.g. Random.Range(0.15f, 0.40f) between arms).
   - Multi-cycle recursion: arms launch back up and slam again for N cycles before returning to the body.
   - Body continues moving and can execute Attack 2 (Stomp) while arms are in flight.

### B. Boss Intensity Phases
- **Phase 1 (100% – 60% HP)**: Base cooldowns (~5.0s), 1–2 arm barrage cycles, standard movement.
- **Phase 2 (60% – 30% HP)**: Reduced cooldowns (~3.5s), 2–3 arm barrage cycles, increased arm launch speed.
- **Phase 3 – ENRAGE (< 30% HP)**: Shortest cooldowns (~2.0s), 4–5 arm barrage cycles, visual Enrage state (material tint/color shift + VFXPlayer emission).

---

## 5. Next Step
- Recommended Skill: gameplay-spec-writing
- Target Implementation Scope:
  - Assets/Scripts/Enemies/Bosses/Golem/
  - Assets/Scripts/Indicators/
  - Assets/Scripts/UI/HUD/BossHUDPresenter.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - ScriptableObject config and prefabs for Golem Boss, Arm Projectiles, and Telegraph Indicators.
