using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Spawners.GridSpace;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Skills.ObjectsImpactingSkills.Crate
{
    public class CollectibleItemsSpawner : MonoBehaviour,
        IOnRandomGridPosSpawner<CollectibleItemsSpawner>
    {
        [Serializable]
        private struct CollectibleItemSpawnData
        {
            [SerializeField] private GameObject _prefab;
            [SerializeField] private float _spawnYOffset;
            [SerializeField] private float _spawnChance;

            public GameObject Prefab => _prefab;
            public float SpawnYOffset => _spawnYOffset;
            public float SpawnChance => _spawnChance;
        }

        [Inject] private readonly IGridManager _gridManager;

        [SerializeField] private byte _maxSpawnedCollectiblesCount = 6;
        [SerializeField] private Transform _collectibleItemsParent;
        [SerializeField] private float _spawnDelay = 8f;
        [SerializeField] private CollectibleItemSpawnData[] _collectibleItemsSpawnData;
        private List<ICollectible> _spawnedCollectibleItems;

        public uint CurrentlySpawnedObjectsCount { get; private set; }

        public event EventHandler OnSpawnedEntityReleased;

        private void Start()
        {
            _spawnedCollectibleItems = new List<ICollectible>(_maxSpawnedCollectiblesCount);
            InvokeRepeating(nameof(SpawnSingleCollectible), _spawnDelay, _spawnDelay);
        }

        public void SpawnAtRandomGridPos(int count)
        {
            for (int i = 0; i < count && _spawnedCollectibleItems.Count < _maxSpawnedCollectiblesCount; i++)
            {
                Cell drawnCell = RandomWalkableCellsFinder
                    .FindCellWithoutCollectible(_gridManager.WorldGrid);

                if (drawnCell == null)
                {
                    Debug.LogError("No walkable cell without collectible found for spawning a skill crate.");
                    return;
                }

                CollectibleItemSpawnData? collectibleItemSpawnData = RandomCollectibleItemBasedOnSpawnChance();
                if (collectibleItemSpawnData != null)
                {
                    GameObject collectibleObject = Instantiate(
                        collectibleItemSpawnData.Value.Prefab,
                        new Vector3(drawnCell.WorldPos.x, collectibleItemSpawnData.Value.SpawnYOffset, drawnCell.WorldPos.z),
                        Quaternion.identity,
                        _collectibleItemsParent
                    );

                    if (collectibleObject.TryGetComponent(out ICollectible collectible))
                    {
                        collectible.OnCollected += Collectible_OnCollected;

                        _spawnedCollectibleItems.Add(collectible);

                        drawnCell.IsOccupiedByCollectible = true;
                    }
                }

                CurrentlySpawnedObjectsCount++;
            }
        }

        private void SpawnSingleCollectible()
        {
            SpawnAtRandomGridPos(1);
        }

        private CollectibleItemSpawnData? RandomCollectibleItemBasedOnSpawnChance()
        {
            float totalChance = _collectibleItemsSpawnData.Sum(info => info.SpawnChance);
            float randomPoint = UnityEngine.Random.value * totalChance;

            float currentSum = 0;
            foreach (CollectibleItemSpawnData collectibleItem in _collectibleItemsSpawnData)
            {
                currentSum += collectibleItem.SpawnChance;
                if (currentSum >= randomPoint)
                {
                    return collectibleItem;
                }
            }

            return null;
        }

        private void Collectible_OnCollected(object sender, EventArgs e)
        {
            var collectibleGameObject = (sender as ICollectible)?.GameObject;

            if (collectibleGameObject != null && collectibleGameObject.TryGetComponent(out ICollectible collectible))
            {
                ReleaseOccupiedCellByCollectible(collectibleGameObject);

                _spawnedCollectibleItems.Remove(collectible);

                OnSpawnedEntityReleased?.Invoke(collectible, EventArgs.Empty);

                CurrentlySpawnedObjectsCount--;
            }
        }

        private void ReleaseOccupiedCellByCollectible(GameObject collectable)
        {
            Cell occupiedCell = WorldPosToCellConverter
                 .GetCellFromGridByWorldPos(_gridManager.WorldGrid, collectable.transform.position);

            if (occupiedCell != null)
            {
                occupiedCell.IsOccupiedByCollectible = false;
            }
        }
    }
}
