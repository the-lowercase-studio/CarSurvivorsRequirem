# Project Agent Guide

## Purpose

This is the vendor-neutral entry point for AI agents working in this repository. Use it to map the project, find the authoritative operational files, choose the right workflow, and validate changes without depending on a specific agent vendor.

## Project Map

- Project: `Car Survivors`, a Unity 3D survivor prototype where the player is a car.
- Runtime code: `Assets/Scripts/`.
- Scenes: `Assets/Scenes/Boot.unity`, `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/RuinedBloodCity.unity`.
- Scene and project DI setup: `Assets/Scripts/ReflexDI/`.
- Designer-authored data: `Assets/ScriptableObjects/`, `Assets/Prefabs/`, `Assets/Resources/`.
- Visual/audio content: `Assets/Animations/`, `Assets/Audio/`, `Assets/Materials/`, `Assets/Textures/`, `Assets/VFX/`, `Assets/Shaders/`.
- Unity package versions: `Packages/manifest.json`.
- Agent operational files: `.agents/`.
- User and human-facing documentation: `.user-docs/`.

Generated and local Unity folders such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, and `ProfilerCaptures/` are not source-of-truth project files.
User documentation under `.user-docs/` is written only upon explicit user request and is intended for human readers; agents must not read `.user-docs/` as an operational source of truth.

## Agent Source Order

Read agent guidance in this order:

1. `AGENTS.md` for the project entry point and workflow.
2. `.agents/README.md` for operational file layout.
3. `.agents/context/project-coding-standards.md` for code style and architectural constraints.
4. Relevant `.agents/context/adr/*.md` files for architectural decision records.
5. `.agents/context/technology-documentation.md` for official documentation links.
6. `.agents/context/ai-game-dev-best-practices.md` for gameplay and review guardrails.
7. Relevant `.agents/context/game-systems/*-system.md` files when the task touches a documented game system.
8. Relevant `.agents/skills/*/SKILL.md` files when the task matches a skill trigger.

Use `.agents/context/` as the current documentation location for agent-facing project guidance.
Use `.agents/context/adr/` for architecture decision records (ADRs).
Use `.agents/context/game-systems/` for game-system documentation.
Store implementation plans under `.agents/context/implementations/plans/` and implementation summaries under `.agents/context/implementations/summaries/`.
Do not read `.user-docs/` as an operational source of truth for agent reasoning; operational truth derives strictly from the codebase and `.agents/`.

## Technology Baseline

- Unity project with C# scripts under `Assets/Scripts/`.
- Reflex is used for dependency injection. Prefer explicit interfaces and bindings over singleton access or broad scene searches.
- DOTween is used for animation/tween flows. Reuse existing tween helpers such as `Assets/Scripts/Extensions/TransformTweenExtensions.cs` when appropriate.
- Universal Render Pipeline, Input System, Cinemachine, Visual Effect Graph, ProBuilder, Unity Test Framework, and NuGetForUnity are listed in `Packages/manifest.json`.

When behavior depends on Unity, Reflex, DOTween, or package-specific APIs, consult official documentation through `.agents/context/technology-documentation.md` before relying on memory.

## Core Architecture Rules

- Keep runtime dependencies explicit through Reflex where DI is already established.
- Register scene/runtime services in the appropriate installer under `Assets/Scripts/ReflexDI/`.
- Reuse existing interfaces before adding new abstractions.
- Colocate a narrow interface above its primary implementation when it is primarily owned by that implementation.
- Do not reintroduce singleton patterns, static mutable service state, or `FindAnyObjectByType`-style lookup as a shortcut.
- Preserve inspector-driven workflows and serialized data compatibility.
- Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly unless the user explicitly asks and the change is safe to review as text.

## Coding Standards

Follow `.agents/context/project-coding-standards.md` for detailed rules. The highest-impact rules are:

