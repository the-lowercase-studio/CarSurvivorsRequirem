---
name: preserve-coding-standards
description: "Use when: auditing and fixing a provided ProjectLizard scope for practices that compile or work but drift from .agents/context/project-coding-standards.md. Triggers: preserve coding standards, coding standards cleanup, style drift, naming/order cleanup, fix standards violations, align scope with ProjectLizard standards."
---

# Preserve Coding Standards

Use this skill to inspect a user-provided scope, find code that is not behaviorally wrong but is misaligned with ProjectLizard coding standards, and apply safe incremental fixes.

## Required Sources

Always read these before editing:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md

Use .agents/context/technology-documentation.md and official Unity or Reflex documentation only when a standards fix depends on framework behavior.

## Scope Rules

1. Work only inside the user-provided scope: files, folders, classes, or systems.
2. If the user gives no scope, infer the narrowest active or mentioned file scope when available; otherwise ask for a scope.
3. Exclude generated, cache, package, and Unity transient directories unless explicitly requested:
   - Library/
   - Temp/
   - Obj/
   - Logs/
   - Builds/
   - Packages/
   - UserSettings/
4. Never edit .prefab files directly.
5. Treat legacy style incrementally: fix touched or clearly local violations, not every unrelated issue nearby.

## Audit Workflow

1. Inventory candidate files with rg or rg --files.
2. Read the relevant code before editing; do not rely on search hits alone.
3. Compare candidates against project-coding-standards.md.
4. Classify each issue:
   - Safe fix now: naming/order/visibility cleanup with local references understood.
   - Needs broader reference update: interface/type/member rename or serialized field rename.
   - Needs user confirmation: any change that may alter gameplay behavior, inspector data, public API, turn flow, shield semantics, DI wiring, or prefab setup.
5. Apply only safe fixes unless the user explicitly approved broader changes.
6. Run a targeted validation command when available, or explain why validation could not be run.

## Search Checklist

Use targeted searches appropriate to the scope. Examples:

```bash
rg --files <scope> -g '*.cs'
rg "const .* [A-Z][A-Za-z0-9]* =" <scope> -g '*.cs'
rg "public (int|float|bool|string|GameObject|Transform|.*) [a-z_][A-Za-z0-9_]*;" <scope> -g '*.cs'
rg "private (?!readonly|const|static).* [a-z][A-Za-z0-9_]*;" <scope> -g '*.cs'
rg "event .* [A-Z][A-Za-z0-9]*(?!;)" <scope> -g '*.cs'
rg "FindObjectOfType|FindFirstObjectByType|FindAnyObjectByType|\\.Instance\\b|GetComponent<" <scope> -g '*.cs'
```

Treat search results as leads. Verify each hit against code context before changing it.

## Safe Fix Patterns

Prefer these fixes when local references can be updated confidently:

- Rename private non-serialized fields to `_camelCase`.
- Rename new or non-serialized const fields to `UPPER_SNAKE_CASE`.
- Reorder MonoBehaviour fields as `[Inject]`, then `[SerializeField]`, then other private fields.
- Change public mutable inspector fields to `[SerializeField] private` only when no external code relies on field access.
- Prefix newly introduced interfaces with `I` and colocate tightly owned interfaces above implementation.
- Keep namespaces aligned with `Assets.<Domain>` folder structure when adding or touching files.
- Remove temporary noisy `Debug.Log` calls that are not intentional diagnostics.
- Keep event names in `OnX` form.

## Serialized Data Safety

Be conservative with serialized fields because renames can break Unity inspector data.

- If renaming an existing `[SerializeField]` field, check whether serialized assets or prefabs may depend on the old name.
- If the rename is necessary and safe, add `UnityEngine.Serialization.FormerlySerializedAs` for the old field name.
- If the field appears widely serialized or prefab-bound, report the issue instead of renaming unless the user approves.
- Do not edit prefabs to repair serialization.

## Behavior Guardrails

Do not make standards cleanup alter gameplay semantics.

- Preserve DI through interfaces where established.
- Do not reintroduce singleton access.
- Preserve turn-event sequencing.
- Preserve shield-first damage behavior.
- Preserve card/effect execution order and target resolution.
- Keep inspector workflows intact for designers.

If a standards violation can only be fixed by changing behavior or architecture, stop and ask for confirmation with the smallest concrete option.

## Output

After applying fixes, report:

1. Scope audited.
2. Files changed.
3. Standards issues fixed.
4. Issues intentionally left for confirmation or wider refactor.
5. Validation performed or not performed.
