# Replace CollectibleItemsSpawner with Enemy Drop System

Date: 2026-07-10

## Background

Currently, collectible items (only skill crates) spawn on a timer via [CollectibleItemsSpawner](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs) at random walkable grid cells. This system must be replaced with an **enemy drop system** where collectible items drop from enemies on death, with per-enemy-prefab drop configuration, animated spawn-in using DOTween (scale up + scatter outward with a curve).

## Design Decisions & Resolutions

- **Drop Parent Hierarchy**: Drops will be spawned directly at the root of the active scene (parent passed as `null`) to keep prefabs clean from scene references and ensure independence from the enemy's lifecycle.
- **Drop Lifetime Independence**: Spawning is fully fire-and-forget; collectibles spawn as independent objects and the enemy can return to the pool immediately without waiting for drops to complete.
- **Animation Details**: We will use DOTween's `DOJump` instead of `DOMove` with an ease. Jump heights and durations will be randomized slightly per drop to give a natural, chaotic scatter effect.
- **Independent Drop Rolls**: Each entry in the drop table has its own independent roll. Multiple collectibles of different types can drop from a single enemy death.
- **Physics**: No Rigidbody is used on the collectible prefabs; they are static/kinematic triggers.
- **Enemies Directory Restructuring**: Basic enemy scripts (e.g., `Enemy.cs`, `EnemyDeathHandler.cs`, `EnemyMovementController.cs`, etc.) will be moved to a new subfolder `Assets/Scripts/Enemies/Base/`, and their namespaces will be updated to `Assets.Scripts.Enemies.Base`. The new drops system scripts (`EnemyDropHandler.cs` and `CollectibleDropNotifier.cs`) will remain directly under `Assets/Scripts/Enemies/` in the `Assets.Scripts.Enemies` namespace. All reference files across the codebase will be updated to import `Assets.Scripts.Enemies.Base`.
- **Skill Upgrade Decoupling**: To separate generic collectible drops (like future health/resource items) from UI-driven skill upgrades, a new `ISkillUpgradeCollectible` interface will be introduced. `SkillCrate` will implement this. The `ICollectibleDropNotifier` will only raise the `OnSkillUpgradeCollectibleCollected` event when an `ISkillUpgradeCollectible` is collected, which `SkillUpgradePresenter` will listen to.
- **Impassable Cells Constraint**: Drops cannot be dropped on impassable cells. The enemy drop handler (Assets/Scripts/Enemies/EnemyDropHandler.cs) will use a validation algorithm that queries the grid manager (Assets/Scripts/Navigation/GridSystem/GridManager.cs) to ensure target landing positions are walkable. If a target position is impassable, the handler will step-back toward the death position, and if needed, perform a local grid spiral search to find the closest walkable cell.
- **Global Drop Animation Configuration**: Rather than using magic numbers or duplicating fields across enemy prefabs, a new `DropAnimationConfiguration` ScriptableObject will hold the shared drop animation settings (scatter radius, durations, jump height ranges, duration multipliers). This configuration will be registered in Reflex DI and injected where needed.

## Proposed Changes

### Enemies Directory Restructuring

#### [NEW] [Base folder](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base)

Create a new directory at `Assets/Scripts/Enemies/Base/`.

#### [MOVE] [Enemy.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/Enemy.cs)

#### [MOVE] [EnemyAnimator.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/EnemyAnimator.cs)

#### [MOVE] [EnemyAttackController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/EnemyAttackController.cs)

#### [MOVE] [EnemyCollisionsController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/EnemyCollisionsController.cs)

#### [MOVE] [EnemyDeathHandler.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs)

#### [MOVE] [EnemyMovementController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/EnemyMovementController.cs)

#### [MOVE] [IAttackAnimationPlayer.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/IAttackAnimationPlayer.cs)

#### [MOVE] [IMovementController.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/IMovementController.cs)

#### [MOVE] [EnemiesOutsidePlayerChunkTeleporter.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/Base/EnemiesOutsidePlayerChunkTeleporter.cs)

