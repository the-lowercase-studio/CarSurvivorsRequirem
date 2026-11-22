using UnityEngine;

namespace Assets.Scripts.Enemies.Constants
{
    public static class EnemyVisualConstants
    {
        public static readonly int SHINE_ENABLED_PROPERTY_ID = Shader.PropertyToID("_ShineEnabled");
        public static readonly int MATERIALIZE_ENABLED_PROPERTY_ID = Shader.PropertyToID("_MaterializeEnabled");
        public static readonly int MATERIALIZE_PROGRESS_PROPERTY_ID = Shader.PropertyToID("_MaterializeProgress");
        public static readonly int MATERIALIZE_MIN_Y_PROPERTY_ID = Shader.PropertyToID("_MaterializeMinY");
        public static readonly int MATERIALIZE_HEIGHT_PROPERTY_ID = Shader.PropertyToID("_MaterializeHeight");

        public const float DEFAULT_MATERIALIZE_HEIGHT = 1.6f;
        public const float MATERIALIZE_BOUNDS_MARGIN = 0.05f;
        public const float MIN_MATERIALIZE_HEIGHT = 0.1f;
    }
}
