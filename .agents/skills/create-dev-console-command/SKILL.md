---
name: create-dev-console-command
description: "Use when: designing, specifying, or planning a new developer console CLI command in Car Survivors. Triggers: create dev console command, add console command, new dev command, dev console command spec, create dev command, new console command, dev command specification."
---

# Create Dev Console Command Skill

Use this skill to design and specify a new in-game Developer Console command in Car Survivors to staff-engineer standards before implementation begins. It extracts and structures architectural context from the Dev Console system documentation and codebase, enforces mandatory user requirements, and delegates specification authoring to the gameplay-spec-writing workflow.

## Mandatory User Input Gate

The user MUST provide two core inputs before a command specification can be drafted:

1. Command Name & Syntax: The exact command keyword and expected arguments or flags (for example: godmode, killall, speed <multiplier>, timescale <float>, or wave jump <number>).
2. Command Behavior & Purpose: What the command does at runtime, what gameplay systems it alters, default values, and how feedback is conveyed to the developer.

If either the command name or the intended behavior is missing, ambiguous, or underspecified:
- Stop immediately and ask the user for the missing details.
- Do NOT fabricate command syntax or assume gameplay effects without explicit user input.
- Once both inputs are provided, proceed to context assembly and specification writing.

## Required Sources

Before drafting the command specification, load context from:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/game-systems/dev-console-system.md
- .agents/skills/gameplay-spec-writing/SKILL.md
- .agents/skills/gameplay-spec-writing/templates/gameplay-spec-template.md
- Relevant domain game system docs under .agents/context/game-systems/ (e.g., enemies-system.md, level-system.md, skills-system.md)
- Primary Dev Console codebase files:
  - Assets/Scripts/UI/DevConsole/DevCommandInfo.cs
  - Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs
  - Assets/Scripts/UI/DevConsole/DevConsoleService.cs
  - Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs
  - Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs

## Architectural Context & Invariants

When preparing the command design, adhere strictly to the Dev Console architecture:

1. Command Registration Pattern:
   - Registration API: IDevConsoleService.RegisterCommand(name, syntax, description, handler).
   - Handlers receive parsed whitespace-separated argument tokens: Action<string[]> handler.
   - Lifecycle containment: Any component that registers commands MUST unregister them in OnDestroy() via IDevConsoleService.UnregisterCommand(name) to prevent memory leaks and dangling delegates across scene reloads.

2. Registrar Placement Strategy:
   - Shared Gameplay Commands: If the command affects common gameplay entities (player health, stats, spawning, enemies, level), extend Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs.
   - Domain-Isolated Commands: If the command belongs to a dedicated or specialized subsystem (e.g., audio diagnostics, save data wiping, network simulation), introduce a specialized registrar MonoBehaviour (e.g., AudioDevCommandsRegistrar) implementing a dedicated marker interface and bind it in Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs.
   - Core Console Utilities: If the command manages the console window itself (history, clearing, formatting), register it directly within Assets/Scripts/UI/DevConsole/DevConsoleService.cs.

3. Release Build Stripping Invariant:
   - Developer commands must have zero runtime overhead in production distribution.
   - Wrap command registration, registrar execution, and debug logic in compile-time checks (#if UNITY_EDITOR || DEVELOPMENT_BUILD) combined with runtime Debug.isDebugBuild evaluation.
   - In release builds, registrars must disable themselves in Awake(), and IDevConsoleService remains inert. Reflex DI bindings remain bound to satisfy downstream injection safely.

4. Argument Parsing & Validation:
   - Tokenization: Tokens are split by whitespace. Quoted multi-word strings require heuristic token concatenation if needed.
   - Validation: Validate token count and data types (e.g. int.TryParse, float.TryParse with CultureInfo.InvariantCulture).
   - Error Feedback: On invalid syntax or missing arguments, output clear, user-facing error feedback using IDevConsoleService.LogError(usageMessage). Never crash or throw unhandled exceptions.
   - Flags: Support standard long (--flag) and short (-f) flags where appropriate.

5. Safe Exception Containment:
   - Guard against null injected domain dependencies (e.g., if PlayerManager or BossEncounterService is absent in a test scene). Output a diagnostic error log rather than allowing NullReferenceException.

6. Constants & Coding Standards:
   - Declare command names, syntax strings, descriptions, usage strings, and magic numbers as constants in Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs or a domain-specific Constants/ folder.
   - Field ordering in registrar classes: [Inject] private readonly fields first, followed by [SerializeField] private fields, then private fields.

## Workflow

1. Validate User Inputs
   - Verify that the user has provided:
     - The command name / syntax keyword.
     - The intended behavior and effects.
   - If missing, prompt the user for clarification before proceeding.

2. Assemble Context & Inspect Touched Domains
   - Read .agents/context/game-systems/dev-console-system.md.
   - Inspect the target gameplay subsystem that the command interacts with (e.g., Assets/Scripts/Player/, Assets/Scripts/Enemies/, Assets/Scripts/LevelSystem/).
   - Determine which services need to be injected into the registrar.
   - Confirm registrar placement (extend GameplayDevCommandsRegistrar vs create new registrar).

3. Invoke gameplay-spec-writing Workflow
   - Package the command requirements and Dev Console architectural context.
   - Follow the gameplay-spec-writing skeleton-first workflow:
     - Formulate the specification using .agents/skills/create-dev-console-command/templates/dev-console-command-spec-template.md.
     - Save the specification directly in the repository at:
       .agents/context/implementations/plans/[command-name]-command-spec.md
       (Use kebab-case without date prefix in filename; include date inside document).
   - Evaluate the Open Questions Gate:
     - If critical unknowns remain (e.g., specific parameter ranges, whether to persist state, target scene availability), document them under Open Questions and pause for user input.
     - If all requirements are settled, record Open Questions: None and finalize the detailed implementation phases and verification steps.

4. Report Plan to User
   - Inform the user of the created specification path.
   - Highlight resolved decisions and any open questions requiring confirmation.
   - Await user approval before moving to the code execution phase.

## Output

Produce and save a completed command specification under:

- .agents/context/implementations/plans/[command-name]-command-spec.md

Structured according to:

- .agents/skills/create-dev-console-command/templates/dev-console-command-spec-template.md
