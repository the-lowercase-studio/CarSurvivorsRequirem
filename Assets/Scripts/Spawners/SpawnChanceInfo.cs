using System;
using UnityEngine;

namespace Assets.Scripts.Spawners
{
    [Serializable]
    public class SpawnChanceInfo
    {
        public float SpawnChance;
        public float TresholdToStartAddingSpawnChanceToOtherInfos;
        public bool SpawnChanceWillNotChange;
        [HideInInspector] public bool HasEverReachedThreshold;

        public bool HasReachedTresholdToStartAddingSpawnChanceToOtherInfos()
        {
            return !SpawnChanceWillNotChange && SpawnChance >= TresholdToStartAddingSpawnChanceToOtherInfos;
        }
    }
}
