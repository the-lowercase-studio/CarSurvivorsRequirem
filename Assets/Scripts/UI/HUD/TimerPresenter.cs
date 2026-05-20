using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.HUD
{
    public interface ITimerPresenter
    {
        uint TimerValue { get; }
    }

    public class TimerPresenter : MonoBehaviour, ITimerPresenter
    {
        [SerializeField] private TextMeshProUGUI _timerText;
        public uint TimerValue { get; private set; }

        private void Start()
        {
            InvokeRepeating(nameof(IncreaseTimer), 1f, 1f);
        }

        private void IncreaseTimer()
        {
            TimerValue++;
            _timerText.text = TimerValue.ToString();
        }
    }
}
