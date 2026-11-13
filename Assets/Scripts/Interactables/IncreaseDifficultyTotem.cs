using System;
using Assets.Scripts.Input;
using Assets.Scripts.Player;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.UI.Input;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Interactables
{
    public class IncreaseDifficultyTotem : MonoBehaviour
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IEnemySpawnDifficultyController _difficultyController = null;
        [Inject] private readonly IGameplayInput _gameplayInput = null;
        [Inject] private readonly IInputPromptProvider _promptProvider = null;

        [SerializeField] private float _interactionRadius = 3f;
        [SerializeField] private GameObject _interactionCanvas;
        [SerializeField] private InputPromptView _interactPromptView;
        [SerializeField] private GameObject _totemVisuals;
        [SerializeField] private VFXPlayer _vfxPlayer;
        [SerializeField] private float _difficultyIncreaseAmount = 4f;

        private bool _hasBeenUsed;

        private void Start()
        {
            if (_gameplayInput == null || _promptProvider == null)
            {
                throw new InvalidOperationException($"[{nameof(IncreaseDifficultyTotem)}] Required input services were not injected on '{name}'.");
            }

            if (_interactPromptView == null)
            {
                throw new InvalidOperationException($"[{nameof(IncreaseDifficultyTotem)}] _interactPromptView is unassigned on '{name}'.");
            }

            _interactPromptView.Bind(_promptProvider, InputActionId.PlayerInteract);
        }

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

                if (_gameplayInput.WasInteractPressedThisFrame)
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
