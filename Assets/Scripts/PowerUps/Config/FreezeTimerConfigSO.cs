using System;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Config
{
    [CreateAssetMenu(fileName = "FreezeTimerConfig", menuName = "Scriptable Objects/Power-Ups/Freeze Timer Config")]
    public class FreezeTimerConfigSO : ScriptableObject
    {
        [SerializeField, Min(0.1f), Tooltip("Seconds of scaled time the freeze lasts. A new pickup resets to this value.")]
        private float _durationSeconds = 5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds for the cold post-processing and frost overlay to fade in.")]
        private float _fadeInDuration = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Seconds before expiry when the cold presentation starts fading out.")]
        private float _fadeOutDuration = 0.5f;
        [SerializeField, Min(1), Tooltip("Preallocated capacity of the tween scratch and pause lists.")]
        private int _tweenBufferCapacity = 256;
        [SerializeField, Range(0f, 1f), Tooltip("Peak alpha of the optional frost overlay.")]
        private float _frostOverlayPeakAlpha = 1f;

        public float DurationSeconds => _durationSeconds;
        public float FadeInDuration => _fadeInDuration;
        public float FadeOutDuration => _fadeOutDuration;
        public int TweenBufferCapacity => _tweenBufferCapacity;
        public float FrostOverlayPeakAlpha => _frostOverlayPeakAlpha;

        public void ValidateConfiguration()
        {
            if (!IsFinite(_durationSeconds) || _durationSeconds <= 0f
                || !IsFinite(_fadeInDuration) || _fadeInDuration <= 0f
                || !IsFinite(_fadeOutDuration) || _fadeOutDuration <= 0f
                || _fadeInDuration + _fadeOutDuration > _durationSeconds
                || _tweenBufferCapacity < 1
                || !IsFinite(_frostOverlayPeakAlpha) || _frostOverlayPeakAlpha < 0f || _frostOverlayPeakAlpha > 1f)
            {
                throw new InvalidOperationException("Freeze timer config requires finite positive durations whose fades fit inside the duration, a positive buffer capacity, and an overlay alpha in 0-1.");
            }
        }

        private void OnValidate()
        {
            _durationSeconds = Mathf.Max(0.1f, _durationSeconds);
            _fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);
            _fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
            _tweenBufferCapacity = Mathf.Max(1, _tweenBufferCapacity);
            _frostOverlayPeakAlpha = Mathf.Clamp01(_frostOverlayPeakAlpha);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
