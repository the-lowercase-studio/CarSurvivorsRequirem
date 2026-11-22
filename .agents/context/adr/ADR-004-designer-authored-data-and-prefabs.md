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
3. **Refactor Safeguards & Unused Field Cleanup:** Check all affected scenes, prefabs, assets, and C# consumers across the repository before renaming, deleting, changing types, or converting serialized fields to properties. Do not use `FormerlySerializedAs`. Confirmed-unused serialized fields may be removed from both C# scripts and prefabs/assets once full non-usage is verified across code, prefabs, and scene overrides. For renames or migrations that require manual inspector reassignment, notify the user in advance and record the affected references and verification steps.
4. **Direct Asset Edit Authority:** Agents are authorized to edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly when the change is safe to review as text, **provided the agent first presents the full scope of planned asset/prefab changes (exact files, components/overrides, and field edits) to the user and receives approval** (or acts on an explicit user request covering that exact scope). After authorized edits, report every modified asset/prefab and verify serialized values and references.

Policy alignment: 2026-11-19. Clarified direct prefab/asset editing and unused serialized field removal authority subject to presenting the full scope to the user first.

## Consequences

### Positive
- Designers can adjust game parameters without recompiling C# assemblies or needing developer intervention.
- Clean separation between gameplay logic (C#) and gameplay balance data (ScriptableObjects/Prefabs).

### Negative / Trade-offs
- Code refactoring requires caution to avoid breaking `.prefab`, `.asset`, or `.unity` YAML references.
