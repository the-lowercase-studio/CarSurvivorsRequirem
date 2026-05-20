namespace Assets.Scripts.StatusEffects
{
    public interface IDamageable
    {
        public void TakeDamage(float damage);

        public void TakeFullHpDamage();
    }
}
