using System;
using DG.Tweening;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.VFX;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Projectiles
{
    public interface IRailgunOrbProjectile
    {
        void Launch(Vector3 startPosition, Vector3 targetPosition, float duration, Action onLanded);
        void PlayImpactAndScheduleReturn(string sfxKey);
        void ReturnToPool();
    }

    public class RailgunOrbProjectile : MonoBehaviour, IRailgunOrbProjectile
    {
        [Header("Audio & Visual Feedback")]
        [Tooltip("Optional audio clip player for orb impact sound effects.")]
        [SerializeField] private AudioClipPlayer _audioClipPlayer;
        [Tooltip("Optional VFX player triggered upon ground detonation.")]
        [SerializeField] private VFXPlayer _impactVfxPlayer;
        [Tooltip("Optional trail renderer following the energy orb flight.")]
        [SerializeField] private TrailRenderer _trailRenderer;
        [Tooltip("Optional visual child deactivated upon impact while VFX/audio finishes.")]
        [SerializeField] private GameObject _visualModel;

        [Header("Flight Configuration")]
        [Tooltip("Subtle parabolic arc height in meters added during flight from muzzle to ground.")]
        [SerializeField] private float _arcHeight = 2.0f;

        private IObjectPool<RailgunOrbProjectile> _pool;
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

        public void SetPool(IObjectPool<RailgunOrbProjectile> pool)
        {
            _pool = pool;
        }

        public void Launch(Vector3 startPosition, Vector3 targetPosition, float duration, Action onLanded)
        {
            KillTweens();

            transform.position = startPosition;
            Vector3 flatDir = targetPosition - startPosition;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
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

            float safeDuration = Mathf.Max(duration, 0.05f);
            _flightTween = DOVirtual.Float(0f, 1f, safeDuration, progress =>
            {
                Vector3 basePos = Vector3.Lerp(startPosition, targetPosition, progress);
                float arcOffset = 4f * _arcHeight * progress * (1f - progress);
                transform.position = new Vector3(basePos.x, basePos.y + arcOffset, basePos.z);
            })
            .SetEase(Ease.Linear)
            .AsFreezable()
            .OnComplete(() =>
            {
                transform.position = targetPosition;
                onLanded?.Invoke();
            });
        }

        public void PlayImpactAndScheduleReturn(string sfxKey)
        {
            KillTweens();

            if (_visualModel != null)
            {
                _visualModel.SetActive(false);
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = false;
            }

            if (_audioClipPlayer != null && !string.IsNullOrEmpty(sfxKey) && _audioClipPlayer.HasPlayableClip(sfxKey))
            {
                _audioClipPlayer.PlayOneShot(sfxKey);
            }

            float releaseDelay = RailgunTowerConstants.ORB_RELEASE_DELAY_MIN;
            if (_impactVfxPlayer != null)
            {
                _impactVfxPlayer.Play(new VFXPlayConfig());
                releaseDelay = Mathf.Clamp(
                    _impactVfxPlayer.GetLongestParticleDuration(),
                    RailgunTowerConstants.ORB_RELEASE_DELAY_MIN,
                    RailgunTowerConstants.ORB_RELEASE_DELAY_MAX
                );
            }

            _releaseTween = DOVirtual.DelayedCall(releaseDelay, () =>
            {
                ReturnToPool();
            }).AsFreezable();
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
