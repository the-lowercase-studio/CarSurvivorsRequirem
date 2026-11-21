using System;
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

namespace Assets.Scripts.UI.EndOfRun
{
    public interface IDemoThankYouPresenter
    {
        void EnableThankYouScreen();
    }

    public class DemoThankYouPresenter : MonoBehaviour, IDemoThankYouPresenter
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IScoreBoardNewScoreSaver _scoreBoardNewScoreSaver = null;
        [Inject] private readonly IScoreBoardBestScoreGetter _scoreBoardBestScoreGetter = null;
        [Inject] private readonly ITimerPresenter _timerPresenter = null;
        [Inject] private readonly IPausePresenter _pausePresenter = null;
        [Inject] private readonly ISkillsStatsPresenter _skillsStatsPresenter = null;
        [Inject] private readonly ISkillUpgradePresenter _skillUpgradePresenter = null;
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IGameTimeService _gameTimeService = null;

        [SerializeField] private GameObject _visual;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _timeText;

        private InputScopeToken _endOfRunScopeToken;

        private void Awake()
        {
            if (_inputContexts == null || _gameTimeService == null)
            {
                throw new InvalidOperationException($"[{nameof(DemoThankYouPresenter)}] Required input or game time dependencies were not injected on '{name}'.");
            }

            if (_visual == null)
            {
                throw new InvalidOperationException($"[{nameof(DemoThankYouPresenter)}] _visual is unassigned on '{name}'.");
            }

            if (_levelText == null)
            {
                throw new InvalidOperationException($"[{nameof(DemoThankYouPresenter)}] _levelText is unassigned on '{name}'.");
            }

            if (_timeText == null)
            {
                throw new InvalidOperationException($"[{nameof(DemoThankYouPresenter)}] _timeText is unassigned on '{name}'.");
            }

            _visual.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_endOfRunScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_endOfRunScopeToken);
                _endOfRunScopeToken = default;
            }
        }

        public void EnableThankYouScreen()
        {
            _skillUpgradePresenter?.HideForStageTransition();

            _scoreBoardNewScoreSaver.Save(_timerPresenter.TimerValue, hasSurvived: true);

            SetLevelText();
            SetTimeText();

            _pausePresenter.HideForDeath();

            _visual.SetActive(true);

            if (!_endOfRunScopeToken.IsValid)
            {
                _endOfRunScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.Death, GetEntityId().GetHashCode());
            }

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
            string timeText = "Completion Time: " +
                TimeConversionUtility.FormatSecondsToTimeString(_timerPresenter.TimerValue);

            if (_scoreBoardBestScoreGetter.TryGetBestTime(hasSurvived: true, out uint bestTime)
                && bestTime == _timerPresenter.TimerValue)
            {
                timeText += " <Color=#F8D61C>(New Best!)</Color>";
            }

            _timeText.text = timeText;
        }
    }
}
