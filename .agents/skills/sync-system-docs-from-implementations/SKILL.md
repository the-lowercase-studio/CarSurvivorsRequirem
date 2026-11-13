---
name: sync-system-docs-from-implementations
description: "Use when: explicitly invoked by the user (or executed as Phase 8 inside user-invoked git-commit) to update existing game-system documentation or create new system documents under .agents/context/game-systems/ from completed implementation plans and summaries, and then delete only those processed completed plan/summary files while preserving templates and unimplemented plans."
disable-model-invocation: true
---

# Sync System Docs from Implementations Skill

Use this skill to consolidate **completed** implementation plans and summaries under .agents/context/implementations/ into authoritative, up-to-date system documentation under .agents/context/game-systems/, and then remove only the processed completed plan and summary files while leaving templates and all unimplemented plans/specs untouched.

## Invocation Authority Guardrail

- This skill may be executed ONLY in two situations:
  1. Explicitly requested by the user (for example, naming `/sync-system-docs-from-implementations` or `sync-system-docs-from-implementations`), OR
  2. Executed as **Phase 8** inside a user-invoked `git-commit` run, strictly scoped to the completed implementation belonging to that commit.
- Agents must NEVER invoke or execute this skill autonomously at the end of a feature implementation, bug fix, refactor, brainstorming session, or spec-writing turn when neither the user nor `git-commit` requested it.
- Do not create a new implementation plan or implementation summary for the execution of this skill itself.

## Required Sources

Before updating or creating system documentation, read:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/skills/document-system/SKILL.md
- .agents/skills/document-system/templates/system-document-template.md
- .agents/skills/agent-docs-review/SKILL.md
- Target completed plans in .agents/context/implementations/plans/
- Target completed summaries in .agents/context/implementations/summaries/
- Affected system documents in .agents/context/game-systems/

## Scope and Safety Rules

1. **Completed Implementations Only (Hard Exclusion of Brainstorms and Unimplemented Specs)**:
   - A plan is eligible for system-doc sync and deletion ONLY when ALL of the following hold:
     - It has a matching implementation summary in .agents/context/implementations/summaries/ (or the user explicitly confirms a standalone completed plan/summary in a manual sync), AND
     - Its key code, asset, or architectural changes are verified as actually present and implemented in the repository (Assets/Scripts/, Assets/ScriptableObjects/, Assets/Prefabs/, Assets/Scenes/, or operational files).
   - **Never delete or consume unimplemented plans/specs**: If a plan or spec in .agents/context/implementations/plans/ has not been implemented yet (missing matching summary and/or missing implemented code changes in the repo), you are **strictly forbidden** from deleting it and **strictly forbidden** from writing its unimplemented/speculative design into .agents/context/game-systems/. Keep the file completely intact and report it as `SKIPPED (unimplemented — retained)`.
   - **Never consume or delete brainstorming files**: Files in .agents/context/brainstorming-summaries/ are exploratory brainstorms, not implemented system truth. Never use them to update .agents/context/game-systems/ and never delete or stage them during a sync.
   - **Scoped Mode inside `git-commit` Phase 8**: When invoked during `git-commit`, scope the sync **exclusively** to the completed plan/summary pair(s) and code belonging to the current commit. Never process, alter, or delete unrelated plans or summaries belonging to other tasks in the working tree. If the commit does not add or modify any gameplay or architectural system documented under .agents/context/game-systems/ (for example, tooling-only or `.agents/` workflow-only changes), exit Phase 8 cleanly as `N/A (no game-system impact)`.

2. **Source Code as Ground Truth**:
   - Implementation plans and summaries describe what was intended and changed, but actual source code in Assets/Scripts/ and authored assets remain the final ground truth.
   - If an implementation plan and its summary differ (for example, adjustments made during execution), prefer the summary verified against current C# source code.
   - Never copy raw changelog prose or step-by-step task checklists into .agents/context/game-systems/. Integrate the changes cleanly into the structured sections of each system document (Purpose, Reading Map, Architecture and Data Flow, Rules and Invariants, Extension Points, Integration Notes, Known Risks and Open Questions).

