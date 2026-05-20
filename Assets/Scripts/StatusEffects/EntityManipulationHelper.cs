using UnityEngine;

namespace Assets.Scripts.StatusEffects
{
    public static class EntityManipulationHelper
    {
        public static void Damage(Collider target, float damage)
        {
            if (target is null)
            {
                return;
            }

            if (target.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
            }
        }

        public static void Knockback(Collider target, Vector3 dir, float range, float timeToArriveAtLocation)
        {
            if (target is null)
            {
                return;
            }

            if (target.TryGetComponent(out IKnockable knockable))
            {
                dir.y = 0;
                knockable.ApplyKnockBack(dir, range, timeToArriveAtLocation);
            }
        }

        public static void Stun(Collider target, float duration)
        {
            if (target is null)
            {
                return;
            }

            if (target.TryGetComponent(out IStunnable stunnable))
            {
                stunnable.ApplyStun(duration);
            }
        }
    }
}
