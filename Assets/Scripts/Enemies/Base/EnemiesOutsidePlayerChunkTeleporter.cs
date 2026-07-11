using Assets.Scripts.Navigation.GridSystem;
using Reflex.Attributes;
using System.Collections.Generic;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Base
{
    public class EnemiesOutsidePlayerChunkTeleporter : MonoBehaviour
    {
        [Inject] private readonly IGridManager _gridManager;
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
                Cell randomCell = _hiddenWalkableCells[cellIndex];

                enemy.transform.position = randomCell.WorldPos;

                cellIndex = (cellIndex + 1) % _hiddenWalkableCells.Count;
            }
        }

        private void FillEnemiesOutsidePlayerChunk(List<Enemy> enemies)
        {
            enemies.Clear();

            NavigationGrid playerChunk = _gridManager.GridPlayerChunk;
            Cell centerCell = playerChunk.Cells[playerChunk.Width / 2, playerChunk.Height / 2];
            Vector3 center = centerCell.WorldPos;

            float width = playerChunk.Width * 0.5f * playerChunk.CellSize;
            float height = playerChunk.Height * 0.5f * playerChunk.CellSize;

            foreach (Transform child in _enemiesHolder)
            {
                if (child.TryGetComponent(out Enemy enemy))
                {
                    Vector3 enemyPos = enemy.transform.position;
                    if (Mathf.Abs(enemyPos.x - center.x) > width || Mathf.Abs(enemyPos.z - center.z) > height)
                    {
                        enemies.Add(enemy);
                    }
                }
            }
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
