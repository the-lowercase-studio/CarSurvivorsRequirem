# Batch Codebase Review Plan

**Date:** [YYYY-MM-DD]  
**Execution Mode:** [Parallel Subagents | Sequential Loop | Prompt Roadmap]  
**Project:** Car Survivors  

---

## 1. Objectives & Quality Gates
- **Standard Alignment:** Enforce project coding standards across all touched files.
- **Architecture Audit:** Audit Reflex DI bindings, singletons, lifecycle unsubscriptions, and pooling.
- **Compilation Invariant:** Every batch must pass `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` with zero warnings.

---

## 2. Batch Partitioning & Scopes

| Batch ID | Domain | Scope Paths | Estimated Files | Owner / Subagent |
| :--- | :--- | :--- | :---: | :--- |
| **Batch 1** | Boot, Reflex DI & Game Flow | `Assets/Scripts/ReflexDI/`, `Initializers/`, `GameFlow/`, `Providers/`, `GameWindow/` | ~15 | |
| **Batch 2** | Player & Navigation | `Assets/Scripts/Player/`, `Navigation/GridSystem/`, `Navigation/FlowFieldSystem/`, `Collisions/` | ~25 | |
| **Batch 3** | Enemies, Waves & Spawners | `Assets/Scripts/Enemies/`, `Waves/`, `Spawners/`, `Pooling/`, `ObjectLifecycle/` | ~30 | |
| **Batch 4** | Combat & Skills | `Assets/Scripts/Skills/`, `Assets/Scripts/Projectiles/` | ~20 | |
| **Batch 5** | Health, Stats & Feedback | `Assets/Scripts/HealthSystem/`, `Stats/`, `StatusEffects/`, `DamageNumbers/` | ~20 | |
| **Batch 6** | UI Systems | `Assets/Scripts/UI/` | ~20 | |
| **Batch 7A** | Audio, VFX & Settings | `Assets/Scripts/Audio/`, `VFX/`, `Effects/`, `ScoreBoard/`, `LevelSystem/`, `Settings/`, `Storage/`, `Interactables/` | ~25 | |
| **Batch 7B** | Utilities & Editor Tools | `Assets/Scripts/Shapes/`, `Volumes/`, `LayerMasks/`, `Utils/`, `Extensions/`, `Common/`, `Editor/` | ~25 | |
