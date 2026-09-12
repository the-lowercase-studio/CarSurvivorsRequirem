using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States
{
    public class MortarCooldownState : IMortarTowerState
    {
        private readonly MortarTowerBoss _boss;
        private float _remainingDuration;
        private int _attackCounter;

        public MortarCooldownState(MortarTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            float min = _boss.IsEnraged ? _boss.Config.EnrageCooldownMin : _boss.Config.CooldownMin;
            float max = _boss.IsEnraged ? _boss.Config.EnrageCooldownMax : _boss.Config.CooldownMax;
            _remainingDuration = Random.Range(min, max);
        }

        public void Exit()
        {
        }

        public void Update()
        {
            _remainingDuration -= Time.deltaTime;
            if (_remainingDuration <= 0f)
            {
                SelectNextAttack();
            }
        }

        public void FixedUpdate()
        {
        }

        public void ResetAttackCounter()
        {
            _attackCounter = 0;
        }

        private void SelectNextAttack()
        {
            int attackIndex = _attackCounter % 3;
            _attackCounter++;

            switch (attackIndex)
            {
                case 0:
                    _boss.ChangeState(_boss.ClusterBurstState);
                    break;
                case 1:
                    _boss.ChangeState(_boss.DiagonalBounceState);
                    break;
                case 2:
                    _boss.ChangeState(_boss.RollingBoulderState);
                    break;
                default:
                    _boss.ChangeState(_boss.ClusterBurstState);
                    break;
            }
        }
    }
}
