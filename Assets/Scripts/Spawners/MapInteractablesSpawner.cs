using Assets.Scripts.Navigation.GridSystem;
using Reflex.Attributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Spawners
{
    [Serializable]
    public class InteractableSpawnRule
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private int _minSpawnCount;
        [SerializeField] private int _maxSpawnCount;
        [SerializeField] private int _minDistanceToImpassable;
        [SerializeField] private int _minDistanceToOtherInteractable = 3;
        [SerializeField] private int _minDistanceToSameType;

        public GameObject Prefab => _prefab;
        public int MinSpawnCount => _minSpawnCount;
        public int MaxSpawnCount => _maxSpawnCount;
        public int MinDistanceToImpassable => _minDistanceToImpassable;
        public int MinDistanceToOtherInteractable => _minDistanceToOtherInteractable;
        public int MinDistanceToSameType => _minDistanceToSameType;
    }

    public class MapInteractablesSpawner : MonoBehaviour
    {
        [Inject] private readonly IGridManager _gridManager;
        [Inject] private readonly Reflex.Core.Container _container;

        [SerializeField] private Transform _spawnParent;
        [SerializeField] private List<InteractableSpawnRule> _spawnRules;

        private void Start()
        {
            if (_gridManager == null || _gridManager.WorldGrid == null)
            {
                Debug.LogWarning("GridManager or WorldGrid is null. Cannot spawn map interactables.");
                return;
            }

            NavigationGrid worldGrid = _gridManager.WorldGrid;
            int width = worldGrid.Width;
            int height = worldGrid.Height;

            // Collect all walkable cells
            List<Cell> walkableCells = new List<Cell>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Cell cell = worldGrid.Cells[x, y];
                    if (cell != null && CellStatusDescriber.IsWalkable(cell))
                    {
                        walkableCells.Add(cell);
                    }
                }
            }

            if (walkableCells.Count == 0)
            {
                Debug.LogWarning("No walkable cells found on grid. Spawning aborted.");
                return;
            }

            // Shuffle walkable cells
            ShuffleList(walkableCells);

            List<Vector2Int> allSpawnedCoordinates = new List<Vector2Int>();

            // Execute each rule
            foreach (InteractableSpawnRule rule in _spawnRules)
            {
                if (rule.Prefab == null)
                {
                    continue;
                }

                int targetSpawnCount = UnityEngine.Random.Range(rule.MinSpawnCount, rule.MaxSpawnCount + 1);
                int spawnedCount = 0;
                List<Vector2Int> spawnedCoordinates = new List<Vector2Int>();

                foreach (Cell cell in walkableCells)
                {
                    if (spawnedCount >= targetSpawnCount)
                    {
                        break;
                    }

                    // Impassable Distance Check
                    bool isNearImpassable = false;
                    for (int dx = -rule.MinDistanceToImpassable; dx <= rule.MinDistanceToImpassable; dx++)
                    {
                        for (int dy = -rule.MinDistanceToImpassable; dy <= rule.MinDistanceToImpassable; dy++)
                        {
                            int checkX = cell.WorldGridPos.x + dx;
                            int checkY = cell.WorldGridPos.y + dy;
                            if (checkX < 0 || checkX >= width || checkY < 0 || checkY >= height ||
                                worldGrid.Cells[checkX, checkY] == null || !CellStatusDescriber.IsWalkable(worldGrid.Cells[checkX, checkY]))
                            {
                                isNearImpassable = true;
                                break;
                            }
                        }
                        if (isNearImpassable)
                        {
                            break;
                        }
                    }

                    if (isNearImpassable)
                    {
                        continue;
                    }

                    // Proximity to Other Interactables Check
                    bool isTooCloseToOtherInteractables = false;
                    foreach (Vector2Int spawnedPos in allSpawnedCoordinates)
                    {
                        if (Mathf.Abs(cell.WorldGridPos.x - spawnedPos.x) <= rule.MinDistanceToOtherInteractable &&
                            Mathf.Abs(cell.WorldGridPos.y - spawnedPos.y) <= rule.MinDistanceToOtherInteractable)
                        {
                            isTooCloseToOtherInteractables = true;
                            break;
                        }
                    }

                    if (isTooCloseToOtherInteractables)
                    {
                        continue;
                    }

                    // Proximity to Same Type Check
                    bool isTooCloseToSameType = false;
                    foreach (Vector2Int spawnedPos in spawnedCoordinates)
                    {
                        if (Mathf.Abs(cell.WorldGridPos.x - spawnedPos.x) <= rule.MinDistanceToSameType &&
                            Mathf.Abs(cell.WorldGridPos.y - spawnedPos.y) <= rule.MinDistanceToSameType)
                        {
                            isTooCloseToSameType = true;
                            break;
                        }
                    }

                    if (isTooCloseToSameType)
                    {
                        continue;
                    }

                    // Perform spawn
                    Quaternion randomRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    GameObject spawnedObject = Instantiate(rule.Prefab, cell.WorldPos, randomRotation, _spawnParent);
                    Reflex.Injectors.GameObjectInjector.InjectRecursive(spawnedObject, _container);
                    spawnedCoordinates.Add(cell.WorldGridPos);
                    allSpawnedCoordinates.Add(cell.WorldGridPos);
                    spawnedCount++;
                }

                if (spawnedCount < targetSpawnCount)
                {
                    Debug.LogWarning($"Could not spawn target count ({targetSpawnCount}) for prefab '{rule.Prefab.name}'. Only spawned {spawnedCount} due to layout constraints.");
                }
            }
        }

        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