- Private and serialized fields use `_camelCase`.
- Constants use `UPPER_SNAKE_CASE` and belong in a `Constants` folder under the owning system when new constants are introduced.
- Field order in runtime classes is `[Inject]` fields, then `[SerializeField]` fields, then other private fields.
- Public members, properties, methods, types, and events use PascalCase.
- Events use `OnX` naming.
- Prefer `[SerializeField] private` fields over public mutable inspector fields.
- Treat warnings as errors during development.
- Keep legacy cleanup incremental and scoped to touched code.

## Gameplay Guardrails

- Keep player, enemy, grid, wave, skill, projectile, health, damage number, settings, score, and UI behavior deterministic unless the user requests a design change.
- Preserve event ordering when changing scene startup, death flow, spawning, leveling, UI presenters, or any turn-like/game-state sequence.
- Preserve designer configuration through ScriptableObjects, prefabs, serialized fields, and scene references.
- Keep balance values as explicit, reviewable data rather than hidden behavior changes.
- Ask the user before changing player-facing mechanics, balance, scene setup, serialized data shape, or Unity Editor workflows.

## Common System Areas

- DI and boot flow: `Assets/Scripts/ReflexDI/`.
- Player: `Assets/Scripts/Player/`, including player-owned car code under `Assets/Scripts/Player/Car/`.
- Enemies and waves: `Assets/Scripts/Enemies/`, `Assets/Scripts/Waves/`.
- Grid and flow field: `Assets/Scripts/Navigation/GridSystem/`, `Assets/Scripts/Navigation/FlowFieldSystem/`.
- Skills and projectiles: `Assets/Scripts/Skills/`, `Assets/Scripts/Projectiles/`.
- Health, status, damage feedback: `Assets/Scripts/HealthSystem/`, `Assets/Scripts/StatusEffects/`, `Assets/Scripts/DamageNumbers/`.
- UI and settings: `Assets/Scripts/UI/`, `Assets/Scripts/Settings/`, `Assets/Scripts/ScoreBoard/`.
- Audio: `Assets/Scripts/Audio/`.
- Pooling, lifecycle, spawners: `Assets/Scripts/Pooling/`, `Assets/Scripts/ObjectLifecycle/`, `Assets/Scripts/Spawners/`.
- Editor tooling: `Assets/Scripts/Editor/`.

Verify exact behavior in code before making behavioral claims; this guide is a map, not a replacement for source inspection.

## Agent Skills

Use `.agents/skills/` as reusable workflows:

- `agent-docs-review`: update agent-facing documentation so it is concise, current, and implementation-grounded.
- `architecture-review`: review DI correctness, ownership boundaries, turn-flow safety, and architecture drift.
- `batch-codebase-review`: partition and orchestrate project-wide architecture review and coding standards preservation via subagents or generated prompt roadmaps.
- `check-optimalization`: inspect performance risks and propose optimizations before implementing them.
- `create-user-doc`: create or update human-facing, user-oriented project documentation in `.user-docs/` upon explicit user request.
- `di-integration`: add or review Reflex bindings, injected services, and dependency boundaries.
- `document-system`: create or update technical documentation for a specific gameplay system.
- `game-brainstorm`: explore gameplay ideas, mechanics, weapon concepts, and architecture trade-offs before creating code or specs.
- `gameplay-spec-writing`: write or review staff-engineer level technical specifications and implementation plans with hard Open Questions gates.
- `preserve-coding-standards`: audit and safely fix coding-standard drift in a pointed scope.
- `reduce-code-volume`: audit and safely reduce code volume/lines of code while keeping code readable and safe.
- `unity-pre-commit-gate`: run a comprehensive pre-commit verification gate (compilation, zero warnings, DI bindings, serialization safety, standards audit).
- `unity-refactor-suggestions`: produce behavior-preserving Unity/C# refactor recommendations.
- `unity-root-cause`: systematically investigate and diagnose Unity runtime bugs, DI issues, and lifecycle defects in read-only mode.

When a request matches a skill trigger, read the relevant `SKILL.md` before editing.

