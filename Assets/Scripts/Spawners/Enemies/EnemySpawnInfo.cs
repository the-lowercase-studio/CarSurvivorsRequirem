using Assets.Scripts.Enemies;
using Assets.Scripts.Spawners;
using System;

namespace Assets.Scripts.Spawners.Enemies
{
    [Serializable]
    public class EnemySpawnInfo
    {
        public Enemy EnemyPrefab;
        public ushort MaxAmount;
        public SpawnChanceInfo SpawnChanceInfo;
    }
}
