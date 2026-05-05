---
name: agent-docs-review
description: "Use when: reviewing, trimming, restructuring, or updating a pointed ProjectLizard documentation file so it becomes accurate, current, and useful for AI agents. Trigger for requests to make docs agent-friendly, reduce documentation noise, verify docs against code, or keep only operationally important guidance in markdown files under .agents/docs/ or other .agents/ operational files."
---

# Agent Docs Review

Use this skill to turn an existing documentation file into concise, implementation-grounded guidance that helps future agents work correctly.

## Inputs

- Target markdown file path.
- User's goal for the document, if provided.
- Related system or code area, if the file is system-specific.

## Workflow

1. Read the pointed documentation file first.
2. Read project source-of-truth docs before editing:
   - `AGENTS.md`
   - `.agents/README.md`
   - `.agents/docs/technology-documentation.md` when framework or package behavior is mentioned.
   - `.agents/docs/project-coding-standards.md` when conventions or coding rules are mentioned.
3. If the document describes a concrete system, inspect the relevant source files before changing behavioral claims.
4. Separate content into three groups:
   - Keep: invariants, architecture boundaries, file maps, workflow steps, validation checks, extension points, failure modes, and open questions.
   - Compress: long explanations, repeated background, broad best-practice advice, historical notes, and examples that do not affect agent decisions.
   - Remove: stale claims, motivational text, generic AI advice, duplicated sections, unverified implementation detail, and guidance that belongs in another canonical file.
5. Rewrite the document for agent use:
   - Start with purpose and when to use the document.
   - Prefer checklists, constraints, file references, and decision rules over prose.
   - Include "verify in code" notes where behavior may drift.
   - Preserve exact relative paths and update broken or renamed links.
   - Keep markdown filenames kebab-case under `.agents/docs/` unless the file is a reserved operational name.
6. For changed behavior claims, make them traceable to code or mark them as assumptions/open questions.
7. Run a final pass for brevity, consistency, and agent trigger usefulness.

## Agent-Friendly Criteria

A documentation file is agent-friendly when it answers:

- What task or system this document covers.
- Which files are authoritative for the behavior.
- What invariants must not be broken.
- What extension points should be reused.
- What validation or manual checks matter.
- What is uncertain and must be confirmed with the user.

## Output

Update the target document directly. In the final response, summarize the main reductions, any corrected stale claims, and any remaining assumptions or code areas not verified.
