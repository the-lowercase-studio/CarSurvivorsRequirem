using Assets.Scripts.Player;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.VFX;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Interactables.CapturePoint
{
    public class CapturePoint : MonoBehaviour
    {
        [Inject] private readonly IPlayerManager _playerManager;
        [Inject] private readonly ISkillUpgradeFlow _skillUpgradeFlow;

        [Header("Capture Settings")]
        [SerializeField] private float _captureRadius = 5f;
        [SerializeField] private float _captureDurationSeconds = 5f;
        [SerializeField] private float _decaySpeedMultiplier = 1f;

        [Header("Visual Progress Configuration")]
        [SerializeField] private Transform _expandingCirclePlane;
        [SerializeField] private float _maxCircleScale = 10f;
        [SerializeField] private float _shrinkDurationSeconds = 0.3f;

        [Header("Radius Outline Circle Settings")]
        [SerializeField] private Transform _outlineCirclePlane;
        [SerializeField] private float _outlineScaleMultiplier = 1f;
        [SerializeField] private float _outlineAnimDuration = 0.3f;
        [SerializeField] private bool _enableOutlinePulse = true;
        [SerializeField] private float _outlinePulseStrength = 0.05f;

        [Header("Captured Visuals & VFX (Optional)")]
        [SerializeField] private GameObject _deactivationVisuals;
        [SerializeField] private VFXPlayer _capturedVfxPlayer;

        [Header("Material Swap Settings")]
        [SerializeField] private Renderer _targetRenderer;
        [SerializeField] private Material _capturedMaterial1;
        [SerializeField] private Material _capturedMaterial2;

        private float _progress;
        private bool _isCaptured;
        private bool _isPlayerInsideRadius;
        private Tween _scaleTween;
        private Tween _outlineTween;
        private Tween _outlinePulseTween;

        private void Awake()
        {
            if (_outlineCirclePlane != null)
            {
                _outlineCirclePlane.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isCaptured || _playerManager?.GameObject == null)
            {
                return;
            }

            Vector3 distanceVector = transform.position - _playerManager.GameObject.transform.position;
            float sqrDistance = distanceVector.sqrMagnitude;
            float sqrCaptureRadius = _captureRadius * _captureRadius;
            bool isPlayerInRadius = sqrDistance <= sqrCaptureRadius;

            if (isPlayerInRadius)
            {
                if (!_isPlayerInsideRadius)
                {
                    _isPlayerInsideRadius = true;
                    ShowOutlineCircle();
                }

                if (_captureDurationSeconds > 0f)
                {
                    _progress += (1f / _captureDurationSeconds) * Time.deltaTime;
                }
                else
                {
                    _progress = 1f;
                }
            }
            else
            {
                if (_isPlayerInsideRadius)
                {
                    _isPlayerInsideRadius = false;
                    HideOutlineCircle();
                }

                if (_captureDurationSeconds > 0f)
                {
                    _progress -= ((1f / _captureDurationSeconds) * _decaySpeedMultiplier) * Time.deltaTime;
                }
            }

            _progress = Mathf.Clamp01(_progress);

            UpdateExpandingCircleScale();

            if (_progress >= 1f)
            {
                CompleteCapture();
            }
        }

        private void OnDestroy()
        {
            _scaleTween?.Kill();
            KillOutlineTweens();
        }

        private void UpdateExpandingCircleScale()
        {
            if (_expandingCirclePlane == null)
            {
                return;
            }

            _expandingCirclePlane.localScale = Vector3.one * (_progress * _maxCircleScale);
        }

        private void ShowOutlineCircle()
        {
            if (_outlineCirclePlane == null)
            {
                return;
            }

            KillOutlineTweens();

            _outlineCirclePlane.gameObject.SetActive(true);
            _outlineCirclePlane.localScale = Vector3.zero;

            Vector3 targetScale = Vector3.one * (_captureRadius * _outlineScaleMultiplier);

            _outlineTween = _outlineCirclePlane
                .DOScale(targetScale, _outlineAnimDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(StartOutlinePulse);
        }

        private void StartOutlinePulse()
        {
            if (_outlineCirclePlane == null || !_enableOutlinePulse || !_isPlayerInsideRadius || _isCaptured)
            {
                return;
            }

            Vector3 baseScale = Vector3.one * (_captureRadius * _outlineScaleMultiplier);
            Vector3 pulseScale = baseScale * (1f + _outlinePulseStrength);

            _outlinePulseTween = _outlineCirclePlane
                .DOScale(pulseScale, 0.6f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void HideOutlineCircle()
        {
            if (_outlineCirclePlane == null)
            {
                return;
            }

            KillOutlineTweens();

            _outlineTween = _outlineCirclePlane
                .DOScale(Vector3.zero, _outlineAnimDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(DisableOutlineCirclePlane);
        }

        private void DisableOutlineCirclePlane()
        {
            if (_outlineCirclePlane != null)
            {
                _outlineCirclePlane.gameObject.SetActive(false);
            }
        }

        private void KillOutlineTweens()
        {
            _outlineTween?.Kill();
            _outlineTween = null;
            _outlinePulseTween?.Kill();
            _outlinePulseTween = null;
        }

        private void CompleteCapture()
        {
            _isCaptured = true;
            _progress = 1f;
            _isPlayerInsideRadius = false;

            _scaleTween?.Kill();
            if (_expandingCirclePlane != null)
            {
                _scaleTween = _expandingCirclePlane
                    .DOScale(Vector3.zero, _shrinkDurationSeconds)
                    .SetEase(Ease.InQuad)
                    .OnComplete(DisableExpandingCirclePlane);
            }

            HideOutlineCircle();

            SwapMaterialsOnCaptured();

            if (_skillUpgradeFlow != null && _playerManager != null)
            {
                _skillUpgradeFlow.QueueRandomSkillUpgradeRequest(_playerManager.SkillsRegistry);
            }

            if (_capturedVfxPlayer != null)
            {
                _capturedVfxPlayer.Play(new VFXPlayConfig());
            }

            if (_deactivationVisuals != null)
            {
                _deactivationVisuals.SetActive(false);
            }

            enabled = false;
        }

        private void DisableExpandingCirclePlane()
        {
            if (_expandingCirclePlane != null)
            {
                _expandingCirclePlane.gameObject.SetActive(false);
            }
        }

        private void SwapMaterialsOnCaptured()
        {
            if (_targetRenderer == null)
            {
                return;
            }

            Material[] materials = _targetRenderer.materials;
            if (materials.Length > 0 && _capturedMaterial1 != null)
            {
                materials[0] = _capturedMaterial1;
            }
            if (materials.Length > 1 && _capturedMaterial2 != null)
            {
                materials[1] = _capturedMaterial2;
            }
            _targetRenderer.materials = materials;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _captureRadius);
        }
#endif
    }
}
