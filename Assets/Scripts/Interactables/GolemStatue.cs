using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.GameFlow.Constants;
using Assets.Scripts.Input;
using Assets.Scripts.Interactables.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.UI.Input;
using DG.Tweening;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Interactables
{
    public interface IGolemStatue
    {
        void DismissForExternalSpawn();
    }

    public class GolemStatue : MonoBehaviour, IGolemStatue
    {
        [Inject] private readonly IBossEncounterService _bossEncounterService = null;
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;
        [Inject] private readonly IGameplayInput _gameplayInput = null;
        [Inject] private readonly IInputPromptProvider _promptProvider = null;

        [Header("Interaction")]
        [Tooltip("Maximum distance in meters from which the player can awaken the statue.")]
        [SerializeField] private float _interactionRadius = GolemStatueConstants.DEFAULT_INTERACTION_RADIUS;

        [Tooltip("Minimum horizontal clearance radius maintained around the Golem spawn point so the car is not pinned inside.")]
        [SerializeField] private float _golemSpawnClearanceRadius = GolemStatueConstants.DEFAULT_GOLEM_CLEARANCE_RADIUS;

        [Header("Scene References")]
        [Tooltip("Visual root transform shaken during the 1.0s awakening sequence.")]
        [SerializeField] private Transform _visualRoot;

        [Tooltip("Blocking physics collider on TerrainLayers.Impassable disabled prior to grid release.")]
        [SerializeField] private Collider _impassableCollider;

        [Tooltip("World-space status text displaying tower progress or the challenge prompt.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("World-space prompt canvas enabled when inside interaction radius and all towers are defeated.")]
        [SerializeField] private GameObject _interactionCanvas;

        [Tooltip("Device-aware interaction prompt view bound to PlayerInteract.")]
        [SerializeField] private InputPromptView _interactPromptView;

        private readonly List<Cell> _reservedCells = new();
        private Sequence _shakeSequence;
        private bool _isAwakening;
        private bool _isDismissed;
        private bool _isRegistered;

        private void Awake()
        {
            ValidateRequiredDependencies();
        }

        private void Start()
        {
            _interactPromptView.Bind(_promptProvider, InputActionId.PlayerInteract);
            _interactionCanvas.SetActive(false);

            ReserveGridFootprint();

            _bossEncounterService.RegisterGolemStatue(this);
            _bossEncounterService.OnTowerDefeated += HandleTowerDefeated;
            _isRegistered = true;

            RefreshStatusText(_bossEncounterService.DefeatedTowersCount, _bossEncounterService.RequiredTowersCount);
        }

        private void Update()
        {
            if (_isAwakening || _isDismissed)
            {
                return;
            }

            bool isUnlocked = _bossEncounterService.DefeatedTowersCount >= _bossEncounterService.RequiredTowersCount;
            if (!isUnlocked || _playerManager == null || _playerManager.GameObject == null)
            {
                if (_interactionCanvas.activeSelf)
                {
                    _interactionCanvas.SetActive(false);
                }

                return;
            }

            float distance = Vector3.Distance(transform.position, _playerManager.GameObject.transform.position);
            if (distance <= _interactionRadius)
            {
                if (!_interactionCanvas.activeSelf)
                {
                    _interactionCanvas.SetActive(true);
                }

                if (_gameplayInput.WasInteractPressedThisFrame)
                {
                    BeginAwakeningSequence();
                }
            }
            else if (_interactionCanvas.activeSelf)
            {
                _interactionCanvas.SetActive(false);
            }
        }

        private void OnDisable()
        {
            KillShakeSequence();
            if (_interactionCanvas != null && _interactionCanvas.activeSelf)
            {
                _interactionCanvas.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            KillShakeSequence();

            if (_isRegistered && _bossEncounterService != null)
            {
                _bossEncounterService.OnTowerDefeated -= HandleTowerDefeated;
                _bossEncounterService.UnregisterGolemStatue(this);
                _isRegistered = false;
            }
        }

        public void DismissForExternalSpawn()
        {
            if (_isDismissed)
            {
                return;
            }

            KillShakeSequence();
            DismissStatueCore();
        }

        private void BeginAwakeningSequence()
        {
            if (_isAwakening || _isDismissed)
            {
                return;
            }

            _isAwakening = true;
            _interactionCanvas.SetActive(false);
            _statusText.gameObject.SetActive(false);

            KillShakeSequence();

            _shakeSequence = DOTween.Sequence();
            _shakeSequence.Join(_visualRoot.DOShakePosition(
                GolemStatueConstants.AWAKEN_SHAKE_DURATION,
                GolemStatueConstants.AWAKEN_SHAKE_POSITION_STRENGTH,
                GolemStatueConstants.AWAKEN_SHAKE_VIBRATO));
            _shakeSequence.Join(_visualRoot.DOShakeRotation(
                GolemStatueConstants.AWAKEN_SHAKE_DURATION,
                GolemStatueConstants.AWAKEN_SHAKE_ROTATION_STRENGTH,
                GolemStatueConstants.AWAKEN_SHAKE_VIBRATO));
            _shakeSequence.OnComplete(CompleteAwakeningAndSpawn);
        }

        private void CompleteAwakeningAndSpawn()
        {
            _shakeSequence = null;

            if (_isDismissed)
            {
                return;
            }

            Vector3 spawnPosition = transform.position;
            DismissStatueCore();
            EnsurePlayerSpawnClearance(spawnPosition);
            _bossEncounterService.SpawnGolemBoss(spawnPosition);
        }

        private void DismissStatueCore()
        {
            _isDismissed = true;
            _isAwakening = false;

            if (_interactionCanvas != null)
            {
                _interactionCanvas.SetActive(false);
            }

            if (_impassableCollider != null)
            {
                _impassableCollider.enabled = false;
            }

            ReleaseGridFootprint();
            gameObject.SetActive(false);
        }

        private void EnsurePlayerSpawnClearance(Vector3 spawnPosition)
        {
            if (_playerManager == null || _playerManager.GameObject == null || _golemSpawnClearanceRadius <= 0f)
            {
                return;
            }

            Transform playerTransform = _playerManager.GameObject.transform;
            Vector3 offset = playerTransform.position - spawnPosition;
            offset.y = 0f;

            float sqrDist = offset.sqrMagnitude;
            float minDist = _golemSpawnClearanceRadius;
            if (sqrDist >= minDist * minDist)
            {
                return;
            }

            Vector3 pushDir = sqrDist > 0.0001f ? offset.normalized : Vector3.forward;
            Vector3 targetPos = spawnPosition + pushDir * minDist;
            targetPos.y = playerTransform.position.y;

            if (GroundSupportQuery.TryGetSupportedPosition(targetPos, out Vector3 supportedPos))
            {
                targetPos.y = supportedPos.y + (playerTransform.position.y - spawnPosition.y);
            }

            playerTransform.position = targetPos;
        }

        private void ReserveGridFootprint()
        {
            _reservedCells.Clear();

            Grid grid = _gridManager != null ? _gridManager.WorldGrid : null;
            if (grid == null || grid.Cells == null || _impassableCollider == null)
            {
                return;
            }

            Bounds bounds = _impassableCollider.bounds;
            float halfCell = grid.CellSize * 0.5f;
            Vector3 margin = new Vector3(
                GolemStatueConstants.FOOTPRINT_PADDING + halfCell,
                0f,
                GolemStatueConstants.FOOTPRINT_PADDING + halfCell);

            Cell minCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, bounds.min - margin);
            Cell maxCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, bounds.max + margin);
            if (minCell == null || maxCell == null)
            {
                return;
            }

            int minX = Mathf.Max(0, Mathf.Min(minCell.WorldGridPos.x, maxCell.WorldGridPos.x));
            int maxX = Mathf.Min(grid.Width - 1, Mathf.Max(minCell.WorldGridPos.x, maxCell.WorldGridPos.x));
            int minY = Mathf.Max(0, Mathf.Min(minCell.WorldGridPos.y, maxCell.WorldGridPos.y));
            int maxY = Mathf.Min(grid.Height - 1, Mathf.Max(minCell.WorldGridPos.y, maxCell.WorldGridPos.y));

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Cell cell = grid.Cells[x, y];
                    if (cell != null)
                    {
                        cell.Cost = byte.MaxValue;
                        _reservedCells.Add(cell);
                    }
                }
            }
        }

        private void ReleaseGridFootprint()
        {
            for (int i = 0; i < _reservedCells.Count; i++)
            {
                Cell cell = _reservedCells[i];
                if (cell != null)
                {
                    cell.ResetCosts();
                }
            }

            _reservedCells.Clear();
        }

        private void HandleTowerDefeated(int defeatedCount, int requiredCount)
        {
            RefreshStatusText(defeatedCount, requiredCount);
        }

        private void RefreshStatusText(int defeatedCount, int requiredCount)
        {
            if (_isDismissed || _isAwakening)
            {
                return;
            }

            LoopTierConfig tierConfig = _runSessionService?.ActiveTierConfig;
            string progressFormat = tierConfig != null && !string.IsNullOrEmpty(tierConfig.StatueProgressFormat)
                ? tierConfig.StatueProgressFormat
                : ProgressionConstants.DEFAULT_STATUE_PROGRESS_FORMAT;
            string challengePrompt = tierConfig != null && !string.IsNullOrEmpty(tierConfig.StatueChallengePrompt)
                ? tierConfig.StatueChallengePrompt
                : ProgressionConstants.DEFAULT_STATUE_CHALLENGE_PROMPT;

            if (defeatedCount >= requiredCount)
            {
                _statusText.text = challengePrompt;
            }
            else
            {
                _statusText.text = string.Format(progressFormat, defeatedCount, requiredCount);
            }
        }

        private void KillShakeSequence()
        {
            if (_shakeSequence != null && _shakeSequence.IsActive())
            {
                _shakeSequence.Kill();
            }

            _shakeSequence = null;

            if (_visualRoot != null)
            {
                _visualRoot.DOKill();
            }
        }

        private void ValidateRequiredDependencies()
        {
            if (_bossEncounterService == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] IBossEncounterService was not injected on '{name}'.");
            }

            if (_gameplayInput == null || _promptProvider == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] Required input dependencies were not injected on '{name}'.");
            }

            if (_visualRoot == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] _visualRoot is unassigned on '{name}'.");
            }

            if (_impassableCollider == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] _impassableCollider is unassigned on '{name}'.");
            }

            if (_statusText == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] _statusText is unassigned on '{name}'.");
            }

            if (_interactionCanvas == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] _interactionCanvas is unassigned on '{name}'.");
            }

            if (_interactPromptView == null)
            {
                throw new InvalidOperationException($"[{nameof(GolemStatue)}] _interactPromptView is unassigned on '{name}'.");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
#endif
    }
}
