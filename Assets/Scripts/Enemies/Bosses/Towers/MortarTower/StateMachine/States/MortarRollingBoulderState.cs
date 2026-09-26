using System.Collections;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles;
using Assets.Scripts.Indicators;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States
{
    public class MortarRollingBoulderState : IMortarTowerState
    {
        private readonly MortarTowerBoss _boss;
        private RectangularTelegraphIndicator _activeTelegraph;
        private Coroutine _boulderRoutine;
        private bool _isExited;

        public MortarRollingBoulderState(MortarTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _isExited = false;
            _boulderRoutine = _boss.StartCoroutine(RunBoulderAttack());
        }

        public void Exit()
        {
            _isExited = true;
            if (_boulderRoutine != null)
            {
                _boss.StopCoroutine(_boulderRoutine);
                _boulderRoutine = null;
            }

            if (_boss.BoulderHitbox != null && _boss.BoulderHitbox.IsRolling)
            {
                _boss.BoulderHitbox.Stop();
            }

            if (_activeTelegraph != null)
            {
                _activeTelegraph.Dismiss();
                _activeTelegraph = null;
            }
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }

        private IEnumerator RunBoulderAttack()
        {
            // Calculate drop position in front of tower towards player
            Vector3 towerPos = _boss.Transform.position;
            towerPos.y = 0f;
            Vector3 playerPos = _boss.PlayerPosition;
            playerPos.y = 0f;
            Vector3 toPlayer = playerPos - towerPos;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.01f)
            {
                toPlayer = _boss.Transform.forward;
                toPlayer.y = 0f;
            }
            toPlayer.Normalize();

            Vector3 dropPos = towerPos + toPlayer * 4.5f;
            dropPos.y = 0f;

            float dropWarning = _boss.Config.Attack3DropWarningDuration;
            var dropTelegraph = _boss.ShowCircularTelegraph(dropPos, 2.5f, dropWarning);
            Vector3 snappedDropPos = dropTelegraph != null ? dropTelegraph.SnappedPosition : dropPos;
            snappedDropPos.y = 0f;

            // Calculate roll direction and display rectangular hazard indicator immediately alongside circular telegraph
            Vector3 rollDir = playerPos - snappedDropPos;
            rollDir.y = 0f;
            if (rollDir.sqrMagnitude < 0.01f || Vector3.Dot(rollDir, toPlayer) <= 0f)
            {
                rollDir = toPlayer;
            }
            rollDir.Normalize();

            float rollLength = _boss.Config.Attack3RollTelegraphLength;
            float rollWidth = _boss.Config.Attack3RollTelegraphWidth;

            _activeTelegraph = _boss.ShowRectangularTelegraph(
                snappedDropPos,
                rollDir,
                rollLength,
                rollWidth,
                dropWarning,
                null,
                autoContractOnFillComplete: false
            );

            _boss.SnapTurretAim(snappedDropPos);
            _boss.PlayFireRecoil();

            MortarShellProjectile dropShell = _boss.ShellPool.Get();
            bool dropLanded = false;

            if (dropShell != null)
            {
                dropShell.LaunchParabolic(
                    _boss.MuzzlePosition,
                    snappedDropPos,
                    5.5f,
                    dropWarning,
                    () =>
                    {
                        dropLanded = true;
                        dropShell.Detonate(2.5f, 20f);
                    }
                );
            }
            else
            {
                dropLanded = true;
            }

            while (!dropLanded && !_isExited)
            {
                yield return null;
            }

            if (_isExited)
            {
                yield break;
            }

            // Launch rolling boulder instantly as soon as the drop projectile impacts the ground
            float speed = _boss.IsEnraged ? _boss.Config.Attack3EnrageRollSpeed : _boss.Config.Attack3RollSpeed;
            float damage = _boss.Config.Attack3RollDamage;
            Vector3 endPos = snappedDropPos + rollDir * rollLength;
            endPos.y = 0f;

            bool rollFinished = false;

            if (_boss.BoulderHitbox != null)
            {
                _boss.BoulderHitbox.Roll(snappedDropPos, endPos, speed, damage, () =>
                {
                    rollFinished = true;
                });
            }
            else
            {
                rollFinished = true;
            }

            while (!rollFinished && !_isExited)
            {
                yield return null;
            }

            if (_activeTelegraph != null)
            {
                _activeTelegraph.ContractAndDismiss();
                _activeTelegraph = null;
            }

            if (!_isExited)
            {
                _boss.ChangeState(_boss.CooldownState);
            }
        }
    }
}