**Refactoring Tasks:**

- Move all files listed above from `Assets/Scripts/Enemies/` to `Assets/Scripts/Enemies/Base/`.
- Update the namespace in all moved files to `namespace Assets.Scripts.Enemies.Base`.
- Update references and imports across all files in the project that reference these classes (including installers, spawners, player controllers, trap controllers, and presenters).

---

#### New Configuration — DropAnimationConfiguration

#### [NEW] Assets/Scripts/Enemies/DropAnimationConfiguration.cs

A global ScriptableObject that stores configuration parameters for drop animations, removing magic numbers.

```csharp
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [CreateAssetMenu(fileName = "DropAnimationConfiguration", menuName = "CarSurvivors/Drops/DropAnimationConfiguration")]
    public class DropAnimationConfiguration : ScriptableObject
    {
        [SerializeField] private float _scatterRadius = 2f;
        [SerializeField] private float _scaleDuration = 0.4f;
        [SerializeField] private float _scatterDuration = 0.5f;
        [SerializeField] private float _minJumpPower = 1.2f;
        [SerializeField] private float _maxJumpPower = 1.8f;
        [SerializeField] private float _minDurationMultiplier = 0.9f;
        [SerializeField] private float _maxDurationMultiplier = 1.1f;

        public float ScatterRadius => _scatterRadius;
        public float ScaleDuration => _scaleDuration;
        public float ScatterDuration => _scatterDuration;
        public float MinJumpPower => _minJumpPower;
        public float MaxJumpPower => _maxJumpPower;
        public float MinDurationMultiplier => _minDurationMultiplier;
        public float MaxDurationMultiplier => _maxDurationMultiplier;
    }
}
```

---

#### New Component — EnemyDropHandler

#### [NEW] [EnemyDropHandler.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/EnemyDropHandler.cs)

A new `MonoBehaviour` attached to **each enemy prefab** that defines what collectible items the enemy can drop on death.

```csharp
using System;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Navigation.GridSystem;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [Serializable]
    public struct CollectibleDropEntry
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Range(0f, 100f)] private float _dropChancePercent;
        [SerializeField] private float _yOffset;

        public GameObject Prefab => _prefab;
        public float DropChancePercent => _dropChancePercent;
        public float YOffset => _yOffset;
    }

    [RequireComponent(typeof(Enemy))]
    public class EnemyDropHandler : MonoBehaviour
    {
        [Inject] private readonly ICollectibleDropNotifier _dropNotifier;
        [Inject] private readonly IGridManager _gridManager;
        [Inject] private readonly DropAnimationConfiguration _animationConfig;

        [SerializeField] private CollectibleDropEntry[] _dropEntries;

        private Enemy _enemy;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            _enemy.Health.OnNoHealth += Health_OnNoHealth;
        }

        private void OnDisable()
        {
            _enemy.Health.OnNoHealth -= Health_OnNoHealth;
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            int totalDrops = 0;
            for (int i = 0; i < _dropEntries.Length; i++)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= _dropEntries[i].DropChancePercent)
                {
                    totalDrops++;
                }
            }

            if (totalDrops == 0)
            {
                return;
            }

            int dropIndex = 0;
            Vector3 basePos = transform.position;

            for (int i = 0; i < _dropEntries.Length; i++)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= _dropEntries[i].DropChancePercent)
                {
                    float angle = (dropIndex * 360f / totalDrops) + UnityEngine.Random.Range(-15f, 15f);
                    float radian = angle * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(radian), 0f, Mathf.Sin(radian));
                    Vector3 targetPos = basePos + direction * _animationConfig.ScatterRadius;

                    // Ensure target position is on a walkable cell
                    targetPos = GetWalkablePosition(basePos, targetPos);

                    Vector3 spawnPos = basePos + Vector3.up * _dropEntries[i].YOffset;
                    targetPos.y = spawnPos.y;

                    _dropNotifier.SpawnCollectible(
                        _dropEntries[i].Prefab, 
                        spawnPos, 
                        targetPos
                    );

                    dropIndex++;
                }
            }
        }

        private Vector3 GetWalkablePosition(Vector3 startPos, Vector3 targetPos)
        {
            Grid grid = _gridManager.WorldGrid;

            // 1. Try targetPos
            Cell targetCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, targetPos);
            if (targetCell != null && CellStatusDescriber.IsWalkable(targetCell))
            {
                return targetPos;
            }

            // 2. Try steps along the line between startPos and targetPos
            Vector3 direction = (targetPos - startPos).normalized;
            float distance = Vector3.Distance(startPos, targetPos);
            int steps = 4;
            for (int i = steps - 1; i >= 1; i--)
            {
                float ratio = (float)i / steps;
                Vector3 testPos = startPos + direction * (distance * ratio);
                Cell testCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, testPos);
                if (testCell != null && CellStatusDescriber.IsWalkable(testCell))
                {
                    return testPos;
                }
            }

            // 3. Try startPos (enemy death position)
            Cell startCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, startPos);
            if (startCell != null && CellStatusDescriber.IsWalkable(startCell))
            {
                return startPos;
            }

            // 4. Spiral search around startCell in the grid to find the nearest walkable cell
            if (startCell != null)
            {
                int startX = startCell.WorldGridPos.x;
                int startY = startCell.WorldGridPos.y;
                int gridWidth = grid.Width;
                int gridHeight = grid.Height;

                for (int radius = 1; radius <= 5; radius++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            {
                                continue;
                            }

                            int x = startX + dx;
                            int y = startY + dy;

                            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                            {
                                Cell candidateCell = grid.Cells[x, y];
                                if (candidateCell != null && CellStatusDescriber.IsWalkable(candidateCell))
                                {
                                    return candidateCell.WorldPos;
                                }
                            }
                        }
                    }
                }
            }

            // Fallback if absolutely nothing is found
            return startPos;
        }
    }
}
```

