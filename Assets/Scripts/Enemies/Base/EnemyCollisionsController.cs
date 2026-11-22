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

        private Enemy _enemy;
        private List<Collider> _colliders;

        public event EventHandler<CollisionEventArgs> OnCollisionWithPlayer;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
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
            if (_enemy != null && _enemy.IsMaterializing)
            {
                return;
            }

            RaycastHit[] hits = Physics.SphereCastAll(transform.position, _collisionRadius, Vector3.up, Mathf.Infinity, EntityLayers.Player);
            foreach (var hit in hits)
            {
                Collider collider = hit.collider;
                if (collider != null && !_colliders.Contains(collider))
                {
                    OnCollisionWithPlayer?.Invoke(this, new CollisionEventArgs(collider));
                }
            }
        }
    }
}
