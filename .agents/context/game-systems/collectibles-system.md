# Collectibles System Documentation

## Purpose

The Collectibles system defines the contract for objects that can be picked up by the player, coordinates enemy-driven drop triggers upon death, and manages the spawning, pooling, and animation of collectible drops (such as skill crates).

It is responsible for:
- Exposing collectible pickup events through Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs.
- Managing collectible instances using UnityEngine.Pool.ObjectPool inside the collectible drop notifier.
- Spawning and animating collectibles with DOTween (scaling up and jumping/scattering outward).
- Ensuring target drop locations are walkable grid cells.
- Notifying downstream systems when a skill upgrade collectible is collected.

It is not responsible for:
- Choosing which skill is unlocked or upgraded after collection (handled by skill upgrade flows and UI presenters).
- Managing player level or experience progression (handled by the level system).
- Defining enemy-specific stats or health logic (handled by enemy health and stats components).

## Reading Map

- Primary code locations:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs
- Related runtime integration:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
  - Assets/Scripts/Navigation/GridSystem/
- Related docs:
  - .agents/context/game-systems/skills-system.md
  - .agents/context/game-systems/grid-system.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/pooling-and-object-lifecycle-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - `ICollectible` is the base interface for pickup objects exposing `OnCollected` and `IGameObjectProvider`.
  - `ISkillUpgradeCollectible` is a narrow tag interface extending `ICollectible` for skill reward drops.
  - `SkillCrate` is a `MonoBehaviour` implementing `ISkillUpgradeCollectible` and `IPoolable`. It checks for player trigger collisions (`EntityLayers.Player`), fires `OnCollected`, and triggers `ReturnToPool`.
  - `CollectibleDropEntry` is a serializable struct configuring prefab, drop chance percentage (0-100%), and spawn Y-offset per enemy drop table entry.
  - `EnemyDropHandler` is attached to enemy prefabs. It subscribes to `Health.OnNoHealth`, rolls independent percentage chances for configured drop entries, resolves walkable grid landing positions, and calls `ICollectibleDropNotifier.SpawnCollectible`.
  - `CollectibleDropNotifier` is a scene service maintaining per-prefab `ObjectPool<GameObject>` instances. It handles object instantiation under a parent transform, sets initial zero scale, runs DOTween `DOScale` and `DOJump` scatter animations, and listens to pickup events.
  - `DropAnimationConfiguration` is a ScriptableObject storing scatter radius, scale duration, scatter duration, jump power range, and duration multipliers.
- Key interfaces:
  - `ICollectibleDropNotifier` exposes `SpawnCollectible` and raises `OnSkillUpgradeCollectibleCollected`.
  - `ISkillUpgradeCollectible` identifies collectibles that trigger skill upgrades.
  - `IPoolable` coordinates lifecycle reset and pool release via `OnCanBeReleased`.
- Runtime flow:
  1. `DefaultGameplaySceneInstaller` binds `CollectibleDropNotifier` as `ICollectibleDropNotifier` and binds `DropAnimationConfiguration` into the scene container.
  2. On enemy death, `Enemy.Health` raises `OnNoHealth`, which triggers `EnemyDropHandler.Health_OnNoHealth`.
  3. `EnemyDropHandler` evaluates each `CollectibleDropEntry`. For successful rolls, it calculates a circular scatter angle around the enemy position.
  4. `EnemyDropHandler` calls `GetWalkablePosition`, testing target position against `IGridManager.WorldGrid` cell walkability, stepping back towards origin, and performing a 5-cell radius spiral search if blocked.
  5. `EnemyDropHandler` calls `ICollectibleDropNotifier.SpawnCollectible` with prefab, spawn position, and validated target walkable position.
  6. `CollectibleDropNotifier` retrieves or creates a pool for the prefab, activates the instance, sets scale to zero, and plays a joined DOTween sequence (`DOScale` to 1 with `Ease.OutBack` and `DOJump` to target position).
  7. When the player car enters the `SkillCrate` trigger collider (`EntityLayers.Player`), `SkillCrate` invokes `OnCollected` and `OnCanBeReleased`.
  8. `CollectibleDropNotifier` catches `OnCollected`, checks if the item is `ISkillUpgradeCollectible`, invokes `OnSkillUpgradeCollectibleCollected`, and returns the instance to its pool.
  9. `SkillUpgradePresenter` catches `OnSkillUpgradeCollectibleCollected` and queues a random skill upgrade request via `ISkillUpgradeFlow`.

## Rules and Invariants

- Critical behavior rules:
  - Drops must never land on impassable grid cells (validated via `CellStatusDescriber.IsWalkable`).
  - Collectible instances and scatter animations are decoupled from enemy lifetime; items spawn under `_collectibleItemsParent` at scene root so enemies can be pooled immediately on death.
  - Multiple drop table entries roll independently; a single enemy death can spawn zero, one, or multiple collectibles.
  - Active transform tweens must be killed via `transform.DOKill()` when a collectible is released to the pool.
- Ordering or sequencing guarantees:
  - Collectible pool release resets scale and unhooks events in `actionOnRelease` before deactivating the GameObject.
  - DOTween scatter sequence combines scaling and jumping simultaneously via `sequence.Join`.
- Constraints contributors must preserve:
  - Centralize drop animation settings in `DropAnimationConfiguration` assets and inject them via Reflex.
  - Use `ISkillUpgradeCollectible` for decoupling generic drop mechanics from UI upgrade presenters.
  - Do not hardcode player layer indices; always use `EntityLayers.Player`.

## Extension Points

- Safe extension areas:
  - Add new collectible drop types by creating a MonoBehaviour implementing `ICollectible` (and `IPoolable`), creating a prefab, and adding it to enemy drop tables.
  - Create new drop category interfaces (e.g. `IHealthCollectible`, `IGoldCollectible`) to notify different gameplay handlers via `CollectibleDropNotifier`.
  - Tune scatter radius, jump heights, and duration multipliers in `DropAnimationConfiguration` assets without code changes.
- Required dependencies and contracts:
  - Enemy drop handling requires Reflex injection of `ICollectibleDropNotifier`, `IGridManager`, and `DropAnimationConfiguration`.
  - Collectibles require a Collider configured as a trigger and an attached `IPoolable` / `ICollectible` implementation.
- Testing implications:
  - Compile C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Verify in Unity play mode that drops spread within configured bounds, avoid impassable terrain/walls, properly return to pool on collection, and raise skill upgrade modals.

## Integration Notes

- Upstream dependencies:
  - `Enemy.Health` emits `OnNoHealth`.
  - `IGridManager.WorldGrid` supplies grid cell walkability data for landing point verification.
  - Reflex DI provides scene container bindings for notifier and animation config.
- Downstream consumers:
  - `SkillUpgradePresenter` listens to `ICollectibleDropNotifier.OnSkillUpgradeCollectibleCollected` to trigger upgrade popups.
- Cross-system coupling risks:
  - If `IGridManager` is uninitialized during enemy death, `GetWalkablePosition` falls back to `startPos`, which may cause drops on impassable cells.

## Known Risks and Open Questions

- Known limitations:
  - `CollectibleDropNotifier` maintains pools in a `Dictionary<GameObject, ObjectPool<GameObject>>` created lazily per prefab without an explicit max pool size cap.
  - The walkable cell spiral search caps at a radius of 5 cells; if surrounding terrain is completely blocked, drops fall back to enemy death position.
- Open design questions:
  - Should collectible items feature a magnet attraction mechanism towards the player when in proximity?
  - Should uncollected drops automatically expire or despawn after a configurable timeout?