---

### New Interface & Service — ICollectibleDropNotifier

#### [NEW] [CollectibleDropNotifier.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/CollectibleDropNotifier.cs)

Provides a centralized service for spawning, pooling, and tracking when collectibles are collected.

```csharp
using System;
using System.Collections.Generic;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies
{
    public interface ICollectibleDropNotifier
    {
        event EventHandler OnSkillUpgradeCollectibleCollected;
        void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos);
    }

    public class CollectibleDropNotifier : MonoBehaviour, ICollectibleDropNotifier
    {
        [Inject] private readonly DropAnimationConfiguration _animationConfig;

        [SerializeField] private Transform _collectibleItemsParent;

        public event EventHandler OnSkillUpgradeCollectibleCollected;

        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _instancePoolMap = new();

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<GameObject>(
                    createFunc: () => Instantiate(prefab, _collectibleItemsParent),
                    actionOnGet: go => {
                        go.SetActive(true);
                        if (go.TryGetComponent<Pooling.IPoolable>(out var poolable))
                        {
                            poolable.OnGet();
                            poolable.OnCanBeReleased += Collectible_OnCanBeReleased;
                        }
                        if (go.TryGetComponent<ICollectible>(out var collectible))
                        {
                            collectible.OnCollected += Collectible_OnCollected;
                        }
                    },
                    actionOnRelease: go => {
                        if (go.TryGetComponent<Pooling.IPoolable>(out var poolable))
                        {
                            poolable.OnRelease();
                            poolable.OnCanBeReleased -= Collectible_OnCanBeReleased;
                        }
                        if (go.TryGetComponent<ICollectible>(out var collectible))
                        {
                            collectible.OnCollected -= Collectible_OnCollected;
                        }
                        go.SetActive(false);
                    },
                    actionOnDestroy: go => Destroy(go)
                );
                _pools.Add(prefab, pool);
            }
            return pool;
        }

        public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
        {
            var pool = GetOrCreatePool(prefab);
            var go = pool.Get();
            _instancePoolMap[go] = pool;

            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.zero;

            float jumpPower = UnityEngine.Random.Range(_animationConfig.MinJumpPower, _animationConfig.MaxJumpPower);
            float duration = _animationConfig.ScatterDuration * UnityEngine.Random.Range(_animationConfig.MinDurationMultiplier, _animationConfig.MaxDurationMultiplier);

            var sequence = DG.Tweening.DOTween.Sequence();
            sequence.Join(go.transform.DOScale(Vector3.one, _animationConfig.ScaleDuration).SetEase(DG.Tweening.Ease.OutBack));
            sequence.Join(go.transform.DOJump(targetPos, jumpPower, 1, duration));
        }

        private void Collectible_OnCollected(object sender, EventArgs e)
        {
            if (sender is ICollectible collectible)
            {
                if (collectible is ISkillUpgradeCollectible)
                {
                    OnSkillUpgradeCollectibleCollected?.Invoke(collectible, EventArgs.Empty);
                }
            }
        }

        private void Collectible_OnCanBeReleased(object sender, EventArgs e)
        {
            if (sender is Pooling.IPoolable poolable && sender is MonoBehaviour mb)
            {
                var go = mb.gameObject;
                if (_instancePoolMap.TryGetValue(go, out var pool))
                {
                    pool.Release(go);
                }
            }
        }
    }
}
```

