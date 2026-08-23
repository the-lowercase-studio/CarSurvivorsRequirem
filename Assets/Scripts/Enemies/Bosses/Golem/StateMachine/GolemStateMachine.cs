using System;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine
{
    public class GolemStateMachine
    {
        public IGolemState CurrentState { get; private set; }

        public float LeapCooldownTimer { get; set; }
        public float StompCooldownTimer { get; set; }
        public float LinearFistCooldownTimer { get; set; }
        public float SkyBarrageCooldownTimer { get; set; }

        public void Initialize(IGolemState startingState)
        {
            CurrentState = startingState;
            CurrentState?.Enter();
        }

        public void ChangeState(IGolemState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState?.Enter();
        }

        public void Update()
        {
            CurrentState?.Update();
        }

        public void FixedUpdate()
        {
            CurrentState?.FixedUpdate();
        }

        public void TickCooldowns(float deltaTime)
        {
            if (LeapCooldownTimer > 0f) LeapCooldownTimer -= deltaTime;
            if (StompCooldownTimer > 0f) StompCooldownTimer -= deltaTime;
            if (LinearFistCooldownTimer > 0f) LinearFistCooldownTimer -= deltaTime;
            if (SkyBarrageCooldownTimer > 0f) SkyBarrageCooldownTimer -= deltaTime;
        }

        public void ResetCooldowns(float initialLeap, float initialStomp, float initialLinearFist, float initialSkyBarrage)
        {
            LeapCooldownTimer = initialLeap;
            StompCooldownTimer = initialStomp;
            LinearFistCooldownTimer = initialLinearFist;
            SkyBarrageCooldownTimer = initialSkyBarrage;
        }
    }
}
