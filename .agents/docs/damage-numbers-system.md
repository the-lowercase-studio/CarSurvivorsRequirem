# Damage Numbers System Documentation

## Purpose

The Damage Numbers system displays short-lived world-space text feedback when enemies take damage. It owns popup creation, threshold-based visual selection, movement, lifetime release, pooling, and the player setting that enables or disables popups.

It does not calculate damage, change health values, choose combat targets, play blood VFX, or own enemy death flow. Those behaviors remain with combat, health, enemy, and VFX systems.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs`
  - `Assets/Scripts/DamageNumbers/DamageNumber.cs`
  - `Assets/Scripts/DamageNumbers/DamageNumberApearance.cs`
- Integration points:
  - `Assets/Scripts/Enemies/Enemy.cs`
  - `Assets/Scripts/ReflexDI/BootLoader.cs`
  - `Assets/Scripts/Settings/DamageNumbersSetting.cs`
  - `Assets/Scripts/UI/Settings/DamageNumbersOption.cs`
  - `Assets/Scripts/Spawners/WorldSpace/IInWorldSpaceSpawner.cs`
  - `Assets/Scripts/ObjectLifeCycle/Actions/IEnableDisableFunctionalityTrigger.cs`
- Related docs:
  - `.agents/docs/project-coding-standards.md`
  - `.agents/docs/ai-game-dev-best-practices.md`
- Related skills or instructions:
  - `.agents/skills/di-integration/SKILL.md`
  - `.agents/skills/check-optimalization/SKILL.md`
  - `.agents/skills/preserve-coding-standards/SKILL.md`

## Architecture and Data Flow

- Core components:
  - `DamageNumbersSpawner` is a scene-level `MonoBehaviour` registered through Reflex as both `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>` and `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`.
  - `DamageNubmersSpawnerConfig` carries the damage value and `ShapeModes` movement shape for a spawn request.
  - `DamageNumber` is the pooled popup instance. It receives `DamageNumberConfig`, writes TextMeshPro text/color/font size, runs DOTween font-size animation, and raises `OnLifeEnd`.
  - `DamageNumberApearance` stores the visual values used by a popup: font size, grow multiplier, and color.
  - `DamageNumbersSetting` stores and loads the enabled state through `AppStorage`.
  - `DamageNumbersOption` binds the setting to a UI `Toggle`.
- Key interfaces:
  - `IInWorldSpaceSpawner<TSelf, TSpecificConfig>` is the spawning contract used by enemy damage handling.
  - `IEnableDisableFunctionalityTrigger<T>` is the settings contract used to enable or disable popup spawning.
  - `IInitializable<DamageNumberConfig>` is used by pooled `DamageNumber` instances.
- Runtime flow:
  - `BootLoader.InstallExtra` registers the serialized `DamageNumbersSpawner` instance into each scene container.
  - `Enemy.TakeDamage` calls `_damageNumbersSpawner.Spawn` at the blood VFX transform position before decreasing health.
  - `DamageNumbersSpawner.Spawn` exits early when popups are disabled.
  - The spawner picks the last serialized threshold entry whose `Treshold` is less than or equal to the incoming damage.
  - The spawner gets a `DamageNumber` from `UnityEngine.Pool.ObjectPool`, positions it, initializes its text appearance, subscribes to `OnLifeEnd`, starts a DOTween `DOMove`, and increments `CurrentlySpawnedObjectsCount`.
  - `DamageNumber.Initialize` grows font size, shrinks it to zero, then raises `OnLifeEnd`.
  - `DamageNumbersSpawner.DamageNumber_OnLifeEnd` unsubscribes, decrements the spawned count, releases the popup back to the pool, and raises `OnSpawnedEntityReleased`.

## Rules and Invariants

- The DI registration in `BootLoader` is the authoritative access path. Do not replace it with singleton access or scene searches.
- Enemy damage numbers are spawned before health is reduced in `Enemy.TakeDamage`; preserve this ordering unless the gameplay meaning of hit feedback changes intentionally.
- `visualApearanceByDamageTresholds` must contain at least one inspector entry or the spawner logs an error and does not spawn.
- Threshold ordering matters. The current lookup iterates from the end of the serialized array, so designer-authored entries should be sorted from lowest to highest threshold for expected "highest matching threshold wins" behavior.
- If no threshold entry has `Treshold <= damage`, the fetched popup is released and no number is shown.
- `DamageNumbersSpawner.DisableFunctionality` suppresses new popups only. It does not cancel or release already active popups.
- Pool release depends on `DamageNumber.OnLifeEnd`; any lifecycle change must still raise this event exactly once per successful spawn.
- `CurrentlySpawnedObjectsCount` is incremented only after initialization and movement start, and decremented only from the life-end handler.
- `DamageNumber` uses TextMeshPro font size as its visibility animation. Its assigned `_textMeshPro` reference is required by the prefab.
- Popup movement destination uses `RandomUtility` and Unity random state; do not assume deterministic placement across runs unless the caller controls Unity random seeding.

## Extension Points

- Safe extension areas:
  - Add new visual threshold entries through the serialized `DamageNumbersSpawner` array.
  - Tune popup lifetime, movement radius, prefab, font size, grow multiplier, and color through inspector data.
  - Add new `ShapeModes` only if `DamageNumbersSpawner.GetDestinationBasedOnSpawnShapeMode` is updated with explicit behavior for the new mode.
  - Add new consumers through `IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>` instead of referencing the concrete spawner directly.
- Required dependencies and contracts:
  - Scene setup must provide a `DamageNumbersSpawner` reference in `BootLoader`.
  - The popup prefab must contain a `DamageNumber` component with a valid TextMeshPro reference.
  - Settings UI requires `ISetting<DamageNumbersSetting, bool>` and the spawner's enable/disable interface to be bound in the active container.
- Testing implications:
  - Compile after C# edits with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - For behavior changes, test with damage values below the first threshold, exactly on thresholds, between thresholds, and above the highest threshold.
  - For pooling changes, verify `CurrentlySpawnedObjectsCount` returns to zero after popup animations finish.
  - For settings changes, verify disabling damage numbers prevents new spawns and re-enabling restores future spawns.

## Integration Notes

- Upstream dependencies:
  - `Enemy.TakeDamage` is the current gameplay producer of damage number requests.
  - `DamageNumbersOption` and `DamageNumbersSetting` control the enabled state from player settings.
  - `BootLoader` provides the spawner instance to Reflex scene containers.
- Downstream consumers:
  - Pooling/counting consumers can observe `OnSpawnedEntityReleased` through the `IObjectReleaseNotifier` part of `IInWorldSpaceSpawner`.
  - Designers interact with the serialized spawner configuration and popup prefab.
- Cross-system coupling risks:
  - Moving `DamageNumbersSpawner` registration out of `BootLoader` can break enemy injection and settings injection together.
  - Changing the popup event lifecycle can leak event subscriptions or leave pooled objects active.
  - Altering `Enemy.TakeDamage` ordering can change the perceived relationship between damage, death, and blood VFX.
  - The serialized type and field names currently include spelling mistakes such as `DamageNubmersSpawnerConfig`, `DamageNumberApearance`, `Treshold`, and `visualApearanceByDamageTresholds`; renaming them affects code references and serialized data compatibility.

## Known Risks and Open Questions

- Known limitations:
  - Active DOTween tweens are not explicitly killed before pooled popup reuse. If reuse happens before old tweens complete, animation state could overlap.
  - `DamageNumber.IsInitialized` remains true after the first initialization and is not reset on pool release.
  - `_popupsSpeedRange` is serialized but not used by the current movement logic.
  - The spawner logs "NOT SPAWNING POPUP" before its error when no thresholds exist; this may be noisy in normal development.
  - Damage text uses `float.ToString()` without formatting, so decimal precision depends on the incoming damage value.
- Open design questions:
  - Should damage numbers be shown for non-enemy damageable objects if more producers are added?
  - Should critical hits, healing, shields, or resisted damage use separate appearance rules?
  - Should popup placement be deterministic for tests or replay-like features?
- Suggested follow-up tasks:
  - Add focused play-mode or edit-mode coverage for threshold selection and pool release.
  - Decide whether to migrate misspelled type and field names with Unity serialization compatibility safeguards.
  - Either use `_popupsSpeedRange` for movement timing/randomization or remove it in a dedicated cleanup.
