# Car Survivors Coding Standards

## Purpose

This document defines project-specific coding standards for contributors and AI agents.

## Scope

Apply these rules to:

1. New files.
2. New code added to existing files.
3. Modified sections in refactors.

When a file contains legacy style, prefer incremental migration in touched areas instead of broad formatting-only rewrites.

## 1) Interface and Class Organization

### Interface Colocation (Required)

- Define interface and implementation in the same file whenever the interface is primarily used by that implementation.
- Place the interface above the implementing class.

Pattern:

```csharp
public interface ITurnManager
{
    void StartPlayerTurn();
}

public class TurnManager : MonoBehaviour, ITurnManager
{
}
```

### One Primary Runtime Type Per File

- Keep each file focused on one primary runtime class.
- Supporting small enums or interface contracts may live in the same file when tightly related.

## 2) Naming Conventions

### General Naming Rules

- All identifiers (classes, structs, interfaces, methods, properties, fields, parameters, local variables, enum values, namespaces) must be in English.

### Constants

- Use UPPER_SNAKE_CASE for all const fields.
- Constants must live in a `Constants` folder under the owning system root (for example Assets/Scripts/Skills/Constants/, Assets/Scripts/DamageNumbers/Constants/, Assets/Scripts/Editor/Constants/).
- Avoid a single global constants root folder; keep constants close to the domain that owns them.
- Do not keep reusable constants inside gameplay classes like `EnemyBase` or `CarController`; reference constants classes instead.
- Use `*Constants` naming for constants containers (for example `PositionConstants`, `DamageNumberConstants`).

Examples:

```csharp
private const float ROTATION_TWEEN_DURATION = 0.4f;
public const int MAX_ENEMIES_ALIVE = 50;
```

Placement example:

```csharp
namespace Assets.Scripts.Skills.Constants
{
    public static class PositionConstants
    {
        public const float DISTANCE_ACCURACY = 0.01f;
    }
}
```

### Interfaces

- Prefix interface names with I.

Examples:

```csharp
ICarController
ITargetsProvider
```

### Fields

- Private fields: \_camelCase.
- Serialized private fields: \_camelCase.
- Boolean fields: prefer \_isX, \_hasX style.
- Use `readonly` for non-serialized fields when the value is assigned once and should not be mutated after initialization.
- Do not use `readonly` on `[SerializeField]` fields or `[field: SerializeField]` auto-property backing fields; Unity must be able to serialize and assign inspector values.

Examples:

```csharp
[SerializeField] private Camera _mainCamera;
private bool _isParalysed;
private readonly Dictionary<int, EnemyBase> _enemiesById = new();
```

### Properties, Methods, Types, and Events

- Public properties and methods: PascalCase.
- Prefer auto-properties (e.g., `public Type Property { get; private set; }`) in standard C# classes instead of separate private fields + getter properties unless custom logic or specific serialization is required.
- Types (class, struct, enum): PascalCase.
- Events: OnX naming.

Examples:

```csharp
public int CurrentTurn { get; private set; }
public void StartEnemyTurn() { }
public event EventHandler OnEnemyTurnEnd;
```

### Markdown Documents

- AI-facing project documentation under .agents/context/ uses kebab-case filenames.
- Game-system documentation belongs under .agents/context/game-systems/ and uses kebab-case filenames ending in `-system.md`.
- Implementation plans under .agents/context/implementations/plans/ and summaries under .agents/context/implementations/summaries/ must use kebab-case filenames without any date prefix (e.g., `description.md`). The date of the plan/summary must only be specified inside the file content, not in the filename.
- Plans and summaries must be stored directly in the repository and never solely in external IDE/runtime directories.
- File and directory paths in agent documentation must be written relative to the project root (e.g., `Assets/Scripts/Player/` or `Assets/Scripts/Audio/AudioClipConfig.cs`).
- Paths must be written as plain text without markdown links and without backticks (e.g. `- Assets/Scripts/...`).
- Reserved operational filenames keep their established uppercase or conventional names, including `AGENTS.md`, `GEMINI.md`, `README.md`, and `SKILL.md`.
- When renaming documentation, update all relative links and references in .agents/context/, .agents/skills/, and root agent entry-point files.

## 3) Member Ordering in Classes

For MonoBehaviour and similar runtime classes, use this field ordering:

1. [Inject] private fields
2. [SerializeField] private fields
3. private non-serialized fields

Then keep methods in lifecycle and behavior order that reads clearly:

1. Unity lifecycle methods (Awake, OnEnable, Start, OnDisable)
2. Public API methods
3. Private helpers
4. Event handlers

## 4) Dependency and Architecture Rules

1. Use dependency injection through interfaces where DI is already established.
2. Do not reintroduce singleton access patterns where replaced by DI.
3. Keep dependencies explicit and narrow.
4. Reuse existing interfaces before adding new abstractions.

## 5) Unity and Inspector Conventions

