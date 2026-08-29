---
name: batch-codebase-review
description: "Use when: orchestrating or partitioning project-wide architecture review and coding standards preservation across the entire codebase or multiple domain systems. Triggers: batch codebase review, partition codebase review, multi-agent code review, parallel codebase audit."
---

# Batch Codebase Review Skill

Use this skill to orchestrate, partition, and execute a comprehensive `architecture-review` and `preserve-coding-standards` audit across all C# codebase scripts under Assets/Scripts/.

It supports both **Parallel Subagent Execution** and **Sequential Loop Execution with Checkpoints and Handoff State**, inspired by enterprise agentic loop workflows.

## Required Sources

Always read these before executing or partitioning:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/skills/architecture-review/SKILL.md
- .agents/skills/preserve-coding-standards/SKILL.md
- .agents/skills/unity-pre-commit-gate/SKILL.md

## Codebase Domain Partitioning

Divide Assets/Scripts/ into cohesive domain batches (10 to 30 files per batch):

- Batch 1: Core Boot, Reflex DI & Game Flow
  - Scopes: Assets/Scripts/ReflexDI/, Assets/Scripts/Initializers/, Assets/Scripts/GameFlow/, Assets/Scripts/Providers/, Assets/Scripts/GameWindow/
- Batch 2: Player, Car Mechanics & Navigation
  - Scopes: Assets/Scripts/Player/, Assets/Scripts/Navigation/GridSystem/, Assets/Scripts/Navigation/FlowFieldSystem/, Assets/Scripts/Collisions/
- Batch 3: Enemies, Waves, Spawners & Object Lifecycle
  - Scopes: Assets/Scripts/Enemies/, Assets/Scripts/Waves/, Assets/Scripts/Spawners/, Assets/Scripts/Pooling/, Assets/Scripts/ObjectLifecycle/
- Batch 4: Combat Systems
  - Scopes: Assets/Scripts/Skills/, Assets/Scripts/Projectiles/
- Batch 5: Health, Stats, Status Effects & Damage Numbers
  - Scopes: Assets/Scripts/HealthSystem/, Assets/Scripts/Stats/, Assets/Scripts/StatusEffects/, Assets/Scripts/DamageNumbers/
- Batch 6: UI Systems
  - Scopes: Assets/Scripts/UI/
- Batch 7A: Progression, Audio, VFX, Settings & Storage
  - Scopes: Assets/Scripts/Audio/, Assets/Scripts/VFX/, Assets/Scripts/Effects/, Assets/Scripts/ScoreBoard/, Assets/Scripts/LevelSystem/, Assets/Scripts/Settings/, Assets/Scripts/Storage/, Assets/Scripts/Interactables/
- Batch 7B: Shared Infrastructure, Utilities & Editor Tools
  - Scopes: Assets/Scripts/Shapes/, Assets/Scripts/Volumes/, Assets/Scripts/LayerMasks/, Assets/Scripts/Utils/, Assets/Scripts/Extensions/, Assets/Scripts/Common/, Assets/Scripts/Editor/

---

## State Machine & Checkpoint Discipline

For multi-batch execution, maintain a structured tracking state machine inside `.agents/context/tmp/`:

1. Tracking Plan: Initialize `.agents/context/tmp/batch_review_plan.md` using the plan template.
2. Handoff Document: Initialize `.agents/context/tmp/batch_review_handoff.md` with a Tasks Table tracking:
   - Batch ID | Domain | Scope | Status (`PENDING`, `IN_PROGRESS`, `DONE`, `BLOCKED`) | Checkpoint Build (`PASS`/`FAIL`) | Notes
3. Checkpoint Verification Gate:
   - After completing each batch, run the compilation checkpoint:
     ```powershell
     dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
     ```
   - If build fails, fix immediately within the batch scope before marking as `DONE`.
   - Update `.agents/context/tmp/batch_review_handoff.md` with findings and status.
4. Resumption Protocol:
   - When resuming an interrupted session, inspect `.agents/context/tmp/batch_review_handoff.md`.
   - Identify the first non-`DONE` batch row and continue execution without re-auditing completed batches.

---

## Execution Branches

### Branch A: Direct Subagent Parallel Execution
When subagent capabilities (`invoke_subagent`) are available:
1. Initialize `.agents/context/tmp/batch_review_plan.md` and `.agents/context/tmp/batch_review_handoff.md`.
2. Launch dedicated subagents for independent batches.
3. Each subagent audits its assigned scope, applies safe standard fixes, runs `dotnet build`, and reports back.
4. The coordinator aggregates results, verifies overall project compilation, and finalizes the handoff report.

### Branch B: Sequential Loop Execution with Checkpoints
When running in a single agent session:
1. Initialize `.agents/context/tmp/batch_review_plan.md` and `.agents/context/tmp/batch_review_handoff.md`.
2. Process batches sequentially:
   - Batch 1 -> Fix standards & audit architecture -> Checkpoint compile -> Mark DONE.
   - Batch 2 -> Fix standards & audit architecture -> Checkpoint compile -> Mark DONE.
   - ... repeat through all batches.
3. Maintain clean commits or diff summaries per batch.

### Branch C: Prompt Roadmap Generation
When preparing instructions for external parallel agent sessions:
1. Generate `.agents/context/tmp/agent_prompts_roadmap.md` with self-contained, copy-pasteable prompts for each batch.
2. Each prompt includes full context references, scope boundaries, and the compilation verification command.

---

## Verification & Completion

A batch codebase review is complete only when:
1. All domain batches are marked `DONE` in `.agents/context/tmp/batch_review_handoff.md`.
2. Whole-project compilation succeeds with zero warnings:
   ```powershell
   dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
   ```
3. A consolidated summary report is presented to the user.

## Output Templates

- .agents/skills/batch-codebase-review/templates/batch-review-plan-template.md
- .agents/skills/batch-codebase-review/templates/batch-review-handoff-template.md