---

### DI Installer Changes

#### [MODIFY] [DefaultGameplaySceneInstaller.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs)

- **Remove** `[SerializeField] private CollectibleItemsSpawner _collectibleItemsSpawner;`
- **Remove** `builder.AddSingleton(_collectibleItemsSpawner, typeof(IOnRandomGridPosSpawner<CollectibleItemsSpawner>));`
- **Add** `[SerializeField] private CollectibleDropNotifier _collectibleDropNotifier;`
- **Add** `builder.AddSingleton(_collectibleDropNotifier, typeof(ICollectibleDropNotifier));`
- **Add** `[SerializeField] private DropAnimationConfiguration _dropAnimationConfiguration;`
- **Add** `builder.AddSingleton(_dropAnimationConfiguration);`
- **Remove** `using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;` and `using Assets.Scripts.Spawners.GridSpace;` imports where no longer needed.

---

### SkillUpgradePresenter Rewiring

#### [MODIFY] [SkillUpgradePresenter.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs)

- **Replace** `[Inject] private readonly IOnRandomGridPosSpawner<CollectibleItemsSpawner> _collectibleItemsSpawner;` with `[Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier;`
- **Update** `Start()`:
  - Replace `_collectibleItemsSpawner.OnSpawnedEntityReleased += HandleCrateRewardRequest;` with `_collectibleDropNotifier.OnSkillUpgradeCollectibleCollected += HandleCrateRewardRequest;`
- **Update** `OnDestroy()`:
  - Replace `_collectibleItemsSpawner.OnSpawnedEntityReleased -= HandleCrateRewardRequest;` with `_collectibleDropNotifier.OnSkillUpgradeCollectibleCollected -= HandleCrateRewardRequest;`
- **Remove** unused `using` statements.

---

### Grid Occupancy Cleanup

#### [MODIFY] [Cell.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Navigation/GridSystem/Cell.cs)

- **Remove** `public bool IsOccupiedByCollectible { get; set; }` property.

#### [MODIFY] [RandomWalkableCellsFinder.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Navigation/GridSystem/RandomWalkableCellsFinder.cs)

- **Remove** `FindCellWithoutCollectible` method.
- **Remove** any `!cell.IsOccupiedByCollectible` filters.

---

### Old System Deletion

#### [DELETE] [CollectibleItemsSpawner.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/CollectibleItemsSpawner.cs)

Entire file deleted.

---

### Preserved Files (No Changes)

- [ICollectible.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/ICollectible.cs) — kept as-is.
- [EnemyDeathHandler.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Enemies/EnemyDeathHandler.cs) — no changes.

---

### Modified Collectibles