## Agent Descriptors

Current vendor-specific descriptors are nested under skills:

- `.agents/skills/agent-docs-review/agents/openai.yaml`
- `.agents/skills/batch-codebase-review/agents/openai.yaml`
- `.agents/skills/create-user-doc/agents/openai.yaml`
- `.agents/skills/preserve-coding-standards/agents/openai.yaml`

Treat these YAML files as optional UI/default-prompt metadata for compatible tools. The portable workflow source remains each skill's `SKILL.md`.

## Implementation Lifecycle & In-Repo Storage Invariant

For every non-trivial task, feature, or refactor, follow this two-phase repository documentation lifecycle:

1. **Planning Phase (Before Code Changes)**:
   - Create the implementation plan directly in the repository at `.agents/context/implementations/plans/[feature-name]-plan.md` (or `[feature-name]-spec.md`).
   - Use `.agents/context/implementations/templates/plan-template.md` as reference.
   - Stop and confirm requirements / open questions with the user before editing code.
2. **Execution Phase**:
   - Implement the approved changes in code adhering to `.agents/context/project-coding-standards.md`.
3. **Summary Phase (Upon Completion)**:
   - Create the implementation summary directly in the repository at `.agents/context/implementations/summaries/[feature-name]-summary.md` (or `[feature-name].md`).
   - Use `.agents/context/implementations/templates/summary-template.md` as reference.
   - Document changes made, automated/manual validation performed, and any editor follow-ups.

**Strict Negative Constraints**:
- **NEVER** save implementation plans or summaries exclusively to external IDE directories (e.g. `brain/`, `AppData`, `/tmp`, or user profile folders).
- If an IDE environment (such as Antigravity) generates an artifact for UI preview, the authoritative in-repo markdown file under `.agents/context/implementations/` must ALWAYS be created/updated first.

## Work Workflow

1. Identify the requested scope and the smallest relevant files.
2. Read this guide, `GEMINI.md` (if running under Gemini/Antigravity), `.agents/README.md`, the relevant `.agents/context/*` file, and any triggered skill.
   For game systems, read the relevant `.agents/context/game-systems/*-system.md` file.
3. Create or update the implementation plan under `.agents/context/implementations/plans/` when planning non-trivial changes.
4. Inspect source files before changing behavior or documenting concrete implementation details.
5. Keep edits scoped; do not mix broad cleanup with gameplay, DI, or UI changes.
6. Prefer existing project patterns over new abstractions.
7. Protect user work in a dirty worktree. Do not revert changes you did not make.
8. Validate with a targeted compile or test command when possible.
9. Create the implementation summary under `.agents/context/implementations/summaries/`.
10. Summarize files changed, validation performed, and any remaining Unity Editor/manual checks.

## Validation

For C# changes, prefer a targeted project compile:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

For documentation-only changes, review links and paths for accuracy. A Unity Editor play-mode check is still required for scene wiring, prefab references, visual timing, audio, VFX, balance, and feel.

## Documentation Maintenance

- Keep agent operational guidance under `.agents/`.
- Keep this root `AGENTS.md` as a short entry point, not a full replacement for `.agents/context/*`.
- Keep game-system documentation in `.agents/context/game-systems/`.
- Keep implementation plans in `.agents/context/implementations/plans/` and summaries in `.agents/context/implementations/summaries/`.
- Use `.agents/context/implementations/templates/` for plan and summary structure.
- Keep human-facing project documentation in `.user-docs/`, creating or editing files there only upon explicit user request.
- Do not prefix filenames for implementation plans and summaries with dates; specify the date inside the file content instead.
- Write file and directory paths in agent documentation relative to the project root as plain text, without markdown links or backticks (e.g. `- Assets/Scripts/...`).
- When adding a new specialist domain, add or update the matching skill or agent file under `.agents/`.
- Keep skill descriptions explicit with `Use when:` trigger language.
- Update cross-references when docs move or filenames change.
