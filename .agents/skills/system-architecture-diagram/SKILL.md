---
name: system-architecture-diagram
description: "Use when: the user points at an existing Car Survivors system or code scope (folder, feature, file set, or cross-system flow) and explicitly asks for a human-facing architecture Mermaid diagram with the responsibilities of every part, saved under .user-docs/architecture/. Triggers: architecture diagram, system architecture diagram, mermaid diagram of system, diagram responsibilities, visualize system architecture. Route agent-facing system documentation to document-system."
---

# System Architecture Diagram Skill

Use this skill to produce a source-verified, human-facing architecture document for one pointed system or scope. The document has one architecture Mermaid diagram and, under it, a responsibilities table for every part shown in the diagram.

Invoking this skill counts as the explicit user request needed to write to .user-docs/. Without that request, do not create or edit files in .user-docs/.

## Inputs

- Target: a game-system name (for example Skills, Health, FlowField Navigation) or an explicit scope (folders, files, namespaces, or a cross-system flow such as "reward flow from level-up to HUD").
- Optional: target audience (default: developers and designers onboarding to the system).
- Optional: focus (for example DI wiring only, runtime event flow only, or data/config ownership).

If the target is ambiguous (no matching system document or folder, or several plausible scopes), ask the user to clarify before researching.

## Output

- .user-docs/architecture/<system-or-scope-kebab-name>-architecture.md, built from .agents/skills/system-architecture-diagram/templates/architecture-doc-template.md.
- A new or updated entry for that document in the .user-docs/README.md index table, with Category "Architecture".
- A short chat report covering the file path, scope boundaries, any drift between code and agent documentation, and anything you could not verify.

## Workflow

### 1. Orient

1. Read AGENTS.md, .agents/README.md, and .agents/context/project-coding-standards.md (only the architecture and DI sections are needed).
2. If a matching .agents/context/game-systems/*-system.md exists, use it only as a map of files and components. Do not copy claims from it without checking them in source.
3. Do not read .user-docs/ as operational truth. The one exception is the existing target document when you are refreshing it: open it to preserve its structure.

### 2. Inventory the scope from source

Build a component inventory from code. For each candidate component, record:

- Type and file path (Assets/Scripts/..., Assets/ScriptableObjects/...).
- The interfaces it implements and the interfaces it exposes to others.
- Reflex DI bindings: search Assets/Scripts/ReflexDI/ installers for its interface or concrete type.
- `[Inject]` dependencies.
- Events it raises (OnX) and who subscribes to them (search for `.OnX +=`).
- Direct calls into other components (method names).
- Designer data it owns or reads (ScriptableObjects, serialized configs).
- Unity lifecycle ordering that matters (Awake/Start/OnEnable discovery, initialization order).

Useful searches (PowerShell):

```powershell
Get-ChildItem -Recurse <scope-folder> -Filter *.cs | Select-String -Pattern 'interface |class |\[Inject\]|event |\+= '
Select-String -Path Assets/Scripts/ReflexDI/*.cs -Pattern '<TypeOrInterface>'
Get-ChildItem -Recurse Assets/Scripts -Filter *.cs | Select-String -Pattern '<InterfaceName>|<EventName>'
```

Also search for components in the scope that nothing references. Report them as currently unused helpers; do not draw them as part of the active flow.

### 3. Define boundaries

- Inside the scope: components owned by the target system.
- Boundary collaborators: external systems that trigger the scope, consume its output, or receive its requests (for example level-up events, damage interfaces, persistence services). Show each one as a single node, named by the interface the scope depends on, inside an "(other systems)" group.
- Leave out everything else. Do not expand external systems internally.

### 4. Group components

Put components into subgraphs by role. Use only the groups that apply, in roughly this order:

| Group | Contains |
| :--- | :--- |
| DI / Boot | Installers and bindings that wire the scope. |
| Sources (other systems) | Events or services that start the flow. |
| Persistence | Save, restore, and session services touching the scope. |
| Logic / Flow | Pure C# services, queues, calculators, request/data structs, constants. |
| Core | Registries, managers, base classes, contracts, generic helpers. |
| Concrete Implementations | Specific behaviors (skills, enemies, weapons, states). |
| Designer Data | ScriptableObjects, configs, stats, cloning utilities. |
| Presentation | Presenters, views, HUD, VFX/audio presenters. |
| Targets (other systems) | Interfaces the scope calls outward (damage, knockback, VFX, audio). |

### 5. Draw the diagram

Rules:

- Use `flowchart TD` (or `flowchart LR` for a short, mostly linear pipeline).
- Name nodes "ConcreteType (IInterface)" when both exist. For a parent-to-child pair, use "Parent → Child". Add a short role in parentheses when the type name alone says little.
- Label every edge with the real event or method name, or with a short verb when several calls are bundled together.
- Solid arrows (`-->` / `-- "label" -->`): runtime calls and events.
- Dotted arrows (`-. "label" .->`): DI wiring, constant limits, and configuration references.
- Edges may target a subgraph id when a relationship applies to the whole group (for example `Installer -. "binds services" .-> UI`).
- Do not add an edge that you cannot point to in source.

Mermaid safety:

- Quote every node label and edge label that contains spaces, punctuation, parentheses, slashes, or ampersands.
- No HTML tags (`<br/>`, `<b>`) and no angle-bracket generics in labels. Write "UpgradeableStat (generic)" or name the concrete subclasses instead.
- Never use `end`, `graph`, `subgraph`, `class`, or `style` as a node or subgraph id. Use short PascalCase ids.
- Keep it readable: aim for 40 nodes or fewer. If the scope is larger, draw one overview diagram of the groups, then one diagram per dense group. Each diagram gets its own responsibilities section.
- Check that every subgraph is closed with `end` and that all ids are unique.

### 6. Write the responsibilities

- Add one "Responsibilities" subsection per subgraph, in diagram order, each with a `| Part | Responsibility |` table.
- Every node in the diagram must have a row, and every row must match a node. Unused helpers found in step 2 may get an extra row marked as currently unused.
- Write each responsibility in plain language. Cover:
  - what the part owns;
  - when it acts, including the lifecycle or event that triggers it;
  - what it hands off;
  - what it explicitly does not own, when that boundary is easy to misread.
- Quote real limits and constants (for example "max 3 active skills") from source, not from memory.
- End with "Key Rules at a Glance": 4–8 invariants a human must respect when changing the scope (hierarchy requirements, name matching, caps, mutation rules).

### 7. Finish and report

1. Write the document from the template. Put a "Last verified against source: YYYY-MM-DD" line under the title.
2. Add or update the row in the .user-docs/README.md index table.
3. If the target document already exists, refresh it in place. Keep any sections the user added, and replace stale components, edges, and responsibilities.
4. Do not edit agent-facing docs (.agents/context/game-systems/) as part of this skill. List any drift you found (components, flows, or behaviors in code that the agent docs are missing or contradict) in the chat report, and suggest document-system as a follow-up.
5. Report in chat: the file path, the scope and boundaries chosen, the drift found, and the parts you could not verify (for example inspector-only wiring or prefab references).

## Validation Checklist

- [ ] Every node and edge traces to a type, call, event, or binding in source.
- [ ] Boundary collaborators appear as single interface nodes, not expanded.
- [ ] Each diagram node has exactly one responsibilities row, and each row has a node.
- [ ] Mermaid labels are quoted, contain no HTML or angle brackets, use no reserved ids, and every subgraph is closed.
- [ ] Constants and limits quoted in the document match current source.
- [ ] .user-docs/README.md index is updated.
- [ ] No changes to code, serialized assets, or agent-facing system docs.
