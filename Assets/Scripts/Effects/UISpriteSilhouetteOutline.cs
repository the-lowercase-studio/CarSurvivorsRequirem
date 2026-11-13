using Assets.Scripts.Effects.Constants;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Effects
{
    /// <summary>
    /// Renders a uniform radial silhouette outline around non-rectangular UI sprites without diagonal gaps.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UISpriteSilhouetteOutline : BaseMeshEffect, IMaterialModifier
    {
        private static readonly int OutlineColorId = Shader.PropertyToID(UiOutlineConstants.OUTLINE_COLOR_PROPERTY);
        private static readonly int OutlineWidthId = Shader.PropertyToID(UiOutlineConstants.OUTLINE_WIDTH_PROPERTY);
        private static readonly int OutlineUvRectId = Shader.PropertyToID(UiOutlineConstants.OUTLINE_UV_RECT_PROPERTY);
        private static readonly int OutlineUvStepId = Shader.PropertyToID(UiOutlineConstants.OUTLINE_UV_STEP_PROPERTY);

        private static readonly int StencilId = Shader.PropertyToID("_Stencil");
        private static readonly int StencilOpId = Shader.PropertyToID("_StencilOp");
        private static readonly int StencilCompId = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilReadMaskId = Shader.PropertyToID("_StencilReadMask");
        private static readonly int StencilWriteMaskId = Shader.PropertyToID("_StencilWriteMask");
        private static readonly int ColorMaskId = Shader.PropertyToID("_ColorMask");
        private static readonly int ClipRectId = Shader.PropertyToID("_ClipRect");

        [Tooltip("UI/SpriteSilhouetteOutline shader serialized directly to keep it included in builds.")]
        [SerializeField] private Shader _outlineShader;

        [SerializeField] private Color _effectColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] private float _effectWidth = 3f;

        private Material _runtimeMaterial;
        private Vector4 _spriteUvRect = new Vector4(0f, 0f, 1f, 1f);
        private Vector4 _uvStepPerPixel = new Vector4(0.001f, 0.001f, 0f, 0f);
        private UIVertex _tempVertex;

        public Color EffectColor
        {
            get { return _effectColor; }
            set
            {
                if (_effectColor == value)
                {
                    return;
                }

                _effectColor = value;
                UpdateMaterialUniforms();
            }
        }

        public float EffectWidth
        {
            get { return _effectWidth; }
            set
            {
                float clamped = Mathf.Max(UiOutlineConstants.MIN_OUTLINE_WIDTH, value);
                if (Mathf.Approximately(_effectWidth, clamped))
                {
                    return;
                }

                _effectWidth = clamped;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }

                UpdateMaterialUniforms();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            MarkGraphicDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            MarkGraphicDirty();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseRuntimeMaterial();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _effectWidth = Mathf.Max(UiOutlineConstants.MIN_OUTLINE_WIDTH, _effectWidth);
            MarkGraphicDirty();
            UpdateMaterialUniforms();
        }
#endif

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive())
            {
                return baseMaterial;
            }

            Shader shader = ResolveShader();
            if (shader == null)
            {
                return baseMaterial;
            }

            if (_runtimeMaterial == null || _runtimeMaterial.shader != shader)
            {
                ReleaseRuntimeMaterial();
                _runtimeMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (baseMaterial != null)
            {
                CopyUiMaterialState(baseMaterial, _runtimeMaterial);
            }

            UpdateMaterialUniforms();
            return _runtimeMaterial;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount != UiOutlineConstants.SIMPLE_QUAD_VERTEX_COUNT)
            {
                return;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minU = float.MaxValue;
            float maxU = float.MinValue;
            float minV = float.MaxValue;
            float maxV = float.MinValue;

            for (int i = 0; i < UiOutlineConstants.SIMPLE_QUAD_VERTEX_COUNT; i++)
            {
                vh.PopulateUIVertex(ref _tempVertex, i);
                Vector3 pos = _tempVertex.position;
                Vector2 uv = _tempVertex.uv0;

                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;

                if (uv.x < minU) minU = uv.x;
                if (uv.x > maxU) maxU = uv.x;
                if (uv.y < minV) minV = uv.y;
                if (uv.y > maxV) maxV = uv.y;
            }

            float quadWidth = maxX - minX;
            float quadHeight = maxY - minY;
            if (quadWidth <= 0.0001f || quadHeight <= 0.0001f)
            {
                return;
            }

            float uvPerPxX = (maxU - minU) / quadWidth;
            float uvPerPxY = (maxV - minV) / quadHeight;

            _spriteUvRect = new Vector4(minU, minV, maxU, maxV);
            _uvStepPerPixel = new Vector4(uvPerPxX, uvPerPxY, 0f, 0f);

            float pad = _effectWidth > 0.001f && _effectColor.a > 0.001f
                ? Mathf.Ceil(_effectWidth) + UiOutlineConstants.QUAD_PADDING_MARGIN_PIXELS
                : 0f;

            if (pad > 0f)
            {
                float centerX = (minX + maxX) * 0.5f;
                float centerY = (minY + maxY) * 0.5f;

                for (int i = 0; i < UiOutlineConstants.SIMPLE_QUAD_VERTEX_COUNT; i++)
                {
                    vh.PopulateUIVertex(ref _tempVertex, i);
                    Vector3 pos = _tempVertex.position;
                    Vector2 uv = _tempVertex.uv0;

                    float signX = pos.x < centerX ? -1f : 1f;
                    float signY = pos.y < centerY ? -1f : 1f;

                    pos.x += signX * pad;
                    pos.y += signY * pad;
                    uv.x += signX * pad * uvPerPxX;
                    uv.y += signY * pad * uvPerPxY;

                    _tempVertex.position = pos;
                    _tempVertex.uv0 = uv;
                    vh.SetUIVertex(_tempVertex, i);
                }
            }

            UpdateMaterialUniforms();
        }

        private Shader ResolveShader()
        {
            if (_outlineShader == null)
            {
                _outlineShader = Shader.Find(UiOutlineConstants.SILHOUETTE_SHADER_NAME);
            }

            return _outlineShader;
        }

        private void MarkGraphicDirty()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
                graphic.SetMaterialDirty();
            }
        }

        private void UpdateMaterialUniforms()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetColor(OutlineColorId, _effectColor);
            _runtimeMaterial.SetFloat(OutlineWidthId, _effectWidth);
            _runtimeMaterial.SetVector(OutlineUvRectId, _spriteUvRect);
            _runtimeMaterial.SetVector(OutlineUvStepId, _uvStepPerPixel);
        }

        private static void CopyUiMaterialState(Material source, Material destination)
        {
            if (source.HasProperty(StencilId))
            {
                destination.SetFloat(StencilId, source.GetFloat(StencilId));
            }

            if (source.HasProperty(StencilOpId))
            {
                destination.SetFloat(StencilOpId, source.GetFloat(StencilOpId));
            }

            if (source.HasProperty(StencilCompId))
            {
                destination.SetFloat(StencilCompId, source.GetFloat(StencilCompId));
            }

            if (source.HasProperty(StencilReadMaskId))
            {
                destination.SetFloat(StencilReadMaskId, source.GetFloat(StencilReadMaskId));
            }

            if (source.HasProperty(StencilWriteMaskId))
            {
                destination.SetFloat(StencilWriteMaskId, source.GetFloat(StencilWriteMaskId));
            }

            if (source.HasProperty(ColorMaskId))
            {
                destination.SetFloat(ColorMaskId, source.GetFloat(ColorMaskId));
            }

            if (source.HasProperty(ClipRectId))
            {
                destination.SetVector(ClipRectId, source.GetVector(ClipRectId));
            }

            destination.shaderKeywords = source.shaderKeywords;
        }

        private void ReleaseRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }
    }
}
