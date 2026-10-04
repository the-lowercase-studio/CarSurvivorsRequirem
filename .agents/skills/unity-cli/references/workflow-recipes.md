# Unity CLI Workflow Recipes

## Contents

- New project bootstrap and source control
- Editor installation and opening
- CI license/build workflow
- Headless build
- Tests and reports

These imported examples are conditional recipes, not task authority. Read the project boundaries in .agents/skills/unity-cli/SKILL.md first. Use only steps authorized by the request. Scene changes, project creation, installs, license actions, source-control publishing, and editor restarts do not follow from a status request. Verify current command help and official sources before using version-dependent recipes.

For this repository, run from the project root and put every scratch log/report/build under .agents/context/tmp/unity-cli/. Explicitly set output, JUnit, coverage, and provenance paths where applicable; never accept a root-file default. Editor-managed Library/Temp data remains managed by Unity. Create the scratch output directory before executing authorized commands. Example versions and platforms must be replaced with the project's verified requirements.

### Bootstrap a new project from scratch

> If the companion new-unity-project skill is available, for a **guided** end-to-end experience — concept questions, installing the Editor in the
> background while you plan, package selection, and monetization handoff — use the
> **`new-unity-project`** skill. This section is the raw CLI recipe that skill builds on; use it
> directly when you just want the commands.

Take an idea to a running, version-controlled project using only the CLI. Decide the **target
platforms first** — they determine which Editor modules you install in step 2. You can add
modules later (`unity install-modules`), but a project can't build for a platform until that
platform's module is installed, so it's simplest to decide up front.

```bash
# 1. Confirm the CLI works and you're signed in and licensed (see references/auth-license-cloud.md).
unity --version
unity auth status --format json      # if signed out:      unity auth login
unity license status --format json   # if none active:      unity license activate

# 2. Pick and install an Editor with the modules your target platforms need.
#    Default to the latest LTS (most stable, ~2 years of patches). Reach for a Tech-stream
#    release (--stream tech) only for a feature not yet in LTS; treat --stream beta/alpha as
#    evaluation-only, never for a project you intend to ship. A deadline argues for LTS.
#    (lts / latest aliases work almost everywhere a version is accepted — `templates` is the
#     exception; see step 3.)
unity releases --stream lts --limit 5 --format json
unity install lts --module android --module ios --yes --accept-eula   # add --module webgl, etc.
unity editors --installed --format json                               # confirm it landed

# 3. List the real template ids this Editor offers — don't guess them — and pick by RENDER
#    PIPELINE, not just by 2D/3D. Default to the URP templates:
#      3D → com.unity.template.urp-blank      ("Universal 3D")
#      2D → com.unity.template.universal-2d   ("Universal 2D": URP + the 2D packages)
#    com.unity.template.3d and com.unity.template.2d are the Built-in Render Pipeline templates
#    (displayName "… (Built-In Render Pipeline)"): deprecated from Unity 6.5, gone in 6.7. Use
#    them only when the user explicitly asks for Built-in. Confirm the pick with the JSON
#    `renderPipeline` field — it is blank for universal-2d on current releases, so match that
#    one by id.
#    NOTE: `templates` does NOT resolve the lts / latest aliases — unlike `install` and
#    `projects create`, it passes --editor straight through and rejects anything that is not a
#    concrete 6000.x.y. Use the version you just installed (read it from `editors --installed`).
unity templates list --editor <6000.x.y> --type core --format json

# 4. Create the project. The first positional arg is the NAME; --path sets the parent directory.
#    All options supplied, so it won't prompt; add --non-interactive in CI.
unity projects create "MyGame" --path ~/UnityProjects \
  --editor-version lts --template com.unity.template.urp-blank
```

**Source control — let the user choose.** The CLI publishes the new project to a fresh remote in
one step for any provider. **Always pass tokens on stdin** (`--git-token-stdin`) so secrets never
land in shell history or the process list. Pick based on the project — don't default to one:

- **Git — GitHub / GitLab** (`--vcs github` / `--vcs gitlab`). Ubiquitous. For asset-heavy games
  add **Git LFS** (`--git-lfs`) so large binaries don't bloat history.
