# Collectibles System Documentation

## Purpose

The Collectibles system defines the contract for objects that can be picked up by the player, coordinates enemy-driven drop triggers, and manages the spawning, pooling, and animation of collectible drops (such as skill crates).

It is responsible for:
- Exposing collectible pickup events through Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs.
- Managing collectible instances using `UnityEngine.Pool.ObjectPool` inside the collectible drop notifier.
- Spawning and animating collectibles with DOTween (scaling up and jumping/scattering outward).
- Ensuring target drop locations are walkable grid cells.
- Notifying downstream systems when a skill upgrade collectible is collected.

It is not responsible for:
- Choosing which skill is unlocked or upgraded after collection.
- Managing player level or experience progression.
- Defining enemy-specific stats or health logic.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs (implements `ISkillUpgradeCollectible` and `IPoolable`)
  - Assets/Scripts/Enemies/EnemyDropHandler.cs
  - Assets/Scripts/Enemies/CollectibleDropNotifier.cs (implements `ICollectibleDropNotifier`)
  - Assets/Scripts/Enemies/DropAnimationConfiguration.cs (ScriptableObject animation parameters)
- Related code:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - **ICollectible.cs**: Exposes the `OnCollected` event and the `IGameObjectProvider` interface.
  - **ISkillUpgradeCollectible**: Narrow interface implemented by collectibles that grant skill rewards (like `SkillCrate`).
  - **SkillCrate.cs**: Concrete implementation that triggers collection when colliding with the player and returns to the pool.
  - **EnemyDropHandler.cs**: Script attached to enemy prefabs. Listens to `Health.OnNoHealth` to trigger collectible drops based on independent chance percentages. Resolves walkable grid landing cells.
  - **CollectibleDropNotifier.cs**: Central manager that maintains object pools for collectible prefabs, instantiates them, executes drop scatter animations, and listens to pickup events.
  - **DropAnimationConfiguration.cs**: ScriptableObject configured in the inspector storing shared animation settings (scatter radius, durations, jump height, multipliers).
- Key interfaces:
  - **ICollectibleDropNotifier**: Exposes `SpawnCollectible` and raises `OnSkillUpgradeCollectibleCollected` when an `ISkillUpgradeCollectible` is collected.
- Runtime flow:
  - When an enemy dies, `EnemyDropHandler.Health_OnNoHealth` rolls a random chance for each entry in its drop table.
  - For successful rolls, it calculates a target position in a circular spread around the death location.
  - It queries `IGridManager.WorldGrid` to ensure target landing positions are walkable. If blocked, it steps-back toward the death position and, if needed, performs a spiral cell search.
  - It requests `ICollectibleDropNotifier.SpawnCollectible`.
  - The notifier gets the collectible from its pool, sets it at the enemy death position, and runs a DOTween sequence (DOScale to 1 with Ease.OutBack, and DOJump to the target walkable position).
  - When the player touches the collectible trigger, it raises `OnCollected` and returns itself to the notifier's pool.
  - The notifier catches `OnCollected`, raises `OnSkillUpgradeCollectibleCollected`, and releases the instance.
  - `SkillUpgradePresenter` catches the event and queues a random skill upgrade.

## Rules and Invariants

- Critical behavior rules:
  - Drops must not land on impassable cells (checked via `CellStatusDescriber.IsWalkable`).
  - Drop triggers and lifetime are completely decoupled from the enemy's lifecycle; collectibles spawn at root level, and enemies return to the pool immediately on death.
  - Multiple drop configurations roll independently; a single enemy death can result in multiple items.
  - Active tween animations on the collectible transform must be killed (`transform.DOKill()`) when the object is released to the pool.
- Constraints contributors must preserve:
  - Centralize drop animation settings in `DropAnimationConfiguration` and inject them via Reflex.
  - Always use `ISkillUpgradeCollectible` for decoupling generic drop behavior from UI presenter upgrades.

## Testing implications

- Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
- Verify in the Unity Editor that enemies drop configured prefabs, multiple drops spread correctly, items never land on impassable cells (e.g. walls/obstacles), and collecting a crate triggers the upgrade UI.
