---
name: sync-system-docs-from-implementations
description: "Use when: explicitly invoked by the user to update existing game-system documentation or create new system documents under .agents/context/game-systems/ from completed implementation plans and summaries, and then delete those processed plans and summaries while preserving templates. User-only skill; never invoke autonomously."
disable-model-invocation: true
---

# Sync System Docs from Implementations Skill

Use this skill to consolidate completed implementation plans and summaries under .agents/context/implementations/ into authoritative, up-to-date system documentation under .agents/context/game-systems/, and then remove the processed plan and summary files so only the templates remain.

## User-Only Invocation Guardrail

- This skill may be invoked ONLY by explicit user request (for example, when the user directly triggers or names sync-system-docs-from-implementations or explicitly asks to sync system documentation from completed plans and summaries and delete them).
- Agents must NEVER invoke or execute this skill autonomously at the end of a feature implementation, bug fix, refactor, or documentation pass.
- Do not create a new implementation plan or implementation summary for the execution of this skill itself; its purpose is to eliminate implementation plan and summary accumulation after promoting their knowledge into system documentation.

## Required Sources

Before updating or creating system documentation, read:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/skills/document-system/SKILL.md
- .agents/skills/document-system/templates/system-document-template.md
- .agents/skills/agent-docs-review/SKILL.md
- Target plans in .agents/context/implementations/plans/
- Target summaries in .agents/context/implementations/summaries/
- Affected system documents in .agents/context/game-systems/

## Scope and Safety Rules

1. Completed Implementations Only:
   - A plan is considered implemented and eligible for consolidation and deletion only when:
     - It has a corresponding implementation summary in .agents/context/implementations/summaries/ (or the user explicitly confirms the standalone plan/summary is completed), AND
     - Its key code, asset, or architectural changes are verified as present in the repository (Assets/Scripts/, Assets/ScriptableObjects/, Assets/Prefabs/, Assets/Scenes/).
   - If a plan in .agents/context/implementations/plans/ has not been implemented yet (no matching summary and missing code changes), do NOT delete it and do NOT document its unimplemented behavior as current system truth. Report it to the user as pending/unimplemented and keep the file intact.

2. Source Code as Ground Truth:
   - Implementation plans and summaries describe what was intended and what was changed, but actual source code in Assets/Scripts/ and authored assets remain the final source of truth.
   - If an implementation plan and its summary differ (for example, design adjustments made during execution), prefer the implementation summary verified against current C# source code.
   - Never copy raw changelog prose or step-by-step task checklists into .agents/context/game-systems/. Integrate the changes cleanly into the structured sections of each system document (Purpose, Reading Map, Architecture and Data Flow, Rules and Invariants, Extension Points, Integration Notes, Known Risks and Open Questions).

3. Template Preservation and Cleanup:
   - Keep .agents/context/implementations/templates/ (containing plan-template.md and summary-template.md) completely untouched, as it holds the reference templates for creating plans and summaries.
   - Delete all processed plan files from .agents/context/implementations/plans/ and all processed summary files from .agents/context/implementations/summaries/ so that, once all completed implementations are synced, only .agents/context/implementations/templates/ remains under .agents/context/implementations/.
   - Never delete files outside .agents/context/implementations/plans/ and .agents/context/implementations/summaries/.

4. Documentation Formatting Standards:
   - Author all system documentation strictly in English.
   - Write all file and directory paths relative to the project root in plain text without markdown links and without backticks (for example: - Assets/Scripts/PowerUps/Armor/ArmorService.cs).
   - Keep system document filenames kebab-case ending with -system.md under .agents/context/game-systems/.

## Workflow

1. Inventory Plans and Summaries:
   - List all markdown files in .agents/context/implementations/plans/ and .agents/context/implementations/summaries/.
   - If the user specified particular plans or summaries, scope the run to those items; otherwise, include all completed plans and summaries found.
   - Pair each plan (such as <feature>-plan.md or <feature>-spec.md) with its matching summary (<feature>-summary.md or <feature>.md).
   - Flag any unpaired plan that has not yet been implemented in code so it is excluded from deletion.

2. Map Changes to Game Systems:
   - For each completed implementation pair (or standalone completed summary/plan):
     - Read the plan and summary in full.
     - Identify all touched source files, ScriptableObjects, prefabs, scenes, and Reflex DI installers.
     - Map those domains to existing system documents under .agents/context/game-systems/ (for example, power-ups-system.md, health-system.md, vfx-system.md, dev-console-system.md, player-system.md, di-and-boot-flow-system.md).
     - Determine whether the implementation:
       - Modifies or extends one or more existing documented systems (update those existing *-system.md files in place), and/or
       - Introduces a distinct new gameplay or architectural system that warrants a new .agents/context/game-systems/<name>-system.md file (guided by .agents/skills/document-system/SKILL.md and .agents/skills/document-system/templates/system-document-template.md).

3. Verify Against Current Codebase:
   - Inspect the actual C# scripts in Assets/Scripts/, configs in Assets/ScriptableObjects/, and DI bindings in Assets/Scripts/ReflexDI/ referenced by the plans and summaries.
   - Verify class names, interfaces, method signatures, constants, serialized fields, lifecycle ordering, and invariants before writing them into system documentation.

4. Update or Create System Documentation:
   - For each affected system document under .agents/context/game-systems/:
     - Update Purpose (responsibilities and non-responsibilities) if scope expanded.
     - Update Reading Map with all newly added or modified scripts, configs, prefabs, shaders, related runtime integrations, and cross-referenced system docs.
     - Update Architecture and Data Flow (core components, key interfaces, centralized constants, and step-by-step runtime flows).
     - Update Rules and Invariants (enum serialization stability, fail-fast validation, event ordering, pooling/tween cleanup, and performance constraints).
     - Update Extension Points and Testing Implications (including new dev console commands or verification checks).
     - Update Integration Notes (upstream dependencies, downstream consumers, and coupling risks) and Known Risks and Open Questions (removing resolved limitations and adding any new known limitations or follow-ups).
   - Apply the quality criteria from .agents/skills/agent-docs-review/SKILL.md so the resulting documentation remains detailed, accurate, concise, and immediately useful for future agents.

5. Verify Documentation Completeness Before Deletion:
   - Confirm that every architectural contract, component, constant, runtime flow, and follow-up from the processed plans and summaries has been accurately captured in the corresponding .agents/context/game-systems/*-system.md files.
   - Check that all file paths in the updated/created system documents exist in the repository and follow the plain-text relative path rule.

6. Delete Processed Plans and Summaries:
   - Delete the processed plan files from .agents/context/implementations/plans/ and the processed summary files from .agents/context/implementations/summaries/ (removing the empty plans/ and summaries/ directories if no unimplemented plans remain).
   - Verify that .agents/context/implementations/templates/ (with plan-template.md and summary-template.md) remains intact as the sole permanent content under .agents/context/implementations/.

7. Report Results to the User:
   - List the completed plans and summaries that were processed and deleted.
   - List all system documents under .agents/context/game-systems/ that were updated or newly created, with a concise summary of the incorporated knowledge.
   - List any unimplemented plans that were intentionally skipped and retained, if any.
