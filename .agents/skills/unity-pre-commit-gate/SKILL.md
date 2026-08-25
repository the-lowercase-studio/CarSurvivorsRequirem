---
name: unity-pre-commit-gate
description: "Use when: running a comprehensive pre-commit verification gate, compiling the Unity C# solution with zero warnings, validating DI bindings, auditing serialized data safety, and ensuring coding standards compliance before finalizing changes or committing. Triggers: pre-commit gate, check and commit, verify build, validate changes, compile check, pre-merge check."
---

# Unity Pre-Commit Gate Skill

Use this skill before finalizing changes, committing, or opening a PR to verify that the codebase compiles cleanly with zero warnings, satisfies Reflex DI bindings, preserves serialized inspector safety, and strictly complies with Car Survivors coding standards.

## Required Sources

Always verify changes against:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- Assets/Scripts/ReflexDI/

## Verification Gates (Mandatory)

All 6 gates must pass. Do not declare code ready if any gate fails.

### Gate 1: Compilation & Warnings
Run targeted project compilation:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Exit code must be 0.
- Zero compilation errors.
- Zero compiler warnings (treat warnings as errors during development).

### Gate 2: Serialized Data & Inspector Safety
- Serialized fields must be `_camelCase` and use `[SerializeField] private` (never public mutable fields).
- Check that existing serialized fields were not renamed without verifying asset/prefab impact.
- Do not edit `.prefab`, `.unity`, or `.asset` files directly unless explicitly requested by the user.

### Gate 3: Reflex DI & Injection Order
- Verify that every injected interface (`[Inject]`) has a corresponding binding in an installer under `Assets/Scripts/ReflexDI/`.
- Verify field ordering in runtime MonoBehaviours and classes:
  1. `[Inject]` fields
  2. `[SerializeField]` fields
  3. Other private/protected fields
- Verify no hidden fallback lookups (e.g. `FindAnyObjectByType` or singleton fallbacks) were added to mask missing DI wiring.

### Gate 4: Coding Standards & Architecture
- Constants use `UPPER_SNAKE_CASE` and belong in a `Constants/` subfolder under the owning domain.
- Events follow `OnX` naming.
- Interfaces are prefixed with `I` and colocated above implementations when tightly coupled.
- No memory allocations (`new`, LINQ, string concatenation) inside `Update()` or `FixedUpdate()` loops.
- High-frequency spawned objects (projectiles, damage numbers, VFX) use object pooling.

### Gate 5: Git & Asset Consistency
- Check `git status --short` to ensure no orphaned files or untracked changes exist in touched scopes.
- Protect user work in dirty worktrees; do not revert unrelated modifications.

### Gate 6: Implementation Lifecycle & Summary
- Verify that non-trivial changes, features, or refactors have a corresponding implementation summary under `.agents/context/implementations/summaries/[task-name].md`.
- Verify that plans and summaries are saved in the project repository and not in external directories (e.g. `brain/`, `AppData`, `/tmp`).

## Workflow

1. Scope the Diff
   - Check modified files with `git status` and `git diff`.
   - Identify touched domains and files.

2. Run Automated Compilation (Gate 1)
   - Execute `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
   - If build fails or produces warnings, fix them immediately and re-run.

3. Audit DI, Serialization & Standards (Gates 2, 3, 4)
   - Inspect diff for field ordering, naming conventions, and DI registrations.
   - Apply safe incremental fixes for any standards drift in touched files.

4. Re-Verify & Check Git State (Gate 5)
   - Ensure clean compilation post-fixes.
   - Confirm all touched files are accounted for.

5. Produce Pre-Commit Report
   - Output the verification status based on .agents/skills/unity-pre-commit-gate/templates/pre-commit-gate-checklist.md.

## Output

Produce a completed checklist summarizing gate results:

- .agents/skills/unity-pre-commit-gate/templates/pre-commit-gate-checklist.md
