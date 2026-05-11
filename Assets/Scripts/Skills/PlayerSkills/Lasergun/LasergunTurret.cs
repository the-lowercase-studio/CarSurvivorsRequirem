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
        [SerializeField] private LineRenderer _laserLineRenderer;
        [SerializeField] private float _startShowLaserShootDuration = 0.1f;
        [SerializeField] private VFXPlayer _laserCumulatingVFX;
        private float _currentShowLaserShootDuration;
        private bool _isShowingLaser;

        private Collider _currentTarget;
        private Vector3 _lastTargetClosestPoint;

        private const float SMALLEST_ANGLE_QUALIFIING_AS_LOOKING_AT_TARGET = 4f;
        private bool _isLookingAtTarget;

        private IAudioClipPlayer _audioClipPlayer;

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

            while (_currentShowLaserShootDuration > 0)
            {
                if (_currentTarget is not null)
                {
                    _lastTargetClosestPoint = _currentTarget.ClosestPoint(_gunTip.position);
                }

                _laserLineRenderer.positionCount = 2;
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
            Collider[] potentialTargets = Physics.OverlapSphere(transform.position, _config.Range, EntityLayers.Enemy);

            if (potentialTargets.Length > 0)
            {
                bool anyFound = false;
                Collider closestTarget = potentialTargets[0];
                float closestDistance = Vector3.Distance(transform.position, closestTarget.transform.position);

                foreach (Collider target in potentialTargets)
                {
                    bool isBehindObstacle = Physics.Linecast(
                        _gunTip.position,
                        target.ClosestPoint(_gunTip.transform.position),
                        TerrainLayers.All
                    );

                    if (isBehindObstacle)
                    {
                        continue;
                    }

                    float currentDistance = Vector3.Distance(transform.position, target.transform.position);
                    if (currentDistance <= _config.Range
                        && currentDistance <= closestDistance)
                    {
                        closestDistance = currentDistance;
                        closestTarget = target;
                        anyFound = true;
                    }
                }

                if (anyFound)
                {
                    _currentTarget = closestTarget;
                    return;
                }
            }

            _currentTarget = null;
        }

        private bool IsCurrentTargetInRange()
        {
            return _currentTarget is not null
                   && Vector3.Distance(transform.position, _currentTarget.transform.position) <= _config.Range;
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

            _isLookingAtTarget = angle <= SMALLEST_ANGLE_QUALIFIING_AS_LOOKING_AT_TARGET;

            float angleThisFrame = _config.RotationSpeed * Time.fixedDeltaTime;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                angleThisFrame
            );
        }
    }
}
