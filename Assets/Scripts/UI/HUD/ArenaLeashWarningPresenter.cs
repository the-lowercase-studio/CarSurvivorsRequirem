using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.HUD
{
    public interface IArenaLeashWarningPresenter
    {
        void ShowWarning(float remainingSeconds);
        void UpdateCountdown(float remainingSeconds);
        void Hide();
    }

    public class ArenaLeashWarningPresenter : MonoBehaviour, IArenaLeashWarningPresenter
    {
        private const string WARNING_PREFIX = "RETURN TO ARENA: ";
        private const string SECONDS_SUFFIX = "s";

        [Tooltip("Root visual container toggled on/off during leash warnings.")]
        [SerializeField] private GameObject _visual;
        [Tooltip("TextMeshPro label showing the countdown text.")]
        [SerializeField] private TextMeshProUGUI _warningText;
        [Tooltip("Scale multiplier punched on each integer second tick.")]
        [SerializeField] private float _punchScaleAmount = 0.2f;
        [Tooltip("Duration of the punch scale tween in seconds.")]
        [SerializeField] private float _punchDuration = 0.2f;

        private Tween _punchTween;
        private int _lastDisplayedSecond = -1;

        private void Awake()
        {
            if (_visual != null)
            {
                _visual.SetActive(false);
            }
        }

        private void OnDisable()
        {
            KillPunchTween();
        }

        private void OnDestroy()
        {
            KillPunchTween();
        }

        public void ShowWarning(float remainingSeconds)
        {
            if (_visual != null)
            {
                _visual.SetActive(true);
            }

            _lastDisplayedSecond = Mathf.CeilToInt(remainingSeconds);
            ApplyCountdownText(remainingSeconds);
            TriggerPunch();
        }

        public void UpdateCountdown(float remainingSeconds)
        {
            int currentSecond = Mathf.CeilToInt(remainingSeconds);
            if (currentSecond != _lastDisplayedSecond)
            {
                _lastDisplayedSecond = currentSecond;
                TriggerPunch();
            }

            ApplyCountdownText(remainingSeconds);
        }

        public void Hide()
        {
            KillPunchTween();

            if (_visual != null)
            {
                _visual.SetActive(false);
            }

            _lastDisplayedSecond = -1;
        }

        private void ApplyCountdownText(float remainingSeconds)
        {
            if (_warningText == null)
            {
                return;
            }

            float displaySeconds = Mathf.Max(0f, remainingSeconds);
            _warningText.text = $"{WARNING_PREFIX}{displaySeconds:F1}{SECONDS_SUFFIX}";
        }

        private void TriggerPunch()
        {
            if (_warningText == null)
            {
                return;
            }

            KillPunchTween();
            _warningText.transform.localScale = Vector3.one;
            _punchTween = _warningText.transform.DOPunchScale(Vector3.one * _punchScaleAmount, _punchDuration, 1, 0.5f);
        }

        private void KillPunchTween()
        {
            if (_punchTween != null && _punchTween.IsActive())
            {
                _punchTween.Kill();
            }
            _punchTween = null;

            if (_warningText != null)
            {
                _warningText.transform.localScale = Vector3.one;
            }
        }
    }
}
