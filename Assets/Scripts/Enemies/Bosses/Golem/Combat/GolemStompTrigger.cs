using System.Collections.Generic;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Combat
{
    public class GolemStompTrigger : MonoBehaviour
    {
        [SerializeField] private Collider _stompCollider;

        private readonly HashSet<Collider> _overlappingPlayers = new HashSet<Collider>();

        public Collider StompCollider
        {
            get
            {
                return _stompCollider;
            }
        }

        public bool HasPlayerInside
        {
            get
            {
                return _overlappingPlayers.Count > 0;
            }
        }

        public IReadOnlyCollection<Collider> OverlappingPlayers
        {
            get
            {
                return _overlappingPlayers;
            }
        }

        private void Awake()
        {
            _stompCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((1 << other.gameObject.layer & EntityLayers.Player) != 0)
            {
                _overlappingPlayers.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if ((1 << other.gameObject.layer & EntityLayers.Player) != 0)
            {
                _overlappingPlayers.Remove(other);
            }
        }

        public void ApplyStompDamage(float damage)
        {
            _overlappingPlayers.RemoveWhere(col => col == null);

            if (_overlappingPlayers.Count > 0)
            {
                foreach (Collider playerCollider in _overlappingPlayers)
                {
                    if (playerCollider != null)
                    {
                        EntityManipulationHelper.Damage(playerCollider, damage);
                    }
                }
                return;
            }

            Collider[] hits = Physics.OverlapBox(
                _stompCollider.bounds.center,
                _stompCollider.bounds.extents,
                Quaternion.identity,
                EntityLayers.Player,
                QueryTriggerInteraction.Collide
            );

            foreach (Collider hit in hits)
            {
                if (hit != null)
                {
                    EntityManipulationHelper.Damage(hit, damage);
                }
            }
        }
    }
}

