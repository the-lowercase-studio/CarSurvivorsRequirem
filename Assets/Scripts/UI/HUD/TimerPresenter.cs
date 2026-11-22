using Assets.Scripts.GameFlow;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.HUD
{
    public interface ITimerPresenter
    {
        uint TimerValue { get; }
        void SetPaused(bool isPaused);
    }

    public class TimerPresenter : MonoBehaviour, ITimerPresenter
    {
        [Inject] private readonly IRunSessionService _runSessionService = null;

        [SerializeField] private TextMeshProUGUI _timerText;

        private bool _isPaused;

        public uint TimerValue { get; private set; }

        private void Start()
        {
            if (_runSessionService != null)
            {
                TimerValue = _runSessionService.AccumulatedRunSeconds;
            }

            UpdateTimerText();
            InvokeRepeating(nameof(IncreaseTimer), 1f, 1f);
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
        }

        private void IncreaseTimer()
        {
            if (_isPaused)
            {
                return;
            }

            TimerValue++;
            UpdateTimerText();
        }

        private void UpdateTimerText()
        {
            _timerText.text = TimerValue.ToString();
        }
    }
}
