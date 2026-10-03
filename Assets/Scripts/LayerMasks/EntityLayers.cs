using UnityEngine;
using static Assets.Scripts.LayerMasks.Constants.LayerMaskConstants;

namespace Assets.Scripts.LayerMasks
{
    public static class EntityLayers
    {
        public static readonly LayerMask Enemy = LayerMask.GetMask(ENEMY);
        public static readonly LayerMask Boss = LayerMask.GetMask(BOSS);
        public static readonly LayerMask Enemies = LayerMask.GetMask(ENEMY, BOSS);
        public static readonly LayerMask Player = LayerMask.GetMask(PLAYER);
        public static readonly LayerMask All = LayerMask.GetMask(ENEMY, BOSS, PLAYER);
    }
}
