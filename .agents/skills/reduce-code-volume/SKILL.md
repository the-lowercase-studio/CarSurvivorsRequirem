---
name: reduce-code-volume
description: "Use when: auditing and refactoring a provided ProjectLizard scope to reduce code volume, minimize lines of code, remove redundancy, and simplify logic while maintaining readability, safety, and compatibility. Triggers: reduce code volume, reduce code size, code reduction audit, audit code reduction, minimize lines of code, simplify code logic, remove code redundancy."
---

# Reduce Code Volume

Use this skill to inspect a user-provided scope, find opportunities to safely reduce code volume and complexity, and implement refactorings that result in fewer lines of code while ensuring that code remains readable, robust, and compatible with Unity serialization and dependency injection.

## Required Sources

Always read these before editing:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md

## Core Guidelines

1. **Readability First**: Reduced code must remain clear and understandable. Avoid overly complex single-line expressions, obscure LINQ chains, or nested ternary operators that hinder debugging.
2. **Safety First**: Reductions must not introduce bugs, trigger compiler warnings/errors, or cause Unity editor crashes.
3. **Preserve Serialization**: Do not rename, remove, or modify `[SerializeField]` fields or public fields in Unity components/ScriptableObjects without explicit user agreement, as this will break inspector-configured values.
4. **Preserve DI Boundaries**: Ensure that dependency injection attributes (`[Inject]`) and lifetime scopes are fully preserved.

## Code Reduction Techniques

Inspect the target scope for the following patterns to reduce code volume:

### 1. Modern C# Syntax Features
- **Expression-bodied members**: Use `=>` for simple properties, indexers, operators, and single-statement methods.
  ```csharp
  // Before
  public float GetDamage()
  {
      return _baseDamage * _multiplier;
  }
  // After
  public float GetDamage() => _baseDamage * _multiplier;
  ```
- **Null-coalescing and null-conditional operators**: Use `??`, `??=`, and `?.` to simplify null checks.
  ```csharp
  // Before
  if (_uiController != null)
  {
      _uiController.Show();
  }
  // After
  _uiController?.Show();
  ```
- **Pattern matching**: Use modern `is` checks and switch expressions.
  ```csharp
  // Before
  var enemy = target as Enemy;
  if (enemy != null) { ... }
  // After
  if (target is Enemy enemy) { ... }
  ```
- **Tuple deconstruction & swap**: Use tuples for compact assignments or value swaps.
- **Auto-implemented properties**: Avoid explicit backing fields unless custom logic is required in getters/setters.

### 2. Eliminating Redundancy (DRY)
- **Extract helper methods**: Identify duplicate or highly similar block patterns and consolidate them.
- **Utilize existing extensions**: Check if utility or extension methods already exist (e.g., `TransformTweenExtensions.cs`) before writing custom DOTween/transform logic.
- **Consolidate conditional branches**: Combine conditions using logical operators (`&&`, `||`) or switch expressions.
- **Standard Library / LINQ**: Replace verbose loops that search, filter, or map collections with concise LINQ expressions (e.g., `Any()`, `All()`, `FirstOrDefault()`, `Select()`).
  ```csharp
  // Before
  bool hasActive = false;
  foreach (var item in items)
  {
      if (item.IsActive)
      {
          hasActive = true;
          break;
      }
  }
  // After
  bool hasActive = items.Any(item => item.IsActive);
  ```

### 3. Cleaning Up Boilerplate & Dead Code
- Remove unused variables, imports (`using` statements), and private helper fields that are never read.
- Remove redundant, noisy comments that merely repeat what the code does.
- Remove empty Unity lifecycle methods (e.g., empty `Start()`, `Update()`, `OnDestroy()`) as they carry a slight performance overhead and clutter classes.
- Use implicit typing (`var`) where the type is obvious from the right-hand side of the assignment.

## Serialization & Unity Safety

Unity relies heavily on serialized fields to link scenes, prefabs, and ScriptableObjects.
- **Never rename a serialized field** (e.g., `_myPrefab`) to reduce code or clean up naming without checking if it is referenced in Unity. If renamed, use `[FormerlySerializedAs("oldName")]` to preserve asset values, and obtain explicit user agreement.
- Do not edit `.meta`, `.prefab`, `.unity`, or `.asset` files directly unless explicitly asked and validated.
- Ensure that Unity API rules are followed (e.g., do not use `?.` on Unity `Object` references if it bypasses Unity's custom lifetime check, or handle it carefully as `obj != null ? obj.name : null` is safer than `obj?.name` for Unity objects).

## Audit & Implementation Workflow

1. **Inventory & Analyze**: Check the file sizes, lines of code, and structure within the target scope.
2. **Find Candidates**: Look for classes/methods with boilerplate, repetitive checks, or verbose loop constructs.
3. **Confirm Safety & Readability**: For each proposed reduction, ask yourself:
   - Will the code be harder for a human to read?
   - Will it break any Unity editor serialized values?
   - Does it change behavior, timing, or side effects?
4. **Draft & Apply**: Apply the refactoring incrementally using precise replacing tools.
5. **Verify**: Build the project to confirm there are no compiler errors or warnings.
6. **Report**: Present the audit results detailing:
   - Files audited
   - Number of lines/complexity reduced
   - Specific techniques applied
   - Reassurance of serialization/DI safety
