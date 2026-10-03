using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States
{
    public class MortarDiagonalBounceState : IMortarTowerState
    {
        private static readonly float[] _diagonalAngles = new float[] { 45f, 135f, 225f, 315f };

        private readonly MortarTowerBoss _boss;
        private readonly List<Coroutine> _rayCoroutines = new List<Coroutine>();
        private Coroutine _salvoCoroutine;
        private bool _isExited;

        public MortarDiagonalBounceState(MortarTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _isExited = false;
            _rayCoroutines.Clear();
            _salvoCoroutine = _boss.StartCoroutine(RunSalvosRoutine());
        }

        public void Exit()
        {
            _isExited = true;
            if (_salvoCoroutine != null)
            {
                _boss.StopCoroutine(_salvoCoroutine);
                _salvoCoroutine = null;
            }

            for (int i = 0; i < _rayCoroutines.Count; i++)
            {
                if (_rayCoroutines[i] != null)
                {
                    _boss.StopCoroutine(_rayCoroutines[i]);
                }
            }
            _rayCoroutines.Clear();
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }

        private IEnumerator RunSalvosRoutine()
        {
            int salvoCount = _boss.IsEnraged ? _boss.Config.Attack2EnrageSalvoCount : 1;

            for (int s = 0; s < salvoCount; s++)
            {
                if (_isExited)
                {
                    yield break;
                }

                yield return RunSingleSalvo();

                if (s < salvoCount - 1)
                {
                    yield return new WaitForSeconds(_boss.Config.Attack2EnrageSalvoInterval);
                }
            }

            if (!_isExited)
            {
                _boss.ChangeState(_boss.CooldownState);
            }
        }

        private IEnumerator RunSingleSalvo()
        {
            // Pick offset point around player
            Vector3 playerPos = _boss.PlayerPosition;
            float randomDist = Random.Range(_boss.Config.Attack2CenterMinOffset, _boss.Config.Attack2CenterMaxOffset);
            float randomAngle = Random.Range(0f, 360f);
            Vector3 offset = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward * randomDist;
            Vector3 targetCenter = playerPos + offset;

            float warning = _boss.Config.Attack2InitialWarning;
            var telegraph = _boss.ShowCircularTelegraph(targetCenter, _boss.Config.Attack2BounceExplosionRadius * 1.2f, warning);
            Vector3 snappedCenter = telegraph != null ? telegraph.SnappedPosition : targetCenter;

            _boss.SnapTurretAim(snappedCenter);
            _boss.PlayFireRecoil();

            MortarShellProjectile centerShell = _boss.ShellPool.Get();
            bool centerLanded = false;

            if (centerShell != null)
            {
                centerShell.LaunchParabolic(
                    _boss.MuzzlePosition,
                    snappedCenter,
                    5.0f,
                    warning,
                    () =>
                    {
                        centerLanded = true;
                        centerShell.Detonate(_boss.Config.Attack2BounceExplosionRadius, _boss.Config.Attack2BounceDamage);
                    }
                );
            }
            else
            {
                centerLanded = true;
            }

            while (!centerLanded && !_isExited)
            {
                yield return null;
            }

            if (_isExited)
            {
                yield break;
            }

            // Launch 4 diagonal rays with 3 bounces each in parallel
            int activeRays = _diagonalAngles.Length;

            for (int r = 0; r < _diagonalAngles.Length; r++)
            {
                float angle = _diagonalAngles[r];
                Vector3 rayDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Coroutine c = _boss.StartCoroutine(RunRayBounces(snappedCenter, rayDirection, () =>
                {
                    activeRays--;
                }));
                _rayCoroutines.Add(c);
            }

            while (activeRays > 0 && !_isExited)
            {
                yield return null;
            }
        }

        private IEnumerator RunRayBounces(Vector3 centerOrigin, Vector3 rayDir, System.Action onRayCompleted)
        {
            int stepCount = _boss.Config.Attack2BounceStepCount;
            float stepDistance = _boss.Config.Attack2BounceStepDistance;
            float jumpPower = _boss.Config.Attack2BounceJumpPower;
            float duration = _boss.Config.Attack2BounceDurationPerStep;
            float explosionRadius = _boss.Config.Attack2BounceExplosionRadius;
            float damage = _boss.Config.Attack2BounceDamage;

            Vector3 previousLanding = centerOrigin;

            for (int step = 1; step <= stepCount; step++)
            {
                if (_isExited)
                {
                    onRayCompleted?.Invoke();
                    yield break;
                }

                Vector3 stepTarget = centerOrigin + rayDir * (step * stepDistance);
                var telegraph = _boss.ShowCircularTelegraph(stepTarget, explosionRadius, duration);
                Vector3 snappedTarget = telegraph != null ? telegraph.SnappedPosition : stepTarget;

                MortarShellProjectile shell = _boss.ShellPool.Get();
                bool stepLanded = false;

                if (shell != null)
                {
                    shell.LaunchParabolic(
                        previousLanding,
                        snappedTarget,
                        jumpPower,
                        duration,
                        () =>
                        {
                            stepLanded = true;
                            shell.Detonate(explosionRadius, damage);
                        }
                    );
                }
                else
                {
                    stepLanded = true;
                }

                while (!stepLanded && !_isExited)
                {
                    yield return null;
                }

                previousLanding = snappedTarget;
            }

            onRayCompleted?.Invoke();
        }
    }
}
