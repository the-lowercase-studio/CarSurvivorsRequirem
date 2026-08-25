using UnityEngine;

namespace Assets.Scripts.Extensions
{
    public static class LayerMaskExtensions
    {
        public static LayerMask LayerToMask(this LayerMask _, int layer)
        {
            return LayerMask.GetMask(LayerMask.LayerToName(layer));
        }

        public static bool ContainsLayer(this LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        public static bool Contains(this LayerMask mask, GameObject gameObject)
        {
            return gameObject != null && mask.ContainsLayer(gameObject.layer);
        }
    }
}
