using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Combat;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Projectiles;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine.States
{
    public class RailgunExplosiveCrossBurstState : IRailgunTowerState
    {
        private readonly struct BurstTarget
        {
            public BurstTarget(Vector3 position, float signedYaw)
            {
                Position = position;
                SignedYaw = signedYaw;
            }

            public Vector3 Position { get; }
            public float SignedYaw { get; }
        }

        private readonly IRailgunTowerBoss _boss;
        private readonly HashSet<IDamageable> _centerHitTargets = new HashSet<IDamageable>();
        private readonly Collider[] _overlapBuffer = new Collider[RailgunTowerConstants.OVERLAP_BUFFER_SIZE];
        private readonly List<BurstTarget> _plannedTargets = new List<BurstTarget>(RailgunTowerConstants.ORB_POOL_CAPACITY);

        private Coroutine _volleyCoroutine;
        private int _pendingCompletions;
        private bool _isExited;
        private bool _isTransitioningToCooldown;

        public RailgunExplosiveCrossBurstState(IRailgunTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _isExited = false;
            _isTransitioningToCooldown = false;
            _pendingCompletions = 0;
            _centerHitTargets.Clear();
            _plannedTargets.Clear();
            _volleyCoroutine = _boss.StartBossCoroutine(RunVolleyRoutine());
        }

        public void Exit()
        {
            _isExited = true;
            if (_volleyCoroutine != null)
            {
                _boss.StopBossCoroutine(_volleyCoroutine);
                _volleyCoroutine = null;
            }

            if (!_isTransitioningToCooldown)
            {
                _boss.ResetBarrelPitch(0f);
            }

            _pendingCompletions = 0;
            _centerHitTargets.Clear();
            _plannedTargets.Clear();
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }

        private IEnumerator RunVolleyRoutine()
        {
            int orbCount = _boss.IsEnraged ? _boss.Config.Attack3EnrageProjectileCount : _boss.Config.Attack3NormalProjectileCount;
            if (orbCount <= 0)
            {
                _volleyCoroutine = null;
                _isTransitioningToCooldown = true;
                _boss.ChangeState(_boss.CooldownState);
                yield break;
            }

            float flightDuration = _boss.IsEnraged ? _boss.Config.Attack3EnrageFlightDuration : _boss.Config.Attack3NormalFlightDuration;
            float stagger = _boss.Config.Attack3LaunchStagger;
            float centerRadius = _boss.Config.Attack3CenterImpactRadius;
            float jitterRange = _boss.Config.Attack3AngleJitterDegrees;
            float baseAngle = Random.Range(0f, 360f);
            float angleStep = 180f / Mathf.Max(orbCount, 1);

            Vector3 towerPos = _boss.Transform.position;
            Vector3 towerXZ = new Vector3(towerPos.x, 0f, towerPos.z);
            float maxTargetRadius = Mathf.Max(2f, _boss.Config.ArenaRadius - RailgunTowerConstants.ARENA_EDGE_SAFETY_MARGIN);

            BuildOrderedTargets(orbCount, towerXZ, maxTargetRadius);
            yield return PreAimAndElevateRoutine(_plannedTargets[0].Position);

            if (_isExited)
            {
                yield break;
            }

            _pendingCompletions = orbCount;
            float burstRecoilDuration = stagger > 0f
                ? Mathf.Min(RailgunTowerConstants.CROSS_BURST_MAX_RECOIL_DURATION, stagger * 0.85f)
                : RailgunTowerConstants.CROSS_BURST_MAX_RECOIL_DURATION;

            for (int i = 0; i < orbCount; i++)
            {
                if (_isExited)
                {
                    yield break;
                }

                float lineAngle = baseAngle + i * angleStep + Random.Range(-jitterRange, jitterRange);
                Vector3 candidateTarget = _plannedTargets[i].Position;

                var telegraph = _boss.ShowCircularTelegraph(candidateTarget, centerRadius, flightDuration);
                Vector3 snappedTarget = telegraph != null ? telegraph.SnappedPosition : candidateTarget;
                snappedTarget.y = 0f;

                _boss.PlayBurstFireFeedback(
                    RailgunTowerConstants.SFX_RAILGUN_ORB_FIRE,
                    RailgunTowerConstants.CROSS_BURST_BARREL_PITCH_DEGREES,
                    burstRecoilDuration
                );

                RailgunOrbProjectile orb = _boss.OrbPool.Get();
                if (orb != null)
                {
                    orb.Launch(
                        _boss.MuzzlePosition,
                        snappedTarget,
                        flightDuration,
                        () =>
                        {
                            OnOrbLanded(orb, snappedTarget, lineAngle);
                        }
                    );
                }
                else
                {
                    OnOrbLanded(null, snappedTarget, lineAngle);
                }

                if (i < orbCount - 1 && stagger > 0f)
                {
                    yield return PanTurretDuringStaggerRoutine(_plannedTargets[i + 1].Position, stagger);
                }
            }

            if (!_isExited)
            {
                yield return WaitFrozenSafeRoutine(burstRecoilDuration);
                if (!_isExited)
                {
                    _boss.ResetBarrelPitch(RailgunTowerConstants.BARREL_PITCH_RESET_DURATION);
                }
            }

            _volleyCoroutine = null;
        }

        private void BuildOrderedTargets(int orbCount, Vector3 towerXZ, float maxTargetRadius)
        {
            _plannedTargets.Clear();

            Vector3 playerPos = _boss.PlayerPosition;
            Vector3 playerXZ = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 referenceForward = new Vector3(playerXZ.x - towerXZ.x, 0f, playerXZ.z - towerXZ.z);
            if (referenceForward.sqrMagnitude <= 0.001f && _boss.TurretHousing != null)
            {
                referenceForward = _boss.TurretHousing.forward;
                referenceForward.y = 0f;
            }
            if (referenceForward.sqrMagnitude <= 0.001f)
            {
                referenceForward = Vector3.forward;
            }
            referenceForward.Normalize();

            float minOffset = Mathf.Max(0f, _boss.Config.Attack3TargetOffsetMin);
            float maxOffset = Mathf.Max(minOffset, _boss.Config.Attack3TargetOffsetMax);
            float minSpacing = Mathf.Max(0f, _boss.Config.Attack3MinTargetSpacing);
            float minSpacingSqr = minSpacing * minSpacing;

            Vector3 zoneCenter = ClampZoneCenter(playerXZ, towerXZ, maxOffset, maxTargetRadius);

            for (int i = 0; i < orbCount; i++)
            {
                Vector3 target = PickSpacedTarget(zoneCenter, towerXZ, minOffset, maxOffset, maxTargetRadius, minSpacingSqr);
                Vector3 fromTower = target - towerXZ;
                fromTower.y = 0f;
                float signedYaw = fromTower.sqrMagnitude > 0.001f
                    ? Vector3.SignedAngle(referenceForward, fromTower, Vector3.up)
                    : 0f;

                _plannedTargets.Add(new BurstTarget(target, signedYaw));
            }

            _plannedTargets.Sort(CompareBurstTargets);

            if (_plannedTargets.Count > 1 && _boss.TurretHousing != null)
            {
                Quaternion currentYaw = _boss.TurretHousing.rotation;
                float angleToFirst = Quaternion.Angle(currentYaw, _boss.GetTurretAimRotation(_plannedTargets[0].Position));
                float angleToLast = Quaternion.Angle(currentYaw, _boss.GetTurretAimRotation(_plannedTargets[_plannedTargets.Count - 1].Position));
                if (angleToLast < angleToFirst)
                {
                    _plannedTargets.Reverse();
                }
            }
        }

        private static int CompareBurstTargets(BurstTarget a, BurstTarget b)
        {
            return a.SignedYaw.CompareTo(b.SignedYaw);
        }

        private IEnumerator PreAimAndElevateRoutine(Vector3 firstTarget)
        {
            Quaternion startYaw = _boss.TurretHousing != null ? _boss.TurretHousing.rotation : _boss.GetTurretAimRotation(firstTarget);
            Quaternion targetYaw = _boss.GetTurretAimRotation(firstTarget);
            float angleDegrees = Quaternion.Angle(startYaw, targetYaw);
            float duration = Mathf.Clamp(
                angleDegrees / RailgunTowerConstants.CROSS_BURST_PRE_AIM_DEG_PER_SEC,
                RailgunTowerConstants.CROSS_BURST_MIN_PRE_AIM_DURATION,
                RailgunTowerConstants.CROSS_BURST_MAX_PRE_AIM_DURATION
            );

            float startPitch = _boss.GetBarrelPitchDegrees();
            float targetPitch = RailgunTowerConstants.CROSS_BURST_BARREL_PITCH_DEGREES;

            float elapsed = 0f;
            while (elapsed < duration && !_isExited)
            {
                if (!_boss.IsTimeFrozen)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float smooth = Mathf.SmoothStep(0f, 1f, progress);
                    _boss.SetTurretAimRotation(Quaternion.Slerp(startYaw, targetYaw, smooth));
                    _boss.SetBarrelPitch(Mathf.Lerp(startPitch, targetPitch, smooth));
                }

                yield return null;
            }

            if (!_isExited)
            {
                _boss.SetTurretAimRotation(targetYaw);
                _boss.SetBarrelPitch(targetPitch);
            }
        }

        private IEnumerator PanTurretDuringStaggerRoutine(Vector3 nextTarget, float duration)
        {
            Quaternion startYaw = _boss.TurretHousing != null ? _boss.TurretHousing.rotation : _boss.GetTurretAimRotation(nextTarget);
            Quaternion endYaw = _boss.GetTurretAimRotation(nextTarget);

            float elapsed = 0f;
            while (elapsed < duration && !_isExited)
            {
                if (!_boss.IsTimeFrozen)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float smooth = Mathf.SmoothStep(0f, 1f, progress);
                    _boss.SetTurretAimRotation(Quaternion.Slerp(startYaw, endYaw, smooth));
                }

                yield return null;
            }

            if (!_isExited)
            {
                _boss.SetTurretAimRotation(endYaw);
            }
        }

        private IEnumerator WaitFrozenSafeRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_isExited)
            {
                if (!_boss.IsTimeFrozen)
                {
                    elapsed += Time.deltaTime;
                }

                yield return null;
            }
        }

        private static Vector3 ClampZoneCenter(Vector3 playerXZ, Vector3 towerXZ, float maxOffset, float maxTargetRadius)
        {
            Vector3 centerOffsetFromTower = new Vector3(playerXZ.x - towerXZ.x, 0f, playerXZ.z - towerXZ.z);
            float maxZoneCenterDist = Mathf.Max(0f, maxTargetRadius - maxOffset * RailgunTowerConstants.CROSS_BURST_ZONE_CENTER_EDGE_PULL_FACTOR);
            if (centerOffsetFromTower.sqrMagnitude > maxZoneCenterDist * maxZoneCenterDist && centerOffsetFromTower.sqrMagnitude > 0.0001f)
            {
                return towerXZ + centerOffsetFromTower.normalized * maxZoneCenterDist;
            }

            return playerXZ;
        }

        private Vector3 PickSpacedTarget(Vector3 zoneCenter, Vector3 towerXZ, float minOffset, float maxOffset, float maxTargetRadius, float minSpacingSqr)
        {
            Vector3 bestFallback = zoneCenter;
            float bestMinDistSqr = -1f;

            if (TrySampleSpacedTarget(
                zoneCenter,
                towerXZ,
                minOffset,
                maxOffset,
                maxTargetRadius,
                minSpacingSqr,
                RailgunTowerConstants.CROSS_BURST_TARGET_SAMPLE_ATTEMPTS,
                ref bestFallback,
                ref bestMinDistSqr,
                out Vector3 validCandidate))
            {
                return validCandidate;
            }

            if (TrySampleSpacedTarget(
                zoneCenter,
                towerXZ,
                0f,
                maxOffset,
                maxTargetRadius,
                minSpacingSqr,
                RailgunTowerConstants.CROSS_BURST_BEST_CANDIDATE_ATTEMPTS,
                ref bestFallback,
                ref bestMinDistSqr,
                out validCandidate))
            {
                return validCandidate;
            }

            return bestFallback;
        }

        private bool TrySampleSpacedTarget(
            Vector3 zoneCenter,
            Vector3 towerXZ,
            float minRadius,
            float maxRadius,
            float maxTargetRadius,
            float minSpacingSqr,
            int maxAttempts,
            ref Vector3 bestFallback,
            ref float bestMinDistSqr,
            out Vector3 validCandidate)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 candidate = SampleCandidateInZone(zoneCenter, towerXZ, minRadius, maxRadius, maxTargetRadius);
                float minDistSqr = ComputeMinDistanceSqrToPlanned(candidate);
                if (_plannedTargets.Count == 0 || minDistSqr >= minSpacingSqr)
                {
                    validCandidate = candidate;
                    return true;
                }

                if (minDistSqr > bestMinDistSqr)
                {
                    bestMinDistSqr = minDistSqr;
                    bestFallback = candidate;
                }
            }

            validCandidate = bestFallback;
            return false;
        }

        private static Vector3 SampleCandidateInZone(Vector3 zoneCenter, Vector3 towerXZ, float minRadius, float maxRadius, float maxTargetRadius)
        {
            float minDistSqr = minRadius * minRadius;
            float maxDistSqr = maxRadius * maxRadius;
            float offsetDistance = maxDistSqr > minDistSqr
                ? Mathf.Sqrt(Random.Range(minDistSqr, maxDistSqr))
                : minRadius;
            float offsetAngleRad = Random.Range(0f, Mathf.PI * 2f);

            Vector3 candidate = new Vector3(
                zoneCenter.x + Mathf.Sin(offsetAngleRad) * offsetDistance,
                0f,
                zoneCenter.z + Mathf.Cos(offsetAngleRad) * offsetDistance
            );

            Vector3 fromTower = candidate - towerXZ;
            fromTower.y = 0f;
            if (fromTower.sqrMagnitude > maxTargetRadius * maxTargetRadius && fromTower.sqrMagnitude > 0.0001f)
            {
                candidate = towerXZ + fromTower.normalized * maxTargetRadius;
            }

            return candidate;
        }

        private float ComputeMinDistanceSqrToPlanned(Vector3 candidate)
        {
            float minDistSqr = float.MaxValue;
            for (int i = 0; i < _plannedTargets.Count; i++)
            {
                Vector3 existing = _plannedTargets[i].Position;
                float dx = candidate.x - existing.x;
                float dz = candidate.z - existing.z;
                float distSqr = dx * dx + dz * dz;
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                }
            }

            return minDistSqr;
        }

        private void OnOrbLanded(RailgunOrbProjectile orb, Vector3 impactPos, float lineAngle)
        {
            if (_isExited)
            {
                return;
            }

            _centerHitTargets.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(
                impactPos,
                _boss.Config.Attack3CenterImpactRadius,
                _overlapBuffer,
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
                    _centerHitTargets.Add(damageable);
                }
            }

            if (_centerHitTargets.Count > 0)
            {
                // Center impact hit the player: deal highest center damage and suppress bidirectional wave split.
                foreach (IDamageable target in _centerHitTargets)
                {
                    target.TakeDamage(_boss.Config.Attack3CenterImpactDamage);
                }

                if (orb != null)
                {
                    orb.PlayImpactAndScheduleReturn(RailgunTowerConstants.SFX_RAILGUN_ORB_IMPACT);
                }
                else
                {
                    _boss.PlayAudioOneShot(RailgunTowerConstants.SFX_RAILGUN_ORB_IMPACT);
                }

                DecrementPendingCompletion();
                return;
            }

            // Player avoided the center impact: split bidirectionally (0 deg and 180 deg) across the arena.
            _boss.PlayAudioOneShot(RailgunTowerConstants.SFX_RAILGUN_WAVE_BURST);
            if (orb != null)
            {
                orb.PlayImpactAndScheduleReturn(null);
            }

            RailgunLinearWaveHitbox wavePair = _boss.WavePool.Get();
            if (wavePair != null)
            {
                Vector3 lineDirection = Quaternion.Euler(0f, lineAngle, 0f) * Vector3.forward;
                wavePair.LaunchBidirectional(
                    impactPos,
                    lineDirection,
                    _boss.Config.Attack3WaveHalfLength,
                    _boss.Config.Attack3WaveWidth,
                    _boss.Config.Attack3WaveSpeed,
                    _boss.Config.Attack3WaveDamage,
                    DecrementPendingCompletion
                );
            }
            else
            {
                DecrementPendingCompletion();
            }
        }

        private void DecrementPendingCompletion()
        {
            if (_isExited)
            {
                return;
            }

            _pendingCompletions--;
            if (_pendingCompletions <= 0)
            {
                _isTransitioningToCooldown = true;
                _boss.ChangeState(_boss.CooldownState);
            }
        }
    }
}
