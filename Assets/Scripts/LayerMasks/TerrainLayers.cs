using UnityEngine;
using static Assets.Scripts.LayerMasks.Constants.LayerMaskConstants;

namespace Assets.Scripts.LayerMasks
{
    public static class TerrainLayers
    {
        public static readonly LayerMask Impassable = LayerMask.GetMask(IMPASSABLE);
        public static readonly LayerMask Rough = LayerMask.GetMask(ROUGH_TERRAIN);
        public static readonly LayerMask Ground = LayerMask.GetMask(GROUND);
        public static readonly LayerMask Walkable = LayerMask.GetMask(GROUND, ROUGH_TERRAIN);
        public static readonly LayerMask All = LayerMask.GetMask(IMPASSABLE, ROUGH_TERRAIN, GROUND);
    }
}
