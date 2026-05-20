using Assets.Scripts.Audio;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Player;
using Assets.Scripts.ScoreBoard;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.Utils;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Death
{
    public interface IPlayerDeathPresenter
    {
        void EnableDeathScreen();
    }

    public class PlayerDeathPresenter : MonoBehaviour, IPlayerDeathPresenter
    {
        [Inject] private readonly IPlayerManager _playerManager;
        [Inject] private readonly IBackgroundAudioManager _backgroundAudioManager;
        [Inject] private readonly IScoreBoardNewScoreSaver _scoreBoardNewScoreSaver;
        [Inject] private readonly IScoreBoardBestScoreGetter _scoreBoardBestScoreGetter;
        [Inject] private readonly ITimerPresenter _timerPresenter;

        [SerializeField] private GameObject _visual;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _timeText;

        private void Start()
        {
            _backgroundAudioManager.ChangeAudioToDefaultAudioMode();
        }

        public void EnableDeathScreen()
        {
            _scoreBoardNewScoreSaver.Save(_timerPresenter.TimerValue);

            SetLevelText();

            SetTimeText();

            _visual.SetActive(true);

            _backgroundAudioManager.ChangeAudioToDeathAudioMode();

            GameTime.Pause();
        }

        private void SetLevelText()
        {
            _levelText.text = "Level: " + _playerManager
                .LevelController
                .LevelData
                .Lvl
                .ToString();
        }

        private void SetTimeText()
        {
            string timeText = "Time Alive: " +
                TimeConversionUtility.FormatSecondsToTimeString(_timerPresenter.TimerValue);

            if (_scoreBoardBestScoreGetter.GetBestScore() == _timerPresenter.TimerValue)
            {
                timeText += $" <Color=#F8D61C>(New Best!)</Color>";
            }

            _timeText.text = timeText;
        }
    }
}
