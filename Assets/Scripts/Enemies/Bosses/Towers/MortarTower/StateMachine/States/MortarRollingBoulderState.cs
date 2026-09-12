using System.Collections;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States
{
    public class MortarRollingBoulderState : IMortarTowerState
    {
        private readonly MortarTowerBoss _boss;
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

            // Direction towards current player position
            Vector3 targetPlayerPos = _boss.PlayerPosition;
            targetPlayerPos.y = 0f;
            Vector3 rollDir = targetPlayerPos - snappedDropPos;
            rollDir.y = 0f;
            if (rollDir.sqrMagnitude < 0.01f)
            {
                rollDir = toPlayer;
            }
            rollDir.Normalize();

            float rollLength = _boss.Config.Attack3RollTelegraphLength;
            float rollWidth = _boss.Config.Attack3RollTelegraphWidth;
            const float AIM_TELEGRAPH_DURATION = 0.6f;

            _boss.ShowRectangularTelegraph(snappedDropPos, rollDir, rollLength, rollWidth, AIM_TELEGRAPH_DURATION);

            float aimTimer = 0f;
            while (aimTimer < AIM_TELEGRAPH_DURATION && !_isExited)
            {
                aimTimer += Time.deltaTime;
                yield return null;
            }

            if (_isExited)
            {
                yield break;
            }

            // Launch rolling boulder
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

            if (!_isExited)
            {
                _boss.ChangeState(_boss.CooldownState);
            }
        }
    }
}
