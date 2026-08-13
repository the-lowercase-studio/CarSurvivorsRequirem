using System;
using Assets.Scripts.GameFlow;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.UI.Common
{
    public class MenuButtonsFunctionality : MonoBehaviour
    {
        [Inject] private readonly IGameSceneLoader _gameSceneLoader;

        [SerializeField] private GameObject[] _enabledDisabledObjects;

        public void OnSceneLoadClicked(string scene)
        {
            if (Enum.TryParse(scene, true, out GameScene gameScene))
            {
                _gameSceneLoader.LoadNewSceneAsync(gameScene);
            }
            else
            {
                Debug.LogError($"Invalid scene name: {scene}");
            }
        }

        [Obsolete("Use OnSceneLoadClicked instead")]
        public void OnSceneLoadButtonClick(string scene)
        {
            OnSceneLoadClicked(scene);
        }

        public void ToggleActivityOfObjectDisableOthers(UnityEngine.Object targetObject)
        {
            if (!TryGetGameObject(targetObject, out GameObject gameObject))
            {
                return;
            }

            foreach (var panel in _enabledDisabledObjects)
            {
                if (panel != gameObject)
                {
                    panel.SetActive(false);
                }
            }

            ToggleActivityOfObject(gameObject);
        }

        public void ToggleActivityOfObjectDisableOthers(GameObject gameObject)
        {
            ToggleActivityOfObjectDisableOthers((UnityEngine.Object)gameObject);
        }

        [Obsolete("Use ToggleActivityOfObjectDisableOthers instead")]
        public void ToogleActivityOfObjectDisableOthers(UnityEngine.Object targetObject)
        {
            ToggleActivityOfObjectDisableOthers(targetObject);
        }

        [Obsolete("Use ToggleActivityOfObjectDisableOthers instead")]
        public void ToogleActivityOfObjectDisableOthers(GameObject gameObject)
        {
            ToggleActivityOfObjectDisableOthers(gameObject);
        }

        public void DisableAllOtherObjects(UnityEngine.Object targetObject)
        {
            if (!TryGetGameObject(targetObject, out GameObject gameObject))
            {
                return;
            }

            foreach (var panel in _enabledDisabledObjects)
            {
                if (panel != gameObject)
                {
                    panel.SetActive(false);
                }
            }
        }

        public void DisableAllOtherObjects(GameObject gameObject)
        {
            DisableAllOtherObjects((UnityEngine.Object)gameObject);
        }

        [Obsolete("Use DisableAllOtherObjects instead")]
        public void DiasbleAllOtherObjects(UnityEngine.Object targetObject)
        {
            DisableAllOtherObjects(targetObject);
        }

        [Obsolete("Use DisableAllOtherObjects instead")]
        public void DiasbleAllOtherObjects(GameObject gameObject)
        {
            DisableAllOtherObjects(gameObject);
        }

        public void ToggleActivityOfObject(UnityEngine.Object targetObject)
        {
            if (!TryGetGameObject(targetObject, out GameObject gameObject))
            {
                return;
            }

            gameObject.SetActive(!gameObject.activeSelf);
        }

        public void ToggleActivityOfObject(GameObject gameObject)
        {
            ToggleActivityOfObject((UnityEngine.Object)gameObject);
        }

        [Obsolete("Use ToggleActivityOfObject instead")]
        public void ToogleActivityOfObject(UnityEngine.Object targetObject)
        {
            ToggleActivityOfObject(targetObject);
        }

        [Obsolete("Use ToggleActivityOfObject instead")]
        public void ToogleActivityOfObject(GameObject gameObject)
        {
            ToggleActivityOfObject(gameObject);
        }

        public void OnTryAgainClicked()
        {
            _gameSceneLoader.ReloadCurrentSceneAsync();
        }

        [Obsolete("Use OnTryAgainClicked instead")]
        public void OnTryAgainClick()
        {
            OnTryAgainClicked();
        }

        public void OnExitClicked()
        {
            Application.Quit();
        }

        [Obsolete("Use OnExitClicked instead")]
        public void OnExitClick()
        {
            OnExitClicked();
        }

        private bool TryGetGameObject(UnityEngine.Object targetObject, out GameObject gameObject)
        {
            gameObject = targetObject switch
            {
                GameObject targetGameObject => targetGameObject,
                Component targetComponent => targetComponent.gameObject,
                _ => null
            };

            if (gameObject == null)
            {
                Debug.LogError($"{nameof(MenuButtonsFunctionality)} expected a GameObject or Component argument, but received {targetObject}.");
                return false;
            }

            return true;
        }
    }
}
