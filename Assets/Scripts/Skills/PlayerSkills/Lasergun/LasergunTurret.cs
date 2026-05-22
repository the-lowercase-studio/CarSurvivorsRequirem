using Assets.ScriptableObjects;
using Assets.Scripts.Audio;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Lasergun
{
    public class LasergunTurret : Turret<TurretConfigSO>
    {
        // Unity truncates NonAlloc query results when this buffer is full; keep it high enough for dense local enemy clusters.
        private const int TARGET_BUFFER_SIZE = 64;
        private const float SMALLEST_ANGLE_QUALIFYING_AS_LOOKING_AT_TARGET = 4f;

        [SerializeField] private LineRenderer _laserLineRenderer;
        [SerializeField] private float _startShowLaserShootDuration = 0.1f;
        [SerializeField] private VFXPlayer _laserCumulatingVFX;

        private float _currentShowLaserShootDuration;
        private bool _isShowingLaser;
        private Collider _currentTarget;
        private Vector3 _lastTargetClosestPoint;
        private bool _isLookingAtTarget;
        private IAudioClipPlayer _audioClipPlayer;
        private readonly Collider[] _targetBuffer = new Collider[TARGET_BUFFER_SIZE];

        protected override void Awake()
        {
            base.Awake();

            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        public override void Initialize(TurretConfigSO config)
        {
            _config = config;

            gameObject.SetActive(true);

            _currentShowLaserShootDuration = _startShowLaserShootDuration;
        }

        private void FixedUpdate()
        {
            HandleRotation();

            if (!IsCurrentTargetInRange())
            {
                AssignNewTarget();
            }
        }

        private void OnEnable()
        {
            _laserCumulatingVFX.OnVFXFinished += LaserCumulatingVFX_OnVFXFinished;
        }

        private void OnDisable()
        {
            _laserCumulatingVFX.OnVFXFinished -= LaserCumulatingVFX_OnVFXFinished;
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
            return _currentTarget is not null
                   && _isShowingLaser == false
                   && _isLookingAtTarget
                   && IsCurrentTargetInRange();
        }

        private void FireLaserBeam()
        {
            if (!IsCurrentTargetInRange())
            {
                AssignNewTarget();
                return;
            }

            StartCoroutine(ShootingLaserEffect());

            _audioClipPlayer.Play("Shoot");

            EntityManipulationHelper.Damage(
                _currentTarget,
                _config.ProjectileStatsSO.Damage
            );
        }

        private IEnumerator ShootingLaserEffect()
        {
            _isShowingLaser = true;
            _laserLineRenderer.positionCount = 2;

            while (_currentShowLaserShootDuration > 0)
            {
                if (_currentTarget is not null)
                {
                    _lastTargetClosestPoint = _currentTarget.ClosestPoint(_gunTip.position);
                }

                _laserLineRenderer.SetPosition(0, _gunTip.position);
                _laserLineRenderer.SetPosition(1, _lastTargetClosestPoint);

                _currentShowLaserShootDuration -= Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }

            _isShowingLaser = false;
            _laserLineRenderer.positionCount = 0;
            _currentShowLaserShootDuration = _startShowLaserShootDuration;
        }

        private void AssignNewTarget()
        {
            Vector3 turretPosition = transform.position;
            float rangeSqr = _config.Range * _config.Range;
            int targetsCount = Physics.OverlapSphereNonAlloc(
                turretPosition,
                _config.Range,
                _targetBuffer,
                EntityLayers.Enemy
            );

            Collider closestTarget = null;
            float closestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < targetsCount; i++)
            {
                Collider target = _targetBuffer[i];

                if (target is null)
                {
                    continue;
                }

                float currentDistanceSqr = (turretPosition - target.transform.position).sqrMagnitude;

                if (currentDistanceSqr > rangeSqr
                    || currentDistanceSqr > closestDistanceSqr)
                {
                    continue;
                }

                bool isBehindObstacle = Physics.Linecast(
                    _gunTip.position,
                    target.ClosestPoint(_gunTip.transform.position),
                    TerrainLayers.All
                );

                if (isBehindObstacle)
                {
                    continue;
                }

                closestDistanceSqr = currentDistanceSqr;
                closestTarget = target;
            }

            _currentTarget = closestTarget;
        }

        private bool IsCurrentTargetInRange()
        {
            float rangeSqr = _config.Range * _config.Range;

            return _currentTarget is not null
                   && (transform.position - _currentTarget.transform.position).sqrMagnitude <= rangeSqr;
        }

        private void HandleRotation()
        {
            if (_currentTarget is null)
            {
                return;
            }

            Vector3 targetPosition = _currentTarget.transform.position;
            Vector3 turretPosition = transform.position;
            Vector3 direction = new Vector3(
                targetPosition.x - turretPosition.x,
                0f,
                targetPosition.z - turretPosition.z
            );

            if (direction.sqrMagnitude < 0.0001f)
            {
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
    }
}
