using Assets.Scripts.Audio;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Player;
using Assets.Scripts.ScoreBoard;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.UI.Pause;
using Assets.Scripts.UI.Skills;
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
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IBackgroundAudioManager _backgroundAudioManager = null;
        [Inject] private readonly IScoreBoardNewScoreSaver _scoreBoardNewScoreSaver = null;
        [Inject] private readonly IScoreBoardBestScoreGetter _scoreBoardBestScoreGetter = null;
        [Inject] private readonly ITimerPresenter _timerPresenter = null;
        [Inject] private readonly IPausePresenter _pausePresenter = null;
        [Inject] private readonly ISkillsStatsPresenter _skillsStatsPresenter = null;

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

            _pausePresenter.HideForDeath();

            _visual.SetActive(true);

            _backgroundAudioManager.ChangeAudioToDeathAudioMode();

            GameTime.Pause();

            _skillsStatsPresenter.ShowDeathStats();
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
