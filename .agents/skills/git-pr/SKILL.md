---
name: git-pr
description: "Use when: the user explicitly invokes git-pr to push the current CSR branch (with git-commit commits), open a GitHub PR to main, wait for CI checks, approve and squash-merge without deleting the branch, pull main, and create the next CSR branch. Never run autonomously."
disable-model-invocation: true
---

# Git PR (User-Invoked Pull Request Cycle)

Push an already-committed CSR feature branch, open a concise Pull Request to `main` via GitHub CLI (`gh`), wait for GitHub PR checks, approve and squash-merge without deleting the branch, pull `main`, and transition onto a newly created `CSR-<max+1>-<slug>` branch while preserving any uncommitted edits as unstaged.

## Required Sources

- AGENTS.md
- .agents/skills/git-commit/SKILL.md
- .github/workflows/ci.yml
- .agents/skills/git-pr/scripts/pr-helper.ps1

## Hard Authority Boundaries

- **User-Invoked Only**: Never run autonomously after implementing a feature or after `git-commit`. Execute ONLY when the user explicitly triggers `git-pr` (e.g. `/git-pr` or `$git-pr`).
- **Prior `git-commit` Commits Required**: Only run from an active feature branch matching `^(CSR-\d+)-[a-z0-9-]+$` (never `main`, `master`, or `development`) that has at least 1 commit ahead of `origin/main` adhering to `CSR-<number> - <summary>`. Never create commits inside `git-pr`.
- **No Pre-PR Local Validation**: Do NOT run .agents/scripts/verify-change.ps1, `dotnet build`, `architecture-review`, or local Unity Editor compilation/playmode checks before opening the PR. GitHub PR workflows (.github/workflows/ci.yml) run PR validation.
- **Never Delete Source Branches**: Never pass `--delete-branch` or `-d` to `gh pr merge`, and never run `git branch -d/-D` or `git push origin --delete` on the PR source branch.
- **Fail Closed on CI Checks**: If any PR check fails, cancels, or times out, stop immediately without approving, merging, pulling `main`, or switching branches. Report the exact failing checks and let the user decide the next action.
- **Self-Approval Pause Handling**: Always attempt default `gh pr review <pr> --approve` once checks pass. If GitHub API rejects self-approval on the user's own PR, pause and inform the user that GitHub blocks self-approval, provide the PR link, ask the user to handle manual approval on GitHub (or confirm continuing), and resume only after user confirmation.
- **Unstaged Carryover Transparency**: Any uncommitted working-tree changes (including previously staged edits or untracked non-ignored files) must arrive **unstaged** on the new `CSR-<N+1>` branch, and the agent must explicitly inform the user whether such files exist and list every carried-over file.

## Execution Workflow

### Step 1: Preflight Check

