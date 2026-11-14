using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine.States
{
    public class RailgunCooldownState : IRailgunTowerState
    {
        private readonly IRailgunTowerBoss _boss;
        private float _remainingDuration;
        private int _attackCounter;

        public RailgunCooldownState(IRailgunTowerBoss boss)
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
            int attackIndex = _attackCounter % RailgunTowerConstants.ATTACK_COUNT;
            _attackCounter++;

            switch (attackIndex)
            {
                case 0:
                    _boss.ChangeState(_boss.LaserSweepState);
                    break;
                case 1:
                    _boss.ChangeState(_boss.ChasingLaserState);
                    break;
                case 2:
                    _boss.ChangeState(_boss.ExplosiveCrossBurstState);
                    break;
                default:
                    _boss.ChangeState(_boss.LaserSweepState);
                    break;
            }
        }
    }
}
