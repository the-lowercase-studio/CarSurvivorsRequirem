using System;
using UnityEngine;

namespace Assets.Scripts.HealthSystem
{
    public interface IRegenativeHealth : IHealth
    {
        float MaxRegenerationAmount { get; }
        float CurrentRegenerationAmount { get; }
        float CurrentRegenerationDelay { get; }
        float StartRegenerationDelay { get; }
    }

    [Serializable]
    public class RegenativeHealth : Health, IRegenativeHealth
    {
        [field: SerializeField] public float MaxRegenerationAmount { get; private set; }
        [field: SerializeField] public float StartRegenerationDelay { get; private set; }

        public float CurrentRegenerationAmount { get; private set; }
        public float CurrentRegenerationDelay { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();

            CurrentRegenerationAmount = MaxRegenerationAmount;
            CurrentRegenerationDelay = StartRegenerationDelay;
        }

        private void Update()
        {
            AdvanceRegeneration(Time.deltaTime);
        }

        public void AdvanceRegeneration(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            if (CurrentHealth < MaxHealth && MaxRegenerationAmount > 0 && StartRegenerationDelay > 0)
            {
                RegenerationProcess(deltaTime);
            }
        }

        private void RegenerationProcess(float deltaTime)
        {
            if (CurrentRegenerationDelay > 0)
            {
                CurrentRegenerationDelay -= deltaTime;
            }
            else
            {
                if (CurrentHealth > 0)
                {
                    IncreaseHealth(MaxRegenerationAmount);
                    CurrentRegenerationDelay = StartRegenerationDelay;
                }
            }
        }
    }
}

