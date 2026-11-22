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
        private bool _isCollected;

        public GameObject GameObject { get; private set; }

        public event EventHandler OnCollected;
        public event EventHandler OnCanBeReleased;

        private void Awake()
        {
            GameObject = gameObject;
        }

        public void OnGet()
        {
            _isCollected = false;
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
            if (_isCollected || (1 << other.gameObject.layer) != EntityLayers.Player)
            {
                return;
            }

            _isCollected = true;
            OnCollected?.Invoke(this, EventArgs.Empty);
            ReturnToPool();
        }
    }
}
