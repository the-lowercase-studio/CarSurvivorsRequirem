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
            float centerRadius = _boss.IsEnraged && _boss.Config.Attack1EnrageCenterExplosionRadius > 0f
                ? _boss.Config.Attack1EnrageCenterExplosionRadius
                : _boss.Config.Attack1CenterExplosionRadius;

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

            bool isEnraged = _boss.IsEnraged;
            float damageMultiplier = isEnraged ? _boss.Config.Attack1EnrageDamageMultiplier : 1f;
            float centerRadius = isEnraged && _boss.Config.Attack1EnrageCenterExplosionRadius > 0f
                ? _boss.Config.Attack1EnrageCenterExplosionRadius
                : _boss.Config.Attack1CenterExplosionRadius;
            float centerDamage = _boss.Config.Attack1CenterDamage * damageMultiplier;

            if (centerShell != null)
            {
                centerShell.Detonate(centerRadius, centerDamage);
            }

            int waveCount = isEnraged
                ? Mathf.Max(1, _boss.Config.Attack1EnrageWaveCount)
                : Mathf.Max(1, _boss.Config.Attack1NormalWaveCount);
            int count = isEnraged && _boss.Config.Attack1EnrageSubShellCount > 0
                ? _boss.Config.Attack1EnrageSubShellCount
                : _boss.Config.Attack1SubShellCount;
            float scatterRadius = _boss.Config.Attack1SubShellScatterRadius;
            float jumpPower = _boss.Config.Attack1SubShellJumpPower;
            float duration = isEnraged && _boss.Config.Attack1EnrageSubShellDuration > 0f
                ? _boss.Config.Attack1EnrageSubShellDuration
                : _boss.Config.Attack1SubShellDuration;
            float explosionRadius = isEnraged && _boss.Config.Attack1EnrageSubShellExplosionRadius > 0f
                ? _boss.Config.Attack1EnrageSubShellExplosionRadius
                : _boss.Config.Attack1SubShellExplosionRadius;
            float damage = _boss.Config.Attack1SubShellDamage * damageMultiplier;

            _pendingSubShells = count * waveCount;
            float ringStep = _boss.Config.Attack1EnrageSecondWaveRadius - scatterRadius;

            for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
            {
                float waveRadius = scatterRadius + waveIndex * ringStep;
                float waveDuration = duration * (1f + 0.35f * waveIndex);
                float waveJumpPower = jumpPower * (1f + 0.4f * waveIndex);
                float angularOffset = (waveIndex % 2) * (180f / Mathf.Max(1, count));

                for (int i = 0; i < count; i++)
                {
                    float angle = i * (360f / count) + angularOffset;
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * waveRadius;
                    Vector3 subTarget = centerPos + offset;

                    var subTelegraph = _boss.ShowCircularTelegraph(subTarget, explosionRadius, waveDuration);
                    Vector3 snappedSubTarget = subTelegraph != null ? subTelegraph.SnappedPosition : subTarget;

                    MortarShellProjectile subShell = _boss.ShellPool.Get();
                    if (subShell != null)
                    {
                        subShell.LaunchParabolic(
                            centerPos,
                            snappedSubTarget,
                            waveJumpPower,
                            waveDuration,
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
