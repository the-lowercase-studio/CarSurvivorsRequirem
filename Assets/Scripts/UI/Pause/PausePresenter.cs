using Assets.Scripts.GameFlow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.UI.Pause
{
    public class PausePresenter : MonoBehaviour
    {
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
