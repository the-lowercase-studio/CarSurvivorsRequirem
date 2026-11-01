using System;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Config
{
    [CreateAssetMenu(fileName = "BubbleShieldConfig", menuName = "Scriptable Objects/Power-Ups/Bubble Shield Config")]
    public class BubbleShieldConfigSO : ScriptableObject
    {
        [SerializeField, Min(0.1f), Tooltip("Seconds of scaled time the shield lasts. A new pickup resets to this value.")]
        private float _durationSeconds = 8f;
        [SerializeField, Min(0.01f), Tooltip("Final seconds in which the bubble and health bar blink. Must be shorter than the duration.")]
        private float _warningWindowSeconds = 2f;
        [SerializeField, Min(0.02f), Tooltip("Seconds per full blink cycle during the warning window.")]
        private float _blinkPeriodSeconds = 0.25f;
        [SerializeField, Tooltip("Bubble tint and the locked health bar color while the shield is active.")]
        private Color _shieldColor = new Color(0.05f, 0.15f, 0.6f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("Bubble alpha while the shield is steady.")]
        private float _steadyAlpha = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("Bubble alpha at the dim point of a warning blink.")]
        private float _warningMinAlpha = 0.05f;
        [SerializeField, Min(0.01f), Tooltip("Seconds for the bubble to scale and fade in.")]
        private float _appearDuration = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Seconds for the bubble to scale and fade out.")]
        private float _disappearDuration = 0.15f;

        public float DurationSeconds => _durationSeconds;
        public float WarningWindowSeconds => _warningWindowSeconds;
        public float BlinkPeriodSeconds => _blinkPeriodSeconds;
        public Color ShieldColor => _shieldColor;
        public float SteadyAlpha => _steadyAlpha;
        public float WarningMinAlpha => _warningMinAlpha;
        public float AppearDuration => _appearDuration;
        public float DisappearDuration => _disappearDuration;

        public void ValidateConfiguration()
        {
            if (!IsFinite(_durationSeconds) || _durationSeconds <= 0f
                || !IsFinite(_warningWindowSeconds) || _warningWindowSeconds <= 0f || _warningWindowSeconds >= _durationSeconds
                || !IsFinite(_blinkPeriodSeconds) || _blinkPeriodSeconds <= 0f
                || !IsFinite(_steadyAlpha) || _steadyAlpha < 0f || _steadyAlpha > 1f
                || !IsFinite(_warningMinAlpha) || _warningMinAlpha < 0f || _warningMinAlpha > _steadyAlpha
                || !IsFinite(_appearDuration) || _appearDuration <= 0f
                || !IsFinite(_disappearDuration) || _disappearDuration <= 0f)
            {
                throw new InvalidOperationException("Bubble shield config requires finite positive durations, a warning window shorter than the duration, and alphas in 0-1 with the warning minimum not above the steady alpha.");
            }
        }

        private void OnValidate()
        {
            _durationSeconds = Mathf.Max(0.1f, _durationSeconds);
            _warningWindowSeconds = Mathf.Clamp(_warningWindowSeconds, 0.01f, _durationSeconds - 0.01f);
            _blinkPeriodSeconds = Mathf.Max(0.02f, _blinkPeriodSeconds);
            _steadyAlpha = Mathf.Clamp01(_steadyAlpha);
            _warningMinAlpha = Mathf.Clamp(_warningMinAlpha, 0f, _steadyAlpha);
            _appearDuration = Mathf.Max(0.01f, _appearDuration);
            _disappearDuration = Mathf.Max(0.01f, _disappearDuration);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
