using Assets.Scripts.Enemies.Barrel.Constants;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.Enemies.Barrel
{
    public class BarrelEnemyAttackFeedback : MonoBehaviour
    {
        [SerializeField, Tooltip("Dedicated rotation pivot outside the animated model.")]
        private Transform _visualPivot;
        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("Attack clip containing the existing hit event.")]
        private AnimationClip _attackClip;
        [SerializeField, Tooltip("Full Animator state path that plays the Attack clip.")]
        private string _attackState = "StandingLayer.Attack";
        [SerializeField, Min(0)] private int _attackLayer;
        [SerializeField, Tooltip("Model renderers only; exclude warnings and particles.")]
        private Renderer[] _renderers;
        [SerializeField] private Vector3 _punchStrength = new Vector3(0f, 0f, 8f);
        [SerializeField, Min(0.01f)] private float _punchCycleDuration = 0.25f;
        [SerializeField, Min(1)] private int _punchVibrato = 4;
        [SerializeField, Range(0f, 1f)] private float _punchElasticity = 0.5f;
        [SerializeField] private Color _targetColor = Color.red;
        [SerializeField, ColorUsage(false, true), Tooltip("Applied only to materials with authored emission enabled.")]
        private Color _targetEmissionColor = Color.red;
        [SerializeField] private Ease _colorEase = Ease.Linear;

        private Quaternion _originalRotation;
        private ColorBinding[] _colorBindings;
        private Tween _punchTween;
        private Sequence _colorTimeline;
        private int _attackStateHash;
        private float _hitNormalizedTime;
        private bool _isInitialized;
        private bool _isConfigured;
        private bool _isPlaying;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            ResetFeedback();
            CreateTweens();
        }

        private void LateUpdate()
        {
            if (!_isPlaying)
            {
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(_attackLayer);
            if (_animator.IsInTransition(_attackLayer))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_attackLayer);
                if (next.fullPathHash == _attackStateHash)
                {
                    state = next;
                }
            }
            if (state.fullPathHash != _attackStateHash)
            {
                ResetFeedback();
                return;
            }

            _colorTimeline.Goto(Mathf.Clamp01(state.normalizedTime / _hitNormalizedTime), false);
            ApplyColors();
        }

        private void OnDisable()
        {
            ResetFeedback();
            KillTweens();
        }

        private void OnDestroy()
        {
            ResetFeedback();
            KillTweens();
        }

        public void BeginFeedback()
        {
            Initialize();
            if (!_isConfigured || _isPlaying || !isActiveAndEnabled)
            {
                return;
            }
            if (!_animator.HasState(_attackLayer, _attackStateHash))
            {
                Debug.LogError("Barrel feedback Attack state is missing from its Animator.", this);
                return;
            }

            CreateTweens();
            _isPlaying = true;
            _punchTween.Restart();
        }

        public void CompleteFeedback()
        {
            if (_isPlaying)
            {
                _colorTimeline.Goto(1f, false);
                ApplyColors();
            }
        }

        public void ResetFeedback()
        {
            _isPlaying = false;
            if (!_isConfigured)
            {
                return;
            }
            if (_punchTween != null)
            {
                _punchTween.Rewind();
            }
            if (_colorTimeline != null)
            {
                _colorTimeline.Rewind();
            }
            if (_visualPivot != null)
            {
                _visualPivot.localRotation = _originalRotation;
            }
            foreach (ColorBinding binding in _colorBindings)
            {
                binding.Restore();
            }
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
            if (_visualPivot == null || _animator == null || _attackClip == null
                || _renderers == null || _renderers.Length == 0
                || _animator.runtimeAnimatorController == null || _attackLayer >= _animator.layerCount
                || !(_punchCycleDuration > 0f) || float.IsInfinity(_punchCycleDuration))
            {
                Debug.LogError("Barrel attack feedback requires authored pivot, Animator, Attack clip, renderers and valid tuning.", this);
                return;
            }

            int hitCount = 0;
            foreach (AnimationEvent animationEvent in _attackClip.events)
            {
                if (animationEvent.functionName == BarrelEnemyConstants.ATTACK_HIT_EVENT)
                {
                    hitCount++;
                    _hitNormalizedTime = animationEvent.time / _attackClip.length;
                }
            }
            bool hasClip = false;
            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                hasClip |= clip == _attackClip;
            }
            if (!hasClip || hitCount != 1 || !(_hitNormalizedTime > 0f) || _hitNormalizedTime > 1f)
            {
                Debug.LogError("Barrel feedback requires one unambiguous hit event in its Animator's authored Attack clip.", this);
                return;
            }

            int count = 0;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    Debug.LogError("Barrel feedback contains an unassigned model renderer.", this);
                    return;
                }
                count += renderer.sharedMaterials.Length;
            }
            _colorBindings = new ColorBinding[count];
            int index = 0;
            foreach (Renderer renderer in _renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    _colorBindings[index++] = new ColorBinding(renderer, slot, materials[slot]);
                }
            }
            _originalRotation = _visualPivot.localRotation;
            _attackStateHash = Animator.StringToHash(_attackState);
            _isConfigured = true;
        }

        private void CreateTweens()
        {
            if (!_isConfigured || _colorTimeline != null)
            {
                return;
            }
            _punchTween = _visualPivot.DOPunchRotation(_punchStrength, _punchCycleDuration,
                    _punchVibrato, _punchElasticity)
                .SetLoops(-1, LoopType.Restart).SetAutoKill(false).SetRecyclable(false).Pause();
            _colorTimeline = DOTween.Sequence().SetAutoKill(false).SetRecyclable(false).Pause();
            foreach (ColorBinding binding in _colorBindings)
            {
                binding.AddTweens(_colorTimeline, _targetColor, _targetEmissionColor, _colorEase);
            }
            _colorTimeline.ForceInit();
        }

        private void KillTweens()
        {
            if (_punchTween != null)
            {
                _punchTween.Kill();
                _punchTween = null;
            }
            if (_colorTimeline != null)
            {
                _colorTimeline.Kill();
                _colorTimeline = null;
            }
        }

        private void ApplyColors()
        {
            foreach (ColorBinding binding in _colorBindings)
            {
                binding.Apply();
            }
        }

        private sealed class ColorBinding
        {
            private readonly Renderer _renderer;
            private readonly int _slot;
            private readonly int _baseColorId;
            private readonly bool _hasEmission;
            private readonly Color _originalColor;
            private readonly Color _originalEmission;
            private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
            private Color _color;
            private Color _emission;

            public ColorBinding(Renderer renderer, int slot, Material material)
            {
                _renderer = renderer;
                _slot = slot;
                if (material == null)
                {
                    return;
                }
                _baseColorId = material.HasProperty(BarrelFeedbackShaderConstants.BASE_COLOR_ID)
                    ? BarrelFeedbackShaderConstants.BASE_COLOR_ID : BarrelFeedbackShaderConstants.COLOR_ID;
                if (!material.HasProperty(_baseColorId))
                {
                    Debug.LogError("Barrel feedback model shader has no supported base color.", renderer);
                    _baseColorId = 0;
                    return;
                }
                _hasEmission = material.HasProperty(BarrelFeedbackShaderConstants.EMISSION_COLOR_ID)
                    && material.IsKeywordEnabled(BarrelFeedbackShaderConstants.EMISSION_KEYWORD);
                ReadBlock();
                _originalColor = _block.HasColor(_baseColorId) ? _block.GetColor(_baseColorId) : material.GetColor(_baseColorId);
                if (_hasEmission)
                {
                    int emissionId = BarrelFeedbackShaderConstants.EMISSION_COLOR_ID;
                    _originalEmission = _block.HasColor(emissionId) ? _block.GetColor(emissionId) : material.GetColor(emissionId);
                }
                _color = _originalColor;
                _emission = _originalEmission;
            }

            public void AddTweens(Sequence timeline, Color targetColor, Color targetEmission, Ease ease)
            {
                if (_baseColorId == 0)
                {
                    return;
                }
                timeline.Join(DOTween.To(GetColor, SetColor, targetColor, 1f).SetEase(ease));
                if (_hasEmission)
                {
                    timeline.Join(DOTween.To(GetEmission, SetEmission, targetEmission, 1f).SetEase(ease));
                }
            }

            public void Apply()
            {
                if (_renderer == null || _baseColorId == 0)
                {
                    return;
                }
                // Read the live block so other effects retain their unrelated overrides.
                ReadBlock();
                _block.SetColor(_baseColorId, _color);
                if (_hasEmission)
                {
                    _block.SetColor(BarrelFeedbackShaderConstants.EMISSION_COLOR_ID, _emission);
                }
                _renderer.SetPropertyBlock(_block, _slot);
            }

            public void Restore()
            {
                _color = _originalColor;
                _emission = _originalEmission;
                Apply();
            }

            private void ReadBlock()
            {
                _renderer.GetPropertyBlock(_block, _slot);
                if (_block.isEmpty)
                {
                    _renderer.GetPropertyBlock(_block);
                }
            }

            private Color GetColor()
            {
                return _color;
            }

            private void SetColor(Color value)
            {
                _color = value;
            }

            private Color GetEmission()
            {
                return _emission;
            }

            private void SetEmission(Color value)
            {
                _emission = value;
            }
        }
    }
}
