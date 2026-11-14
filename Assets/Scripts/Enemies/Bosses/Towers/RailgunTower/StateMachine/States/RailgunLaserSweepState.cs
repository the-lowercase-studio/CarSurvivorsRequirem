using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine.States
{
    public class RailgunLaserSweepState : IRailgunTowerState
    {
        private enum Phase
        {
            Warning,
            Sweeping
        }

        private readonly IRailgunTowerBoss _boss;
        private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
        private readonly Collider[] _overlapBuffer = new Collider[RailgunTowerConstants.OVERLAP_BUFFER_SIZE];

        private Phase _phase;
        private int _currentPass;
        private int _totalPasses;
        private float _phaseTimer;
        private float _sweepElapsed;
        private float _sweepDuration;
        private float _sweepArc;
        private float _laneLength;
        private float _laneWidth;
        private float _previousRadius;
        private float _warningDuration;
        private float _warningStartPitch;
        private float _sweepStartPitch;
        private bool _isTransitioningToCooldown;
        private Vector3 _groundOrigin;
        private Vector3 _centerDir;

        public RailgunLaserSweepState(IRailgunTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _hitTargets.Clear();
            _currentPass = 0;
            _isTransitioningToCooldown = false;
            _totalPasses = _boss.IsEnraged ? Mathf.Max(1, _boss.Config.Attack1EnragePassCount) : 1;
            _laneLength = _boss.Config.Attack1LaneLength;
            _laneWidth = _boss.Config.Attack1LaneWidth;
            _sweepArc = _boss.IsEnraged ? _boss.Config.Attack1EnrageSweepAngleDegrees : _boss.Config.Attack1SweepAngleDegrees;
            _sweepDuration = _boss.IsEnraged ? _boss.Config.Attack1EnrageSweepDuration : _boss.Config.Attack1SweepDuration;

            Vector3 towerPos = _boss.Transform.position;
            _groundOrigin = new Vector3(towerPos.x, 0f, towerPos.z);

            float initialWarningDuration = _boss.IsEnraged
                ? _boss.Config.Attack1EnragePreSweepWarningDuration
                : _boss.Config.Attack1PreSweepWarningDuration;
            BeginWarningPass(initialWarningDuration);
        }

        public void Exit()
        {
            if (_boss.LaserSweepLine != null)
            {
                _boss.LaserSweepLine.enabled = false;
            }

            if (_boss.ArcLaserBeam != null)
            {
                _boss.ArcLaserBeam.Hide();
            }

            if (_boss.ReusableSweepArcIndicator != null)
            {
                _boss.ReusableSweepArcIndicator.HidePersistent();
            }

            if (_boss.ReusableSweepIndicator != null)
            {
                _boss.ReusableSweepIndicator.HidePersistent();
            }

            if (!_isTransitioningToCooldown)
            {
                _boss.ResetBarrelPitch(0f);
            }

            _isTransitioningToCooldown = false;
            _hitTargets.Clear();
        }

        public void Update()
        {
            if (_phase == Phase.Warning)
            {
                _phaseTimer -= Time.deltaTime;
                float warningProgress = _warningDuration > 0.01f
                    ? Mathf.Clamp01(1f - (_phaseTimer / _warningDuration))
                    : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, warningProgress);
                _boss.SetBarrelPitch(Mathf.Lerp(_warningStartPitch, _sweepStartPitch, eased));

                if (_phaseTimer <= 0f)
                {
                    BeginSweepPass();
                }
                return;
            }

            _sweepElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_sweepElapsed / Mathf.Max(_sweepDuration, 0.05f));
            float currentRadius = _laneLength * t;

            _boss.SnapTurretAim(_groundOrigin + _centerDir * _laneLength);
            _boss.SetBarrelPitch(ComputeSweepPitchDegrees(t));
            _boss.ArcLaserBeam.ShowArcLaser(
                _groundOrigin,
                _centerDir,
                _boss.MuzzlePosition,
                currentRadius,
                _laneLength,
                _sweepArc
            );

            CheckArcBandOverlap(_previousRadius, currentRadius);
            _previousRadius = currentRadius;

            if (_sweepElapsed >= _sweepDuration)
            {
                _boss.ArcLaserBeam.Hide();
                _currentPass++;

                if (_currentPass < _totalPasses)
                {
                    BeginWarningPass(_boss.Config.Attack1EnrageReaimWarningDuration);
                }
                else
                {
                    _isTransitioningToCooldown = true;
                    _boss.ResetBarrelPitch(RailgunTowerConstants.BARREL_PITCH_RESET_DURATION);
                    _boss.ReusableSweepArcIndicator.HidePersistent();
                    _boss.ChangeState(_boss.CooldownState);
                }
            }
        }

        public void FixedUpdate()
        {
        }

        private void BeginWarningPass(float warningDuration)
        {
            _phase = Phase.Warning;
            _phaseTimer = warningDuration;
            _warningDuration = warningDuration;
            _warningStartPitch = _boss.GetBarrelPitchDegrees();
            _sweepStartPitch = ComputeSweepPitchDegrees(0f);
            _boss.LaserSweepLine.enabled = false;
            _boss.ArcLaserBeam.Hide();
            _boss.ReusableSweepIndicator.HidePersistent();

            float centerAngle = ComputeAngleToPlayer();
            _centerDir = Quaternion.Euler(0f, centerAngle, 0f) * Vector3.forward;

            _boss.SnapTurretAim(_groundOrigin + _centerDir * _laneLength);
            _boss.ReusableSweepArcIndicator.ShowArcWarning(_groundOrigin, _centerDir, _laneLength, _sweepArc, warningDuration);
            _boss.PlayAudioOneShot(RailgunTowerConstants.SFX_RAILGUN_CHARGE);
        }

        private void BeginSweepPass()
        {
            _phase = Phase.Sweeping;
            _sweepElapsed = 0f;
            _previousRadius = 0f;
            _hitTargets.Clear();

            _boss.SnapTurretAim(_groundOrigin + _centerDir * _laneLength);
            _boss.SetBarrelPitch(_sweepStartPitch);
            _boss.ArcLaserBeam.ShowArcLaser(
                _groundOrigin,
                _centerDir,
                _boss.MuzzlePosition,
                0f,
                _laneLength,
                _sweepArc
            );

            CheckArcBandOverlap(0f, 0f);
            _boss.PlayFireFeedback(RailgunTowerConstants.SFX_RAILGUN_SWEEP, includeBarrelRecoil: false);
        }

        private float ComputeSweepPitchDegrees(float t)
        {
            float horizDist = Mathf.Lerp(RailgunTowerConstants.SWEEP_PITCH_MIN_HORIZ_DISTANCE, _laneLength, t);
            return -Mathf.Atan2(_boss.BarrelPivotHeight, horizDist) * Mathf.Rad2Deg;
        }

        private float ComputeAngleToPlayer()
        {
            Vector3 playerPos = _boss.PlayerPosition;
            Vector3 toPlayer = new Vector3(playerPos.x - _groundOrigin.x, 0f, playerPos.z - _groundOrigin.z);
            if (toPlayer.sqrMagnitude < 0.001f)
            {
                toPlayer = _boss.Transform.forward;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 0.001f)
                {
                    toPlayer = Vector3.forward;
                }
            }

            toPlayer.Normalize();
            return Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
        }

        private void CheckArcBandOverlap(float previousRadius, float currentRadius)
        {
            float halfBand = Mathf.Max(0.1f, _laneWidth * 0.5f);
            float rMin = Mathf.Clamp(Mathf.Min(previousRadius, currentRadius) - halfBand, 0f, _laneLength);
            float rMax = Mathf.Clamp(Mathf.Max(previousRadius, currentRadius) + halfBand, 0f, _laneLength);
            if (rMax <= 0.01f)
            {
                return;
            }

            Vector3 sphereCenter = _groundOrigin + Vector3.up * (RailgunTowerConstants.BEAM_HITBOX_HEIGHT * 0.5f);
            float queryRadius = rMax + RailgunTowerConstants.BEAM_HITBOX_HEIGHT;

            int hitCount = Physics.OverlapSphereNonAlloc(
                sphereCenter,
                queryRadius,
                _overlapBuffer,
                EntityLayers.Player,
                QueryTriggerInteraction.Collide
            );

            float halfArcDeg = _sweepArc * 0.5f;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                _overlapBuffer[i] = null;

                if (hit == null || !hit.enabled)
                {
                    continue;
                }

                if (!IntersectsAnnularSector(hit, rMin, rMax, halfArcDeg))
                {
                    continue;
                }

                if (hit.TryGetComponent(out IDamageable damageable) || (damageable = hit.GetComponentInParent<IDamageable>()) != null)
                {
                    if (_hitTargets.Add(damageable))
                    {
                        damageable.TakeDamage(_boss.Config.Attack1Damage);
                    }
                }
            }
        }

        private bool IntersectsAnnularSector(Collider collider, float rMin, float rMax, float halfArcDeg)
        {
            Bounds bounds = collider.bounds;
            if (bounds.min.y > _groundOrigin.y + RailgunTowerConstants.BEAM_HITBOX_HEIGHT ||
                bounds.max.y < _groundOrigin.y - 0.5f)
            {
                return false;
            }

            float probeY = Mathf.Clamp(_groundOrigin.y + RailgunTowerConstants.BEAM_GROUND_Y_OFFSET, bounds.min.y, bounds.max.y);
            Vector3 probe = bounds.center;

            for (int iteration = 0; iteration < 2; iteration++)
            {
                Vector3 sectorPoint = ClampPointToAnnularSector(probe, rMin, rMax, halfArcDeg, probeY);
                Vector3 colliderPoint = collider.ClosestPoint(sectorPoint);

                float dx = colliderPoint.x - sectorPoint.x;
                float dz = colliderPoint.z - sectorPoint.z;
                if (dx * dx + dz * dz <= RailgunTowerConstants.ARC_LASER_HITBOX_TOLERANCE_SQR)
                {
                    return true;
                }

                probe = colliderPoint;
            }

            return false;
        }

        private Vector3 ClampPointToAnnularSector(Vector3 point, float rMin, float rMax, float halfArcDeg, float probeY)
        {
            float offsetX = point.x - _groundOrigin.x;
            float offsetZ = point.z - _groundOrigin.z;
            float distSqr = offsetX * offsetX + offsetZ * offsetZ;
            if (distSqr < 0.000001f)
            {
                return new Vector3(
                    _groundOrigin.x + _centerDir.x * rMin,
                    probeY,
                    _groundOrigin.z + _centerDir.z * rMin
                );
            }

            float dist = Mathf.Sqrt(distSqr);
            Vector3 dirXZ = new Vector3(offsetX / dist, 0f, offsetZ / dist);
            float signedAngleDeg = Vector3.SignedAngle(_centerDir, dirXZ, Vector3.up);
            float absAngleDeg = Mathf.Abs(signedAngleDeg);
            float clampedAngleDeg = Mathf.Clamp(signedAngleDeg, -halfArcDeg, halfArcDeg);

            float targetRadius = dist;
            if (absAngleDeg > halfArcDeg)
            {
                float excessRad = (absAngleDeg - halfArcDeg) * Mathf.Deg2Rad;
                targetRadius = excessRad < Mathf.PI * 0.5f ? dist * Mathf.Cos(excessRad) : 0f;
            }

            float clampedRadius = Mathf.Clamp(targetRadius, rMin, rMax);
            Vector3 clampedDir = Quaternion.Euler(0f, clampedAngleDeg, 0f) * _centerDir;

            return new Vector3(
                _groundOrigin.x + clampedDir.x * clampedRadius,
                probeY,
                _groundOrigin.z + clampedDir.z * clampedRadius
            );
        }
    }
}
