using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Constants
{
    public static class MortarTowerConstants
    {
        public const float DEFAULT_MAX_HEALTH = 5000f;
        public const float DEFAULT_ENRAGE_PERCENT = 0.40f;
        public const float DEFAULT_ARENA_RADIUS = 22f;
        public const float DEFAULT_LEASH_TIME = 2.5f;

        public static readonly Vector3 NORMAL_FOLLOW_OFFSET = new Vector3(0f, 11f, -7f);
        public static readonly Vector3 COMBAT_FOLLOW_OFFSET = new Vector3(0f, 17f, -13f);
        public const float CAMERA_TWEEN_DURATION = 0.8f;

        public const int PROJECTILE_POOL_CAPACITY = 24;
        public const int PROJECTILE_POOL_MAX_SIZE = 64;

        public const string SFX_MORTAR_FIRE = "MortarFire";
        public const string SFX_MORTAR_IMPACT = "MortarImpact";
        public const string SFX_ENRAGE_ROAR = "MortarEnrage";
        public const string SFX_LEASH_TICK = "LeashTick";

        public const string BASE_COLOR_PROPERTY = "_BaseColor";
        public const string EMISSION_COLOR_PROPERTY = "_EmissionColor";
    }
}
