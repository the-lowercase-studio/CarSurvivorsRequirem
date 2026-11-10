using System;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Config
{
    [CreateAssetMenu(fileName = "NukeConfig", menuName = "Scriptable Objects/Power-Ups/Nuke Config")]
    public class NukeConfigSO : ScriptableObject
    {
        [SerializeField, Range(0f, 100f), Tooltip("Percent of MaxHealth dealt to each visible engaged boss.")]
        private float _bossDamagePercent = 15f;
        [SerializeField, Min(0f), Tooltip("Meters added around a regular enemy position when testing camera visibility.")]
        private float _enemyVisibilityPadding = 1f;
        [SerializeField, Min(0f), Tooltip("Meters added around a boss position when testing camera visibility.")]
        private float _bossVisibilityPadding = 4f;
        [SerializeField, Min(1), Tooltip("Preallocated capacity of the active enemy snapshot list.")]
        private int _snapshotCapacity = 512;
        [SerializeField, Min(0.01f), Tooltip("Seconds for the blast post-processing and dirt overlay to reach full strength.")]
        private float _flashInDuration = 0.1f;
        [SerializeField, Min(0.02f), Tooltip("Total seconds of the blast post-processing and dirt overlay presentation.")]
        private float _blastTotalDuration = 3f;
        [SerializeField, Range(0f, 1f), Tooltip("Peak alpha of the optional dirt overlay.")]
        private float _dirtOverlayPeakAlpha = 1f;

        public float BossDamagePercent => _bossDamagePercent;
        public float EnemyVisibilityPadding => _enemyVisibilityPadding;
        public float BossVisibilityPadding => _bossVisibilityPadding;
        public int SnapshotCapacity => _snapshotCapacity;
        public float FlashInDuration => _flashInDuration;
        public float BlastTotalDuration => _blastTotalDuration;
        public float DirtOverlayPeakAlpha => _dirtOverlayPeakAlpha;

        public void ValidateConfiguration()
        {
            if (!IsFinite(_bossDamagePercent) || _bossDamagePercent < 0f || _bossDamagePercent > 100f
                || !IsFinite(_enemyVisibilityPadding) || _enemyVisibilityPadding < 0f
                || !IsFinite(_bossVisibilityPadding) || _bossVisibilityPadding < 0f
                || _snapshotCapacity < 1
                || !IsFinite(_flashInDuration) || _flashInDuration <= 0f
                || !IsFinite(_blastTotalDuration) || _blastTotalDuration <= _flashInDuration
                || !IsFinite(_dirtOverlayPeakAlpha) || _dirtOverlayPeakAlpha < 0f || _dirtOverlayPeakAlpha > 1f)
            {
                throw new InvalidOperationException("Nuke config requires finite values, a boss damage percent in 0-100, a positive capacity, and a blast duration longer than the flash-in.");
            }
        }

        private void OnValidate()
        {
            _bossDamagePercent = Mathf.Clamp(_bossDamagePercent, 0f, 100f);
            _enemyVisibilityPadding = Mathf.Max(0f, _enemyVisibilityPadding);
            _bossVisibilityPadding = Mathf.Max(0f, _bossVisibilityPadding);
            _snapshotCapacity = Mathf.Max(1, _snapshotCapacity);
            _flashInDuration = Mathf.Max(0.01f, _flashInDuration);
            _blastTotalDuration = Mathf.Max(_flashInDuration + 0.01f, _blastTotalDuration);
            _dirtOverlayPeakAlpha = Mathf.Clamp01(_dirtOverlayPeakAlpha);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
