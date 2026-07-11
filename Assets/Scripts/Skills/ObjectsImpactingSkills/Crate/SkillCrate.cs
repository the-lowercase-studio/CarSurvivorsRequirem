using Assets.Scripts.LayerMasks;
using Assets.Scripts.Pooling;
using DG.Tweening;
using System;
using UnityEngine;

namespace Assets.Scripts.Skills.ObjectsImpactingSkills.Crate
{
    public interface ISkillUpgradeCollectible : ICollectible
    {
    }

    public class SkillCrate : MonoBehaviour, ISkillUpgradeCollectible, IPoolable
    {
        public GameObject GameObject { get; private set; }

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
            if (1 << other.gameObject.layer == EntityLayers.Player)
            {
                OnCollected?.Invoke(this, EventArgs.Empty);
                ReturnToPool();
            }
        }
    }
}
