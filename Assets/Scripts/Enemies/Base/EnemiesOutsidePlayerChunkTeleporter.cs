using Assets.Scripts.Navigation.GridSystem;
using Reflex.Attributes;
using System.Collections.Generic;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Base
{
    public class EnemiesOutsidePlayerChunkTeleporter : MonoBehaviour
    {
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly Camera _mainCamera = null;

        [SerializeField] private Transform _enemiesHolder;
        [SerializeField] private float _checkForEnemiesOutsidePlayerChunkDelay = 2f;

        private readonly List<Enemy> _enemiesOutsidePlayerChunk = new();
        private readonly List<Cell> _hiddenWalkableCells = new();

        private void Start()
        {
            InvokeRepeating(
                nameof(TeleportEnemiesFromOutsideToInsidePlayerChunk),
                _checkForEnemiesOutsidePlayerChunkDelay,
                _checkForEnemiesOutsidePlayerChunkDelay);
        }

        public void TeleportEnemiesFromOutsideToInsidePlayerChunk()
        {
            FillEnemiesOutsidePlayerChunk(_enemiesOutsidePlayerChunk);

            if (_enemiesOutsidePlayerChunk.Count == 0)
            {
                return;
            }

            GridCellsNotVisibleByMainCamera.FillWalkableCells(_gridManager.GridPlayerChunk, _mainCamera, _hiddenWalkableCells);
            ShuffleCells(_hiddenWalkableCells);

            if (_hiddenWalkableCells.Count == 0)
            {
                return;
            }

            int cellIndex = 0;

            foreach (Enemy enemy in _enemiesOutsidePlayerChunk)
            {
                if (!CanRelocate(enemy))
                {
                    continue;
                }
                for (int attempt = 0; attempt < _hiddenWalkableCells.Count; attempt++)
                {
                    Cell cell = _hiddenWalkableCells[cellIndex];
                    cellIndex = (cellIndex + 1) % _hiddenWalkableCells.Count;
                    if (GridCellsNotVisibleByMainCamera.TryGetPlacement(cell, _gridManager.GridPlayerChunk,
                        _mainCamera, -1, out Vector3 position) && CanRelocate(enemy))
                    {
                        enemy.transform.position = position;
                        enemy.MovementController.ResetAfterRelocation();
                        break;
                    }
                }
            }
        }

        private void FillEnemiesOutsidePlayerChunk(List<Enemy> enemies)
        {
            enemies.Clear();

            NavigationGrid playerChunk = _gridManager.GridPlayerChunk;
            if (playerChunk == null || playerChunk.Cells == null || playerChunk.Width <= 0 || playerChunk.Height <= 0)
            {
                return;
            }

            Cell minCell = playerChunk.Cells[0, 0];
            Cell maxCell = playerChunk.Cells[playerChunk.Width - 1, playerChunk.Height - 1];
            if (minCell == null || maxCell == null)
            {
                return;
            }

            float halfCell = playerChunk.CellSize * 0.5f;
            float minX = minCell.WorldPos.x - halfCell;
            float maxX = maxCell.WorldPos.x + halfCell;
            float minZ = minCell.WorldPos.z - halfCell;
            float maxZ = maxCell.WorldPos.z + halfCell;

            foreach (Transform child in _enemiesHolder)
            {
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (child.TryGetComponent(out Enemy enemy) && CanRelocate(enemy))
                {
                    Vector3 enemyPos = enemy.transform.position;
                    if (enemyPos.x < minX || enemyPos.x > maxX || enemyPos.z < minZ || enemyPos.z > maxZ)
                    {
                        enemies.Add(enemy);
                    }
                }
            }
        }

        private static bool CanRelocate(Enemy enemy)
        {
            return enemy != null && enemy.gameObject.activeInHierarchy && enemy.Health.IsAlive()
                && enemy.MovementController.CanBeTeleported;
        }

        private void ShuffleCells(List<Cell> cells)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (cells[i], cells[randomIndex]) = (cells[randomIndex], cells[i]);
            }
        }
    }
}
