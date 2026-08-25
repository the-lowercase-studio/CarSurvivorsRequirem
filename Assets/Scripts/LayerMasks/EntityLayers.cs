using UnityEngine;

namespace Assets.Scripts.LayerMasks
{
    public static class EntityLayers
    {
        private const string ENEMY = "Enemy";
        private const string BOSS = "Boss";
        private const string PLAYER = "Player";

        public static readonly LayerMask Enemy = LayerMask.GetMask(ENEMY);
        public static readonly LayerMask Boss = LayerMask.GetMask(BOSS);
        public static readonly LayerMask Enemies = LayerMask.GetMask(ENEMY, BOSS);
        public static readonly LayerMask Player = LayerMask.GetMask(PLAYER);
        public static readonly LayerMask All = LayerMask.GetMask(ENEMY, BOSS, PLAYER);
    }
}