- **Unity Version Control — UVCS** (`--vcs uvcs`). Unity's own VCS, built for large binary game
  assets: it handles them natively (**no LFS needed**) and supports file locking — often the
  better fit for art-heavy projects or larger teams. Auth uses your Unity sign-in; `--vcs-region`
  selects the region.

```bash
# Git (GitHub) — drop --git-lfs if the game isn't asset-heavy. Add --no-initial-commit if you
# want to add packages/assets BEFORE the first commit (see the new-unity-project flow).
unity projects create "MyGame" --path ~/UnityProjects \
  --editor-version lts --template com.unity.template.urp-blank \
  --vcs github --git-namespace my-org --git-repo my-game \
  --git-visibility private --git-default-branch main --git-token-stdin --git-lfs

# Unity Version Control (UVCS) — handles binaries natively, so no LFS:
unity projects create "MyGame" --path ~/UnityProjects \
  --editor-version lts --template com.unity.template.urp-blank \
  --vcs uvcs --git-namespace my-org --git-repo my-game --vcs-region <region>
```

Feed the token to `--git-token-stdin` from a secret store, never a literal — e.g.
`… --git-token-stdin <<<"$GIT_TOKEN"` where `$GIT_TOKEN` comes from your CI/secret manager
(UVCS uses your Unity sign-in, so no token is needed).

**Working with a UVCS workspace day to day: a few wrapped reads, everything else straight through
to `cm`.** The split is deliberate and worth teaching, because guessing wrong wastes a user's time:

- **`unity vcs uvcs <verb>`** wraps the reads that **join `cm`'s data to your project** —
  `locks` (who holds a lock, *and which locks cover files you have already changed*),
  `changesets`, and `review`. Those joins are the thing `cm` cannot do for you, and they come in a
  stable envelope, so prefer them whenever something *parses* the output.
- **`unity uvcs <args>`** forwards the whole command line to `cm` verbatim, `--help` and
  `--format` included. That is the supported route, not a workaround: `cm` owns and versions this
  vocabulary, so wrapping it would pin a paraphrase that goes stale. Reach for it for **partial
  checkout**, **shelves**, and **taking or releasing a lock**, and when a human reads the output.

```bash
unity vcs uvcs locks                       # who holds what, and what collides with your changes
unity uvcs lock list                       # the raw listing, cm's own flags and output
unity uvcs partial update /Assets/Levels   # cm's own vocabulary, unchanged
unity uvcs shelve -c "wip: lighting pass"
```

Every verb, flag and trap: [version-control.md](version-control.md).

`unity cm <args>` is the same passthrough under cm's own name. Both need the `cm` client; install
it with `unity plugin install plastic` only if installing the dependency is authorized; otherwise report that it is missing.

**Beyond setup, the `vcs` group covers the whole day-2 loop** — `status`, `sync`, `switch`,
`merge-setup`, `conflicts` / `explain` / `resolve`, `diff`, `blame`, `summarize`, `affected`,
`hooks`, `doctor`, `providers` — and the Unity semantics are the reason to reach for it over raw
`git`. Full reference, with the flags and the traps:
[version-control.md](version-control.md).