3. **Template Preservation and Safe Cleanup**:
   - Keep .agents/context/implementations/templates/ (containing plan-template.md and summary-template.md) completely untouched.
   - Delete ONLY the specific completed plan file(s) from .agents/context/implementations/plans/ and completed summary file(s) from .agents/context/implementations/summaries/ that were fully verified and synced into .agents/context/game-systems/.
   - Never delete .agents/context/implementations/plans/ or .agents/context/implementations/summaries/ directories while unimplemented plans or unrelated summaries remain.
   - Never delete files outside .agents/context/implementations/plans/ and .agents/context/implementations/summaries/.

4. **Documentation Formatting Standards**:
   - Author all system documentation strictly in English.
   - Write all file and directory paths relative to the project root in plain text without markdown links and without backticks (for example: - Assets/Scripts/PowerUps/Armor/ArmorService.cs).
   - Keep system document filenames kebab-case ending with -system.md under .agents/context/game-systems/.

## Workflow

1. **Inventory and Filter Plans and Summaries**:
   - List markdown files in .agents/context/implementations/plans/ and .agents/context/implementations/summaries/.
   - When invoked with an explicit scope (such as a specific feature or `git-commit` commit scope), filter strictly to the plan/summary pair(s) belonging to that scope.
   - Pair each plan (`<feature>-plan.md` or `<feature>-spec.md`) with its matching summary (`<feature>-summary.md` or `<feature>.md`).
   - Immediately mark and exclude any unpaired plan or any plan whose code changes are not implemented in the repository (`SKIPPED — unimplemented, retained intact`). Also exclude .agents/context/brainstorming-summaries/.

2. **Map Completed Changes to Game Systems**:
   - For each eligible completed implementation:
     - Read the plan and summary in full.
     - Identify all touched source files, ScriptableObjects, prefabs, scenes, and Reflex DI installers.
     - Map those domains to existing system documents under .agents/context/game-systems/ (for example, power-ups-system.md, health-system.md, vfx-system.md, dev-console-system.md, player-system.md, di-and-boot-flow-system.md, input-system.md).
     - Determine whether the implementation:
       - Modifies or extends one or more existing documented systems (update those existing `*-system.md` files in place), and/or
       - Introduces a distinct new gameplay or architectural system that warrants a new .agents/context/game-systems/<name>-system.md file (guided by .agents/skills/document-system/SKILL.md and .agents/skills/document-system/templates/system-document-template.md).

3. **Verify Against Current Codebase**:
   - Inspect the actual C# scripts in Assets/Scripts/, configs in Assets/ScriptableObjects/, and DI bindings in Assets/Scripts/ReflexDI/ referenced by the completed implementation.
   - Verify class names, interfaces, method signatures, constants, serialized fields, lifecycle ordering, and invariants before writing them into system documentation.

4. **Update or Create System Documentation**:
   - For each affected system document under .agents/context/game-systems/:
     - Update Purpose (responsibilities and non-responsibilities) if scope expanded.
     - Update Reading Map with newly added or modified scripts, configs, prefabs, shaders, related runtime integrations, and cross-referenced system docs.
     - Update Architecture and Data Flow (core components, key interfaces, centralized constants, and step-by-step runtime flows).
     - Update Rules and Invariants (enum serialization stability, fail-fast validation, event ordering, pooling/tween cleanup, and performance constraints).
     - Update Extension Points and Testing Implications (including new dev console commands or verification checks).
     - Update Integration Notes (upstream dependencies, downstream consumers, and coupling risks) and Known Risks and Open Questions (removing resolved limitations and adding new known limitations or follow-ups).
   - Apply the quality criteria from .agents/skills/agent-docs-review/SKILL.md so the resulting documentation remains detailed, accurate, concise, and immediately useful for future agents.

5. **Verify Documentation Completeness Before Deletion**:
   - Confirm that every architectural contract, component, constant, runtime flow, and follow-up from the processed completed plan and summary has been captured in the corresponding .agents/context/game-systems/*-system.md files.
   - Check that all file paths in the updated/created system documents exist in the repository and follow the plain-text relative path rule.

6. **Delete Only Processed Completed Plans and Summaries**:
   - Delete only the processed completed plan file(s) from .agents/context/implementations/plans/ and processed summary file(s) from .agents/context/implementations/summaries/.
   - Leave all unimplemented plans/specs, all brainstorming summaries, and .agents/context/implementations/templates/ completely untouched.

7. **Report Results**:
   - List the completed plans and summaries that were processed and deleted.
   - List all system documents under .agents/context/game-systems/ that were updated or newly created, with a concise summary of the incorporated knowledge.
   - Explicitly list any unimplemented plans that were skipped and retained intact.
