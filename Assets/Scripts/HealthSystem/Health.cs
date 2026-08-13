using System;
using UnityEngine;

namespace Assets.Scripts.HealthSystem
{
    public interface IHealthy
    {
        IHealth Health { get; }
    }

    public interface IHealth
    {
        float CurrentHealth { get; }
        float MaxHealth { get; set; }

        event EventHandler OnHealthChanged;
        event EventHandler OnHealthDecreased;
        event EventHandler OnHealthIncreased;
        event EventHandler OnNoHealth;

        void DecreaseHealth(float value);
        void IncreaseHealth(float value);
        bool IsAlive();
    }

    [Serializable]
    public class Health : MonoBehaviour, IHealth
    {
        [field: SerializeField] public float MaxHealth { get; set; }

        public float CurrentHealth { get; protected set; }

        public event EventHandler OnHealthChanged;
        public event EventHandler OnHealthDecreased;
        public event EventHandler OnHealthIncreased;
        public event EventHandler OnNoHealth;

        private bool _isAlive;

        protected virtual void OnEnable()
        {
            OnHealthDecreased += InvokeOnHealthChanged;
            OnHealthIncreased += InvokeOnHealthChanged;
            OnNoHealth += InvokeOnHealthChanged;

            _isAlive = true;
            CurrentHealth = MaxHealth;
        }

        protected virtual void OnDisable()
        {
            OnHealthDecreased -= InvokeOnHealthChanged;
            OnHealthIncreased -= InvokeOnHealthChanged;
            OnNoHealth -= InvokeOnHealthChanged;
        }

        public void DecreaseHealth(float value)
        {
            if (!_isAlive)
            {
                return;
            }

            if (CurrentHealth > value)
            {
                CurrentHealth -= value;
                OnHealthDecreased?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                CurrentHealth = 0;
                _isAlive = false;
                OnNoHealth?.Invoke(this, EventArgs.Empty);
            }
        }

        public void IncreaseHealth(float value)
        {
            if (!_isAlive)
            {
                return;
            }

            if (CurrentHealth + value < MaxHealth)
            {
                CurrentHealth += value;
                OnHealthIncreased?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                CurrentHealth = MaxHealth;
            }
        }

        public bool IsAlive()
        {
            return _isAlive;
        }

        private void InvokeOnHealthChanged(object sender, EventArgs e)
        {
            OnHealthChanged?.Invoke(sender, e);
        }
    }
}

