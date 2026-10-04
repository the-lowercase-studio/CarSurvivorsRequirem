using Assets.Scripts.GameFlow;
using Assets.Scripts.Player;
using Assets.Scripts.UI.Skills;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [SerializeField] private GameObject _visual;

        private void OnEnable()
        {
            InputSystem.actions.FindAction("Pause").performed += OnPausePerformed;
        }

        private void OnDisable()
        {
            InputSystem.actions.FindAction("Pause").performed -= OnPausePerformed;
        }

        public void ToggleActivation()
        {
            if (!_playerManager.Health.IsAlive())
            {
                return;
            }

            if (_visual.activeSelf)
            {
                _visual.SetActive(false);
                GameTime.Resume();
            }
            else
            {
                _visual.SetActive(true);
                GameTime.Pause();
            }

            _skillsStatsPresenter.SetPauseVisible(_visual.activeSelf);
        }

        public void HideForDeath()
        {
            _visual.SetActive(false);
            _skillsStatsPresenter.SetPauseVisible(false);
        }

        [System.Obsolete("Use ToggleActivation instead")]
        public void ToogleActivation()
        {
            ToggleActivation();
        }

        private void OnPausePerformed(InputAction.CallbackContext obj)
        {
            ToggleActivation();
        }
    }
}
