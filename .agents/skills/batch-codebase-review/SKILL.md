---
name: batch-codebase-review
description: "Use when: orchestrating or partitioning project-wide architecture review and coding standards preservation across the entire codebase or multiple domain systems. Triggers: batch codebase review, partition codebase review, multi-agent code review, parallel codebase audit."
---

# Batch Codebase Review Skill

Use this skill to orchestrate, partition, and execute a comprehensive `architecture-review` and `preserve-coding-standards` audit across all C# codebase scripts under `Assets/Scripts/`.

## Required Sources

Always read these before executing or partitioning:

- `AGENTS.md`
- `.agents/README.md`
- `.agents/context/project-coding-standards.md`
- `.agents/skills/architecture-review/SKILL.md`
- `.agents/skills/preserve-coding-standards/SKILL.md`

## Capability Assessment & Execution Branching

Before taking action, check whether the current agent environment supports **direct subagent spawning** (e.g. `invoke_subagent` tool or subagent delegation API).

---

### Branch A: Direct Subagent Execution (Subagent Capabilities Available)

If subagent spawning is supported in the toolset:

1. **Partition Codebase**: Divide `Assets/Scripts/` into cohesive domain batches (10 to 30 files per batch):
   - **Batch 1**: Core Boot, Reflex DI & Game Flow (`ReflexDI/`, `Initializers/`, `GameFlow/`, `Providers/`, `GameWindow/`)
   - **Batch 2**: Player, Car Mechanics & Navigation (`Player/`, `Navigation/GridSystem/`, `Navigation/FlowFieldSystem/`, `Collisions/`)
   - **Batch 3**: Enemies, Waves, Spawners & Object Lifecycle (`Enemies/`, `Waves/`, `Spawners/`, `Pooling/`, `ObjectLifecycle/`)
   - **Batch 4**: Combat Systems (`Skills/`, `Projectiles/`)
   - **Batch 5**: Health, Stats, Status Effects & Damage Numbers (`HealthSystem/`, `Stats/`, `StatusEffects/`, `DamageNumbers/`)
   - **Batch 6**: UI Systems (`UI/`)
   - **Batch 7A**: Progression, Audio, VFX, Settings & Storage (`Audio/`, `VFX/`, `Effects/`, `ScoreBoard/`, `LevelSystem/`, `Settings/`, `Storage/`, `Interactables/`)
   - **Batch 7B**: Shared Infrastructure, Utilities & Editor Tools (`Shapes/`, `Volumes/`, `LayerMasks/`, `Utils/`, `Extensions/`, `Common/`, `Editor/`)

2. **Spawn Specialized Subagents**: For each domain batch, invoke a dedicated subagent with an explicit prompt task instructing it to:
   - Perform `architecture-review` (DI correctness, Reflex installer bindings, interface colocation, singleton removal).
   - Perform `preserve-coding-standards` (field ordering `[Inject]` -> `[SerializeField]` -> `private`, `_camelCase` private fields, `UPPER_SNAKE_CASE` constants in `Constants/` subfolders, `[SerializeField] private` encapsulation, `OnX` event naming).
   - Run compilation check: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.

3. **Aggregate & Report**: Collect results from all subagents and present a unified summary of changes and compile validation results to the user.

---

### Branch B: Prompt Roadmap & Batch Plan (Subagent Capabilities Unavailable)

If subagent spawning tools are **NOT available** in the current agent environment:

1. **Partition Codebase**: Divide `Assets/Scripts/` into the baseline domain batches listed in Branch A.
2. **Generate Implementation Plan**: Create or update `implementation_plan.md` detailing the file scope, file counts, and architectural review goals for each batch.
3. **Generate Prompts Roadmap**: Create an `agent_prompts_roadmap.md` artifact containing ready-to-copy, self-contained prompt templates for each batch. Each prompt must instruct an external/parallel agent to:
   - Read `AGENTS.md` and `.agents/context/project-coding-standards.md`.
   - Apply both `architecture-review` and `preserve-coding-standards` on the specific folder scope.
   - Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
4. **Offer Execution Choices**: Ask the user whether to:
   - Copy prompts to run parallel agent sessions in separate chat windows.
   - Execute the batches sequentially in the current chat session, starting with Batch 1.

---

## Verification

Every executed batch (whether run by a subagent, an external agent, or sequentially in-session) must be validated with:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

No batch refactor is complete until C# compilation succeeds cleanly without new build errors.
