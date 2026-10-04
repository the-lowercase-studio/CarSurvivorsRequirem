---
name: reduce-code-volume
description: "Use when: auditing and refactoring a provided Car Survivors scope to reduce code volume, minimize lines of code, remove redundancy, and simplify logic while maintaining readability, safety, and compatibility. Triggers: reduce code volume, reduce code size, code reduction audit, audit code reduction, minimize lines of code, simplify code logic, remove code redundancy."
---

# Reduce Code Volume

Use this skill to inspect a user-provided scope, find opportunities to safely reduce code volume and complexity, and implement refactorings that result in fewer lines of code while ensuring that code remains readable, robust, and compatible with Unity serialization and dependency injection.

## Required Sources

Always read these before editing:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/adr/ADR-004-designer-authored-data-and-prefabs.md
- .agents/context/adr/ADR-005-fail-fast-and-inspector-null-checks-policy.md

## Authority and Routing

An audit request produces candidates without editing code. A request to implement reductions authorizes behavior-preserving refactoring within the named scope. Use unity-refactor-suggestions for suggestions alone. Before non-trivial code edits, create the durable plan under .agents/context/implementations/plans/ and resolve consequential requirements with the user, reusing existing decisions and authorization. Record the outcome under .agents/context/implementations/summaries/.

## Core Guidelines

1. **Readability First**: Reduced code must remain clear and understandable. Avoid overly complex single-line expressions, obscure LINQ chains, or nested ternary operators that hinder debugging.
2. **Safety First**: Reductions must not introduce bugs, trigger compiler warnings/errors, or cause Unity editor crashes.
3. **Preserve Serialization**: Do not rename, remove, or modify `[SerializeField]` fields or public fields in Unity components/ScriptableObjects without explicit user agreement, as this will break inspector-configured values.
4. **Preserve DI Boundaries**: Ensure that dependency injection attributes (`[Inject]`) and lifetime scopes are fully preserved.

## Code Reduction Techniques

Inspect the target scope for the following patterns to reduce code volume:

### 1. Modern C# Syntax Features
- **Expression-bodied properties**: Use `=>` for simple read-only properties and indexers. (Note: per project standards, methods/functions MUST use standard `{}` block syntax with explicit `return`).
  ```csharp
  // Property (Allowed):
  public float BaseDamage => _baseDamage * _multiplier;
  ```
- **Optional dependency checks**: Simplify checks only for documented optional cosmetic/sensory dependencies or permitted inspector overrides under ADR-005. Preserve fail-fast calls for required services, controllers, animators, hitboxes, and configs. Do not introduce `??`, `??=`, or `?.` fallbacks that hide missing mechanical dependencies. For Unity Object references, retain Unity lifetime-aware checks rather than substituting C# null operators.
- **Pattern matching**: Use modern `is` checks and switch expressions when they preserve existing semantics. For Unity Object types, a type pattern does not replace the existing lifetime-aware `!= null` check. This example preserves an optional target check; it must not introduce a silent guard for a required mechanical dependency.
  ```csharp
  // Before
  var enemy = target as Enemy;
  if (enemy != null) { ... }
  // After
  if (target is Enemy enemy && enemy != null) { ... }
  ```
- **Tuple deconstruction & swap**: Use tuples for compact assignments or value swaps.
- **Auto-implemented properties**: Use for non-serialized state when no custom logic is required. Converting existing serialized fields to auto-properties changes serialized identity; handle it as a migration rather than boilerplate cleanup.

### 2. Eliminating Redundancy (DRY)
- **Extract helper methods**: Identify duplicate or highly similar block patterns and consolidate them.
- **Utilize existing extensions**: Check if utility or extension methods already exist (e.g., `TransformTweenExtensions.cs`) before writing custom DOTween/transform logic.
- **Consolidate conditional branches**: Combine conditions using logical operators (`&&`, `||`) or switch expressions.
- **Loop Consolidation**: Consolidate redundant loops and early exits using clean helper methods while strictly adhering to the project's LINQ ban (no `System.Linq` methods like `Any()`, `Where()`, etc.).

### 3. Cleaning Up Boilerplate & Dead Code
- Remove unused variables, imports (`using` statements), and private helper fields only after checking serialization, reflection, and external consumers.
- Remove redundant, noisy comments that merely repeat what the code does.
- Remove empty Unity lifecycle methods (e.g., empty `Start()`, `Update()`, `OnDestroy()`) as they carry a slight performance overhead and clutter classes.
- Use implicit typing (`var`) where the type is obvious from the right-hand side of the assignment.

## Serialization & Unity Safety

Unity relies heavily on serialized fields to link scenes, prefabs, and ScriptableObjects.
- Preserve serialized field identities, including public serialized fields and auto-property backing fields. Inspect asset/prefab bindings and consumers before any authorized rename, removal, type change, or property conversion. Do not use `FormerlySerializedAs`. Notify the user before an authorized rename that values must be reassigned in the editor; record compatibility checks and editor follow-ups.
- Do not edit `.meta`, `.prefab`, `.unity`, or `.asset` files directly unless explicitly asked and validated.
- The coding-standards exception for small text migrations does not independently authorize direct asset edits under AGENTS.md.
- Ensure that Unity API rules are followed (e.g., do not use `?.` on Unity `Object` references if it bypasses Unity's custom lifetime check, or handle it carefully as `obj != null ? obj.name : null` is safer than `obj?.name` for Unity objects).

## Audit & Implementation Workflow

1. **Inventory & Analyze**: Check the file sizes, lines of code, and structure within the target scope.
2. **Find Candidates**: Look for classes/methods with boilerplate, repetitive checks, or verbose loop constructs.
3. **Confirm Safety & Readability**: For each proposed reduction, ask yourself:
   - Will the code be harder for a human to read?
   - Will it break any Unity editor serialized values?
   - Does it change behavior, timing, or side effects?
4. **Draft & Apply**: In audit mode, report reviewable candidates. In authorized implementation mode, apply approved reductions incrementally after the plan/requirements gate.
5. **Verify**: Compile implemented C# changes and require zero introduced errors or warnings. Repair introduced diagnostics within the authorized scope; report unrelated baseline failures or unavailable checks without broadening work. For audit-only proposals, distinguish future validation from performed checks.
6. **Report**: Present the audit results detailing:
   - Files audited
   - Number of lines/complexity reduced
   - Specific techniques applied
   - Evidence for serialization/DI safety, remaining uncertainty, and durable summary path
