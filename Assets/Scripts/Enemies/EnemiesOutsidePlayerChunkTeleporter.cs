using Assets.Scripts.Extensions;
using Assets.Scripts.Navigation.GridSystem;
using Reflex.Attributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies
{
    public class EnemiesOutsidePlayerChunkTeleporter : MonoBehaviour
    {
        [Inject] private readonly IGridManager _gridManager;

        [SerializeField] private Transform _enemiesHolder;
        [SerializeField] private float _checkForEnemiesOutsidePlayerChunkDelay = 2f;

        private void Start()
        {
            InvokeRepeating(
                nameof(TeleportEnemiesFromOutsideToInsidePlayerChunk),
                _checkForEnemiesOutsidePlayerChunkDelay,
                _checkForEnemiesOutsidePlayerChunkDelay);
        }

        public void TeleportEnemiesFromOutsideToInsidePlayerChunk()
        {
            Enemy[] enemiesOutsidePlayerChunk = GetEnemiesOutsidePlayerChunk();

            if (enemiesOutsidePlayerChunk.Length == 0)
            {
                return;
            }

            List<Cell> cells = GridCellsNotVisibleByMainCamera
                .GetWalkableCells(_gridManager.GridPlayerChunk)
                .Shuffle()
                .ToList();

            if (cells.Count == 0)
            {
                return;
            }

            int cellIndex = 0;

            foreach (Enemy enemy in enemiesOutsidePlayerChunk)
            {
                Cell randomCell = cells[cellIndex];

                enemy.transform.position = randomCell.WorldPos;

                cellIndex = (cellIndex + 1) % cells.Count;
            }
        }

        private Enemy[] GetEnemiesOutsidePlayerChunk()
        {
            List<Enemy> enemies = new List<Enemy>();

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

            return enemies.ToArray();
        }
    }
}