1. Prefer `[SerializeField] private` fields when inspector data does not need public access. When public read access is needed, prefer one-line serialized auto-properties: `[field: SerializeField] public Type Value { get; private set; }`. Use a private serialized field plus a public property when the accessor needs logic or existing serialized field names must be preserved. Avoid public mutable fields unless Unity/editor integration or serialized compatibility requires them.
2. Add `[Tooltip("...")]` to non-obvious serialized fields, written clearly and concisely in English. Keep inspector headers `[Header("...")]` in English.
3. Preserve existing inspector workflows and serialized data compatibility.
4. **Fail-Fast & Explicit Dependency Rule**:
   - Do not add defensive fallback searches in `Awake()` (e.g., `if (_field == null) _field = GetComponentInChildren<T>();` or `transform.GetChild(0)`).
   - If a dependency is authored on the prefab or inspector, serialize it via `[SerializeField] private T _field;` and assign it in the editor.
   - If a dependency resides on the same GameObject, use `[RequireComponent(typeof(T))]` and assign it directly in `Awake()` (`_field = GetComponent<T>();`) without null checks.
   - Do not add silent null-guards in `Awake()` (e.g. `if (_visual != null) _visual.SetActive(false);`) or downstream silent null-swallowings in gameplay methods (`_animator?.Play()` or `if (_hitbox == null) return;`) for required mechanical dependencies. Missing mechanical references must fail fast loudly in the Unity console.
   - **Authorized Exceptions**: Null checks in `Awake()` or runtime methods are permitted strictly for:
     1. Cosmetic & sensory components (`VFXPlayer`, `IAudioClipPlayer`, particle systems, trail renderers) so missing visuals/sounds do not break mechanics.
     2. Genuinely optional visual polish elements (e.g. secondary visual indicators).
     3. DI dependencies with optional inspector overrides (e.g., `if (_inspectorOverride != null) _service = _inspectorOverride;`).
   - Unassigned configuration masks (such as `_groundLayerMask` in `CarController`) must validate in `Awake()` and throw an explicit `System.InvalidOperationException` if unassigned (`value == 0`).
5. Do not use `FormerlySerializedAs` when renaming serialized fields. Before changing serialized field names:
   - Always notify the user in advance that they will need to manually re-assign the serialized values in the Unity Editor.
   - Exception: If the serialized change is small and straightforward to update directly in a text-formatted prefab or asset file, the agent may apply this exception to edit the prefab/asset directly, but MUST still explicitly inform the user that this action was performed.

## 6) Events and Gameplay-Flow Safety

1. Subscribe/unsubscribe in matching lifecycle methods.
2. Avoid side effects in event callbacks that break event ordering or game state transitions.
3. For combat, wave, and spawner logic changes, validate spawn, damage, and death transitions explicitly.

## 7) Logging and Comments

1. Use Debug.Log for meaningful runtime diagnostics only.
2. Remove temporary noisy logs before finalizing unless they are intentional diagnostics.
3. Write comments, XML docstrings, and log messages in English to explain non-obvious intent and architectural rationale. Do not restate what code does.

## 8) Legacy Style Migration

If you touch code that does not follow standards:

1. Align changed lines with these rules.
2. Avoid large unrelated rewrites in the same change.
3. If a full cleanup is needed, do it as a dedicated follow-up change.

## 9) Current Known Legacy Exceptions

Some files currently contain non-standard constant naming (for example PascalCase const names).

- New code must use UPPER_SNAKE_CASE.
- During edits, migrate nearby touched constants when safe.

## 10) Warning

Treat warnings as error they must be handled during development

## 11) AI Agent Execution Checklist

Before finalizing a change:

1. Interfaces colocated correctly where applicable.
2. Const naming uses UPPER_SNAKE_CASE.
3. Field ordering follows Inject -> SerializeField -> private.
4. No singleton reintroduction.
5. Gameplay flow, wave transitions, and combat behavior remain intact.
6. LINQ is not used (System.Linq namespace/methods are banned).
7. Functions/methods use block syntax ({}) instead of expression-bodied syntax (=>).
8. Implementation plan (under .agents/context/implementations/plans/) and implementation summary (under .agents/context/implementations/summaries/) are created and tracked in the repository.
9. All newly written or touched code identifiers, comments, tooltips, logs, templates, and markdown files are strictly in English.
10. Fail-Fast null-check policy is followed (no fallback searches or silent null-swallowing for mechanical dependencies; see ADR-005).

## 12) Programming Guidelines and Constraints

### Banned Patterns
- **LINQ Ban**: Do not use LINQ (`System.Linq` namespace or methods like `.Any()`, `.Sum()`, `.Where()`, `.ToList()`, etc.) in gameplay, spawning, or general logic, to avoid extra allocations and keep control flow explicit.
- **Defensive Fallback Lookups & Silent Null-Guards**: Do not perform fallback `GetComponentInChildren` queries in `Awake()` or use `?.` / early returns to silently swallow missing required mechanical components, hitboxes, animators, or configs. See ADR-005.

### Syntax Rules
- **Method Block Syntax**: Do not use expression-bodied arrow syntax (`=>`) for methods/functions. Always use standard block bodies with curly braces `{}` and explicit `return` statements where applicable. (Expression-bodied auto-properties remain acceptable).

### Language Invariant
- All repository files, source code, comments, inspector tooltips, templates, and agent documentation must be authored in English.
- Multilingual communication is supported exclusively in direct user-facing chat, never in repository artifacts.
