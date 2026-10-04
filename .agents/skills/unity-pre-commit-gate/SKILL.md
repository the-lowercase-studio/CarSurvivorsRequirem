---
name: unity-pre-commit-gate
description: "Use when: the user requests a comprehensive Car Survivors pre-commit or pre-merge verification gate covering compilation, DI, serialized data, coding standards, git consistency, and lifecycle documentation. Triggers: pre-commit gate, check and commit, full validation gate, pre-merge check. A narrow compile/build check uses targeted compilation without a whole-codebase audit."
---

# Unity Pre-Commit Gate

Verify the changed scope with six gates. Verification does not itself authorize repairs, commits, or unrelated cleanup.

## Required Sources

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- Relevant ADRs, system docs, and installers under Assets/Scripts/ReflexDI/.

Consult official sources through .agents/context/technology-documentation.md before relying on framework behavior.

## Scope and Authority

- A requested full pre-commit gate requires all six gates. A single compile request runs only the targeted compile unless the user requests more.
- Inspect git status and diff, including authorized untracked additions. Preserve unrelated work; a dirty worktree is not itself a failure.
- Verification-only mode reports findings without edits. Existing explicit repair authority permits scoped compatible fixes after the durable plan/requirements gate for non-trivial code edits. Apply preserve-coding-standards classifications rather than assuming every naming/visibility fix is safe.
- Fix introduced diagnostics within authorized scope. Report unrelated baseline diagnostics, missing tools/generated projects, and blocked checks. Do not broaden repairs or run an unproductive fix loop.
- Use PASS, FAIL, PENDING, or N/A with evidence for every gate. N/A requires a scope-based reason; unavailable checks are PENDING. Overall PASS requires all applicable checks to pass.

## Six Gates

### 1. Compilation and Warnings

For C# changes, execute:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

Require exit code 0, zero errors, and zero warnings. Report actual diagnostics and distinguish baseline from introduced failures. For documentation-only changes, record compilation as N/A with a reason.

### 2. Serialized Data and Inspector Safety

Validate canonical permitted patterns: private serialized fields, serialized auto-properties when public read access is appropriate, and justified Unity/editor or serialized compatibility exceptions. Do not impose a second never-public policy.

Check serialized identities, field types, property conversions, and prefab/asset impact. Do not use FormerlySerializedAs; advance notification and reassignment/migration checks are required for authorized renames. Direct .prefab, .unity, .asset, or .meta edits require the explicit user request in AGENTS.md; the small text-migration exception alone is insufficient authority.

### 3. Reflex DI and Field Order

Trace touched injected dependencies to the appropriate existing installer binding and lifetime. Check injected, serialized, then other private field order. Reject hidden global lookups or singleton fallbacks. Source review does not prove live injection; record required editor checks separately.

### 4. Coding Standards and Architecture

Check the changed scope against canonical naming, constants placement, interface colocation, no LINQ, block-bodied methods, fail-fast dependencies, lifecycle/event order, and pooling requirements. Inspect hot-path allocation risks without presenting source inspection as profiler measurement.

### 5. Git and Asset Consistency

Account for touched files and expected Unity metadata where applicable. Inspect orphaned references and accidental generated additions. Preserve pre-existing user edits and untracked files; do not demand a globally clean worktree or stage unrelated work.

### 6. Implementation Lifecycle

For non-trivial changes verify the durable plan under .agents/context/implementations/plans/, resolved consequential requirements, and summary under .agents/context/implementations/summaries/. Temporary state and external IDE artifacts do not replace them.

## Workflow and Output

Scope the diff, run applicable checks, inspect DI/serialization/standards, and report results using .agents/skills/unity-pre-commit-gate/templates/pre-commit-gate-checklist.md. If repairs are authorized, apply only scoped corrections and repeat checks affected by those edits. A full gate with failures or pending checks is not passed. Report editor/manual checks and remaining blockers explicitly; commit only when the request authorizes it.
