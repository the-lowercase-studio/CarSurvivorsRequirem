using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States
{
    public class MortarClusterBurstState : IMortarTowerState
    {
        private readonly MortarTowerBoss _boss;
        private int _pendingSubShells;
        private bool _isExited;

        public MortarClusterBurstState(MortarTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _isExited = false;
            _pendingSubShells = 0;

            Vector3 playerPos = _boss.PlayerPosition;
            float warningDuration = _boss.Config.Attack1InitialWarning;
            float centerRadius = _boss.Config.Attack1CenterExplosionRadius;

            var telegraph = _boss.ShowCircularTelegraph(playerPos, centerRadius, warningDuration);
            Vector3 targetPosition = telegraph != null ? telegraph.SnappedPosition : playerPos;

            _boss.SnapTurretAim(targetPosition);
            _boss.PlayFireRecoil();

            MortarShellProjectile centerShell = _boss.ShellPool.Get();
            if (centerShell != null)
            {
                centerShell.LaunchParabolic(
                    _boss.MuzzlePosition,
                    targetPosition,
                    6.0f,
                    warningDuration,
                    () =>
                    {
                        OnCenterLanded(targetPosition, centerShell);
                    }
                );
            }
            else
            {
                OnCenterLanded(targetPosition, null);
            }
        }

        public void Exit()
        {
            _isExited = true;
            _pendingSubShells = 0;
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }

        private void OnCenterLanded(Vector3 centerPos, MortarShellProjectile centerShell)
        {
            if (_isExited)
            {
                return;
            }

            if (centerShell != null)
            {
                centerShell.Detonate(_boss.Config.Attack1CenterExplosionRadius, _boss.Config.Attack1CenterDamage);
            }

            int count = _boss.Config.Attack1SubShellCount;
            float scatterRadius = _boss.Config.Attack1SubShellScatterRadius;
            float jumpPower = _boss.Config.Attack1SubShellJumpPower;
            float duration = _boss.Config.Attack1SubShellDuration;
            float explosionRadius = _boss.Config.Attack1SubShellExplosionRadius;
            float damage = _boss.Config.Attack1SubShellDamage;

            bool isEnraged = _boss.IsEnraged;
            int totalSubShells = count + (isEnraged ? count : 0);
            _pendingSubShells = totalSubShells;

            // Wave 1: Hexagonal scatter
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * scatterRadius;
                Vector3 subTarget = centerPos + offset;

                var subTelegraph = _boss.ShowCircularTelegraph(subTarget, explosionRadius, duration);
                Vector3 snappedSubTarget = subTelegraph != null ? subTelegraph.SnappedPosition : subTarget;

                MortarShellProjectile subShell = _boss.ShellPool.Get();
                if (subShell != null)
                {
                    subShell.LaunchParabolic(
                        centerPos,
                        snappedSubTarget,
                        jumpPower,
                        duration,
                        () =>
                        {
                            subShell.Detonate(explosionRadius, damage);
                            OnSubShellCompleted();
                        }
                    );
                }
                else
                {
                    OnSubShellCompleted();
                }
            }

            // Wave 2: Outer concentric ring when enraged
            if (isEnraged)
            {
                float wave2Radius = _boss.Config.Attack1EnrageSecondWaveRadius;
                float wave2Duration = duration * 1.35f;
                float wave2JumpPower = jumpPower * 1.4f;

                for (int j = 0; j < count; j++)
                {
                    float angle2 = (j * (360f / count)) + 30f;
                    Vector3 offset2 = Quaternion.Euler(0f, angle2, 0f) * Vector3.forward * wave2Radius;
                    Vector3 subTarget2 = centerPos + offset2;

                    var subTelegraph2 = _boss.ShowCircularTelegraph(subTarget2, explosionRadius, wave2Duration);
                    Vector3 snappedSubTarget2 = subTelegraph2 != null ? subTelegraph2.SnappedPosition : subTarget2;

                    MortarShellProjectile subShell2 = _boss.ShellPool.Get();
                    if (subShell2 != null)
                    {
                        subShell2.LaunchParabolic(
                            centerPos,
                            snappedSubTarget2,
                            wave2JumpPower,
                            wave2Duration,
                            () =>
                            {
                                subShell2.Detonate(explosionRadius, damage);
                                OnSubShellCompleted();
                            }
                        );
                    }
                    else
                    {
                        OnSubShellCompleted();
                    }
                }
            }
        }

        private void OnSubShellCompleted()
        {
            if (_isExited)
            {
                return;
            }

            _pendingSubShells--;
            if (_pendingSubShells <= 0)
            {
                _boss.ChangeState(_boss.CooldownState);
            }
        }
    }
}
