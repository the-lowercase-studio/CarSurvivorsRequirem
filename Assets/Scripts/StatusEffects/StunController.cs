using System;
using UnityEngine;

namespace Assets.Scripts.StatusEffects
{
    public interface IStunController
    {
        public bool IsStunned { get; }

        public event EventHandler OnStunEnd;

        public event EventHandler OnStunStart;

        public event EventHandler OnStunExtended;

        public void PerformStun(float duration);
    }

    public class StunController : MonoBehaviour, IStunController
    {
        public bool IsStunned { get; private set; }

        public event EventHandler OnStunEnd;

        public event EventHandler OnStunStart;

        public event EventHandler OnStunExtended;

        private float _stunTimer;

        private void Update()
        {
            if (IsStunned)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer <= 0f)
                {
                    IsStunned = false;
                    OnStunEnd?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void PerformStun(float duration)
        {
            if (!IsStunned)
            {
                IsStunned = true;
                _stunTimer = duration;
                OnStunStart?.Invoke(this, EventArgs.Empty);
            }
            else if (_stunTimer < duration)
            {
                _stunTimer = duration;
                OnStunExtended?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
