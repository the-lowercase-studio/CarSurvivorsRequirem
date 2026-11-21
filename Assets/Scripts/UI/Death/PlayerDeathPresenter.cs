using System;
using Assets.Scripts.Audio;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Input;
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
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IGameTimeService _gameTimeService = null;

        [SerializeField] private GameObject _visual;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _timeText;

        private InputScopeToken _deathScopeToken;

        private void Awake()
        {
            if (_inputContexts == null || _gameTimeService == null)
            {
                throw new InvalidOperationException($"[{nameof(PlayerDeathPresenter)}] Required input or game time dependencies were not injected on '{name}'.");
            }
        }

        private void Start()
        {
            _backgroundAudioManager.ChangeAudioToDefaultAudioMode();
        }

        private void OnDestroy()
        {
            if (_deathScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_deathScopeToken);
                _deathScopeToken = default;
            }
        }

        public void EnableDeathScreen()
        {
            _scoreBoardNewScoreSaver.Save(_timerPresenter.TimerValue, hasSurvived: false);

            SetLevelText();

            SetTimeText();

            _pausePresenter.HideForDeath();

            _visual.SetActive(true);

            if (!_deathScopeToken.IsValid)
            {
                _deathScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.Death, GetEntityId().GetHashCode());
            }

            _backgroundAudioManager.ChangeAudioToDeathAudioMode();

            _gameTimeService.Pause();

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

            if (_scoreBoardBestScoreGetter.TryGetBestTime(hasSurvived: false, out uint bestTime)
                && bestTime == _timerPresenter.TimerValue)
            {
                timeText += $" <Color=#F8D61C>(New Best!)</Color>";
            }

            _timeText.text = timeText;
        }
    }
}
