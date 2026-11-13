---
name: git-commit
description: "Use when: the user explicitly invokes git-commit to validate and commit scoped changes with full technical, architectural, logical, and system-docs sync verification. Never run autonomously."
---

# Git Commit (Validated Safe Commit Workflow)

Create clean, verified git commits strictly when explicitly invoked by the user. Designed to keep commits trustworthy even when reviewing from a mobile device without inspecting full diffs or the Unity Editor directly.

## Required Sources

- AGENTS.md
- .agents/context/project-coding-standards.md
- .agents/skills/unity-pre-commit-gate/SKILL.md
- .agents/skills/architecture-review/SKILL.md
- .agents/skills/sync-system-docs-from-implementations/SKILL.md

## Hard Authority Boundaries

- **User-Invoked Only**: Never commit autonomously. Only execute a commit when the user explicitly triggers this skill.
- **No Pull Requests**: Never create Pull Requests automatically.
- **No Unsolicited Push**: Never push to any remote unless the user explicitly asks to push in the same request.
- **No Hook Bypass**: Never pass `--no-verify` to `git commit`. Let .githooks/pre-commit and .githooks/commit-msg enforce their checks.
- **Protect Dirty Worktrees**: Never stage unrelated pre-existing edits, untracked work, local visual proof screenshots under .user-docs/evidences/, or tracked generated files (such as Assets/Data/AppStorage.Editor.json, CarSurvivorsRequirem.slnx, or scratch_scan_results.txt) unless explicitly instructed.
- **Protect Unimplemented Specs and Brainstorms**: Never include .agents/context/brainstorming-summaries/*.md or unimplemented/preliminary plans/specs in .agents/context/implementations/plans/*.md in a commit unless the user explicitly asks to commit those specific design documents (passing `-AllowDesignDocs` only when explicitly authorized).
- **Fail Closed**: If any applicable gate, logical review check, or scoped system-doc check fails, abort immediately without creating a commit.

## Branch and Commit Prefix Standard

- Feature branches follow `CSR-<number>-<kebab-case-slug>` (for example `CSR-51-pad-controller-support`).
- Every commit message must extract `CSR-<number>` from the active branch name and follow `CSR-<number> - <concise English summary>` (for example `CSR-51 - add controller navigation focus fix`).
- Direct commits on `main`, `master`, or `development` are blocked unless the user explicitly overrides this rule. If the active branch has no `CSR-<number>` prefix, stop and ask the user before proceeding.

## Execution Workflow

### Step 1: Scope the Target Changes

Run `git status --short` and inspect `git diff` / `git diff --cached`:
- Identify the exact files belonging to the current task.
- Keep pre-existing unrelated user modifications and untracked files isolated outside the commit scope.
- Exclude .agents/context/brainstorming-summaries/, unimplemented plans/specs under .agents/context/implementations/plans/, and local visual proof screenshots under .user-docs/evidences/ (except .gitkeep) from the commit scope unless explicitly instructed otherwise.
- Confirm any `.unity`, `.prefab`, `.asset`, or `.meta` changes in scope were explicitly authorized.

### Step 2: Run the Automated Verification Harness (Phases 1–6)

Execute .agents/scripts/verify-change.ps1 against the exact commit scope and proposed commit message:

```powershell
powershell -ExecutionPolicy Bypass -File .agents/scripts/verify-change.ps1 -Files "<comma-separated-paths>" -CommitMessage "CSR-<number> - <summary>" -StrictWarnings
```

Wait for all six automated harness gates to pass or report permitted `PENDING`/`N/A`:
1. **Compilation**: `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` build with 0 errors and 0 warnings.
2. **Standards & Architecture**: diff-aware .agents/scripts/check-architecture.ps1 and .agents/ documentation naming rules pass.
3. **Conditional Tests**: if tests exist under Assets/ or `unity command test list` reports registered tests for the touched area, execute them and require 100% pass; otherwise record `N/A`.
4. **Assets, .meta, Hygiene & Design-Doc Scope Guard**: all added/modified Assets/ files have valid `.meta` pairing, no orphaned `.meta` files exist, no forbidden/generated/scratch files are in scope, and no brainstorm or unimplemented plan `.md` files leaked into the commit scope.
5. **Live Unity Editor**: when connected via `unity` CLI, verify `script recompile_status` and `console status` (`compilationFailed == false`, `consoleErrors == 0`); otherwise mark `PENDING`.
6. **Branch & Prefix Guard**: active branch and commit message match the `CSR-<number>` standard.

If any new code triggers `-StrictWarnings` warnings (`ARCH004`, `ARCH006`, `ARCH008`), fix them within the authorized scope before committing (or confirm whether a legacy exception applies to untouched lines).

### Step 3: Perform Correctness and Logical Consistency Review (Phase 7)

Automated checks do not prove runtime logic. Inspect the actual diff of every file being committed and verify:
- **Project Consistency & Reflex DI**: new or modified `[Inject]` dependencies are properly bound with the right lifetime in the owning installer under Assets/Scripts/ReflexDI/; `[Inject]`, `[SerializeField]`, then private non-serialized field ordering is preserved; no hidden `FindAnyObjectByType` or singleton fallbacks exist.
- **Correctness & Logical Flow**: null/fail-fast invariants hold (no silent null-swallowing on required mechanical dependencies); event subscriptions in `Awake`/`OnEnable` have matching unsubscriptions in `OnDisable`/`OnDestroy`/`Dispose`; state transitions, pooling reuse, and turn/gameplay event ordering remain deterministic and leak-free.
- **Performance & Serialization**: no LINQ, no avoidable per-frame allocations in `Update`/`FixedUpdate`, no `FormerlySerializedAs`, and serialized field types/names remain compatible.

### Step 4: Sync System Documentation (Phase 8 — `/sync-system-docs-from-implementations`)

After Phase 7 passes, evaluate whether the verified commit adds or modifies a gameplay or architectural system documented under .agents/context/game-systems/:
- **When applicable** (the scoped changes introduce a new game system or alter architecture, data flow, contracts, invariants, or key files of an existing game system, and belong to a completed implementation):
  - Execute the scoped workflow in .agents/skills/sync-system-docs-from-implementations/SKILL.md **strictly limited to the current commit's completed plan/summary and affected system document(s)**:
    - Update existing .agents/context/game-systems/*-system.md file(s) or create a new `<name>-system.md` file if a distinct system was introduced.
    - Never use .agents/context/brainstorming-summaries/ or unimplemented plans/specs to update system docs.
    - Delete **only** the processed completed plan and summary belonging to this commit (never delete unimplemented plans/specs or unrelated summaries in the working tree; keep .agents/context/implementations/templates/ intact).
    - Add the updated/created `*-system.md` file(s) (and any git-tracked deletions of the processed plan/summary) to the commit scope, and re-run .agents/scripts/verify-change.ps1 on the updated scope.
- **When not applicable** (for example, tooling-only, `.agents/` workflow-only, or trivial changes with no game-system impact, or an explicit design-doc-only commit):
  - Record Phase 8 as `N/A (no game-system impact)` and leave .agents/context/game-systems/ and unrelated plans/summaries untouched.

### Step 5: Stage, Verify Index, and Commit (Phase 9)

Only after Steps 2, 3, and 4 pass:
1. Stage only the final scoped files (including paired `.meta` files and Phase 8 system-doc updates/removals):
   `git add -- <explicit-file-list>` (and `git rm -- <deleted-tracked-plan-or-summary>` if a tracked plan/summary was deleted in Phase 8).
2. Verify staged files match the exact intended scope:
   `git diff --cached --name-only`
   Abort and unstage (`git reset HEAD -- <unexpected-file>`) if any brainstorm file, unimplemented plan/spec, or generated/ignored file appears.
3. Create the commit (without `--no-verify`):
   `git commit -m "CSR-<number> - <concise summary>"`
4. Confirm clean commit state via `git log -1 --oneline` and `git status --short`.

## Output Format (Compact, Mobile-Readable)

Report the outcome concisely:
- **Verdict**: `COMMITTED <short-hash>` on `<branch>` (or `ABORTED — NO COMMIT` with exact failure reasons).
- **Commit Message**: `CSR-<number> - <summary>`
- **Phases / Gates**: one-line status per gate/phase (Phases 1–6 harness gates, Phase 7 logical review, Phase 8 `/sync-system-docs-from-implementations`).
- **Committed Files**: bullet list of files actually included (plus note that unrelated dirty files, brainstorms, and unimplemented plans remain preserved in the working tree).
- **Manual Editor Follow-ups**: explicit list of Play Mode / inspector checks still needed on PC (or `None`).
