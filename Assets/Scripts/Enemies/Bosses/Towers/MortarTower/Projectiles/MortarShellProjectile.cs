using System;
using DG.Tweening;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Constants;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles
{
    public interface IMortarShellProjectile
    {
        void LaunchParabolic(Vector3 startPosition, Vector3 targetPosition, float jumpPower, float duration, Action onLanded);
        void Detonate(float radius, float damage);
        void ReturnToPool();
    }

    public class MortarShellProjectile : MonoBehaviour, IMortarShellProjectile
    {
        [Tooltip("Audio clip player used for mortar shell impact explosion.")]
        [SerializeField] private AudioClipPlayer _audioClipPlayer;
        [Tooltip("VFX player for ground impact explosion particles.")]
        [SerializeField] private VFXPlayer _impactVfxPlayer;
        [Tooltip("Trail renderer following the parabolic flight.")]
        [SerializeField] private TrailRenderer _trailRenderer;
        [Tooltip("Optional mesh/visual child deactivated upon detonation while VFX finishes.")]
        [SerializeField] private GameObject _visualModel;

        private readonly Collider[] _hitBuffer = new Collider[16];
        private IObjectPool<MortarShellProjectile> _pool;
        private Tween _flightTween;
        private Tween _releaseTween;

        private void OnDisable()
        {
            KillTweens();
            if (_visualModel != null)
            {
                _visualModel.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            KillTweens();
        }

        public void SetPool(IObjectPool<MortarShellProjectile> pool)
        {
            _pool = pool;
        }

        public void LaunchParabolic(Vector3 startPosition, Vector3 targetPosition, float jumpPower, float duration, Action onLanded)
        {
            KillTweens();

            transform.position = startPosition;

            Vector3 initialDir = targetPosition - startPosition;
            initialDir.y += jumpPower * 2f;
            if (initialDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(initialDir.normalized, Vector3.up);
            }

            if (_visualModel != null)
            {
                _visualModel.SetActive(true);
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
                _trailRenderer.enabled = true;
                _trailRenderer.emitting = true;
            }

            gameObject.SetActive(true);

            Vector3 previousPosition = startPosition;

            _flightTween = transform.DOJump(targetPosition, jumpPower, 1, duration)
                .SetEase(Ease.Linear)
                .OnUpdate(() =>
                {
                    Vector3 delta = transform.position - previousPosition;
                    if (delta.sqrMagnitude > 0.00001f)
                    {
                        transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    }
                    previousPosition = transform.position;
                })
                .OnComplete(() =>
                {
                    onLanded?.Invoke();
                });
        }

        public void Detonate(float radius, float damage)
        {
            KillTweens();

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, _hitBuffer, EntityLayers.Player);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hitBuffer[i];
                if (hit != null)
                {
                    EntityManipulationHelper.Damage(hit, damage);
                }
                _hitBuffer[i] = null;
            }

            if (_visualModel != null)
            {
                _visualModel.SetActive(false);
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = false;
            }

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(MortarTowerConstants.SFX_MORTAR_IMPACT);
            }

            float releaseDelay = 0.5f;
            if (_impactVfxPlayer != null)
            {
                _impactVfxPlayer.Play(new VFXPlayConfig());
                releaseDelay = Mathf.Clamp(_impactVfxPlayer.GetLongestParticleDuration(), 0.5f, 1.5f);
            }

            _releaseTween = DOVirtual.DelayedCall(releaseDelay, () =>
            {
                ReturnToPool();
            });
        }

        public void ReturnToPool()
        {
            KillTweens();

            if (_visualModel != null)
            {
                _visualModel.SetActive(true);
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }

            if (_pool != null)
            {
                _pool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void KillTweens()
        {
            if (_flightTween != null && _flightTween.IsActive())
            {
                _flightTween.Kill();
            }
            _flightTween = null;

            if (_releaseTween != null && _releaseTween.IsActive())
            {
                _releaseTween.Kill();
            }
            _releaseTween = null;
        }
    }
}
