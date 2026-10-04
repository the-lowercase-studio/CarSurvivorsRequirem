---
name: check-optimalization
description: "Use when: auditing current optimizations or performance risks in a pointed Car Survivors system, script, method, update loop, allocation path, combat/UI flow, DI usage, or data access and proposing reviewable changes. Triggers: optimization/optimisation/optimalization review. Source inspection does not imply profiling or implementation authority."
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

Separate project-policy violations from measured runtime costs. Source patterns establish risks; severity and expected impact are provisional without workload/profiler evidence. Do not infer a measured bottleneck or zero-allocation guarantee from inspection alone.

Classify all performance findings into four severity tiers:

- 🔴 Blocker (Unacceptable Hot-Path Cost / Memory Leak)
  - Heap allocations (`new`, LINQ, string concatenation/formatting, closures, enum boxing) inside `Update()`, `FixedUpdate()`, physics callbacks, or high-frequency loops (e.g. FlowField calculations, bullet ticks).
  - Runtime component searches (`FindAnyObjectByType`, `FindObjectOfType`, `GameObject.Find`, or uncached `GetComponent`) inside per-frame updates.
  - Infinite or unkilled DOTween sequences / coroutines lingering after object deactivation or destruction.

- 🟠 Major (Scale & Pooling Bottlenecks)
  - Spawning dynamic combat instances (`Instantiate`/`Destroy` on projectiles, damage numbers, enemy units, particle VFX) without using Assets/Scripts/Pooling/.
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

2. Inspect Risks and Record Available Measurements
   - Audit code against the Performance Gate checklist.
   - Label each finding as source evidence, inference, or profiler measurement. Record profiler environment, workload, samples, and capture paths when measurements exist; otherwise state that profiling was not performed.
   - Report inspected allocation patterns and unverified paths without claiming measured zero allocations or speedups.

3. Formulate Optimization Proposals
   - Label each proposal with Severity (`Blocker`, `Major`, `Minor`, `Nit`) and Expected Impact (`High`, `Medium`, `Low`).
   - Ground every proposal in Car Survivors architecture:
     - Preserve Reflex DI bindings (never replace DI with static singletons for "speed").
     - Preserve inspector workflows and ScriptableObject configurability.
     - Preserve deterministic combat invariants and event ordering.

4. Establish Implementation Authority
   - Present the structured review report to the user.
   - Audit requests remain read-only. Present concrete affected files, proposed changes, expected qualitative impact, and invariant risks before asking for missing implementation authority.
   - Reuse explicit authority already given; do not ask again for approved changes. Before non-trivial code edits, follow the durable plan/requirements gate in AGENTS.md and create the durable summary on completion.

5. Validation
   - For implemented C# changes, run project compilation:
     ```powershell
     dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
     ```
   - For review-only proposals, list compilation as a proposed future check. Distinguish executed results, baseline failures, unavailable checks, and pending Unity Profiler comparison. Claim measured gains only with comparable before/after captures.

## Output

Produce a structured optimization report using:

- .agents/skills/check-optimalization/templates/optimization-review-template.md
