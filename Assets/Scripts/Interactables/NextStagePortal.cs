using System;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.GameFlow.Constants;
using Assets.Scripts.Input;
using Assets.Scripts.Interactables.Constants;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.UI.EndOfRun;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.UI.Skills;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Interactables
{
    public interface INextStagePortal
    {
    }

    [RequireComponent(typeof(Collider))]
    public class NextStagePortal : MonoBehaviour, INextStagePortal
    {
        [Inject] private readonly IRunSessionService _runSessionService = null;
        [Inject] private readonly IGameSceneLoader _gameSceneLoader = null;
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly ITimerPresenter _timerPresenter = null;
        [Inject] private readonly IInputContexts _inputContexts = null;
        [Inject] private readonly IDemoThankYouPresenter _demoThankYouPresenter = null;
        [Inject] private readonly ISkillUpgradePresenter _skillUpgradePresenter = null;

        [Tooltip("Optional custom center transform for the vortex pull. Uses portal transform if unassigned.")]
        [SerializeField] private Transform _vortexCenter;

        private Collider _triggerCollider;
        private Sequence _vortexSequence;
        private InputScopeToken _transitionScopeToken;
        private bool _hasTriggered;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
        }

        private void OnDisable()
        {
            KillVortexSequence();
            ReleaseTransitionScope();
        }

        private void OnDestroy()
        {
            KillVortexSequence();
            ReleaseTransitionScope();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered || other == null)
            {
                return;
            }

            bool isPlayerLayer = ((1 << other.gameObject.layer) & EntityLayers.Player) != 0;
            if (!isPlayerLayer && other.GetComponentInParent<IPlayerManager>() == null)
            {
                return;
            }

            if (_playerManager == null
                || _playerManager.GameObject == null
                || _playerManager.Health == null
                || !_playerManager.Health.IsAlive())
            {
                return;
            }

            TriggerPortalSequence();
        }

        private void TriggerPortalSequence()
        {
            if (_runSessionService == null)
            {
                throw new InvalidOperationException($"[{nameof(NextStagePortal)}] IRunSessionService was not injected on '{name}'.");
            }

            if (_gameSceneLoader == null)
            {
                throw new InvalidOperationException($"[{nameof(NextStagePortal)}] IGameSceneLoader was not injected on '{name}'.");
            }

            _hasTriggered = true;

            _skillUpgradePresenter?.HideForStageTransition();

            if (!_transitionScopeToken.IsValid && _inputContexts != null)
            {
                _transitionScopeToken = _inputContexts.AcquireScope(InputContextScopeKind.Dying, GetEntityId().GetHashCode());
            }

            _timerPresenter?.SetPaused(true);
            _playerManager.DamagedHandler?.SetPortalTransitionInvulnerable(true);

            if (_playerManager.SkillsRegistry is Component skillsComponent)
            {
                skillsComponent.gameObject.SetActive(false);
            }

            Rigidbody playerRigidbody = _playerManager.GameObject.GetComponent<Rigidbody>();
            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
                playerRigidbody.isKinematic = true;
            }

            float duration = _runSessionService.ProgressionConfig != null
                ? _runSessionService.ProgressionConfig.PortalVortexDuration
                : ProgressionConstants.DEFAULT_PORTAL_VORTEX_DURATION;
            float spins = _runSessionService.ProgressionConfig != null
                ? _runSessionService.ProgressionConfig.PortalVortexSpins
                : ProgressionConstants.DEFAULT_PORTAL_VORTEX_SPINS;

            Transform carTransform = _playerManager.GameObject.transform;
            Vector3 targetCenter = (_vortexCenter != null ? _vortexCenter.position : transform.position)
                + Vector3.up * PortalConstants.VORTEX_CENTER_Y_OFFSET;

            KillVortexSequence();

            _vortexSequence = DOTween.Sequence();
            _vortexSequence.Join(carTransform.DOMove(targetCenter, duration).SetEase(Ease.InQuad));
            _vortexSequence.Join(carTransform.DORotate(new Vector3(0f, spins * 360f, 0f), duration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
            _vortexSequence.Join(carTransform.DOScale(new Vector3(0.01f, 0.01f, 0.01f), duration).SetEase(Ease.InBack));
            _vortexSequence.OnComplete(CompletePortalTransition);
        }

        private void CompletePortalTransition()
        {
            _vortexSequence = null;
            ReleaseTransitionScope();

            uint elapsedSeconds = _timerPresenter != null ? _timerPresenter.TimerValue : 0u;

            if (!_runSessionService.IsFinalTier)
            {
                _runSessionService.CaptureSnapshotAndAdvanceTier(_playerManager, elapsedSeconds);

                LoopTierConfig nextTier = _runSessionService.ActiveTierConfig;
                if (nextTier != null && nextTier.OverrideScene)
                {
                    _gameSceneLoader.LoadNewSceneAsync(nextTier.SceneOverride);
                }
                else
                {
                    _gameSceneLoader.ReloadCurrentSceneAsync();
                }

                return;
            }

            _runSessionService.CaptureFinalRunTimer(elapsedSeconds);

            if (_demoThankYouPresenter == null)
            {
                throw new InvalidOperationException($"[{nameof(NextStagePortal)}] IDemoThankYouPresenter was not injected on '{name}'.");
            }

            _demoThankYouPresenter.EnableThankYouScreen();
        }

        private void KillVortexSequence()
        {
            if (_vortexSequence != null && _vortexSequence.IsActive())
            {
                _vortexSequence.Kill();
            }

            _vortexSequence = null;
        }

        private void ReleaseTransitionScope()
        {
            if (_transitionScopeToken.IsValid && _inputContexts != null)
            {
                _inputContexts.ReleaseScope(_transitionScopeToken);
                _transitionScopeToken = default;
            }
        }
    }
}
