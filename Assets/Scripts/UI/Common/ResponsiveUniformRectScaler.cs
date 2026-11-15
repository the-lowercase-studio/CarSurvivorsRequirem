using System;
using Assets.Scripts.UI.Constants;
using UnityEngine;

namespace Assets.Scripts.UI.Common
{
    /// <summary>
    /// Uniformly scales a canonical fixed-size UI root to fit inside its parent safe-viewport rect without distortion.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ResponsiveUniformRectScaler : MonoBehaviour
    {
        [Tooltip("Canonical fixed-size container scaled uniformly inside this viewport.")]
        [SerializeField] private RectTransform _targetRoot;
        [Tooltip("Reference canonical width and height in canvas units.")]
        [SerializeField] private Vector2 _referenceSize = new Vector2(
            SkillsStatsConstants.CANONICAL_PANEL_WIDTH,
            SkillsStatsConstants.CANONICAL_PANEL_HEIGHT);
        [Tooltip("Upper bound for uniform scaling so layouts do not upscale past authored size.")]
        [SerializeField] private float _maxScale = 1f;

        private RectTransform _viewportRect;
        private bool _isInitialized;

        private void Awake()
        {
            if (_targetRoot == null || _referenceSize.x <= 0f || _referenceSize.y <= 0f || _maxScale <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{nameof(ResponsiveUniformRectScaler)}] Requires a valid target root, positive reference size, and positive max scale on '{name}'.");
            }

            _viewportRect = GetComponent<RectTransform>();
            _isInitialized = true;
            UpdateScale();
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateScale();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateScale();
        }

        /// <summary>
        /// Recomputes and applies uniform local scale to fit the target root within the current viewport bounds.
        /// </summary>
        public void UpdateScale()
        {
            Rect rect = _viewportRect.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float scale = Mathf.Clamp(Mathf.Min(rect.width / _referenceSize.x, rect.height / _referenceSize.y), 0f, _maxScale);
            _targetRoot.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
