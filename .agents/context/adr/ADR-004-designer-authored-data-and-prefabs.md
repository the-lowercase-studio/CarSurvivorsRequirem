# ADR-004: Inspector-Driven Configuration & Data Safeguards

- Status: Accepted
- Date: 2026-08-12
- Decision Makers: Game Development Team & AI Agents

## Context

Game balance values (player car stats, skill damage, enemy spawn rates, VFX presets, sound clips) must be tweakable by game designers without modifying C# code. In Unity, hardcoding balance constants in C# scripts prevents rapid iteration and breaks inspector-driven workflows.

## Decision

We enforce inspector-driven configuration via **ScriptableObjects** and **Serialized Fields**:

1. **ScriptableObject Assets:** Game balance, wave definitions, skill upgrades, and settings data reside in designer-authored ScriptableObject assets under `Assets/ScriptableObjects/`.
2. **Serialized Field Integrity:** C# scripts expose configurable fields using `[SerializeField] private Type _fieldName`.
3. **Refactor Safeguards:** AI agents and developers must NOT rename or delete `[SerializeField]` fields without checking if asset serialization bindings will break. If renaming a serialized field is unavoidable, use `[UnityEngine.Serialization.FormerlySerializedAs("oldName")]`.

## Consequences

### Positive
- Designers can adjust game parameters without recompiling C# assemblies or needing developer intervention.
- Clean separation between gameplay logic (C#) and gameplay balance data (ScriptableObjects/Prefabs).

### Negative / Trade-offs
- Code refactoring requires caution to avoid breaking `.prefab`, `.asset`, or `.unity` YAML references.
