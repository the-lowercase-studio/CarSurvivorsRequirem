using System;
using System.Collections.Generic;
using DG.Tweening;
using Assets.Scripts.Audio;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Combat
{
    public interface IMortarBoulderHitbox
    {
        bool IsRolling { get; }
        void Roll(Vector3 startPos, Vector3 endPos, float speed, float damage, Action onComplete);
        void Stop();
    }

    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public class MortarBoulderHitbox : MonoBehaviour, IMortarBoulderHitbox
    {
        [Tooltip("Trigger box collider defining the rolling boulder's damage volume.")]
        [SerializeField] private BoxCollider _boxCollider;
        [Tooltip("Kinematic rigidbody attached to support trigger events.")]
        [SerializeField] private Rigidbody _rigidbody;
        [Tooltip("Visual model transformed with procedural rolling rotation.")]
        [SerializeField] private Transform _visualModel;
        [Tooltip("Audio player for rolling or impact sounds.")]
        [SerializeField] private AudioClipPlayer _audioClipPlayer;
        [Tooltip("VFX player triggered when colliding with the player car.")]
        [SerializeField] private VFXPlayer _hitVfxPlayer;

        private readonly HashSet<Collider> _hitColliders = new HashSet<Collider>();
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private Sequence _rollSequence;
        private Tween _rotationTween;
        private float _damage;
        private bool _isRolling;

        public bool IsRolling
        {
            get
            {
                return _isRolling;
            }
        }

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            _rigidbody = GetComponent<Rigidbody>();
            _boxCollider.isTrigger = true;
            _boxCollider.enabled = false;
            _rigidbody.isKinematic = true;
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        public void Roll(Vector3 startPos, Vector3 endPos, float speed, float damage, Action onComplete)
        {
            Stop();

            _damage = damage;
            _isRolling = true;
            _hitColliders.Clear();

            Vector3 direction = endPos - startPos;
            direction.y = 0f;
            float distance = direction.magnitude;

            if (distance < 0.1f)
            {
                Stop();
                onComplete?.Invoke();
                return;
            }

            direction.Normalize();

            transform.position = startPos;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            if (_boxCollider != null)
            {
                _boxCollider.enabled = true;
            }

            gameObject.SetActive(true);

            float duration = distance / Mathf.Max(speed, 0.1f);

            _rollSequence = DOTween.Sequence();
            _rollSequence.Append(transform.DOMove(endPos, duration).SetEase(Ease.Linear).OnUpdate(CheckOverlap));
            _rollSequence.OnComplete(() =>
            {
                Stop();
                onComplete?.Invoke();
            });

            if (_visualModel != null)
            {
                _rotationTween = _visualModel.DORotate(new Vector3(360f, 0f, 0f), 0.5f, RotateMode.LocalAxisAdd)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetEase(Ease.Linear);
            }
        }

        public void Stop()
        {
            _isRolling = false;

            if (_rollSequence != null && _rollSequence.IsActive())
            {
                _rollSequence.Kill();
            }
            _rollSequence = null;

            if (_rotationTween != null && _rotationTween.IsActive())
            {
                _rotationTween.Kill();
            }
            _rotationTween = null;

            if (_boxCollider != null)
            {
                _boxCollider.enabled = false;
            }

            _hitColliders.Clear();
            gameObject.SetActive(false);
        }

        private void CheckOverlap()
        {
            if (!_isRolling || _boxCollider == null)
            {
                return;
            }

            Vector3 center = transform.TransformPoint(_boxCollider.center);
            Vector3 halfExtents = Vector3.Scale(_boxCollider.size * 0.5f, transform.lossyScale);

            int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, transform.rotation, EntityLayers.Player, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit != null && !_hitColliders.Contains(hit))
                {
                    _hitColliders.Add(hit);
                    EntityManipulationHelper.Damage(hit, _damage);

                    if (_hitVfxPlayer != null)
                    {
                        _hitVfxPlayer.Play(new VFXPlayConfig());
                    }
                }
                _overlapBuffer[i] = null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isRolling || other == null)
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

            if (_hitVfxPlayer != null)
            {
                _hitVfxPlayer.Play(new VFXPlayConfig());
            }
        }
    }
}
