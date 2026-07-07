# Collectibles System Documentation

## Purpose

The Collectibles system defines the contract for objects that can be picked up by the player and coordinates the current skill-crate collectible spawning flow.

It is responsible for:
- Exposing collectible pickup events through Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs.
- Spawning configured collectible prefabs onto unoccupied walkable grid cells.
- Marking grid cells as collectible-occupied while a collectible is active.
- Notifying downstream systems when a collectible is collected and released.

It is not responsible for:
- Choosing which skill is unlocked or upgraded after collection.
- Managing player level or experience progression.
- Pooling collectible instances.
- Defining all future pickup types. The only current concrete collectible found in source is Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs
- Related code:
  - Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs
  - Assets/Scripts/Navigation/GridSystem/Cell.cs
  - Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs
  - Assets/Scripts/Providers/IGameObjectProvider.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md for DI binding changes.
  - .agents/skills/architecture-review/SKILL.md for reviewing ownership or event-flow changes.

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs is the shared collectible contract. It extends Assets/Scripts/Providers/IGameObjectProvider.cs and exposes `OnCollected`.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs is a `MonoBehaviour` collectible. It raises `OnCollected` when a trigger collider on the player layer enters the crate, then destroys its own GameObject.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs owns periodic and requested collectible spawning. It is scene-bound, injected with `IGridManager`, and configured through serialized fields.
  - Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs.FindCellWithoutCollectible chooses a random cell that is both walkable and not already marked as occupied by a collectible.
  - Assets/Scripts/Navigation/GridSystem/Cell.cs.IsOccupiedByCollectible is the grid occupancy flag used to prevent overlapping collectible spawns.
- Key interfaces:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs:
    - Requires a `GameObject` provider.
    - Raises `OnCollected` when the player collects the item.
  - Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs:
    - Exposes `SpawnAtRandomGridPos`.
    - Exposes `CurrentlySpawnedObjectsCount`.
    - Exposes `OnSpawnedEntityReleased`.
- Runtime flow:
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds the scene `CollectibleItemsSpawner` as Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs.Start initializes its active collectible list and starts `InvokeRepeating` using `_spawnDelay`.
  - Each spawn request selects a free walkable grid cell, picks a prefab using weighted spawn chance data, instantiates it under `_collectibleItemsParent`, subscribes to Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs.OnCollected, tracks it, and marks the selected cell as occupied.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs.OnTriggerEnter checks the entering collider's layer against `EntityLayers.Player`, raises `OnCollected`, and destroys the crate object.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs.Collectible_OnCollected receives the event, converts the collected object's world position back to a grid cell, clears that cell's collectible occupancy flag, removes the collectible from tracking, raises `OnSpawnedEntityReleased`, and decrements `CurrentlySpawnedObjectsCount`.
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs listens to `OnSpawnedEntityReleased`, asks `ISkillUpgradeFlow` to queue a reward request, and uses the returned request to show skill initialization or upgrade UI.

## Rules and Invariants

- Critical behavior rules:
  - Collectible prefabs spawned by Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs must implement Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs; otherwise they are instantiated but not tracked or released by the spawner.
  - A cell marked `IsOccupiedByCollectible` must be cleared when its collectible is collected.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs collection is player-layer gated through `EntityLayers.Player`.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs enforces `maxSpawnedCollectiblesCount` against its tracked collectible list.
  - Weighted random spawn selection uses the sum of `SpawnChance` values in `_collectibleItemsSpawnData`.
- Ordering or sequencing guarantees:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs raises `OnCollected` before destroying its GameObject.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs clears grid occupancy before raising `OnSpawnedEntityReleased`.
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs reacts to the release event, not directly to the collectible's trigger event.
- Constraints contributors must preserve:
  - Keep the spawner registered through Reflex instead of adding scene searches or singleton access.
  - Preserve inspector-configured prefab, parent, delay, Y offset, spawn chance, and maximum count data.
  - Avoid changing the release event timing without checking skill-upgrade UI behavior.
  - Do not edit scene, prefab, asset, or meta files directly unless the user explicitly requests it.

## Extension Points

- Safe extension areas:
  - Add new collectible prefabs that implement Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs and configure them in `_collectibleItemsSpawnData`.
  - Add new collection effects by subscribing to `OnSpawnedEntityReleased` through the DI-exposed spawner contract.
  - Add new spawn data fields if the serialized data migration is intentional and reviewed.
- Required dependencies and contracts:
  - New collectible implementations must expose a valid `GameObject` before `OnCollected` is handled by the spawner.
  - New spawner consumers should depend on Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs rather than the concrete component where possible.
  - Any system that changes grid occupancy must use the same world-position-to-cell assumptions as `CollectibleItemsSpawner.ReleaseOccupiedCellByCollectible`.
- Testing implications:
  - Compile after C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - In Unity, verify crates spawn on walkable cells, do not overlap occupied collectible cells, can be collected by the player, and trigger the skill UI.
  - For new collectible types, test that collection releases the grid cell and updates `CurrentlySpawnedObjectsCount`.

## Integration Notes

- Upstream dependencies:
  - `IGridManager.WorldGrid` provides the grid used for random placement and release.
  - Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs and `CellStatusDescriber` determine which cells are valid spawn targets.
  - Serialized scene references define the spawner parent, spawn cadence, maximum active count, and weighted prefab list.
- Downstream consumers:
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs listens for collectible release and triggers skill reward queueing through Assets/Scripts/Skills/UpgradeFlow/SkillUpgradeFlow.cs.
  - Any future systems can observe `OnSpawnedEntityReleased` through the DI-bound Assets/Scripts/Spawners/GridSpace/IOnRandomGridPosSpawner.cs.
- Cross-system coupling risks:
  - Collection currently has player-facing skill progression consequences through Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs.
  - Grid occupancy is coupled to the collectible object's world position at collection time; movement or tweening of collectibles could release the wrong cell unless occupancy ownership is changed.
  - The contract name is generic, but the only concrete implementation is skill-crate-specific and lives under the skill crate folder.

## Known Risks and Open Questions

- Known limitations:
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs.CurrentlySpawnedObjectsCount increments once per spawn loop iteration when spawn data exists, but an instantiated prefab without Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs is not tracked or releasable through the current event path.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs subscribes to each collectible's `OnCollected` event but does not explicitly unsubscribe before the collectible is destroyed.
  - The current collectible contract has no method for collection, ownership, or release; it only exposes an event and GameObject.
  - Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs.GameObject is assigned in `Start`, so collection before `Start` would leave the provider unset.
- Open design questions:
  - Should generic collectible logic remain under Assets/Scripts/Skills/ObjectsImpactingSkills/Crate, or should a dedicated collectible domain be reintroduced if more collectible types are added?
  - Should collectible spawn occupancy store the claimed `Cell` directly instead of resolving by world position on release?
  - Should collectibles be pooled rather than instantiated and destroyed if spawn frequency increases?
- Suggested follow-up tasks:
  - Audit serialized crate prefab configuration in the Unity Editor to ensure every configured prefab implements Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs.
  - Add a focused play-mode or edit-mode test around collectible count, occupancy release, and `OnSpawnedEntityReleased` event ordering if this system becomes more central.