Run the helper preflight check (does not execute pre-PR build/architecture gates):

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/git-pr/scripts/pr-helper.ps1 -Action Preflight
```

Parse the JSON output:
- Confirm `Ok == true`, `GhPath`, `GhUser`, `RepoSlug`, `Branch`, `CsrPrefix`, `Commits`, `ExistingPr`, and `DirtyFiles`.
- If `Ok == false` (for example: `gh` not authenticated, active branch is `main`, `0` commits ahead of `origin/main`, or commits do not match `CSR-<number> - <summary>`), stop immediately (`ABORTED`), report the exact `Errors`, and advise committing with `git-commit` first if needed.
- Note any `DirtyFiles`: keep them untouched in the working tree during PR push, CI wait, and merge (they are NOT pushed).

### Step 2: Push Branch and Open (or Reuse) the Pull Request

1. Push the active branch commits to `origin`:
   ```powershell
   git push -u origin <Branch>
   ```
2. If `ExistingPr` is null, build an automatic, concise PR title and body strictly in English from `Commits`:
   - **Title**: follow `CSR-<number> - <short summary>` (for a single commit, reuse its `CSR-<number> - <summary>` subject directly; for multiple commits, synthesize `CSR-<number> - <short combined summary>` under ~80 characters).
   - **Body**: concise automatic summary (1–4 short bullets listing the key commits/changes, without inventing unverified validation claims).
   - Create the PR targeting `main`:
     ```powershell
     & "<GhPath>" pr create --repo "<RepoSlug>" --base main --head "<Branch>" --title "<Title>" --body "<Body>"
     ```
3. If `ExistingPr` already exists (e.g. retrying after a CI fix commit), reuse its PR number and URL.

### Step 3: Wait for PR Checks (Fail Closed)

1. Confirm checks attach (GitHub Actions workflows from .github/workflows/ci.yml such as `EditMode Tests` and `PlayMode Tests` can take a few seconds after `pr create` to register). If `& "<GhPath>" pr checks <pr-number> --repo "<RepoSlug>"` initially reports no checks yet, wait briefly (e.g. one short check or `--watch` once queued).
2. Watch checks to completion:
   ```powershell
   & "<GhPath>" pr checks <pr-number> --repo "<RepoSlug>" --watch --interval 15
   ```
   - If the command transitions to an async background task while waiting for CI, stop calling tools and wait for the completion notification instead of polling in a loop.
3. **If ANY check fails (`EXIT != 0` or any check is failed/cancelled/timed out)**:
   - **STOP IMMEDIATELY**. Do NOT approve, merge, pull `main`, or switch branches.
   - Fetch concise failure details (`& "<GhPath>" pr checks <pr-number> --repo "<RepoSlug>"` and `& "<GhPath>" run view <run-id> --repo "<RepoSlug>" --log-failed` if a workflow run ID is present).
   - Report the PR URL, exact failing check name(s), and concise failure reason to the user, and wait for user instructions.

### Step 4: Approve and Squash-Merge (Retain Branch)

Only after all PR checks pass:

1. Attempt default PR approval:
   ```powershell
   & "<GhPath>" pr review <pr-number> --repo "<RepoSlug>" --approve
   ```
2. Check the result:
   - **If `review --approve` succeeds**: proceed directly to Step 4.3.
   - **If `review --approve` fails due to GitHub self-approval restriction** (for example `Can not approve your own pull request` or HTTP 422 self-review error):
     - **STOP and ask the user**: inform the user that all PR checks passed, explain that GitHub API blocks self-approval for PRs authored by `<GhUser>`, provide the clickable PR URL, and ask the user to manually approve on GitHub (or confirm continuing) and tell the agent to continue.
     - Do NOT merge until the user responds to continue. When the user replies to continue, re-verify PR state (`& "<GhPath>" pr view <pr-number> --repo "<RepoSlug>" --json state,reviewDecision,statusCheckRollup`) to confirm checks remain green, then proceed to Step 4.3.
   - **If `review --approve` fails for any other reason**: fail closed, report the error, and stop.
3. Complete the PR using squash merge **without deleting the branch**:
   ```powershell
   & "<GhPath>" pr merge <pr-number> --repo "<RepoSlug>" --squash
   ```
   - Never pass `--delete-branch` or `-d`.
   - Confirm via `& "<GhPath>" pr view <pr-number> --repo "<RepoSlug>" --json state,mergedAt,mergeCommit` that `state == "MERGED"`.

### Step 5: Pull `main` and Ask User for Next Branch Name

Run the helper `PullMain` action:

```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/git-pr/scripts/pr-helper.ps1 -Action PullMain
```

What `PullMain` safely performs:
- If `DirtyFiles` exist (modified, staged, or untracked non-ignored files), stashes them temporarily via `git stash push --include-untracked` so `main` can switch and fast-forward cleanly.
- Checks out `main` and runs `git pull --ff-only origin main`.
- Computes `NextBranchInfo` by scanning all local `CSR-<N>` branches (`refs/heads/`) and `origin` remote `CSR-<N>` branches (`refs/remotes/origin/`, strictly ignoring legacy `upstream/CS-*` branches) and setting `NextNumber = max(LocalMaxNumber, RemoteMaxNumber) + 1`.

Then report to the user:
- Merged PR summary + updated `main` commit (`MainHeadShortHash`) + note that `<Branch>` was preserved locally and on `origin`.
- If `StashCreated == true`, mention that uncommitted changes are temporarily safeguarded in stash and will be restored unstaged onto the new branch.
- **Ask the user**: which branch they want to continue on (telling them they can provide just the short name/slug and the computed branch will be `CSR-<NextNumber>-<slug>`). Wait for the user's answer.

### Step 6: Create Next Branch and Restore Unstaged Carryover

Once the user provides the desired branch name/slug, run `CreateNextBranch` while explicitly passing the stash handoff from Step 5 (`-StashRef "<StashMessage>"` when `StashCreated == true`, or `-NoCarryover` when `StashCreated == false` so stale carryover stashes from earlier runs are never popped accidentally):

```powershell
# When Step 5 reported StashCreated == true:
powershell -ExecutionPolicy Bypass -File .agents/skills/git-pr/scripts/pr-helper.ps1 -Action CreateNextBranch -BranchName "<user-supplied-name>" -StashRef "<StashMessage>"

# When Step 5 reported StashCreated == false:
powershell -ExecutionPolicy Bypass -File .agents/skills/git-pr/scripts/pr-helper.ps1 -Action CreateNextBranch -BranchName "<user-supplied-name>" -NoCarryover
```

What `CreateNextBranch` performs:
- Normalizes the user-supplied name into lowercase `kebab-case` (stripping any accidental `CSR-<N>-` prefix so `max + 1` always governs) and creates `CSR-<NextNumber>-<slug>` from `main`.
- Switches to the new branch.
- Restores any stashed uncommitted files (`git stash pop`) followed by `git reset --quiet` so **all** carried-over changes (including any previously staged edits) land **unstaged** on the new branch.
- If a carryover merge conflict occurs (`CarryoverStatus == "CONFLICT_STASH_PRESERVED"`), leaves the stash entry intact and stops with exact conflict details so no work is lost.

Finally, **explicitly inform the user**:
- Active branch is now `<CreatedBranch>` (branched from updated `main`).
- **Unstaged Carryover Files**: clearly state whether any uncommitted files were carried over onto `<CreatedBranch>`, and list every file from `CarriedOverFiles` (path + git status) — or state `None (clean working tree)` if none existed.

## Output Format (Compact, Mobile-Readable)

Report status cleanly at checkpoints and upon final completion:
- **PR Status**: `OPENED` / `MERGED #<number> (<url>)` / `BLOCKED (CI FAILED)` / `AWAITING USER APPROVAL` / `ABORTED`
- **Source Branch**: `<Branch>` (explicitly noting branch retained locally and on `origin`)
- **CI Checks**: summary of `EditMode Tests` / `PlayMode Tests` (or exact failure details)
- **Active Branch**: `<CreatedBranch>` (or `main` while awaiting next branch name, showing computed `CSR-<NextNumber>`)
- **Carried-Over Unstaged Files**: explicit bullet list of every uncommitted file present on `<CreatedBranch>` (or `None`)
