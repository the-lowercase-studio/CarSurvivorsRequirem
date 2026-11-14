using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Spawners.Constants;
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
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly Reflex.Core.Container _container = null;

        [SerializeField] private Transform _spawnParent;
        [Tooltip("Extra clearance in meters beyond each Tower Boss arena radius where interactables cannot spawn.")]
        [SerializeField] private float _towerArenaExclusionBuffer = MapInteractablesSpawnerConstants.DEFAULT_TOWER_ARENA_EXCLUSION_BUFFER;
        [SerializeField] private List<InteractableSpawnRule> _spawnRules;

        private IBossEncounterService _bossEncounterService;

        private void Start()
        {
            if (_gridManager == null || _gridManager.WorldGrid == null)
            {
                throw new InvalidOperationException($"[{nameof(MapInteractablesSpawner)}] IGridManager or WorldGrid is unavailable.");
            }

            if (_playerManager == null || _playerManager.GameObject == null)
            {
                throw new InvalidOperationException($"[{nameof(MapInteractablesSpawner)}] IPlayerManager or player GameObject is unavailable.");
            }

            if (_bossEncounterService == null && _container != null && _container.HasBinding<IBossEncounterService>())
            {
                _bossEncounterService = _container.Resolve<IBossEncounterService>();
            }

            NavigationGrid worldGrid = _gridManager.WorldGrid;
            List<Cell> walkableCells = CollectValidWalkableCells(worldGrid);

            if (walkableCells.Count == 0)
            {
                Debug.LogWarning("No valid walkable cells found outside initial player chunk and boss arenas. Spawning aborted.");
                return;
            }

            ShuffleList(walkableCells);

            List<Vector2Int> allSpawnedCoordinates = new List<Vector2Int>();

            foreach (InteractableSpawnRule rule in _spawnRules)
            {
                if (rule.Prefab == null)
                {
                    continue;
                }

                SpawnRuleInteractables(worldGrid, walkableCells, rule, allSpawnedCoordinates);
            }
        }

        private List<Cell> CollectValidWalkableCells(NavigationGrid worldGrid)
        {
            int width = worldGrid.Width;
            int height = worldGrid.Height;
            Cell playerInitialCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(worldGrid, _playerManager.GameObject.transform.position);

            GetInitialPlayerChunkBounds(
                worldGrid,
                _gridManager.GridPlayerChunk,
                playerInitialCell,
                out int minChunkX,
                out int maxChunkX,
                out int minChunkY,
                out int maxChunkY);

            List<Cell> walkableCells = new List<Cell>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Cell cell = worldGrid.Cells[x, y];
                    if (cell == null || !CellStatusDescriber.IsWalkable(cell))
                    {
                        continue;
                    }

                    if (x >= minChunkX && x <= maxChunkX && y >= minChunkY && y <= maxChunkY)
                    {
                        continue;
                    }

                    if (_bossEncounterService != null &&
                        _bossEncounterService.IsPositionWithinAnyTowerArena(cell.WorldPos, _towerArenaExclusionBuffer))
                    {
                        continue;
                    }

                    walkableCells.Add(cell);
                }
            }

            return walkableCells;
        }

        private static void GetInitialPlayerChunkBounds(
            NavigationGrid worldGrid,
            NavigationGrid playerChunk,
            Cell playerInitialCell,
            out int minChunkX,
            out int maxChunkX,
            out int minChunkY,
            out int maxChunkY)
        {
            if (playerInitialCell == null)
            {
                minChunkX = 0;
                maxChunkX = -1;
                minChunkY = 0;
                maxChunkY = -1;
                return;
            }

            if (playerChunk != null && playerChunk.Width > 0 && playerChunk.Height > 0)
            {
                int chunkWidth = playerChunk.Width;
                int chunkHeight = playerChunk.Height;
                int halfWidth = chunkWidth >> 1;
                int halfHeight = chunkHeight >> 1;

                minChunkX = Mathf.Clamp(playerInitialCell.WorldGridPos.x - halfWidth, 0, Mathf.Max(0, worldGrid.Width - chunkWidth));
                maxChunkX = Mathf.Min(worldGrid.Width - 1, minChunkX + chunkWidth - 1);
                minChunkY = Mathf.Clamp(playerInitialCell.WorldGridPos.y - halfHeight, 0, Mathf.Max(0, worldGrid.Height - chunkHeight));
                maxChunkY = Mathf.Min(worldGrid.Height - 1, minChunkY + chunkHeight - 1);
                return;
            }

            minChunkX = playerInitialCell.WorldGridPos.x;
            maxChunkX = playerInitialCell.WorldGridPos.x;
            minChunkY = playerInitialCell.WorldGridPos.y;
            maxChunkY = playerInitialCell.WorldGridPos.y;
        }

        private void SpawnRuleInteractables(
            NavigationGrid worldGrid,
            List<Cell> walkableCells,
            InteractableSpawnRule rule,
            List<Vector2Int> allSpawnedCoordinates)
        {
            int targetSpawnCount = UnityEngine.Random.Range(rule.MinSpawnCount, rule.MaxSpawnCount + 1);
            int spawnedCount = 0;
            List<Vector2Int> spawnedCoordinates = new List<Vector2Int>();

            foreach (Cell cell in walkableCells)
            {
                if (spawnedCount >= targetSpawnCount)
                {
                    break;
                }

                if (IsNearImpassable(worldGrid, cell, rule.MinDistanceToImpassable))
                {
                    continue;
                }

                if (IsTooCloseToAnyPosition(cell.WorldGridPos, allSpawnedCoordinates, rule.MinDistanceToOtherInteractable))
                {
                    continue;
                }

                if (IsTooCloseToAnyPosition(cell.WorldGridPos, spawnedCoordinates, rule.MinDistanceToSameType))
                {
                    continue;
                }

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

        private static bool IsNearImpassable(NavigationGrid worldGrid, Cell cell, int minDistance)
        {
            int width = worldGrid.Width;
            int height = worldGrid.Height;

            for (int dx = -minDistance; dx <= minDistance; dx++)
            {
                for (int dy = -minDistance; dy <= minDistance; dy++)
                {
                    int checkX = cell.WorldGridPos.x + dx;
                    int checkY = cell.WorldGridPos.y + dy;
                    if (checkX < 0 || checkX >= width || checkY < 0 || checkY >= height ||
                        worldGrid.Cells[checkX, checkY] == null || !CellStatusDescriber.IsWalkable(worldGrid.Cells[checkX, checkY]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsTooCloseToAnyPosition(Vector2Int cellPos, List<Vector2Int> existingPositions, int minDistance)
        {
            foreach (Vector2Int spawnedPos in existingPositions)
            {
                if (Mathf.Abs(cellPos.x - spawnedPos.x) <= minDistance &&
                    Mathf.Abs(cellPos.y - spawnedPos.y) <= minDistance)
                {
                    return true;
                }
            }

            return false;
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
