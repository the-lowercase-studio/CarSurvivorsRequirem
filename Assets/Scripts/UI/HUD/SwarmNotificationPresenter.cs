using DG.Tweening;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.UI.HUD
{
    public interface ISwarmNotificationPresenter
    {
        void ShowIncoming(int secondsRemaining);
        void ShowOngoing();
        void Hide();
    }

    public class SwarmNotificationPresenter : MonoBehaviour, ISwarmNotificationPresenter
    {
        [Inject] private readonly Volume _postProcessVolume;

        [SerializeField] private TextMeshProUGUI _swarmText;
        [SerializeField] private string _incomingTemplateMessage = "Swarm Incoming in {0}s";
        [SerializeField] private string _ongoingMessage = "Swarm ongoing";

        [Header("Animation")]
        [SerializeField] private float _growDuration = 0.4f;
        [SerializeField] private float _punchScale = 0.15f;
        [SerializeField] private float _punchDuration = 0.6f;
        [SerializeField] private float _exitDuration = 0.3f;

        [Header("Post Processing")]
        [Tooltip("Target midtone gamma value (typically 0 to 2, where 1.0 is neutral). Values below 1.0 (e.g. 0.7) darken the screen, values above 1.0 brighten it.")]
        [SerializeField] private float _targetGamma = 0.7f;
        [SerializeField] private float _gammaEnterDuration = 10f;
        [SerializeField] private float _gammaExitDuration = 2f;

        private Vector3 _defaultScale;
        private Tween _punchTween;
        private Tween _activeTween;
        private LiftGammaGain _liftGammaGain;
        private Vector4 _originalGamma;
        private bool _hasOriginalGamma;
        private Tween _gammaTween;
        private float _targetOffset;

        private void Awake()
        {
            _defaultScale = _swarmText.rectTransform.localScale;
            _swarmText.gameObject.SetActive(false);
        }

        private void Start()
        {
            // Support both:
            // 1. Real Gamma value (range 0 to 2, where 1.0 is neutral, e.g. 0.7f maps to offset -0.3f)
            // 2. Legacy raw offset (e.g. -0.7f or positive values that were negated)
            if (_targetGamma < 0f)
            {
                // Legacy negative offset
                _targetOffset = _targetGamma;
            }
            else if (_targetGamma > 0f && _targetGamma <= 2f)
            {
                // Real Gamma value (0 to 2), map to URP offset (-1 to 1)
                _targetOffset = _targetGamma - 1f;
            }
            else
            {
                // Fallback / legacy positive values that meant to be offsets (e.g. 0.7f offset)
                _targetOffset = -_targetGamma;
            }

            if (_postProcessVolume != null && _postProcessVolume.profile.TryGet(out _liftGammaGain))
            {
                _originalGamma = _liftGammaGain.gamma.value;
                _hasOriginalGamma = true;
            }
        }

        public void ShowIncoming(int secondsRemaining)
        {
            string text = string.Format(_incomingTemplateMessage, secondsRemaining);
            _swarmText.text = text;

            if (!_swarmText.gameObject.activeSelf)
            {
                _swarmText.gameObject.SetActive(true);
                _swarmText.rectTransform.localScale = Vector3.zero;

                KillTextTweens();
                TweenGamma(_targetOffset, _gammaEnterDuration);

                _activeTween = _swarmText.rectTransform
                    .DOScale(_defaultScale, _growDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        _activeTween = null;
                        PunchText();
                    });
            }
            else
            {
                if (_activeTween == null)
                {
                    PunchText();
                }
            }
        }

        public void ShowOngoing()
        {
            _swarmText.text = _ongoingMessage;

            KillTextTweens();
            _swarmText.rectTransform.localScale = _defaultScale;
            PunchText();
        }

        public void Hide()
        {
            KillPunchTween();

            if (_hasOriginalGamma)
            {
                TweenGamma(_originalGamma.w, _gammaExitDuration);
            }

            _activeTween = _swarmText.rectTransform
                .DOScale(Vector3.zero, _exitDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => _swarmText.gameObject.SetActive(false));
        }

        private void PunchText()
        {
            KillPunchTween();
            _punchTween = _swarmText.rectTransform
                .DOPunchScale(Vector3.one * _punchScale, _punchDuration, 5, 0.5f);
        }

        private void SetGamma(float val)
        {
            if (_liftGammaGain != null)
            {
                _liftGammaGain.gamma.overrideState = true;
                Vector4 currentVal = _liftGammaGain.gamma.value;
                _liftGammaGain.gamma.value = new Vector4(currentVal.x, currentVal.y, currentVal.z, val);
            }
        }

        private void TweenGamma(float targetVal, float duration)
        {
            _gammaTween?.Kill();
            if (_liftGammaGain != null)
            {
                _gammaTween = DOTween.To(
                    () => _liftGammaGain.gamma.value.w,
                    x => SetGamma(x),
                    targetVal,
                    duration
                );
            }
        }

        private void RestoreGamma()
        {
            if (_liftGammaGain != null && _hasOriginalGamma)
            {
                _liftGammaGain.gamma.value = _originalGamma;
            }
        }

        private void KillActiveTweens()
        {
            KillTextTweens();
            _gammaTween?.Kill();
        }

        private void KillTextTweens()
        {
            _activeTween?.Kill();
            _activeTween = null;
            KillPunchTween();
        }

        private void KillPunchTween()
        {
            _punchTween?.Kill();
            _punchTween = null;
        }

        private void OnDisable()
        {
            KillActiveTweens();
            RestoreGamma();
        }
    }
}
