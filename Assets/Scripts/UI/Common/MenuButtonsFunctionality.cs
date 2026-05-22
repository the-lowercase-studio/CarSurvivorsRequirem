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

        public void OnSceneLoadButtonClick(string scene)
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

        public void ToogleActivityOfObjectDisableOthers(UnityEngine.Object targetObject)
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

            ToogleActivityOfObject(gameObject);
        }

        public void ToogleActivityOfObjectDisableOthers(GameObject gameObject)
        {
            ToogleActivityOfObjectDisableOthers((UnityEngine.Object)gameObject);
        }

        public void DiasbleAllOtherObjects(UnityEngine.Object targetObject)
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

        public void DiasbleAllOtherObjects(GameObject gameObject)
        {
            DiasbleAllOtherObjects((UnityEngine.Object)gameObject);
        }

        public void ToogleActivityOfObject(UnityEngine.Object targetObject)
        {
            if (!TryGetGameObject(targetObject, out GameObject gameObject))
            {
                return;
            }

            gameObject.SetActive(!gameObject.activeSelf);
        }

        public void ToogleActivityOfObject(GameObject gameObject)
        {
            ToogleActivityOfObject((UnityEngine.Object)gameObject);
        }

        public void OnTryAgainClick()
        {
            _gameSceneLoader.ReloadCurrentSceneAsync();
        }

        public void OnExitClick()
        {
            Application.Quit();
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
