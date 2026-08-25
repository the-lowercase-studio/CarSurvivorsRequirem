---
name: unity-root-cause
description: "Use when: investigating and diagnosing bugs, runtime exceptions, unexpected gameplay behavior, broken DI injection, physics/flowfield glitches, or lifecycle issues in Unity without making premature code changes. Triggers: root cause, investigate bug, find bug, why is this broken, null reference exception, trace defect, bug investigation."
---

# Unity Root Cause Skill

Use this skill for read-only systematic bug investigation in Unity. It traces defects to their exact source, categorizes failure domains, and defines the minimal change set needed for a safe fix without modifying codebase files during the investigation.

## Hard Gate (Read-Only)

This skill is strictly read-only.
- Do not edit C# scripts, prefabs, scenes, or ScriptableObjects during diagnosis.
- Do not make speculative fixes while investigating.
- The output is a clear, actionable Root Cause Report that guides the subsequent fix.

## Required Sources

Ground the investigation in:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/project-scripts-folder-map.md
- Relevant game system docs under .agents/context/game-systems/

## Unity Defect Categories

Classify the defect into one or more categories:

1. C# Logic & State: calculation errors, missing state resets, incorrect condition checks.
2. Reflex DI Wiring: missing container bindings, injected interface mismatch, incorrect container scope (Project vs Scene).
3. Unity Lifecycle & Race Conditions: `Awake` vs `Start` vs `OnEnable` order conflicts, execution after destruction.
4. Object Pooling & State Leakage: pooled objects retaining dirty state, active tweens, or stale physics velocities on release/spawn.
5. Serialization & Asset References: null `[SerializeField]` fields, broken asset GUIDs, missing scene bindings.
6. Event Subscriptions & Memory Leaks: dangling event handlers, missing unsubscription in `OnDisable`/`OnDestroy`.

## Workflow

1. Ingest Symptom & Reproduction Steps
   - Analyze error message, stack trace, or gameplay anomaly described by the user.
   - Note affected systems (e.g. Car controller, Spawner, DamageNumbers, FlowField, Waves).

2. Trace Code Path & Execution Flow
   - Search for the entry point (event trigger, physics collision, update loop, UI action).
   - Trace callers and callees across touched classes.
   - Check where null references, unhandled states, or race conditions originate.

3. Audit Dependencies & Lifecycle
   - Check `[Inject]` fields: are they populated via Reflex? Is the installer binding them properly?
   - Check Unity lifecycle hooks: are references initialized in `Awake`/`Start` before being used in `OnEnable` or `Update`?
   - Check DOTween / Coroutine lifecycles: are tweens killed when GameObjects are recycled or disabled?

4. Formulate & Confirm Root Cause
   - Identify the primary flaw causing the defect.
   - Explain why the bug occurs and under what exact conditions.

5. Define Minimal Change Set
   - Specify the exact files, methods, and lines to modify.
   - Ensure the proposed fix is minimal and avoids collateral architectural changes.
   - Identify potential regression risks and how to verify the fix.

## Output

Produce a structured report based on:

- .agents/skills/unity-root-cause/templates/root-cause-report-template.md
