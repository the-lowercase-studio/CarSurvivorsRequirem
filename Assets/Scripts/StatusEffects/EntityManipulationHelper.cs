using UnityEngine;

namespace Assets.Scripts.StatusEffects
{
    public static class EntityManipulationHelper
    {
        public static void Damage(Collider target, float damage)
        {
            if (target == null || !target.enabled)
            {
                return;
            }

            Damage(target, damage, target.bounds.center);
        }

        public static void Damage(Collider target, float damage, Vector3 hitWorldPosition)
        {
            if (target == null || !target.enabled)
            {
                return;
            }

            if (target.TryGetComponent(out IDamageable damageable) || (damageable = target.GetComponentInParent<IDamageable>()) != null)
            {
                if (damageable is IPositionalDamageable positionalDamageable)
                {
                    positionalDamageable.TakeDamage(damage, hitWorldPosition);
                }
                else
                {
                    damageable.TakeDamage(damage);
                }
            }
        }

        public static void Knockback(Collider target, Vector3 dir, float range, float timeToArriveAtLocation)
        {
            if (target == null || !target.enabled)
            {
                return;
            }

            if (target.TryGetComponent(out IKnockable knockable) || (knockable = target.GetComponentInParent<IKnockable>()) != null)
            {
                dir.y = 0;
                knockable.ApplyKnockBack(dir, range, timeToArriveAtLocation);
            }
        }
    }
}
