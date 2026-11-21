using UnityEngine;

namespace Assets.Scripts.StatusEffects
{
    public interface IPositionalDamageable : IDamageable
    {
        void TakeDamage(float damage, Vector3 hitWorldPosition);
    }

    public interface IDamageable
    {
        void TakeDamage(float damage);
        void TakeFullHpDamage();
    }
}

