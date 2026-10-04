---
name: unity-cli
description: "Use when: interacting with Unity CLI from the terminal, or controlling a running/connected Unity Editor from the command line — creating or modifying GameObjects, editing scenes and assets, inspecting the hierarchy, running C# in a live Editor instead of hand-editing scene/asset files, installing or managing editors, managing modules/licenses/auth, building/testing projects, or configuring Unity MCP servers. Triggers: unity cli, unity command, unity status, drive unity editor, unity headless, unity terminal, unity-cli."
---


# Unity CLI

Use the CLI to inspect or perform authorized Unity Editor, project, build, test, or installation work. Command availability depends on the installed CLI and connected Editor; the skill catalog does not prove either is available.

## Project Authority and Storage

- Read AGENTS.md, .agents/README.md, and relevant canonical standards, ADRs, and system guidance before project mutations. Consult official sources through .agents/context/technology-documentation.md before relying on Unity/package behavior.
- Use existing task authority. A status request permits inspection; an installed CLI or open Editor does not authorize scene changes, asset creation, package/editor/skill installation, licensing, or configuration changes. Request missing authority only after preparing a concrete reviewable action.
- Preserve the durable plan/requirements gate for non-trivial code changes and create the durable summary. Serialized data, mechanics, scene setup, and editor workflow changes require the project-guide authority for that task.
- Direct .unity, .prefab, .asset, or .meta edits require an explicit user request and a safe text review under AGENTS.md. Connection failure is not authorization for direct edits. The coding-standards small-migration exception and SECURITY.md do not override this boundary.
- Run commands from the verified project root and target the confirmed project/Editor explicitly when needed. Inspect active scene and dirty Editor state before authorized mutations; preserve user work.
- Put agent-created scratch scripts, logs, test/JUnit reports, coverage, diagnostic exports, build/provenance artifacts, and other intermediate data under .agents/context/tmp/unity-cli/. Set explicit output paths and create the directory before running commands; do not accept defaults that write at the root. Unity-managed generated directories remain Editor-owned.

## Inspection and Live Editor Workflow

1. Check command availability with Get-Command unity on PowerShell (or command -v unity on Unix). If missing, report it; install only when the task authorizes installation.
2. Check the installed version/help, then inspect editor connectivity and supported commands:

```powershell
unity --version
unity status --format json
unity command --help
```

3. For authorized live changes, discover commands using unity command or unity list, confirm the target project and active scene, and use Editor commands rather than direct YAML edits. Inspect the requested command schema before constructing its arguments. Consult .agents/skills/unity-cli/references/integration-advanced.md for project targeting, eval, and headless workflows.
4. If disconnected, distinguish missing package, Safe Mode/compile errors, sandbox visibility, and a closed Editor. No instances is not sufficient evidence of a closed Editor. Use supported inspection and report uncertainty; do not silently substitute file edits, a separate editor, package installation, or an editor restart.
5. Safe Mode recovery requires separate repair/restart authority if not already in scope. Report unrelated baseline errors and preserve user work rather than widening a status task into repairs.
6. Execute only authorized actions, verify actual results, and report failed/unavailable checks as pending or failed. CLI connectivity does not establish gameplay correctness.

## Command References

Read only the reference needed for the selected task. Imported command/version claims require confirmation with installed help or current official documentation before use.

| Task | Reference |
| --- | --- |
| Installation, global flags, environment, help, exit codes, output parsing | .agents/skills/unity-cli/references/cli-usage.md |
| Connected Editor, Pipeline, eval, MCP, skill configuration, connection troubleshooting | .agents/skills/unity-cli/references/integration-advanced.md |
| Build, run, tests, reports, coverage, accelerator | .agents/skills/unity-cli/references/build-run-test.md |
| Editor versions, modules, installation | .agents/skills/unity-cli/references/editors-install.md |
| Projects, templates, authored asset inspection | .agents/skills/unity-cli/references/projects-templates.md |
| Authentication, licenses, Unity Cloud | .agents/skills/unity-cli/references/auth-license-cloud.md |
| Configuration, proxy, contexts, Hub | .agents/skills/unity-cli/references/config-hub.md |
| Diagnostics, logs, maintenance | .agents/skills/unity-cli/references/diagnostics-maintenance.md |
| Source control, worktrees, conflicts | .agents/skills/unity-cli/references/version-control.md |
| Collaboration commands (only with explicit communication authority) | .agents/skills/unity-cli/references/collaboration.md |
| Conditional bootstrap and CI recipes | .agents/skills/unity-cli/references/workflow-recipes.md |

For machine output, request JSON and inspect success and the process exit code. Some commands stream progress before a terminal result; consult the relevant reference rather than parsing arbitrary stdout as a single object. Report actual command/version evidence separately from untested recipes.

## Conditional Companion Skills

Use unity-pipeline, new-unity-project, unity-package-management, or other companion workflows only when they exist in the active catalog and fit the authorized task. Otherwise use the relevant bundled reference and official Unity documentation. Do not install companion skills, packages, or a new Editor merely because a reference names them.
