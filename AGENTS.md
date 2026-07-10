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

Generated and local Unity folders such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, and `ProfilerCaptures/` are not source-of-truth project files.

## Agent Source Order

Read agent guidance in this order:

1. `AGENTS.md` for the project entry point and workflow.
2. `.agents/README.md` for operational file layout.
3. `.agents/context/project-coding-standards.md` for code style and architectural constraints.
4. `.agents/context/technology-documentation.md` for official documentation links.
5. `.agents/context/ai-game-dev-best-practices.md` for gameplay and review guardrails.
6. Relevant `.agents/context/game-systems/*-system.md` files when the task touches a documented game system.
7. Relevant `.agents/skills/*/SKILL.md` files when the task matches a skill trigger.

Use `.agents/context/` as the current documentation location for agent-facing project guidance.
Use `.agents/context/game-systems/` for game-system documentation.
Store implementation plans under `.agents/context/implementations/plans/` and implementation summaries under `.agents/context/implementations/summaries/`.

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
- `check-optimalization`: inspect performance risks and propose optimizations before implementing them.
- `di-integration`: add or review Reflex bindings, injected services, and dependency boundaries.
- `document-system`: create or update technical documentation for a specific gameplay system.
- `preserve-coding-standards`: audit and safely fix coding-standard drift in a pointed scope.
- `reduce-code-volume`: audit and safely reduce code volume/lines of code while keeping code readable and safe.
- `unity-refactor-suggestions`: produce behavior-preserving Unity/C# refactor recommendations.

When a request matches a skill trigger, read the relevant `SKILL.md` before editing.

## Agent Descriptors

Current vendor-specific descriptors are nested under skills:

- `.agents/skills/agent-docs-review/agents/openai.yaml`
- `.agents/skills/preserve-coding-standards/agents/openai.yaml`

Treat these YAML files as optional UI/default-prompt metadata for compatible tools. The portable workflow source remains each skill's `SKILL.md`.

## Work Workflow

1. Identify the requested scope and the smallest relevant files.
2. Read this guide, `.agents/README.md`, the relevant `.agents/context/*` file, and any triggered skill.
   For game systems, read the relevant `.agents/context/game-systems/*-system.md` file.
3. Inspect source files before changing behavior or documenting concrete implementation details.
4. Keep edits scoped; do not mix broad cleanup with gameplay, DI, or UI changes.
5. Prefer existing project patterns over new abstractions.
6. Protect user work in a dirty worktree. Do not revert changes you did not make.
7. Validate with a targeted compile or test command when possible.
8. Summarize files changed, validation performed, and any remaining Unity Editor/manual checks.

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
- Do not prefix filenames for implementation plans and summaries with dates; specify the date inside the file content instead.
- Write file and directory paths in agent documentation relative to the project root as plain text, without markdown links or backticks (e.g. `- Assets/Scripts/...`).
- When adding a new specialist domain, add or update the matching skill or agent file under `.agents/`.
- Keep skill descriptions explicit with `Use when:` trigger language.
- Update cross-references when docs move or filenames change.
