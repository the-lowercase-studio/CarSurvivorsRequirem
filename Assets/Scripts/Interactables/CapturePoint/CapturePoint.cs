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

        [Header("Captured Visuals & VFX (Optional)")]
        [SerializeField] private GameObject _deactivationVisuals;
        [SerializeField] private VFXPlayer _capturedVfxPlayer;

        [Header("Material Swap Settings")]
        [SerializeField] private Renderer _targetRenderer;
        [SerializeField] private Material _capturedMaterial1;
        [SerializeField] private Material _capturedMaterial2;

        private float _progress;
        private bool _isCaptured;
        private Tween _scaleTween;

        private void Update()
        {
            if (_isCaptured || _playerManager?.GameObject == null)
            {
                return;
            }

            Vector3 distanceVector = transform.position - _playerManager.GameObject.transform.position;
            float sqrDistance = distanceVector.sqrMagnitude;
            float sqrCaptureRadius = _captureRadius * _captureRadius;

            if (sqrDistance <= sqrCaptureRadius)
            {
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
        }

        private void UpdateExpandingCircleScale()
        {
            if (_expandingCirclePlane == null)
            {
                return;
            }

            _expandingCirclePlane.localScale = Vector3.one * (_progress * _maxCircleScale);
        }

        private void CompleteCapture()
        {
            _isCaptured = true;
            _progress = 1f;

            _scaleTween?.Kill();
            if (_expandingCirclePlane != null)
            {
                _scaleTween = _expandingCirclePlane
                    .DOScale(Vector3.zero, _shrinkDurationSeconds)
                    .SetEase(Ease.InQuad)
                    .OnComplete(DisableExpandingCirclePlane);
            }

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
