using System;
using Assets.Scripts.PowerUps.Constants;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Config
{
    [CreateAssetMenu(fileName = "ArmorConfig", menuName = "Scriptable Objects/Power-Ups/Armor Config")]
    public class ArmorConfigSO : ScriptableObject
    {
        [SerializeField, Tooltip("Capacities for each armor plate level (Level 1, Level 2, Level 3).")]
        private float[] _plateCapacities = new float[] { 20f, 40f, 60f };

        [SerializeField, Tooltip("Color tint of the armor plate mini-bars.")]
        private Color _plateColor = new Color(0.05f, 0.15f, 0.6f, 1f);

        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the previous element width overlapped by the next plate.")]
        private float _horizontalOverlapRatio = ArmorConstants.DEFAULT_HORIZONTAL_OVERLAP_RATIO;

        [SerializeField, Tooltip("Upward vertical offset in local units for each subsequent plate (deprecated, plates share base health bar height).")]
        private float _verticalStepOffset = ArmorConstants.DEFAULT_VERTICAL_STEP_OFFSET;

        [SerializeField, Min(0.01f), Tooltip("Reference width of the 100 HP health bar in world canvas units.")]
        private float _referenceBaseHealthWidth = ArmorConstants.DEFAULT_BASE_HEALTH_WIDTH;

        [SerializeField, Min(1f), Tooltip("Reference base max health used to compute proportional plate widths.")]
        private float _referenceBaseMaxHealth = ArmorConstants.DEFAULT_BASE_MAX_HEALTH;

        public int MaxLevel => _plateCapacities != null ? _plateCapacities.Length : 0;
        public Color PlateColor => _plateColor;
        public float HorizontalOverlapRatio => _horizontalOverlapRatio;
        public float VerticalStepOffset => _verticalStepOffset;
        public float ReferenceBaseHealthWidth => _referenceBaseHealthWidth;
        public float ReferenceBaseMaxHealth => _referenceBaseMaxHealth;

        public float GetPlateCapacity(int levelIndex)
        {
            if (_plateCapacities == null || levelIndex < 0 || levelIndex >= _plateCapacities.Length)
            {
                return 0f;
            }

            return _plateCapacities[levelIndex];
        }

        public void ValidateConfiguration()
        {
            if (_plateCapacities == null || _plateCapacities.Length != ArmorConstants.MAX_ARMOR_LEVEL)
            {
                throw new InvalidOperationException($"ArmorConfig requires exactly {ArmorConstants.MAX_ARMOR_LEVEL} plate capacities.");
            }

            for (int i = 0; i < _plateCapacities.Length; i++)
            {
                float capacity = _plateCapacities[i];
                if (!IsFinite(capacity) || capacity <= 0f)
                {
                    throw new InvalidOperationException($"ArmorConfig plate capacity at index {i} must be a finite positive number.");
                }
            }

            if (!IsFinite(_horizontalOverlapRatio) || _horizontalOverlapRatio < 0f || _horizontalOverlapRatio > 1f
                || !IsFinite(_verticalStepOffset)
                || !IsFinite(_referenceBaseHealthWidth) || _referenceBaseHealthWidth <= 0f
                || !IsFinite(_referenceBaseMaxHealth) || _referenceBaseMaxHealth <= 0f)
            {
                throw new InvalidOperationException("ArmorConfig layout parameters must be finite positive numbers with overlap ratio in 0-1.");
            }
        }

        private void OnValidate()
        {
            if (_plateCapacities != null && _plateCapacities.Length > 0)
            {
                for (int i = 0; i < _plateCapacities.Length; i++)
                {
                    _plateCapacities[i] = Mathf.Max(0.01f, _plateCapacities[i]);
                }
            }

            _horizontalOverlapRatio = Mathf.Clamp01(_horizontalOverlapRatio);
            _referenceBaseHealthWidth = Mathf.Max(0.01f, _referenceBaseHealthWidth);
            _referenceBaseMaxHealth = Mathf.Max(1f, _referenceBaseMaxHealth);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
