using Assets.ScriptableObjects;
using Assets.Scripts.Audio;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using System;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Lasergun
{
    public class LasergunTurret : Turret<TurretConfigSO>
    {
        // Unity truncates NonAlloc query results when this buffer is full; keep it high enough for dense local enemy clusters.
        private const int TARGET_BUFFER_SIZE = 64;
        private const int MIN_NUMBER_OF_TARGETS = 1;
        private const float SMALLEST_ANGLE_QUALIFYING_AS_LOOKING_AT_TARGET = 4f;

        [SerializeField] private LineRenderer _laserLineRenderer;
        [SerializeField] private float _startShowLaserShootDuration = 0.1f;
        [SerializeField] private VFXPlayer _laserCumulatingVFX;

        private float _currentShowLaserShootDuration;
        private float _targetSearchCooldown;
        private int _numberOfTargets = MIN_NUMBER_OF_TARGETS;
        private int _trackedTargetsCount;
        private int _hitTargetsCount;
        private bool _isShowingLaser;
        private Collider _primaryTarget;
        private bool _isLookingAtTarget;
        private IAudioClipPlayer _audioClipPlayer;
        private LineRenderer[] _laserLineRenderers;
        private Collider[] _trackedTargets = new Collider[MIN_NUMBER_OF_TARGETS];
        private float[] _trackedTargetDistancesSqr = new float[MIN_NUMBER_OF_TARGETS];
        private Collider[] _hitTargets = new Collider[MIN_NUMBER_OF_TARGETS];
        private Vector3[] _hitTargetClosestPoints = new Vector3[MIN_NUMBER_OF_TARGETS];
        private readonly Collider[] _targetBuffer = new Collider[TARGET_BUFFER_SIZE];
        private readonly WaitForEndOfFrame _waitForEndOfFrame = new();

        protected override void Awake()
        {
            base.Awake();

            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
            _laserLineRenderers = new[] { _laserLineRenderer };
        }

        public override void Initialize(TurretConfigSO config)
        {
            _config = config;

            gameObject.SetActive(true);

            _currentShowLaserShootDuration = _startShowLaserShootDuration;
            SetNumberOfTargets(_numberOfTargets);
        }

        private void FixedUpdate()
        {
            UpdateTrackedTargets();
            HandleRotation();
        }

        private void OnEnable()
        {
            _laserCumulatingVFX.OnVFXFinished += LaserCumulatingVFX_OnVFXFinished;
        }

        private void OnDisable()
        {
            _laserCumulatingVFX.OnVFXFinished -= LaserCumulatingVFX_OnVFXFinished;
            ClearLaserLines();
            ClearTrackedTargets();
        }

        public void SetNumberOfTargets(int numberOfTargets)
        {
            _numberOfTargets = Mathf.Max(MIN_NUMBER_OF_TARGETS, numberOfTargets);

            EnsureTargetCapacity(_numberOfTargets);
            EnsureLaserLineRendererCapacity(_numberOfTargets);
        }

        public override void Shoot(float shootPreparingAnimationSpeed = 1f)
        {
            if (!CanShoot())
            {
                return;
            }

            _laserCumulatingVFX.Play(new VFXPlayConfig(simulationSpeed: shootPreparingAnimationSpeed));
        }

        private void LaserCumulatingVFX_OnVFXFinished(object sender, System.EventArgs e)
        {
            FireLaserBeam();
        }

        private bool CanShoot()
        {
            return _primaryTarget is not null
                   && _isShowingLaser == false
                   && _isLookingAtTarget
                   && IsCurrentTargetInRange();
        }

        private void FireLaserBeam()
        {
            if (!IsCurrentTargetInRange())
            {
                AssignNewTargets();
                return;
            }

            CaptureHitTargets();
            if (_hitTargetsCount == 0)
            {
                AssignNewTargets();
                return;
            }

            StartCoroutine(ShootingLaserEffect());
            _audioClipPlayer.Play("Shoot");

            DamageHitTargets();
        }

        private IEnumerator ShootingLaserEffect()
        {
            _isShowingLaser = true;

            while (_currentShowLaserShootDuration > 0)
            {
                UpdateLaserLines();

                _currentShowLaserShootDuration -= Time.deltaTime;

                yield return _waitForEndOfFrame;
            }

            _isShowingLaser = false;
            ClearLaserLines();
            _currentShowLaserShootDuration = _startShowLaserShootDuration;
        }

        private void UpdateTrackedTargets()
        {
            _targetSearchCooldown -= Time.fixedDeltaTime;

            if (_targetSearchCooldown > 0f && IsCurrentTargetInRange())
            {
                return;
            }

            AssignNewTargets();
            _targetSearchCooldown = _config.SearchForTargetInterval;
        }

        private void AssignNewTargets()
        {
            Vector3 turretPosition = transform.position;
            float rangeSqr = _config.Range * _config.Range;
            int targetsCount = Physics.OverlapSphereNonAlloc(
                turretPosition,
                _config.Range,
                _targetBuffer,
                EntityLayers.Enemy
            );

            ClearTrackedTargets();

            for (int i = 0; i < targetsCount; i++)
            {
                Collider target = _targetBuffer[i];

                if (target is null)
                {
                    continue;
                }

                float currentDistanceSqr = (turretPosition - target.transform.position).sqrMagnitude;

                if (currentDistanceSqr > rangeSqr || !IsTargetVisible(target))
                {
                    continue;
                }

                InsertTarget(target, currentDistanceSqr);
            }

            _primaryTarget = _trackedTargetsCount > 0 ? _trackedTargets[0] : null;
        }

        private void InsertTarget(Collider target, float distanceSqr)
        {
            int targetIndex;

            if (_trackedTargetsCount < _trackedTargets.Length)
            {
                targetIndex = _trackedTargetsCount;
                _trackedTargetsCount++;
            }
            else
            {
                int lastIndex = _trackedTargets.Length - 1;
                if (distanceSqr >= _trackedTargetDistancesSqr[lastIndex])
                {
                    return;
                }

                targetIndex = lastIndex;
            }

            _trackedTargets[targetIndex] = target;
            _trackedTargetDistancesSqr[targetIndex] = distanceSqr;

            while (targetIndex > 0 && _trackedTargetDistancesSqr[targetIndex] < _trackedTargetDistancesSqr[targetIndex - 1])
            {
                SwapTrackedTargets(targetIndex, targetIndex - 1);
                targetIndex--;
            }
        }

        private void SwapTrackedTargets(int firstIndex, int secondIndex)
        {
            (_trackedTargets[firstIndex], _trackedTargets[secondIndex]) =
                (_trackedTargets[secondIndex], _trackedTargets[firstIndex]);
            (_trackedTargetDistancesSqr[firstIndex], _trackedTargetDistancesSqr[secondIndex]) =
                (_trackedTargetDistancesSqr[secondIndex], _trackedTargetDistancesSqr[firstIndex]);
        }

        private bool IsCurrentTargetInRange()
        {
            return IsTargetValid(_primaryTarget);
        }

        private bool IsTargetValid(Collider target)
        {
            return target is not null && IsTargetInRange(target) && IsTargetVisible(target);
        }

        private bool IsTargetInRange(Collider target)
        {
            float rangeSqr = _config.Range * _config.Range;

            return (transform.position - target.transform.position).sqrMagnitude <= rangeSqr;
        }

        private bool IsTargetVisible(Collider target)
        {
            return !Physics.Linecast(
                _gunTip.position,
                target.ClosestPoint(_gunTip.position),
                TerrainLayers.All
            );
        }

        private void CaptureHitTargets()
        {
            _hitTargetsCount = 0;

            for (int i = 0; i < _trackedTargetsCount; i++)
            {
                Collider target = _trackedTargets[i];

                if (!IsTargetValid(target))
                {
                    continue;
                }

                _hitTargets[_hitTargetsCount] = target;
                _hitTargetClosestPoints[_hitTargetsCount] = target.ClosestPoint(_gunTip.position);
                _hitTargetsCount++;
            }
        }

        private void DamageHitTargets()
        {
            for (int i = 0; i < _hitTargetsCount; i++)
            {
                EntityManipulationHelper.Damage(
                    _hitTargets[i],
                    _config.ProjectileStatsSO.Damage
                );
            }
        }

        private void UpdateLaserLines()
        {
            for (int i = 0; i < _hitTargetsCount; i++)
            {
                Collider target = _hitTargets[i];
                LineRenderer lineRenderer = _laserLineRenderers[i];

                if (target is not null)
                {
                    _hitTargetClosestPoints[i] = target.ClosestPoint(_gunTip.position);
                }

                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, _gunTip.position);
                lineRenderer.SetPosition(1, _hitTargetClosestPoints[i]);
            }
        }

        private void ClearLaserLines()
        {
            for (int i = 0; i < _laserLineRenderers.Length; i++)
            {
                _laserLineRenderers[i].positionCount = 0;
            }
        }

        private void ClearTrackedTargets()
        {
            for (int i = 0; i < _trackedTargetsCount; i++)
            {
                _trackedTargets[i] = null;
                _trackedTargetDistancesSqr[i] = float.PositiveInfinity;
            }

            _trackedTargetsCount = 0;
        }

        private void HandleRotation()
        {
            if (_primaryTarget is null)
            {
                _isLookingAtTarget = false;
                return;
            }

            Vector3 targetPosition = _primaryTarget.transform.position;
            Vector3 turretPosition = transform.position;
            Vector3 direction = new Vector3(
                targetPosition.x - turretPosition.x,
                0f,
                targetPosition.z - turretPosition.z
            );

            if (direction.sqrMagnitude < 0.0001f)
            {
                _isLookingAtTarget = true;
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

            float angle = Quaternion.Angle(transform.rotation, targetRotation);

            _isLookingAtTarget = angle <= SMALLEST_ANGLE_QUALIFYING_AS_LOOKING_AT_TARGET;

            float angleThisFrame = _config.RotationSpeed * Time.fixedDeltaTime;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                angleThisFrame
            );
        }

        private void EnsureTargetCapacity(int capacity)
        {
            if (_trackedTargets.Length == capacity)
            {
                return;
            }

            ClearTrackedTargets();

            Array.Resize(ref _trackedTargets, capacity);
            Array.Resize(ref _trackedTargetDistancesSqr, capacity);
            Array.Resize(ref _hitTargets, capacity);
            Array.Resize(ref _hitTargetClosestPoints, capacity);

            _hitTargetsCount = 0;
        }

        private void EnsureLaserLineRendererCapacity(int capacity)
        {
            if (_laserLineRenderers.Length >= capacity)
            {
                return;
            }

            int oldLength = _laserLineRenderers.Length;
            Array.Resize(ref _laserLineRenderers, capacity);

            for (int i = oldLength; i < _laserLineRenderers.Length; i++)
            {
                LineRenderer lineRenderer = Instantiate(_laserLineRenderer, _laserLineRenderer.transform.parent);
                lineRenderer.positionCount = 0;
                _laserLineRenderers[i] = lineRenderer;
            }
        }
    }
}
