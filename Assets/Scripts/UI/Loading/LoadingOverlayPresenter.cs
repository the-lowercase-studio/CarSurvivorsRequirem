using System;
using System.Collections;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Constants;
using DG.Tweening;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Loading
{
    public interface ILoadingOverlayPresenter
    {
        bool IsVisible { get; }
        void ShowImmediate(string title = null);
    }

    public class LoadingOverlayPresenter : MonoBehaviour, ILoadingOverlayPresenter
    {
        [Inject] private readonly IGameSceneLoader _gameSceneLoader = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;

        [Header("References")]
        [Tooltip("Root visual container toggled on during scene transitions.")]
        [SerializeField] private GameObject _visualRoot;

        [Tooltip("Canvas controlling overlay sorting order.")]
        [SerializeField] private Canvas _overlayCanvas;

        [Tooltip("CanvasGroup used to fade and block raycasts during loading.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Header text updated with the active tier loading label.")]
        [SerializeField] private TextMeshProUGUI _loadingTitleText;

        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float _fadeOutDuration = ProgressionConstants.LOADING_FADE_OUT_DURATION;
        [SerializeField, Min(0.05f)] private float _dotsAnimationInterval = ProgressionConstants.LOADING_DOTS_INTERVAL;

        private static readonly string[] DOT_SUFFIXES = { ".", "..", "..." };

        private Tween _fadeTween;
        private Coroutine _fadeRoutine;
        private Coroutine _dotsRoutine;
        private string _baseTitle;
        private bool _isSubscribed;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (_visualRoot == null)
            {
                throw new InvalidOperationException($"[{nameof(LoadingOverlayPresenter)}] _visualRoot is unassigned on '{name}'.");
            }

            if (_overlayCanvas == null)
            {
                throw new InvalidOperationException($"[{nameof(LoadingOverlayPresenter)}] _overlayCanvas is unassigned on '{name}'.");
            }

            if (_canvasGroup == null)
            {
                throw new InvalidOperationException($"[{nameof(LoadingOverlayPresenter)}] _canvasGroup is unassigned on '{name}'.");
            }

            if (_loadingTitleText == null)
            {
                throw new InvalidOperationException($"[{nameof(LoadingOverlayPresenter)}] _loadingTitleText is unassigned on '{name}'.");
            }

            _overlayCanvas.sortingOrder = ProgressionConstants.LOADING_OVERLAY_SORTING_ORDER;
            ShowImmediate(ProgressionConstants.DEFAULT_LOADING_TEXT);
        }

        private void OnEnable()
        {
            SubscribeToSceneLoader();
            if (IsVisible && _dotsRoutine == null)
            {
                StartDotsRoutine();
            }
        }

        private void Start()
        {
            SubscribeToSceneLoader();
        }

        private void OnDisable()
        {
            UnsubscribeFromSceneLoader();
            StopFadeRoutine();
            StopDotsRoutine();
            KillFadeTween();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSceneLoader();
            StopFadeRoutine();
            StopDotsRoutine();
            KillFadeTween();
        }

        public void ShowImmediate(string title = null)
        {
            StopFadeRoutine();
            KillFadeTween();

            if (!string.IsNullOrEmpty(title))
            {
                string trimmedTitle = title.TrimEnd('.');
                _baseTitle = string.IsNullOrEmpty(trimmedTitle) ? ProgressionConstants.DEFAULT_LOADING_TEXT : trimmedTitle;
            }
            else if (string.IsNullOrEmpty(_baseTitle))
            {
                _baseTitle = ProgressionConstants.DEFAULT_LOADING_TEXT;
            }

            _visualRoot.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            IsVisible = true;
            StartDotsRoutine();
        }

        private void SubscribeToSceneLoader()
        {
            if (_isSubscribed || _gameSceneLoader == null)
            {
                return;
            }

            _gameSceneLoader.OnStartLoadingScene += HandleStartLoadingScene;
            _gameSceneLoader.OnSceneLoaded += HandleSceneLoaded;
            _isSubscribed = true;
        }

        private void UnsubscribeFromSceneLoader()
        {
            if (!_isSubscribed || _gameSceneLoader == null)
            {
                return;
            }

            _gameSceneLoader.OnStartLoadingScene -= HandleStartLoadingScene;
            _gameSceneLoader.OnSceneLoaded -= HandleSceneLoaded;
            _isSubscribed = false;
        }

        private void HandleStartLoadingScene(object sender, ValueEventArgs<GameScene> e)
        {
            string title = ResolveLoadingTitle(e.Value);
            ShowImmediate(title);
        }

        private void HandleSceneLoaded(object sender, ValueEventArgs<GameScene> e)
        {
            if (!isActiveAndEnabled)
            {
                HideImmediate();
                return;
            }

            StopFadeRoutine();
            _fadeRoutine = StartCoroutine(FadeOutNextFrameCoroutine());
        }

        private IEnumerator FadeOutNextFrameCoroutine()
        {
            yield return null;

            KillFadeTween();
            _fadeTween = _canvasGroup
                .DOFade(0f, _fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(HideImmediate);

            _fadeRoutine = null;
        }

        private void HideImmediate()
        {
            StopDotsRoutine();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _visualRoot.SetActive(false);
            IsVisible = false;
            _fadeTween = null;
        }

        private string ResolveLoadingTitle(GameScene targetScene)
        {
            if (targetScene == GameScene.MainMenu || _runSessionService == null)
            {
                return ProgressionConstants.DEFAULT_LOADING_TEXT;
            }

            int displayTierNumber = _runSessionService.CurrentTierIndex + 1;
            return string.Format(ProgressionConstants.DEFAULT_LOADING_TIER_FORMAT, displayTierNumber);
        }

        private void StartDotsRoutine()
        {
            StopDotsRoutine();

            string baseTitle = string.IsNullOrEmpty(_baseTitle) ? ProgressionConstants.DEFAULT_LOADING_TEXT : _baseTitle;
            _loadingTitleText.text = string.Concat(baseTitle, DOT_SUFFIXES[0]);

            if (isActiveAndEnabled)
            {
                _dotsRoutine = StartCoroutine(AnimateDotsCoroutine());
            }
        }

        private IEnumerator AnimateDotsCoroutine()
        {
            int dotIndex = 0;
            while (IsVisible)
            {
                string baseTitle = string.IsNullOrEmpty(_baseTitle) ? ProgressionConstants.DEFAULT_LOADING_TEXT : _baseTitle;
                _loadingTitleText.text = string.Concat(baseTitle, DOT_SUFFIXES[dotIndex]);
                dotIndex = (dotIndex + 1) % DOT_SUFFIXES.Length;
                yield return new WaitForSecondsRealtime(_dotsAnimationInterval);
            }

            _dotsRoutine = null;
        }

        private void StopDotsRoutine()
        {
            if (_dotsRoutine != null)
            {
                StopCoroutine(_dotsRoutine);
                _dotsRoutine = null;
            }
        }

        private void StopFadeRoutine()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
        }

        private void KillFadeTween()
        {
            if (_fadeTween != null && _fadeTween.IsActive())
            {
                _fadeTween.Kill();
            }

            _fadeTween = null;
        }
    }
}
