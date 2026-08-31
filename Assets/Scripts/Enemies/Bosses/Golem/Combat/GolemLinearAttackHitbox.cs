using System;
using System.Collections.Generic;
using DG.Tweening;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Combat
{
    public interface IGolemLinearAttackHitbox
    {
        bool IsActive { get; }
        void Activate(Vector3 origin, Vector3 direction, float width, float height, float depth, float verticalOffset, float maxDistance, float speed, float damage, Action onComplete = null);
        void Deactivate();
    }

    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public class GolemLinearAttackHitbox : MonoBehaviour, IGolemLinearAttackHitbox
    {
        [SerializeField] private BoxCollider _boxCollider;
        [SerializeField] private Rigidbody _rigidbody;

        private readonly HashSet<Collider> _hitColliders = new HashSet<Collider>();
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private Transform _initialParent;
        private Sequence _activeSequence;
        private float _damage;
        private float _verticalOffset;
        private float _width;
        private float _height;
        private float _depth;
        private bool _isActive;

        public bool IsActive
        {
            get
            {
                return _isActive;
            }
        }

        private void Awake()
        {
            _initialParent = transform.parent;
            _boxCollider = GetComponent<BoxCollider>();
            _rigidbody = GetComponent<Rigidbody>();
            _boxCollider.isTrigger = true;
            _boxCollider.enabled = false;
            _rigidbody.isKinematic = true;
        }

        private void OnDisable()
        {
            KillActiveSequence();
            _isActive = false;
            _hitColliders.Clear();
            if (_boxCollider != null)
            {
                _boxCollider.enabled = false;
            }
        }

        private void OnDestroy()
        {
            KillActiveSequence();
            _isActive = false;
            _hitColliders.Clear();
        }

        public void Activate(
            Vector3 origin,
            Vector3 direction,
            float width,
            float height,
            float depth,
            float verticalOffset,
            float maxDistance,
            float speed,
            float damage,
            Action onComplete = null)
        {
            Deactivate();

            _damage = damage;
            _width = width;
            _height = height;
            _depth = depth;
            _verticalOffset = verticalOffset;
            _isActive = true;
            _hitColliders.Clear();

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            if (_initialParent == null)
            {
                _initialParent = transform.parent;
            }

            transform.SetParent(null);
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            if (_boxCollider != null)
            {
                _boxCollider.size = new Vector3(width, height, depth);
                _boxCollider.center = new Vector3(0f, verticalOffset, 0f);
                _boxCollider.enabled = true;
            }

            gameObject.SetActive(true);

            CheckOverlap();

            Vector3 targetPosition = origin + direction * maxDistance;
            float travelTime = maxDistance / Mathf.Max(speed, 0.1f);
            float returnTime = travelTime * 0.8f;

            _activeSequence = DOTween.Sequence();
            _activeSequence.Append(transform.DOMove(targetPosition, travelTime)
                .SetEase(Ease.OutQuad)
                .OnUpdate(CheckOverlap));
            _activeSequence.AppendCallback(() =>
            {
                _hitColliders.Clear();
            });
            _activeSequence.Append(transform.DOMove(origin, returnTime)
                .SetEase(Ease.InQuad)
                .OnUpdate(CheckOverlap));
            _activeSequence.OnComplete(() =>
            {
                Deactivate();
                onComplete?.Invoke();
            });
        }

        public void Deactivate()
        {
            _isActive = false;
            KillActiveSequence();

            if (_boxCollider != null)
            {
                _boxCollider.enabled = false;
            }

            if (_initialParent != null && transform != null && transform.parent != _initialParent)
            {
                if (gameObject.activeInHierarchy && _initialParent.gameObject.activeInHierarchy)
                {
                    transform.SetParent(_initialParent);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                }
            }

            _hitColliders.Clear();
        }

        private void KillActiveSequence()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }
            _activeSequence = null;
        }

        private void CheckOverlap()
        {
            if (!_isActive || _boxCollider == null)
            {
                return;
            }

            Vector3 center = transform.TransformPoint(_boxCollider.center);
            Vector3 halfExtents = new Vector3(_width * 0.5f, _height * 0.5f, _depth * 0.5f);

            int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, transform.rotation, EntityLayers.Player, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit != null && !_hitColliders.Contains(hit))
                {
                    _hitColliders.Add(hit);
                    EntityManipulationHelper.Damage(hit, _damage);
                }
                _overlapBuffer[i] = null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive || other == null)
            {
                return;
            }

            if (((1 << other.gameObject.layer) & EntityLayers.Player) == 0)
            {
                return;
            }

            if (_hitColliders.Contains(other))
            {
                return;
            }

            _hitColliders.Add(other);
            EntityManipulationHelper.Damage(other, _damage);
        }
    }
}
