using System;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Input;
using Assets.Scripts.Player;
using Assets.Scripts.UI.Input;
using Assets.Scripts.UI.Skills;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.UI.Pause
{
    public interface IPausePresenter
    {
        void HideForDeath();
    }

    public class PausePresenter : MonoBehaviour, IPausePresenter
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly ISkillsStatsPresenter _skillsStatsPresenter = null;
        [Inject] private readonly IInputCommands _inputCommands = null;
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly UiInputScope _uiInputScope = null;

        [SerializeField] private GameObject _visual;

        private InputScopeToken _pauseScopeToken;
        private bool _isInitialized;

        private void Awake()
        {
            if (_inputCommands == null || _inputContexts == null || _uiInputScope == null)
            {
                throw new InvalidOperationException($"[{nameof(PausePresenter)}] Required input dependencies were not injected on '{name}'.");
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputCommands.OnCommand += HandleInputCommand;
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _inputCommands.OnCommand -= HandleInputCommand;
        }

        private void OnDestroy()
        {
            ReleasePauseScope();
        }

        public void ToggleActivation()
        {
            if (!_playerManager.Health.IsAlive())
            {
                return;
            }

            if (_visual.activeSelf)
            {
                ClosePause();
            }
            else
            {
                OpenPause();
            }
        }

        public void HideForDeath()
        {
            _visual.SetActive(false);
            _skillsStatsPresenter.SetPauseVisible(false);
            ReleasePauseScope();
        }

        [System.Obsolete("Use ToggleActivation instead")]
        public void ToogleActivation()
        {
            ToggleActivation();
        }

        private void HandleInputCommand(InputCommand command)
        {
            if (command.Kind == InputCommandKind.Pause)
            {
                if (_playerManager.Health.IsAlive() && !_visual.activeSelf)
                {
                    OpenPause();
                }

                return;
            }

            if (command.Kind == InputCommandKind.UiCancel
                && _visual.activeSelf
                && _inputContexts.EffectiveContext == InputContextScopeKind.Pause
                && !_uiInputScope.TryHandleWidgetCancel())
            {
                ClosePause();
            }
        }

        private void OpenPause()
        {
            if (!_playerManager.Health.IsAlive())
            {
                return;
            }

            if (!_pauseScopeToken.IsValid)
            {
                _pauseScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.Pause, GetEntityId().GetHashCode());
            }

            _visual.SetActive(true);
            GameTime.Pause();
            _skillsStatsPresenter.SetPauseVisible(true);
        }

        private void ClosePause()
        {
            _visual.SetActive(false);
            GameTime.Resume();
            _skillsStatsPresenter.SetPauseVisible(false);
            ReleasePauseScope();
            _inputContexts.ConsumeActiveControlsUntilRelease();
        }

        private void ReleasePauseScope()
        {
            if (_pauseScopeToken.IsValid)
            {
                _inputContexts?.ReleaseScope(_pauseScopeToken);
                _pauseScopeToken = default;
            }
        }
    }
}
