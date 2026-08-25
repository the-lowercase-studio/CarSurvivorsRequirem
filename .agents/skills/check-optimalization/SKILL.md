---
name: check-optimalization
description: "Use when: checking current optimizations or performance risks for pointed Car Survivors systems, scripts, methods, code parts, Unity components, combat flows, UI flows, allocations, update loops, events, DI usage, or data access before proposing and requesting approval to implement possible optimization changes. Also trigger for optimization/optimisation/optimalization review requests."
---

# Check Optimalization Skill

Use this skill to inspect a targeted Car Survivors system, script, or code section for performance risks, frame-rate bottlenecks, memory allocations (GC pressure), and safe optimization opportunities.

## Required Sources

Before reviewing code, ground the work in:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/technology-documentation.md
- Relevant game system docs under .agents/context/game-systems/

## Performance Gate & Severity Classification

Classify all performance findings into four severity tiers:

- 🔴 Blocker (Unacceptable Hot-Path Cost / Memory Leak)
  - Heap allocations (`new`, LINQ, string concatenation/formatting, closures, enum boxing) inside `Update()`, `FixedUpdate()`, physics callbacks, or high-frequency loops (e.g. FlowField calculations, bullet ticks).
  - Runtime component searches (`FindAnyObjectByType`, `FindObjectOfType`, `GameObject.Find`, or uncached `GetComponent`) inside per-frame updates.
  - Infinite or unkilled DOTween sequences / coroutines lingering after object deactivation or destruction.

- 🟠 Major (Scale & Pooling Bottlenecks)
  - Spawning dynamic combat instances (`Instantiate`/`Destroy` on projectiles, damage numbers, enemy units, particle VFX) without using `Assets/Scripts/Pooling/`.
  - Non-layer-masked physics raycasts, sphere casts, or overlap queries executed per unit per frame.
  - Heavy UI canvas rebuilds triggered repeatedly every frame instead of event-driven updates.

- 🟡 Minor (Algorithmic & Math Inefficiencies)
  - Missing transform or property caching in frequently called methods.
  - Expensive distance calculations using `Vector3.Distance` instead of `sqrMagnitude` in tight comparisons.
  - Redundant collection resizing (missing initial capacity in `List<T>` or `HashSet<T>`).

- ⚪ Nit (Micro-Optimizations & Style)
  - Using `Mathf.Pow(x, 2)` instead of `x * x`.
  - Minor struct vs class data layout adjustments.

## Performance Audit Workflow

1. Identify Review Boundary
   - Confirm in-scope files and runtime invocation paths (e.g. `Update`, event handlers, physics queries).
   - Trace hot paths (methods called 60+ times per second or per-unit-per-frame).

2. Profile & Identify Hot Spots
   - Audit code against the Performance Gate checklist.
   - Separate verified zero-allocation hot paths from risky allocation patterns.

3. Formulate Optimization Proposals
   - Label each proposal with Severity (`Blocker`, `Major`, `Minor`, `Nit`) and Expected Impact (`High`, `Medium`, `Low`).
   - Ground every proposal in Car Survivors architecture:
     - Preserve Reflex DI bindings (never replace DI with static singletons for "speed").
     - Preserve inspector workflows and ScriptableObject configurability.
     - Preserve deterministic combat invariants and event ordering.

4. Obtain Approval Before Implementing
   - Present the structured review report to the user.
   - If the user approves implementation, proceed with atomic, behavior-preserving changes.

5. Validation
   - Run project compilation:
     ```powershell
     dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
     ```
   - Specify Unity Profiler / Deep Profile verification instructions for the user.

## Output

Produce a structured optimization report using:

- .agents/skills/check-optimalization/templates/optimization-review-template.md
