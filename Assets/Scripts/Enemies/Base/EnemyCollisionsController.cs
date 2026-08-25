using Assets.Scripts.Collisions;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.Extensions;
using Assets.Scripts.LayerMasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [RequireComponent(typeof(Enemy))]
    public class EnemyCollisionsController : MonoBehaviour, ICollisionsController
    {
        [SerializeField] private float _collisionCheckDelay = 0.05f;
        [SerializeField] private float _collisionRadius = 1f;

        private List<Collider> _colliders;

        public event EventHandler<CollisionEventArgs> OnCollisionWithOtherEnemy;

        public event EventHandler<CollisionEventArgs> OnCollisionWithPlayer;

        private void Awake()
        {
            SetAllColliders();
        }

        private void OnEnable()
        {
            InvokeRepeating(nameof(HandleCollisionsCheck), 0f, _collisionCheckDelay);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(HandleCollisionsCheck));
        }

        private void OnDrawGizmos()
        {
            new Debug().DrawCircle(transform.position, _collisionRadius, EnemyCombatConstants.CIRCLE_DEBUG_SEGMENTS, Color.yellow);
        }

        private void SetAllColliders()
        {
            _colliders = new List<Collider>();
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                {
                    _colliders.Add(collider);
                }
            }
        }

        private void HandleCollisionsCheck()
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, _collisionRadius, Vector3.up, Mathf.Infinity, EntityLayers.All);
            foreach (var hit in hits)
            {
                Collider collider = hit.collider;
                if (hit.collider != null && !_colliders.Contains(hit.collider))
                {
                    if (EntityLayers.Enemies.ContainsLayer(collider.gameObject.layer))
                    {
                        OnCollisionWithOtherEnemy?.Invoke(this, new CollisionEventArgs(collider));
                    }
                    else if (EntityLayers.Player.ContainsLayer(collider.gameObject.layer))
                    {
                        OnCollisionWithPlayer?.Invoke(this, new CollisionEventArgs(collider));
                    }
                }
            }
        }
    }
}
