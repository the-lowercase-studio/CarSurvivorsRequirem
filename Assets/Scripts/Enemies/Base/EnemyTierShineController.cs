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

        private MaterialPropertyBlock _propertyBlock;
        private bool _hasAppliedShine;

        public bool IsShineEnabled => _isShineEnabled;

        private void Awake()
        {
            ApplyShineState();
        }

        private void OnEnable()
        {
            ApplyShineState();
        }

        private void OnValidate()
        {
            ApplyShineState();
        }

        public void SetShineEnabled(bool isEnabled)
        {
            _isShineEnabled = isEnabled;
            ApplyShineState();
        }

        public void ApplyShineState()
        {
            if (!_isShineEnabled && !_hasAppliedShine)
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
                    if (material == null || !material.HasProperty(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID))
                    {
                        continue;
                    }

                    targetRenderer.GetPropertyBlock(_propertyBlock, slot);
                    _propertyBlock.SetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID, shineValue);
                    targetRenderer.SetPropertyBlock(_propertyBlock, slot);
                }
            }

            _hasAppliedShine = _isShineEnabled;
        }
    }
}
