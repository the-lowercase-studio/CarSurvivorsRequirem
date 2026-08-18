# Specification: [Feature Name]

**Date:** [YYYY-MM-DD]  
**Author:** [Agent / Author]  
**Target Systems:** [e.g. Assets/Scripts/Skills/, Assets/Scripts/ReflexDI/]  

---

## 1. Overview & Player Experience
- **Summary:** Concise description of the gameplay feature.
- **Player-Facing Goals:** How does this feel to the player? What feedback (visual, audio, haptic) is expected?
- **In-Scope vs. Out-of-Scope:** Explicit scope boundaries.

---

## 2. Open Questions & Resolved Decisions
### Resolved Decisions
- [x] Decision 1: ...
- [x] Decision 2: ...

### Open Questions (Hard Gate - Must be answered before full implementation)
- [ ] **Q1:** ...
- [ ] **Q2:** ...

---

## 3. Data Model & Serialization
- **ScriptableObjects:** (Config assets, upgrade definitions, stats)
  - `Assets/ScriptableObjects/...`
- **Serialized Fields & Inspector Setup:**
  - Naming convention: `_camelCase` with `[SerializeField] private`
  - Prefab link requirements:

---

## 4. Architecture & Reflex DI Contracts
- **Interfaces & Abstractions:**
  - `I...` (Colocated above implementation or in shared contracts)
- **Service Implementation:**
  - Owning domain:
  - Lifetime: (Singleton in Scene container, Transient, etc.)
- **Installer Registration:**
  - Target Installer: `Assets/Scripts/ReflexDI/...`
- **Consumers:**
  - Field order: `[Inject] private`, then `[SerializeField] private`, then private fields.

---

## 5. Visual, Audio & Tweening Integration
- **VFX / Shaders:** Visual Effect Graph or particle prefabs.
- **Audio:** SFX clips, audio source channels, mixer routing.
- **Animations / Tweens:** DOTween animations using `TransformTweenExtensions` where appropriate, ensuring sequence killing on object disable/destroy.

---

## 6. Edge Cases, Performance & Lifecycle Invariants
- **Pause & State Changes:** Behavior on game pause, death sequence, scene unload.
- **Object Pooling:** Pooling strategy for spawned projectiles/VFX/damage numbers.
- **Performance Invariants:** Zero GC allocations in `Update`/`FixedUpdate`, no `FindAnyObjectByType`.
- **Event Unsubscription:** Explicit unsubscription in `OnDisable`/`OnDestroy`.

---

## 7. Implementation Plan (Phases & Steps)

### Phase 1: Foundation & Data Contracts
- [ ] **Step 1.1:** Create ScriptableObject configurations and interfaces.
  - Files: `Assets/Scripts/...`
  - Verification: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`
- [ ] **Step 1.2:** Implement core logic / service and bind in Reflex installer.
  - Files: `Assets/Scripts/...`, `Assets/Scripts/ReflexDI/...`
  - Verification: Compile & DI check.

### Phase 2: Gameplay Integration & Feedback
- [ ] **Step 2.1:** Hook into consumer components (Player Car, Enemy, Skill runner).
  - Files: `Assets/Scripts/...`
  - Verification: Event ordering and mechanics test.
- [ ] **Step 2.2:** Add visual feedback, DOTween animations, and audio events.
  - Files: `Assets/Scripts/...`
  - Verification: Lifecycle & cleanup check.

### Phase 3: Polish & Validation
- [ ] **Step 3.1:** Run `unity-pre-commit-gate` (compilation, zero warnings, standards audit).
- [ ] **Step 3.2:** Perform Unity Editor playmode verification.

---

## 8. Verification & Acceptance Criteria
- [ ] Solution compiles with zero warnings: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- [ ] Coding standards strictly followed (naming, field order, constant placement).
- [ ] No memory leaks, dangling tween sequences, or unregistered DI services.
- [ ] Inspector data and prefab compatibility preserved.
