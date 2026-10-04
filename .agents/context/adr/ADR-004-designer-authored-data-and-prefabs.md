# ADR-004: Inspector-Driven Configuration & Data Safeguards

- Status: Accepted
- Date: 2026-08-12
- Decision Makers: Game Development Team & AI Agents

## Context

Game balance values (player car stats, skill damage, enemy spawn rates, VFX presets, sound clips) must be tweakable by game designers without modifying C# code. In Unity, hardcoding balance constants in C# scripts prevents rapid iteration and breaks inspector-driven workflows.

## Decision

We enforce inspector-driven configuration via **ScriptableObjects** and **Serialized Fields**:

1. **ScriptableObject Assets:** Game balance, wave definitions, skill upgrades, and settings data reside in designer-authored ScriptableObject assets under Assets/ScriptableObjects/.
2. **Serialized Field Integrity:** Follow .agents/context/project-coding-standards.md: prefer private serialized fields, use serialized auto-properties when public read access is needed, and preserve existing field identities and permitted Unity/editor compatibility exceptions.
3. **Refactor Safeguards:** Check affected scenes, prefabs, assets, and consumers before renaming, deleting, changing types, or converting serialized fields to properties. Do not use `FormerlySerializedAs`. Before an authorized rename, notify the user that serialized values require manual reassignment in the Unity Editor and record the affected references and verification steps.
4. **Direct Asset Edit Authority:** AGENTS.md requires an explicit user request and a text-reviewable change before directly editing .prefab, .unity, .asset, or .meta files. The coding-standards exception for small text migrations does not independently grant this authority. When explicitly authorized, report the edits and verify serialized values and references; otherwise preserve field names or leave reassignment as a documented editor follow-up.

Policy alignment: 2026-10-04. This replaces the previous migration-attribute guidance with the established coding-standards policy.

## Consequences

### Positive
- Designers can adjust game parameters without recompiling C# assemblies or needing developer intervention.
- Clean separation between gameplay logic (C#) and gameplay balance data (ScriptableObjects/Prefabs).

### Negative / Trade-offs
- Code refactoring requires caution to avoid breaking `.prefab`, `.asset`, or `.unity` YAML references.
