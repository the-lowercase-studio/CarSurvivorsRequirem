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
        [Header("Audio & Visual Feedback")]
        [Tooltip("Audio clip player used for mortar shell impact explosion.")]
        [SerializeField] private AudioClipPlayer _audioClipPlayer;
        [Tooltip("VFX player for ground impact explosion particles.")]
        [SerializeField] private VFXPlayer _impactVfxPlayer;
        [Tooltip("Trail renderer following the parabolic flight.")]
        [SerializeField] private TrailRenderer _trailRenderer;
        [Tooltip("Optional mesh/visual child deactivated upon detonation while VFX finishes.")]
        [SerializeField] private GameObject _visualModel;

        [Header("Flight & Trajectory Configuration")]
        [Tooltip("Euler rotation offset to align the 3D model nose/tip with the forward velocity vector.")]
        [SerializeField] private Vector3 _modelRotationOffset = new Vector3(90f, 0f, 0f);
        [Tooltip("Optional custom height curve over normalized flight time [0, 1]. If empty, uses standard parabolic arc.")]
        [SerializeField] private AnimationCurve _customHeightCurve;
        [Tooltip("Optional axial spin speed (degrees per second) around the forward flight axis.")]
        [SerializeField] private float _riflingSpinSpeed = 360f;

        private readonly Collider[] _hitBuffer = new Collider[16];
        private IObjectPool<MortarShellProjectile> _pool;
        private Tween _flightTween;
        private Tween _releaseTween;
        private Vector3 _lastValidHorizontalDir = Vector3.forward;

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

            Vector3 horizontalDir = new Vector3(targetPosition.x - startPosition.x, 0f, targetPosition.z - startPosition.z);
            _lastValidHorizontalDir = horizontalDir.sqrMagnitude > 0.0001f ? horizontalDir.normalized : Vector3.forward;

            EvaluateTrajectory(0f, startPosition, targetPosition, jumpPower, out Vector3 initialPos, out Vector3 initialTangent);
            transform.position = initialPos;
            transform.rotation = ComputeTrajectoryRotation(initialTangent, 0f);

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

            _flightTween = DOVirtual.Float(0f, 1f, duration, progress =>
            {
                EvaluateTrajectory(progress, startPosition, targetPosition, jumpPower, out Vector3 currentPos, out Vector3 currentTangent);
                transform.position = currentPos;

                float spinAngle = progress * duration * _riflingSpinSpeed;
                transform.rotation = ComputeTrajectoryRotation(currentTangent, spinAngle);
            })
            .SetEase(Ease.Linear)
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

        private void EvaluateTrajectory(float t, Vector3 start, Vector3 target, float jumpPower, out Vector3 position, out Vector3 tangent)
        {
            t = Mathf.Clamp01(t);

            Vector3 horizontal = Vector3.Lerp(start, target, t);
            float heightOffset;
            bool hasCustomCurve = _customHeightCurve != null && _customHeightCurve.length > 0;

            if (hasCustomCurve)
            {
                heightOffset = jumpPower * _customHeightCurve.Evaluate(t);
            }
            else
            {
                heightOffset = 4f * jumpPower * t * (1f - t);
            }

            position = new Vector3(horizontal.x, Mathf.Lerp(start.y, target.y, t) + heightOffset, horizontal.z);

            if (hasCustomCurve)
            {
                const float dt = 0.001f;
                float nextT = Mathf.Min(t + dt, 1f);
                float prevT = Mathf.Max(t - dt, 0f);
                float sampleDelta = nextT - prevT;

                if (sampleDelta > 0.00001f)
                {
                    Vector3 posNext = SamplePosition(nextT, start, target, jumpPower);
                    Vector3 posPrev = SamplePosition(prevT, start, target, jumpPower);
                    tangent = (posNext - posPrev) / sampleDelta;
                }
                else
                {
                    tangent = target - start;
                }
            }
            else
            {
                // Analytical derivative of P(t) = Lerp(start, target, t) + (0, 4 * jumpPower * t * (1 - t), 0)
                float vy = (target.y - start.y) + 4f * jumpPower * (1f - 2f * t);
                tangent = new Vector3(target.x - start.x, vy, target.z - start.z);
            }
        }

        private Vector3 SamplePosition(float t, Vector3 start, Vector3 target, float jumpPower)
        {
            Vector3 horizontal = Vector3.Lerp(start, target, t);
            float heightOffset;
            if (_customHeightCurve != null && _customHeightCurve.length > 0)
            {
                heightOffset = jumpPower * _customHeightCurve.Evaluate(t);
            }
            else
            {
                heightOffset = 4f * jumpPower * t * (1f - t);
            }

            return new Vector3(horizontal.x, Mathf.Lerp(start.y, target.y, t) + heightOffset, horizontal.z);
        }

        private Quaternion ComputeTrajectoryRotation(Vector3 tangent, float spinAngle)
        {
            if (tangent.sqrMagnitude < 0.00001f)
            {
                return transform.rotation;
            }

            Vector3 forward = tangent.normalized;

            Vector3 horizontal = new Vector3(forward.x, 0f, forward.z);
            if (horizontal.sqrMagnitude > 0.0001f)
            {
                _lastValidHorizontalDir = horizontal.normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, _lastValidHorizontalDir).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            Vector3 up = Vector3.Cross(forward, right).normalized;

            Quaternion flightRotation = Quaternion.LookRotation(forward, up);

            if (Mathf.Abs(spinAngle) > 0.001f)
            {
                flightRotation *= Quaternion.AngleAxis(spinAngle, Vector3.forward);
            }

            return flightRotation * Quaternion.Euler(_modelRotationOffset);
        }
    }
}
