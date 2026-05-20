using Assets.Scripts.LayerMasks;
using System;
using UnityEngine;

namespace Assets.Scripts.Skills.ObjectsImpactingSkills.Crate
{
    public class SkillCrate : MonoBehaviour, ICollectible
    {
        public GameObject GameObject { get; private set; }

        public event EventHandler OnCollected;

        private void Start()
        {
            GameObject = gameObject;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (1 << other.gameObject.layer == EntityLayers.Player)
            {
                OnCollected?.Invoke(this, EventArgs.Empty);
                Destroy(gameObject);
            }
        }
    }
}
