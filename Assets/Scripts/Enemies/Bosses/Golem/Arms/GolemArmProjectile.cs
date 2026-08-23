using System;
using DG.Tweening;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Arms
{
    public enum GolemArmState
    {
        Docked,
        LinearThrust,
        SkyAirborne,
        SkyDropping,
        Returning
    }

    [RequireComponent(typeof(Collider))]
    public class GolemArmProjectile : MonoBehaviour
    {
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private VFXPlayer _impactVfx;
        [SerializeField] private Collider _damageCollider;
        [SerializeField] private GameObject _rigArmVisual;

        private Transform _socketTransform;
        private GolemArmState _state = GolemArmState.Docked;
        private float _currentDamage;
        private Sequence _activeSequence;
        private bool _hasDealtDamageThisFlight;

        public GolemArmState State => _state;
        public bool IsDocked => _state == GolemArmState.Docked;

        private void Awake()
        {
            if (_damageCollider == null)
            {
                _damageCollider = GetComponent<Collider>();
            }
            if (_damageCollider != null)
            {
                _damageCollider.isTrigger = true;
                _damageCollider.enabled = false;
            }
        }

        private void OnDisable()
        {
            KillActiveSequence();
        }

        public void Initialize(Transform socketTransform, GameObject rigArmVisual = null)
        {
            _socketTransform = socketTransform;
            if (rigArmVisual != null)
            {
                _rigArmVisual = rigArmVisual;
            }
            DockToSocket();
        }

        public void DockToSocket()
        {
            KillActiveSequence();
            _state = GolemArmState.Docked;

            if (_damageCollider != null)
            {
                _damageCollider.enabled = false;
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = false;
            }

            if (_socketTransform != null)
            {
                transform.SetParent(_socketTransform);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            gameObject.SetActive(false);

            if (_rigArmVisual != null)
            {
                _rigArmVisual.SetActive(true);
            }
        }

        public void FireLinear(Vector3 targetDirection, float maxDistance, float speed, float damage, Action onComplete)
        {
            if (_rigArmVisual != null)
            {
                _rigArmVisual.SetActive(false);
            }
            gameObject.SetActive(true);

            KillActiveSequence();
            _state = GolemArmState.LinearThrust;
            _currentDamage = damage;
            _hasDealtDamageThisFlight = false;

            transform.SetParent(null);
            if (_damageCollider != null)
            {
                _damageCollider.enabled = true;
            }
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
                _trailRenderer.emitting = true;
            }

            targetDirection.y = 0f;
            targetDirection.Normalize();

            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + targetDirection * maxDistance;
            float travelTime = maxDistance / Mathf.Max(speed, 0.1f);
            float returnTime = travelTime * 0.8f;

            transform.rotation = Quaternion.LookRotation(targetDirection, Vector3.up);

            _activeSequence = DOTween.Sequence();
            _activeSequence.Append(transform.DOMove(targetPos, travelTime).SetEase(Ease.OutQuad));
            _activeSequence.AppendCallback(() =>
            {
                _state = GolemArmState.Returning;
                if (_damageCollider != null)
                {
                    _damageCollider.enabled = false;
                }
            });
            _activeSequence.Append(transform.DOMove(_socketTransform != null ? _socketTransform.position : startPos, returnTime)
                .SetEase(Ease.InQuad)
                .OnUpdate(() =>
                {
                    if (_socketTransform != null)
                    {
                        transform.rotation = Quaternion.LookRotation((_socketTransform.position - transform.position).normalized + Vector3.up * 0.01f);
                    }
                }));
            _activeSequence.OnComplete(() =>
            {
                DockToSocket();
                onComplete?.Invoke();
            });
        }

        public void LaunchToSky(float launchHeight, float duration, Action onSkyReached)
        {
            if (_rigArmVisual != null)
            {
                _rigArmVisual.SetActive(false);
            }
            gameObject.SetActive(true);

            KillActiveSequence();
            _state = GolemArmState.SkyAirborne;

            transform.SetParent(null);
            if (_damageCollider != null)
            {
                _damageCollider.enabled = false;
            }
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
                _trailRenderer.emitting = true;
            }

            Vector3 skyPos = transform.position + Vector3.up * launchHeight;

            _activeSequence = DOTween.Sequence();
            _activeSequence.Append(transform.DOMove(skyPos, duration).SetEase(Ease.InQuad));
            _activeSequence.OnComplete(() =>
            {
                onSkyReached?.Invoke();
            });
        }

        public void DropFromSky(Vector3 targetSlamPosition, float fallSpeed, float damage, float impactRadius, Action onImpact)
        {
            if (_rigArmVisual != null)
            {
                _rigArmVisual.SetActive(false);
            }
            gameObject.SetActive(true);

            KillActiveSequence();
            _state = GolemArmState.SkyDropping;
            _currentDamage = damage;
            _hasDealtDamageThisFlight = false;

            float skyY = Mathf.Max(transform.position.y, targetSlamPosition.y + 25f);
            Vector3 skyPos = new Vector3(targetSlamPosition.x, skyY, targetSlamPosition.z);
            transform.position = skyPos;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            if (_damageCollider != null)
            {
                _damageCollider.enabled = true;
            }
            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = true;
            }

            float distance = Mathf.Abs(skyPos.y - targetSlamPosition.y);
            float fallDuration = distance / Mathf.Max(fallSpeed, 0.1f);

            _activeSequence = DOTween.Sequence();
            _activeSequence.Append(transform.DOMove(targetSlamPosition, fallDuration).SetEase(Ease.InQuad));
            _activeSequence.OnComplete(() =>
            {
                if (_damageCollider != null)
                {
                    _damageCollider.enabled = false;
                }
                if (_impactVfx != null)
                {
                    _impactVfx.Play(new VFXPlayConfig());
                }

                Collider[] colliders = Physics.OverlapSphere(targetSlamPosition, impactRadius, EntityLayers.Player);
                foreach (Collider col in colliders)
                {
                    if (col.TryGetComponent<IDamageable>(out var damageable))
                    {
                        damageable.TakeDamage(_currentDamage);
                    }
                }

                onImpact?.Invoke();
            });
        }

        public void ReturnAndDock(float duration, Action onDocked = null)
        {
            KillActiveSequence();
            _state = GolemArmState.Returning;

            if (_damageCollider != null)
            {
                _damageCollider.enabled = false;
            }

            if (_socketTransform == null)
            {
                DockToSocket();
                onDocked?.Invoke();
                return;
            }

            _activeSequence = DOTween.Sequence();
            _activeSequence.Append(transform.DOMove(_socketTransform.position, duration).SetEase(Ease.InOutQuad));
            _activeSequence.Join(transform.DORotateQuaternion(_socketTransform.rotation, duration));
            _activeSequence.OnComplete(() =>
            {
                DockToSocket();
                onDocked?.Invoke();
            });
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasDealtDamageThisFlight)
            {
                return;
            }

            if ((1 << other.gameObject.layer & EntityLayers.Player) != 0)
            {
                if (other.TryGetComponent<IDamageable>(out var damageable))
                {
                    _hasDealtDamageThisFlight = true;
                    damageable.TakeDamage(_currentDamage);
                }
            }
        }

        private void KillActiveSequence()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }
            _activeSequence = null;
        }
    }
}