#### [MODIFY] [SkillCrate.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Skills/ObjectsImpactingSkills/Crate/SkillCrate.cs)

- Colocate `ISkillUpgradeCollectible` at the top of the file.
- Implement `IPoolable` and `ISkillUpgradeCollectible`.
- Clean up active tweens on the transform using `transform.DOKill()` inside `OnRelease`.
- Invoke `ReturnToPool()` inside `OnTriggerEnter` instead of destroying the GameObject.

```csharp
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Pooling;
using DG.Tweening;
using System;
using UnityEngine;

namespace Assets.Scripts.Skills.ObjectsImpactingSkills.Crate
{
    public interface ISkillUpgradeCollectible : ICollectible
    {
    }

    public class SkillCrate : MonoBehaviour, ISkillUpgradeCollectible, IPoolable
    {
        public GameObject GameObject { get; private set; }

        public event EventHandler OnCollected;
        public event EventHandler OnCanBeReleased;

        private void Awake()
        {
            GameObject = gameObject;
        }

        public void OnGet()
        {
        }

        public void OnRelease()
        {
            transform.DOKill();
        }

        public void ReturnToPool()
        {
            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (1 << other.gameObject.layer == EntityLayers.Player)
            {
                OnCollected?.Invoke(this, EventArgs.Empty);
                ReturnToPool();
            }
        }
    }
}
```

---

### Documentation Updates

#### [MODIFY] [collectibles-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/collectibles-system.md)

Update to reflect that collectibles now drop from enemies instead of spawning on grid cells.

#### [MODIFY] [spawners-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/spawners-system.md)

Remove `CollectibleItemsSpawner` references and add `EnemyDropHandler` / `CollectibleDropNotifier` references where appropriate.

---

## Summary of Drop Animation

```
Enemy dies at position P
  │
  ├─ For each CollectibleDropEntry that passes the independent chance roll:
  │    │
  │    ├─ Instantiate prefab at (P.x, P.y + entry.YOffset, P.z) as a root-level object (parent = null)
  │    │  with localScale = Vector3.zero
  │    │
  │    ├─ Calculate target position:
  │    │    angle = (i * 360 / totalDrops) + Random(-15, 15)  // spread evenly with jitter
  │    │    rawTargetPos = SpawnPos + direction(angle) * config.ScatterRadius
  │    │    targetPos = GetWalkablePosition(P, rawTargetPos)  // avoid impassable cells
  │    │
  │    └─ DOTween Sequence:
  │         ├─ DOScale(Vector3.one, config.ScaleDuration).SetEase(Ease.OutBack)                               // grow in
  │         └─ DOJump(targetPos, jumpPower, 1, scatterDuration)  // jump/eject out (values read/calculated from config)
  │
  └─ Notify ICollectibleDropNotifier for each spawned collectible
```

## Verification Plan

### Automated Tests

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification (Unity Editor)

- Create a `DropAnimationConfiguration` ScriptableObject in the project folder (e.g. `Assets/ScriptableObjects/`) and configure the animation values.
- Bind the created `DropAnimationConfiguration` asset inside the `DefaultGameplaySceneInstaller` in the gameplay scene.
- Remove `CollectibleItemsSpawner` component from the gameplay scene.
- Remove the serialized `_collectibleItemsSpawner` reference from `DefaultGameplaySceneInstaller` in the scene.
- Add `EnemyDropHandler` component to each enemy prefab, configure drop entries (SkillCrate prefab, chance, Y offset).
- Play the scene:
  - Verify enemies drop configured collectibles on death.
  - Verify drop animation uses the configured parameters from the ScriptableObject (scale from zero, eject outward via jump with randomized curves).
  - Verify multiple drops spread in different directions.
  - Verify collecting a dropped skill crate triggers the skill upgrade UI.
  - Verify no grid occupancy errors or null references.
  - Verify that collectibles are not spawned on impassable cells (e.g. walls, water) when enemies die near them. Test deaths right next to obstacles to ensure drops scatter safely to walkable areas.
