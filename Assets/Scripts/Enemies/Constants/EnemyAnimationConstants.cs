namespace Assets.Scripts.Enemies.Constants
{
    public static class EnemyAnimationConstants
    {
        public const string ANIM_PARAM_SPEED = "Speed";
        public const string ANIM_PARAM_IS_ON_GROUND = "IsOnGround";
        public const string ANIM_PARAM_IS_MOVING_BY_CRAWLING = "IsMovingByCrawling";
        public const string ANIM_TRIGGER_ATTACK = "Attack";

        public const string ANIM_STATE_ZOMBIE_ATTACK_STANDING = "Zombie Attack Standing";
        public const string ANIM_STATE_ZOMBIE_BITING_CRAWLING = "Zombie Biting Crawling";
        public const string ANIM_STATE_ATTACK = "Attack";
        public const string ANIM_STATE_FALLING = "Falling";
        public const string ANIM_STATE_FALL_OVER = "FallOver";

        public const float MAX_ATTACK_ANIMATION_DURATION = 3.5f;
    }
}
