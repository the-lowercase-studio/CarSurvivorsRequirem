---
name: pre-commit-evidence
description: "Use when: the user explicitly invokes pre-commit-evidence to capture visual Unity Game View or Scene View proof images in .user-docs/evidences/ for the changes intended for the next commit, after verifying whether visual evidence is actually possible. Never commits code."
---

# Pre-Commit Evidence (Visual Proof Generator)

Generate inspectable visual proof images in .user-docs/evidences/ demonstrating that the pending changes intended for the next commit (when the user later invokes git-commit) are visibly present and working in Unity. Designed for remote or mobile workflows where the user cannot directly watch the Unity Editor Game window before authorizing a commit.

## Required Sources

- AGENTS.md
- .agents/skills/unity-cli/SKILL.md
- .agents/skills/git-commit/SKILL.md

## Hard Authority Boundaries

- **User-Invoked Only**: Run only when the user explicitly requests or invokes `pre-commit-evidence`. Never generate files under .user-docs/ automatically during normal coding tasks.
- **Never Commit or Push**: Never run `git add`, `git commit`, `git push`, or open Pull Requests. Leaving `.user-docs/evidences/` untracked/gitignored and untouched in git history is mandatory.
- **Do Not Mutate Project Source or Authored Assets**: Do not permanently alter `.cs`, `.unity`, `.prefab`, `.asset`, or `.meta` files during evidence capture. Never save temporary Play Mode or scene changes back to disk.
- **Protect Unsaved Editor Work**: Before entering Play Mode or opening another scene, check `unity command scene list_open --json`. If any open scene has `isDirty == true` and capturing requires opening a different scene, stop and warn the user instead of discarding unsaved Editor work.
- **No Fake or Meaningless Screenshots**: If the pending changes cannot be proven visually in gameplay, UI, or Scene View, stop at Step 2 and report `NOT_PROVABLE` instead of saving irrelevant frames.

## Execution Workflow

### Step 1: Identify the Next-Commit Candidate Scope

Run `git status --short` and inspect `git diff` / `git diff --cached` / `git diff --stat`:
- Identify the exact files intended for the user's next commit (or the explicit scope the user specified).
- Exclude unrelated dirty worktree modifications, untracked scratch files, .agents/context/brainstorming-summaries/, unimplemented plans/specs under .agents/context/implementations/plans/, and existing artifacts in .user-docs/evidences/.

### Step 2: Mandatory Visual Provability Gate

Before launching Play Mode or capturing anything, inspect the scoped diff and evaluate whether visual gameplay or Editor view evidence can actually demonstrate the change.

Classify the scope strictly into one of three categories:

1. **`NOT_PROVABLE` (Stop Immediately — Do Not Capture)**
   Changes where screenshots from Game View or Scene View cannot prove correctness or addition:
   - Workflow, tooling, or documentation only (.agents/, .githooks/, .gitignore, `.md` specs/plans/summaries).
   - Audio-only adjustments with no visual indicator.
   - Pure internal refactoring, Reflex DI bindings, interface extraction, allocation/GC optimization, or backend math/state changes that intentionally keep visuals and layout identical.
   - Build settings, git configuration, or CI workflow changes.
   - Action: Stop here. State clearly why visual gameplay evidence cannot prove this change and advise proceeding with technical/logical validation via `git-commit`.

2. **`SCENE_VIEW_ONLY`**
   Changes whose primary observable delta is static Editor/scene/prefab geometry, lighting, material placement, or world layout without runtime gameplay behavior.
   - Action: Proceed to Step 3 and capture focused `capture scene_view` (and static `capture game_view` if applicable).

3. **`PLAYMODE_GAME_VIEW`**
   Changes with runtime UI, HUD, menus, input-driven UI focus/highlights, player/enemy visuals, weapons, projectiles, VFX, shaders, camera framing, damage numbers, or visible gameplay state transitions.
   - Action: Proceed to Step 3 and capture deterministic Play Mode `capture game_view` screenshots (`source=screen`, which preserves Screen Space - Overlay UI).

If only a subset of the next-commit scope is visually provable, explicitly state which files/aspects are provable and which are verified only by build/code review.

### Step 3: Unity Editor Pre-Flight Check

