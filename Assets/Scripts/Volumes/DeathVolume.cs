using Assets.Scripts.Pooling;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Volumes
{
    [RequireComponent(typeof(BoxCollider))]
    public class DeathVolume : MonoBehaviour
    {
        [SerializeField] private BoxCollider _boxCollider;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeFullHpDamage();
            }
            else if (other.TryGetComponent(out IPoolable poolable))
            {
                poolable.ReturnToPool();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.black;
            Gizmos.DrawCube(transform.position, _boxCollider.size);
        }
    }
}
