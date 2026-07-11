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
            var chosenDrops = new System.Collections.Generic.List<CollectibleDropEntry>();
            for (int i = 0; i < _dropEntries.Length; i++)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= _dropEntries[i].DropChancePercent)
                {
                    chosenDrops.Add(_dropEntries[i]);
                }
            }

            int totalDrops = chosenDrops.Count;
            if (totalDrops == 0)
            {
                return;
            }

            Vector3 basePos = transform.position;

            for (int i = 0; i < totalDrops; i++)
            {
                CollectibleDropEntry drop = chosenDrops[i];
                float angle = (i * 360f / totalDrops) + UnityEngine.Random.Range(-15f, 15f);
                float radian = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(radian), 0f, Mathf.Sin(radian));
                Vector3 targetPos = basePos + direction * _animationConfig.ScatterRadius;

                // Ensure target position is on a walkable cell
                targetPos = GetWalkablePosition(basePos, targetPos);

                Vector3 spawnPos = basePos + Vector3.up * drop.YOffset;
                targetPos.y = spawnPos.y;

                _dropNotifier.SpawnCollectible(
                    drop.Prefab, 
                    spawnPos, 
                    targetPos
                );
            }
        }

        private Vector3 GetWalkablePosition(Vector3 startPos, Vector3 targetPos)
        {
            var grid = _gridManager.WorldGrid;

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
