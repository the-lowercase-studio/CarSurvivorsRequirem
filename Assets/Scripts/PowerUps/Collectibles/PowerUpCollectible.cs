using System;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Pooling;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Collectibles
{
    public interface IPowerUpCollectible : ICollectible
    {
        PowerUpType Type { get; }
    }

    public class PowerUpCollectible : MonoBehaviour, IPowerUpCollectible, IPoolable
    {
        [SerializeField] private PowerUpType _type;

        public GameObject GameObject { get; private set; }
        public PowerUpType Type => _type;

        public event EventHandler OnCollected;
        public event EventHandler OnCanBeReleased;

        private void Awake()
        {
            GameObject = gameObject;
        }

        public void OnGet()
        {
        }

        public void OnRelease()
        {
            transform.DOKill();
        }

        public void ReturnToPool()
        {
            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((1 << other.gameObject.layer) == EntityLayers.Player)
            {
                OnCollected?.Invoke(this, EventArgs.Empty);
                ReturnToPool();
            }
        }
    }
}