**Git tokens belong to the user's credential manager, not the CLI.** When no token flag or env var
is given, the CLI asks `git credential fill` and uses whatever the configured helper returns; it
stores nothing it is passed or told. Don't suggest the CLI can save a Git token, and don't reach for
a token flag when the user already has a working credential helper. If they want a different token
per organization, that is `git config --global credential.useHttpPath true` plus a multi-account
helper such as [Git Credential Manager](https://github.com/git-ecosystem/git-credential-manager).
The CLI passes the full repo URL so the helper can discriminate, but it never installs or
reconfigures a helper. `UNITY_GITHUB_TOKEN` / `UNITY_GITLAB_TOKEN` are one token per provider, so a
CI job spanning several orgs should pass `--git-token-stdin` per invocation instead. See
[references/projects-templates.md](projects-templates.md) for the full
source-control flag set. For a purely local Git repository instead, initialize git with a
Unity-appropriate ignore so the multi-GB `Library/` and other generated folders are never committed:

```bash
cd ~/UnityProjects/MyGame
git init -b main
# Download (do not pipe to a shell) a maintained Unity .gitignore:
curl -fsSL https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore -o .gitignore

# Asset-heavy game? Keep large binaries out of git history with Git LFS:
git lfs install
git lfs track "*.psd" "*.fbx" "*.wav" "*.mp3" "*.png"   # adjust to your asset types
git add .gitattributes

git add -A
git status                             # sanity-check: Library/ Temp/ obj/ Build/ must NOT be staged
git commit -m "Initial Unity project: MyGame"
git ls-files | rg -c '^Library/'     # must print 0
```

**Conditional companion routes.** Use companion skills only when present in the active catalog and relevant to the authorized task. If absent, use the command references and official Unity documentation; do not install skills or packages merely to satisfy this recipe.

**What the CLI does and doesn't cover.** The CLI handles editor, project, and source control.
It does **not** manage UPM (Unity Package Manager) packages — to add packages beyond the
template headlessly, use the **`unity-package-management`** skill (C# PackageManager Client
API). For monetization/backend, hand off to the dedicated skills: `implement-in-app-purchases`
(IAP), `levelplay-unity-integration` (ads), or `build-live-game` (accounts, cloud save,
economy, remote config, leaderboards). Open the project to start working:
`unity open ~/UnityProjects/MyGame`.

### Find and install a missing editor

```bash
# 1. Check what's installed
unity editors --installed --format json

# 2. Browse available LTS versions
unity releases --lts --limit 5 --format json

# 3. Install
unity install 6000.0.47f1 --yes --accept-eula
```

### Open a project with the correct editor

```bash
# 1. Check the project's required editor version
unity projects info /path/to/MyProject --format json
# Look at "editorVersion" in the result

# 2. Confirm that editor is installed
unity editors --installed --format json

# 3. Open (warns if the editor version is missing)
unity open /path/to/MyProject
```

### CI: activate a license, then build

```bash
# 1. Sign in non-interactively with a service account
unity auth login --client-id "$UNITY_SERVICE_ACCOUNT_ID" --secret-from-stdin <<<"$UNITY_SERVICE_ACCOUNT_SECRET"

# 2. Activate the entitlement license (or use --serial / --floating)
unity license activate

# 3. Build
unity build /path/to/MyProject \
  --editor-version 6000.0.47f1 \
  --target StandaloneLinux64 \
  --execute-method Builder.PerformBuild \
  --allow-install
echo "Exit code: $?"

# 4. Return the seat when done (floating/assigned)
unity license return --yes
```

### CI: headless build

Prefer the dedicated `unity build` command (handles batch mode, logging, and CI flags):

```bash
unity build /path/to/MyProject \
  --editor-version 6000.0.47f1 \
  --target StandaloneLinux64 \
  --execute-method Builder.PerformBuild \
  --allow-install
echo "Exit code: $?"
```

Or use `unity run` (batch mode is automatic — never pass `-batchmode`/`-quit`):

```bash
unity run /path/to/MyProject \
  --editor-version 6000.0.47f1 \
  --allow-install \
  -- -executeMethod Builder.PerformBuild -logFile .agents/context/tmp/unity-cli/build.log
echo "Exit code: $?"
```

### CI: run tests and publish results

```bash
unity test /path/to/MyProject \
  --editor-version 6000.0.47f1 \
  --mode EditMode \
  --report-format junit \
  --output .agents/context/tmp/unity-cli/test-results.xml \
  --allow-install \
  --timeout 600
case $? in
  0) echo "All tests passed" ;;
  8) echo "Tests failed — report to developers, do not retry" ;;
  *) echo "Run did not complete — infrastructure failure, safe to retry" ;;
esac
```

Exit `8` means the run finished and reported failing tests; any other non-zero code means it never produced a verdict. Under `--format json` the same split is `errors[0].code`: `TESTS_FAILED` versus `TEST_RUN_ERROR` / `TEST_TIMED_OUT`.

`--report-format junit` makes `--output` a JUnit-schema report, which GitHub Actions and GitLab ingest as native test results with no converter step. It is written even when tests fail. Drop the flag for the NUnit3 default, or use `--report-format nunit,junit` to get both from one run. Add `--coverage` to collect coverage via the Unity Code Coverage package — it warns and carries on if the project doesn't have the package. See [build-run-test.md](build-run-test.md).
