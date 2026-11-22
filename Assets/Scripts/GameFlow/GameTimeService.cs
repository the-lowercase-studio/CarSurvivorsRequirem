using System;
using Assets.Scripts.GameFlow.Constants;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.GameFlow
{
    public interface IGameTimeService
    {
        float TargetTimeScale { get; }
        bool IsPaused { get; }

        void Pause();
        void Resume();
        void SetTimeScale(float timeScale);
        void Reset();
    }

    public class GameTimeService : IGameTimeService
    {
        private readonly Action<float> _applyTimeScale;
        private float _targetTimeScale = GameTimeConstants.DEFAULT_TIME_SCALE;
        private bool _isPaused;

        public float TargetTimeScale
        {
            get
            {
                return _targetTimeScale;
            }
        }

        public bool IsPaused
        {
            get
            {
                return _isPaused;
            }
        }

        [ReflexConstructor]
        public GameTimeService() : this(ApplyUnityTimeScale)
        {
            _applyTimeScale(GameTimeConstants.DEFAULT_TIME_SCALE);
        }

        public GameTimeService(Action<float> applyTimeScale)
        {
            _applyTimeScale = applyTimeScale ?? throw new ArgumentNullException(nameof(applyTimeScale));
        }

        public void Pause()
        {
            _isPaused = true;
            _applyTimeScale(GameTimeConstants.PAUSED_TIME_SCALE);
        }

        public void Resume()
        {
            _isPaused = false;
            _applyTimeScale(_targetTimeScale);
        }

        public void SetTimeScale(float timeScale)
        {
            if (float.IsNaN(timeScale) ||
                float.IsInfinity(timeScale) ||
                timeScale < GameTimeConstants.MIN_TIME_SCALE ||
                timeScale > GameTimeConstants.MAX_TIME_SCALE)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeScale),
                    timeScale,
                    $"Time scale must be between {GameTimeConstants.MIN_TIME_SCALE} and {GameTimeConstants.MAX_TIME_SCALE}."
                );
            }

            _targetTimeScale = timeScale;
            if (!_isPaused)
            {
                _applyTimeScale(_targetTimeScale);
            }
        }

        public void Reset()
        {
            _targetTimeScale = GameTimeConstants.DEFAULT_TIME_SCALE;
            _isPaused = false;
            _applyTimeScale(GameTimeConstants.DEFAULT_TIME_SCALE);
        }

        private static void ApplyUnityTimeScale(float timeScale)
        {
            Time.timeScale = timeScale;
        }
    }
}
