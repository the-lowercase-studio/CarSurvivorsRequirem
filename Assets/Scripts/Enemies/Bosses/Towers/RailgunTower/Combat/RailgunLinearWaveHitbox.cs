using System;
using System.Collections.Generic;
using DG.Tweening;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using Assets.Scripts.Indicators;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.StatusEffects;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Combat
{
    public interface IRailgunLinearWaveHitbox
    {
        bool IsActive { get; }
        void LaunchBidirectional(Vector3 impactCenter, Vector3 lineDirection, float halfLength, float width, float speed, float damage, Action onComplete);
        void ReturnToPool();
    }

    public class RailgunLinearWaveHitbox : MonoBehaviour, IRailgunLinearWaveHitbox
    {
        [Header("Opposing Wavefront Visuals")]
        [Tooltip("Visual transform moving along the positive (+lineDirection) wave half.")]
        [SerializeField] private Transform _positiveWaveVisual;
        [Tooltip("Visual transform moving along the negative (-lineDirection) wave half.")]
        [SerializeField] private Transform _negativeWaveVisual;

        [Header("Paired Lane Telegraphs")]
        [Tooltip("Reusable rectangular telegraph showing the positive (+lineDirection) wave lane.")]
        [SerializeField] private RectangularTelegraphIndicator _positiveTelegraph;
        [Tooltip("Reusable rectangular telegraph showing the negative (-lineDirection) wave lane.")]
        [SerializeField] private RectangularTelegraphIndicator _negativeTelegraph;

        private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
        private readonly Collider[] _overlapBuffer = new Collider[RailgunTowerConstants.OVERLAP_BUFFER_SIZE];
        private IObjectPool<RailgunLinearWaveHitbox> _pool;
        private Tween _travelTween;
        private Vector3 _impactCenter;
        private Vector3 _positiveDir;
        private Vector3 _negativeDir;
        private float _halfLength;
        private float _width;
        private float _damage;
        private float _previousTravelDistance;
        private bool _isActive;

        public bool IsActive
        {
            get { return _isActive; }
        }

        private void Awake()
        {
            if (_positiveWaveVisual == null || _negativeWaveVisual == null ||
                _positiveTelegraph == null || _negativeTelegraph == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunLinearWaveHitbox)}] Required wave visual or telegraph child references are unassigned.");
            }

            _positiveTelegraph.SetFreezable(true);
            _negativeTelegraph.SetFreezable(true);
            _positiveTelegraph.HidePersistent();
            _negativeTelegraph.HidePersistent();
        }

        private void OnDisable()
        {
            KillTravelTween();
            _isActive = false;
            _hitTargets.Clear();
            HideChildren();
        }

        private void OnDestroy()
        {
            KillTravelTween();
            _isActive = false;
            _hitTargets.Clear();
        }

        public void SetPool(IObjectPool<RailgunLinearWaveHitbox> pool)
        {
            _pool = pool;
        }

        public void LaunchBidirectional(
            Vector3 impactCenter,
            Vector3 lineDirection,
            float halfLength,
            float width,
            float speed,
            float damage,
            Action onComplete)
        {
            KillTravelTween();
            _hitTargets.Clear();

            lineDirection.y = 0f;
            if (lineDirection.sqrMagnitude < 0.0001f)
            {
                lineDirection = Vector3.forward;
            }
            lineDirection.Normalize();

            _impactCenter = new Vector3(impactCenter.x, 0f, impactCenter.z);
            _positiveDir = lineDirection;
            _negativeDir = -lineDirection;
            _halfLength = Mathf.Max(halfLength, 0.5f);
            _width = Mathf.Max(width, 0.25f);
            _damage = damage;
            _previousTravelDistance = 0f;
            _isActive = true;

            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            float travelDuration = _halfLength / Mathf.Max(speed, 0.1f);

            _positiveTelegraph.SetFreezable(true);
            _negativeTelegraph.SetFreezable(true);
            _positiveTelegraph.Show(_impactCenter, _positiveDir, _halfLength, _width, travelDuration, null, autoContractOnFillComplete: false);
            _negativeTelegraph.Show(_impactCenter, _negativeDir, _halfLength, _width, travelDuration, null, autoContractOnFillComplete: false);

            SetupWaveVisual(_positiveWaveVisual, _positiveDir);
            SetupWaveVisual(_negativeWaveVisual, _negativeDir);

            UpdateWavefronts(0f);

            _travelTween = DOVirtual.Float(0f, 1f, travelDuration, progress =>
            {
                UpdateWavefronts(progress);
            })
            .SetEase(Ease.Linear)
            .AsFreezable()
            .OnComplete(() =>
            {
                _isActive = false;
                onComplete?.Invoke();
                ReturnToPool();
            });
        }

        public void ReturnToPool()
        {
            KillTravelTween();
            _isActive = false;
            _hitTargets.Clear();
            HideChildren();

            if (_pool != null)
            {
                _pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void SetupWaveVisual(Transform waveVisual, Vector3 direction)
        {
            waveVisual.gameObject.SetActive(true);
            waveVisual.position = _impactCenter + Vector3.up * (RailgunTowerConstants.BEAM_HITBOX_HEIGHT * 0.35f);
            waveVisual.rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 scale = waveVisual.localScale;
            waveVisual.localScale = new Vector3(_width, scale.y, scale.z);
        }

        private void HideChildren()
        {
            if (_positiveTelegraph != null)
            {
                _positiveTelegraph.HidePersistent();
            }

            if (_negativeTelegraph != null)
            {
                _negativeTelegraph.HidePersistent();
            }

            if (_positiveWaveVisual != null)
            {
                _positiveWaveVisual.gameObject.SetActive(false);
            }

            if (_negativeWaveVisual != null)
            {
                _negativeWaveVisual.gameObject.SetActive(false);
            }
        }

        private void UpdateWavefronts(float progress)
        {
            if (!_isActive)
            {
                return;
            }

            float currentDistance = Mathf.Clamp01(progress) * _halfLength;
            float halfDepth = RailgunTowerConstants.WAVE_FRONT_DEPTH * 0.5f;
            float segStart = Mathf.Max(0f, _previousTravelDistance - halfDepth);
            float segEnd = currentDistance + halfDepth;
            float segMidpoint = (segStart + segEnd) * 0.5f;
            float segHalfLength = Mathf.Max((segEnd - segStart) * 0.5f, halfDepth);

            _positiveWaveVisual.position = _impactCenter + _positiveDir * currentDistance + Vector3.up * (RailgunTowerConstants.BEAM_HITBOX_HEIGHT * 0.35f);
            _negativeWaveVisual.position = _impactCenter + _negativeDir * currentDistance + Vector3.up * (RailgunTowerConstants.BEAM_HITBOX_HEIGHT * 0.35f);

            CheckHalfWaveOverlap(_positiveDir, segMidpoint, segHalfLength);
            CheckHalfWaveOverlap(_negativeDir, segMidpoint, segHalfLength);

            _previousTravelDistance = currentDistance;
        }

        private void CheckHalfWaveOverlap(Vector3 direction, float centerDistance, float halfDepth)
        {
            Vector3 center = _impactCenter + direction * centerDistance + Vector3.up * (RailgunTowerConstants.BEAM_HITBOX_HEIGHT * 0.5f);
            Vector3 halfExtents = new Vector3(_width * 0.5f, RailgunTowerConstants.BEAM_HITBOX_HEIGHT * 0.5f, halfDepth);
            Quaternion orientation = Quaternion.LookRotation(direction, Vector3.up);

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _overlapBuffer,
                orientation,
                EntityLayers.Player,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                _overlapBuffer[i] = null;

                if (hit == null || !hit.enabled)
                {
                    continue;
                }

                if (hit.TryGetComponent(out IDamageable damageable) || (damageable = hit.GetComponentInParent<IDamageable>()) != null)
                {
                    if (_hitTargets.Add(damageable))
                    {
                        damageable.TakeDamage(_damage);
                    }
                }
            }
        }

        private void KillTravelTween()
        {
            if (_travelTween != null && _travelTween.IsActive())
            {
                _travelTween.Kill();
            }
            _travelTween = null;
        }
    }
}
