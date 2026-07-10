using Assets.Scripts.Player;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Interactables
{
    public class IncreaseDifficultyTotem : MonoBehaviour
    {
        [Inject] private readonly IPlayerManager _playerManager;
        [Inject] private readonly IEnemySpawnDifficultyController _difficultyController;

        [SerializeField] private float _interactionRadius = 3f;
        [SerializeField] private GameObject _interactionCanvas;
        [SerializeField] private GameObject _totemVisuals;
        [SerializeField] private VFXPlayer _vfxPlayer;
        [SerializeField] private float _difficultyIncreaseAmount = 4f;

        private bool _hasBeenUsed;

        private void Update()
        {
            if (_hasBeenUsed || _playerManager?.GameObject == null)
            {
                if (_interactionCanvas != null && _interactionCanvas.activeSelf)
                {
                    _interactionCanvas.SetActive(false);
                }
                return;
            }

            float distance = Vector3.Distance(transform.position, _playerManager.GameObject.transform.position);
            if (distance <= _interactionRadius)
            {
                if (_interactionCanvas != null && !_interactionCanvas.activeSelf)
                {
                    _interactionCanvas.SetActive(true);
                }

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    _difficultyController.IncreaseSpawnChanceRedistributionFactor(_difficultyIncreaseAmount);

                    if (_interactionCanvas != null)
                    {
                        _interactionCanvas.SetActive(false);
                    }

                    if (_totemVisuals != null)
                    {
                        _totemVisuals.SetActive(false);
                    }

                    if (_vfxPlayer != null)
                    {
                        _vfxPlayer.Play(new VFXPlayConfig());
                    }

                    _hasBeenUsed = true;
                    enabled = false;
                }
            }
            else
            {
                if (_interactionCanvas != null && _interactionCanvas.activeSelf)
                {
                    _interactionCanvas.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            if (_interactionCanvas != null && _interactionCanvas.activeSelf)
            {
                _interactionCanvas.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
#endif
    }
}
