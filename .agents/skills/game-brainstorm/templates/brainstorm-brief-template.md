# Brainstorm Brief: [Feature / Idea Name]

Date: YYYY-MM-DD

## 1. Context & Motivation
- Feature / Idea: [Comprehensive description of the feature, weapon, enemy, mechanic, or system exploration]
- Player-Facing Goal: [Direct gameplay feel, challenge, feedback, juice, or progression goal]
- Impacted Game Systems:
  - [System 1, e.g. Enemies and Boss logic]
  - [System 2, e.g. Navigation and Grid / FlowField System]
  - [System 3, e.g. Skills and Projectiles]
  - [System 4, e.g. UI / HUD Presenters]
  - [System 5, e.g. Audio, VFX, and Materials]

---

## 2. Explored Alternatives & Trade-Offs

### Option 1 (Selected / Recommended): [Approach Name]
- Pros:
  - [Architectural advantage, clean separation of concerns, scalability]
  - [Runtime performance, zero GC allocations, pooling safety]
  - [Designer iteration speed, ScriptableObject configuration]
- Cons / Risks:
  - [Potential complexity, edge case lifecycle management, migration overhead]

### Option 2: [Alternative Paradigm Name]
- Pros:
  - [Pros of this alternative approach]
- Cons / Risks:
  - [Why this was rejected or deprioritized, coupling risks, maintenance costs]

### Option 3 (Minimal / Build Nothing / Reuse Existing): [Baseline Option]
- Pros:
  - [Pros of minimal or reuse approach]
- Cons / Risks:
  - [Why it fails to meet gameplay feel or system extensibility goals]

---

## 3. Unity & Architecture Considerations

- Data Authoring:
  - [ScriptableObject configuration assets, fields, health curves, cooldowns, multipliers]
- Navigation, Physics & Spatial Checks:
  - [FlowField integration vs direct pursuit, obstacle sliding, raycast/sphere cast layers, physical colliders]
- Indicators & Telegraph Systems / UI Presenters:
  - [Telegraph visuals (circular, rectangular, line), DOTween scale/fade easing, grid passability snapping, UI HUD presenter bindings]
- Modular Entities & Lifecycle:
  - [Detachable parts, bone sockets, projectiles, unsubscription, return-to-dock transitions on death/interruption]
- Performance, Allocations & Invariants:
  - [Zero garbage collection in update loops, struct parameters, DOTween recycling, object pooling]
- Spawn, Wave & Defeat Flow:
  - [Trigger mechanisms, wave manager coordination, swarm suppression, defeat portal/rewards]

---

## 4. Key Decisions & Detailed Specifications

### A. Combat / Gameplay Patterns
1. **[Pattern / Ability 1 Name]**:
   - [Detailed execution loop, telegraph duration, action phase, recovery phase]
   - [Priority overrides or conditional triggers (e.g. anti-kiting distance trigger)]
2. **[Pattern / Ability 2 Name]**:
   - [Trigger conditions, collision checks, damage types, secondary effects]
3. **[Pattern / Ability 3 Name]**:
   - [Visual feedback, projectile velocities, trajectories, docking/return logic]

### B. Intensity & Phase Progression
- **Phase 1 ([e.g. 100% – 60% HP])**: [Base cooldowns, basic pattern cycles, default movement speed]
- **Phase 2 ([e.g. 60% – 30% HP])**: [Reduced cooldowns, aggressive multipliers, upgraded projectile count]
- **Phase 3 – Enrage ([e.g. < 30% HP])**: [Shortest cooldowns, multi-cycle recursion, visual state shifts (material tint / VFX emission)]

### C. Settled Design Parameters
- [Parameter 1, e.g. Base Movement Speed = X, Health = Y]
- [Parameter 2, e.g. Telegraph Warning Duration = Z seconds]

---

## 5. Next Step & Implementation Scope

- Recommended Next Skill: [e.g. gameplay-spec-writing, di-integration, unity-refactor-suggestions]
- Target Implementation Scope:
  - Assets/Scripts/[Domain]/[SubFolder]/
  - Assets/Scripts/UI/[PresenterName].cs
  - Assets/Scripts/ReflexDI/[InstallerName].cs
  - Assets/ScriptableObjects/[ConfigType].asset
  - Assets/Prefabs/[EntityPrefab].prefab