Verify that the running Editor is connected and running compiled code matching the current working tree:
1. Run targeted build check (`dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`) or check `.agents/scripts/verify-change.ps1` compilation status so stale assemblies are not mistaken for new behavior.
2. Check Unity CLI connectivity and state:
   ```powershell
   unity status --format json
   unity command script recompile_status --json
   unity command console status --json
   unity command scene list_open --json
   ```
3. Fail closed if Unity Editor is disconnected, compiling (`isCompiling == true`), has compile errors (`compilationFailed == true`), or cannot run the target scene cleanly. Never guess or reuse old screenshots.

### Step 4: Capture Focused Evidence Screenshots

Create a scoped folder under `.user-docs/evidences/`:
- Folder naming: `.user-docs/evidences/<CSR-number-or-scope-slug>/` (for example `.user-docs/evidences/CSR-51/`).
- Filename convention: kebab-case ordered steps, e.g. `01-main-menu-focus.png`, `02-gameplay-hud.png`.
- Store only `.png` evidence files (never scratch text/JSON logs inside `.user-docs/evidences/`; put any temporary helper diagnostics strictly under `.agents/context/tmp/`).

When capturing `PLAYMODE_GAME_VIEW`:
1. Clear console buffer (`unity command console clear --json`) so errors from the capture session are isolated.
2. If another scene must be inspected and all open scenes are clean (`isDirty == false`), note the originally active scene path and open the target scene via `unity command scene open --path "<scene-path>" --json`.
3. Start Play Mode:
   ```powershell
   unity command editor.playmode start --json
   ```
4. Wait until runtime is active (`unity command runtime status --json`) and advance through only the minimal steps required to reach the changed UI or gameplay state (using `unity command runtime simulate_key` / `unity command runtime simulate_pointer` when input-triggered state can be driven via keyboard/pointer, or short timing waits for spawn/animation frames).
   - Use `unity command editor.playmode pause --json` before capturing fast transient moments (such as projectiles, VFX, or damage numbers) if needed.
5. Capture Game View PNG directly to `.user-docs/evidences/`:
   ```powershell
   unity command capture game_view --save_path "<repo-root>/.user-docs/evidences/<scope>/<step>.png" --json
   ```
   (Default `source=screen` includes Screen Space - Overlay UI. Add `--max_resolution 1280` or explicit `--width`/`--height` if crisp mobile preview sizing is helpful. Use `capture scene_view` if spatial scene inspection adds useful proof alongside Game View.)
6. Always stop Play Mode cleanly after capturing:
   ```powershell
   unity command editor.playmode stop --json
   ```
7. Inspect `unity command console get --json` / `console status --json` to ensure no runtime `NullReferenceException` or `MissingReferenceException` fired during the evidence run, and restore the originally active scene if Step 4.2 switched scenes.

### Step 5: Visually Inspect Generated PNGs

Never claim an image proves a change without inspecting the generated file yourself:
- Read every generated PNG under `.user-docs/evidences/<scope>/` via `read_file`.
- Verify the frame is rendered properly (not black/loading/obstructed), shows the actual changed UI element, effect, or behavior, and matches the code diff from Step 1.
- If a feature only activates via physical gamepad hardware input that `runtime simulate_key` / `runtime simulate_pointer` cannot trigger in the current Input Action mapping, capture what is safely reachable (or report `PARTIAL` / `NOT_CAPTURED` honestly) and state the exact hardware limitation instead of claiming full runtime proof.

### Step 6: Deliver User Evidence Report (No Commit)

Reply cleanly in mobile-friendly markdown:
- **Evidence Verdict**: `CAPTURED (<count> image(s))` | `PARTIAL (<reason>)` | `NOT_PROVABLE (<reason>)`
- **Inspected Commit Candidate Scope**: brief bullet list of the pending files/features evaluated (noting unrelated dirty files were excluded).
- **Evidence Artifacts**: clickable `file:///` links to every PNG saved under `.user-docs/evidences/<scope>/`, with a one-line explanation of what each image demonstrates.
- **Runtime Console Status**: observed Unity console error/warning state during Play Mode capture (or `N/A` if not entered).
- **Next Step**: remind the user to review the images and invoke `git-commit` when ready to validate and create the commit.
