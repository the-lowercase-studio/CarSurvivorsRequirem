using Assets.Scripts.Enemies.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [ExecuteAlways]
    public class EnemyTierShineController : MonoBehaviour
    {
        [Tooltip("Enables the tier shiny shader effect on configured renderers without duplicating materials.")]
        [SerializeField] private bool _isShineEnabled;

        [Tooltip("Renderers whose shared materials support the _ShineEnabled shader toggle.")]
        [SerializeField] private Renderer[] _targetRenderers;

        [Min(EnemyVisualConstants.MIN_MATERIALIZE_HEIGHT)]
        [Tooltip("Fallback world height used for shader materialization if renderer bounds are unavailable.")]
        [SerializeField] private float _fallbackMaterializeHeight = EnemyVisualConstants.DEFAULT_MATERIALIZE_HEIGHT;

        private MaterialPropertyBlock _propertyBlock;
        private bool _hasAppliedShine;
        private bool _isMaterializing;
        private float _materializeProgress = 1f;
        private float _materializeMinY;
        private float _materializeHeight = EnemyVisualConstants.DEFAULT_MATERIALIZE_HEIGHT;
        private bool _hasAppliedMaterialize;

        public bool IsShineEnabled => _isShineEnabled;
        public bool IsMaterializing => _isMaterializing;
        public float MaterializeProgress => _materializeProgress;

        private void Awake()
        {
            RefreshPropertyBlocks();
        }

        private void OnEnable()
        {
            RefreshPropertyBlocks();
        }

        private void OnValidate()
        {
            RefreshPropertyBlocks();
        }

        public void SetShineEnabled(bool isEnabled)
        {
            _isShineEnabled = isEnabled;
            RefreshPropertyBlocks();
        }

        public void ApplyShineState()
        {
            RefreshPropertyBlocks();
        }

        public void BeginMaterialization()
        {
            ComputeMaterializeBounds();
            _isMaterializing = true;
            _materializeProgress = 0f;
            RefreshPropertyBlocks();
        }

        public void SetMaterializeProgress(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            if (!_isMaterializing && !_hasAppliedMaterialize)
            {
                return;
            }

            _materializeProgress = clampedProgress;
            RefreshPropertyBlocks();
        }

        public void EndMaterialization()
        {
            _isMaterializing = false;
            _materializeProgress = 1f;
            RefreshPropertyBlocks();
        }

        private void ComputeMaterializeBounds()
        {
            bool hasBounds = false;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            if (_targetRenderers != null)
            {
                for (int i = 0; i < _targetRenderers.Length; i++)
                {
                    Renderer targetRenderer = _targetRenderers[i];
                    if (targetRenderer == null || !targetRenderer.enabled)
                    {
                        continue;
                    }

                    Bounds bounds = targetRenderer.bounds;
                    if (bounds.size.sqrMagnitude <= 0.000001f)
                    {
                        continue;
                    }

                    minY = hasBounds ? Mathf.Min(minY, bounds.min.y) : bounds.min.y;
                    maxY = hasBounds ? Mathf.Max(maxY, bounds.max.y) : bounds.max.y;
                    hasBounds = true;
                }
            }

            float rawHeight = hasBounds ? maxY - minY : 0f;
            if (hasBounds && rawHeight >= EnemyVisualConstants.MIN_MATERIALIZE_HEIGHT)
            {
                _materializeMinY = minY - EnemyVisualConstants.MATERIALIZE_BOUNDS_MARGIN;
                _materializeHeight = rawHeight + (EnemyVisualConstants.MATERIALIZE_BOUNDS_MARGIN * 2f);
                return;
            }

            _materializeMinY = transform.position.y - EnemyVisualConstants.MATERIALIZE_BOUNDS_MARGIN;
            _materializeHeight = Mathf.Max(_fallbackMaterializeHeight, EnemyVisualConstants.MIN_MATERIALIZE_HEIGHT)
                + (EnemyVisualConstants.MATERIALIZE_BOUNDS_MARGIN * 2f);
        }

        private void RefreshPropertyBlocks()
        {
            if (!_isShineEnabled && !_hasAppliedShine && !_isMaterializing && !_hasAppliedMaterialize)
            {
                return;
            }

            if (_targetRenderers == null || _targetRenderers.Length == 0)
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            float shineValue = _isShineEnabled ? 1f : 0f;
            float materializeValue = _isMaterializing ? 1f : 0f;
            bool writeShineState = _isShineEnabled || _hasAppliedShine;
            bool writeMaterializeState = _isMaterializing || _hasAppliedMaterialize;

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                Renderer targetRenderer = _targetRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] materials = targetRenderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    if (material == null)
                    {
                        continue;
                    }

                    bool writeShine = writeShineState && material.HasProperty(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID);
                    bool writeMaterialize = writeMaterializeState
                        && material.HasProperty(EnemyVisualConstants.MATERIALIZE_ENABLED_PROPERTY_ID);

                    if (!writeShine && !writeMaterialize)
                    {
                        continue;
                    }

                    targetRenderer.GetPropertyBlock(_propertyBlock, slot);
                    if (writeShine)
                    {
                        _propertyBlock.SetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID, shineValue);
                    }

                    if (writeMaterialize)
                    {
                        _propertyBlock.SetFloat(EnemyVisualConstants.MATERIALIZE_ENABLED_PROPERTY_ID, materializeValue);
                        _propertyBlock.SetFloat(EnemyVisualConstants.MATERIALIZE_PROGRESS_PROPERTY_ID, _materializeProgress);
                        _propertyBlock.SetFloat(EnemyVisualConstants.MATERIALIZE_MIN_Y_PROPERTY_ID, _materializeMinY);
                        _propertyBlock.SetFloat(EnemyVisualConstants.MATERIALIZE_HEIGHT_PROPERTY_ID, _materializeHeight);
                    }

                    targetRenderer.SetPropertyBlock(_propertyBlock, slot);
                }
            }

            _hasAppliedShine = _isShineEnabled;
            _hasAppliedMaterialize = _isMaterializing;
        }
    }
}
