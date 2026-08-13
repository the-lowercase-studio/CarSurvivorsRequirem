using Assets.Scripts.Navigation.Constants;
using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public class Cell
    {
        public Vector3 WorldPos { get; private set; }
        public Vector2Int WorldGridPos { get; private set; }
        public Vector2Int ChunkGridPos { get; set; }
        public byte Cost { get; set; }
        public ushort BestCost { get; set; }
        public GridDirection BestDirection { get; set; } = GridDirection.None;

        public Cell(Vector3 worldPos, Vector2Int gridPos, Vector2Int chunkGridPos)
        {
            WorldPos = worldPos;
            WorldGridPos = gridPos;
            ChunkGridPos = chunkGridPos;
            Cost = GridConstants.DEFAULT_FIELD_COST;
            BestCost = ushort.MaxValue;
        }

        public void IncreaseCost(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (Cost + amount < byte.MaxValue)
            {
                Cost += (byte)amount;
            }
            else
            {
                Cost = byte.MaxValue;
            }
        }

        public void ResetCosts()
        {
            Cost = GridConstants.DEFAULT_FIELD_COST;
            BestCost = ushort.MaxValue;
        }
    }
}
