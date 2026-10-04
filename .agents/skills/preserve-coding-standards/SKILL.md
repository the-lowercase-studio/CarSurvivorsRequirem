---
name: preserve-coding-standards
description: "Use when: auditing or applying authorized fixes within a provided Car Survivors scope for drift from .agents/context/project-coding-standards.md. Triggers: preserve coding standards, coding standards cleanup, style drift, naming/order cleanup, fix standards violations, align scope with Car Survivors standards."
---

# Preserve Coding Standards

Inspect the named scope against the canonical standards. An audit-only request produces findings; a cleanup/fix request permits compatible incremental corrections within that scope.

## Required Sources

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/adr/ADR-004-designer-authored-data-and-prefabs.md
- .agents/context/adr/ADR-005-fail-fast-and-inspector-null-checks-policy.md

Consult official sources through .agents/context/technology-documentation.md before relying on Unity or Reflex behavior.

## Scope and Lifecycle

- Use the named files, classes, folders, or systems. Infer only the narrowest clear active scope; clarify consequential ambiguity while inspecting independent evidence.
- Exclude generated directories and package/vendor code. Preserve unrelated user changes and avoid broad legacy rewrites.
- Before non-trivial code changes, create or update the durable implementation plan and resolve requirements under AGENTS.md. Reuse existing authorization and approved decisions. Write the durable implementation summary upon completion.
- Do not directly edit .prefab, .unity, .asset, or .meta files without an explicit user request and a safe text review. The coding-standards migration exception alone does not supply this authority.

## Checklist

- Field order: injected fields, serialized fields, other private fields.
- Private and serialized private fields: _camelCase. Constants: UPPER_SNAKE_CASE in the owning domain's Constants folder.
- Public members: PascalCase. Events: OnX. Narrow owned interfaces colocated above their implementation.
- Encapsulation: prefer private serialized fields; use serialized auto-properties for public read access where compatible. Preserve canonical Unity/editor and serialized compatibility exceptions.
- No LINQ, expression-bodied methods, singleton shortcuts, or silent guards/fallback lookups for required mechanical dependencies.
- Preserve intentional diagnostics, event order, DI ownership, and designer configuration.

## Fix Classification

For every candidate inspect serialization, references, reflection/string-based access, event consumers, and initialization effects before classifying it.

- **Compatible within authorized cleanup:** reorder declarations only when initializer behavior is unchanged; rename non-serialized private fields only after establishing private ownership and updating all references; remove unused imports or comments with no behavioral role.
- **Requires complete consumer analysis and existing implementation authority:** relocate constants or encapsulate fields only after accounting for all usages and preserving public/serialized contracts. If authority or compatibility is uncertain, propose the change rather than applying it.
- **Public or serialized contract change:** OnX event renames, visibility changes affecting consumers, serialized field renames, type changes, or conversions to auto-properties are not automatically safe fixes. Obtain missing authority and define the affected consumer updates/migration before editing. DI lifetime changes and gameplay changes require explicit design authority.

Preserve serialized identities wherever possible. Do not use FormerlySerializedAs. Notify the user before an authorized serialized rename that editor reassignment is required; check affected assets and record follow-ups. Do not label a private field safe merely because it is private: it may be serialized.

## Workflow and Verification

1. Inventory with rg --files, read the scope and its consumers, and record user changes and available baseline diagnostics.
2. Classify candidates and either report them in audit mode or apply compatible authorized fixes after the lifecycle gate.
3. Compile implemented C# changes:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

4. Require exit code 0 with zero errors and warnings. In authorized fix mode, repair diagnostics introduced by these edits within the owned scope and re-run the check. Do not repair unrelated baseline failures or expand scope. Stop an unproductive correction loop, record evidence and the blocker, and continue independent work.
5. Inspect the diff for serialized identity, consumers, and behavioral compatibility. Report unavailable checks as pending; suggestions list future commands separately from executed checks.

## Output

Report audited scope, changed files, violations fixed or proposed, deliberately preserved/deferred items, baseline versus introduced diagnostics, actual compilation result, and durable artifacts. Do not claim a clean gate without evidence.
