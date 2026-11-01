using System;
using System.Collections.Generic;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Constants;
using Assets.Scripts.PowerUps.Presentation;
using Assets.Scripts.VFX;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.PowerUps.Freeze
{
    public interface ITimeFreezeState
    {
        bool IsFrozen { get; }
        float UnfrozenTime { get; }
        event Action<bool> OnFrozenChanged;
    }

    public class TimeFreezeService : MonoBehaviour, ITimeFreezeState
    {
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;

        [SerializeField] private FreezeTimerConfigSO _config;
        [SerializeField] private Volume _freezeVolume;
        [SerializeField] private ScreenOverlayPresenter _frostOverlayPresenter;
        [SerializeField] private VFXPlayer _freezeVfxPlayer;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;

        private readonly PowerUpTimer _timer = new PowerUpTimer();
        private readonly List<Tween> _pausedTweens = new List<Tween>();
        private readonly List<Tween> _tweenScratch = new List<Tween>();
        private Sequence _presentationSequence;
        private bool _isFrozen;
        private bool _isFadingOut;
        private float _freezeStartedAt;
        private float _accumulatedFrozenSeconds;

        public bool IsFrozen => _isFrozen;

        public float UnfrozenTime
        {
            get
            {
                float currentFreezeDuration = _isFrozen ? (Time.time - _freezeStartedAt) : 0f;
                return Time.time - _accumulatedFrozenSeconds - currentFreezeDuration;
            }
        }

        public event Action<bool> OnFrozenChanged;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException("TimeFreezeService requires an authored FreezeTimerConfigSO.");
            }

            _config.ValidateConfiguration();
            _pausedTweens.Capacity = Mathf.Max(_pausedTweens.Capacity, _config.TweenBufferCapacity);
            _tweenScratch.Capacity = Mathf.Max(_tweenScratch.Capacity, _config.TweenBufferCapacity);

            if (_freezeVolume != null)
            {
                _freezeVolume.weight = 0f;
            }

            if (_frostOverlayPresenter != null)
            {
                _frostOverlayPresenter.ResetOverlay();
            }
        }

        private void Start()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected += HandlePowerUpCollected;
            }
        }

        private void Update()
        {
            if (!_isFrozen)
            {
                return;
            }

            if (!_isFadingOut && _timer.IsInWindow(_config.FadeOutDuration))
            {
                _isFadingOut = true;
                StartFadeOutPresentation();
            }

            if (_timer.Tick(Time.deltaTime))
            {
                EndFreeze(notifyPresentation: true);
            }
        }

        private void OnDisable()
        {
            EndFreeze(notifyPresentation: false);
        }

        private void OnDestroy()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected -= HandlePowerUpCollected;
            }

            EndFreeze(notifyPresentation: false);
        }

        private void HandlePowerUpCollected(PowerUpType powerUpType)
        {
            if (powerUpType == PowerUpType.FreezeTimer)
            {
                StartOrResetFreeze();
            }
        }

        private void StartOrResetFreeze()
        {
            if (_isFrozen)
            {
                _timer.Start(_config.DurationSeconds);
                _isFadingOut = false;
                KillPresentation();

                if (_freezeVolume != null)
                {
                    _freezeVolume.weight = 1f;
                }

                if (_frostOverlayPresenter != null)
                {
                    _frostOverlayPresenter.SetAlpha(_config.FrostOverlayPeakAlpha);
                }

                return;
            }

            _isFrozen = true;
            _freezeStartedAt = Time.time;
            _timer.Start(_config.DurationSeconds);
            _isFadingOut = false;

            PauseFreezableTweens();
            OnFrozenChanged?.Invoke(true);

            StartFadeInPresentation();

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_FREEZE_START);
            }

            if (_freezeVfxPlayer != null)
            {
                _freezeVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        private void EndFreeze(bool notifyPresentation)
        {
            if (!_isFrozen)
            {
                return;
            }

            _isFrozen = false;
            _accumulatedFrozenSeconds += (Time.time - _freezeStartedAt);
            _isFadingOut = false;
            _timer.Stop();

            OnFrozenChanged?.Invoke(false);
            ResumeFreezableTweens();

            KillPresentation();

            if (notifyPresentation && _audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_FREEZE_END);
            }
        }

        private void PauseFreezableTweens()
        {
            _pausedTweens.Clear();
            _tweenScratch.Clear();
            DOTween.PlayingTweens(_tweenScratch);

            for (int i = 0; i < _tweenScratch.Count; i++)
            {
                Tween tween = _tweenScratch[i];
                if (tween != null && tween.IsActive() && tween.id is int id && id == TimeFreezeConstants.FREEZABLE_TWEEN_ID)
                {
                    tween.Pause();
                    _pausedTweens.Add(tween);
                }
            }

            _tweenScratch.Clear();
        }

        private void ResumeFreezableTweens()
        {
            for (int i = 0; i < _pausedTweens.Count; i++)
            {
                Tween tween = _pausedTweens[i];
                if (tween != null && tween.IsActive() && tween.id is int id && id == TimeFreezeConstants.FREEZABLE_TWEEN_ID)
                {
                    tween.Play();
                }
            }

            _pausedTweens.Clear();
        }

        private void StartFadeInPresentation()
        {
            KillPresentation();
            _presentationSequence = DOTween.Sequence();

            if (_freezeVolume != null)
            {
                _presentationSequence.Append(DOTween.To(
                    () => _freezeVolume.weight,
                    weight => _freezeVolume.weight = weight,
                    1f,
                    _config.FadeInDuration).SetEase(Ease.OutQuad));
            }

            if (_frostOverlayPresenter != null)
            {
                Sequence overlaySequence = DOTween.Sequence();
                overlaySequence.Append(DOTween.To(
                    () => _frostOverlayPresenter.CanvasGroup.alpha,
                    alpha => _frostOverlayPresenter.SetAlpha(alpha),
                    _config.FrostOverlayPeakAlpha,
                    _config.FadeInDuration).SetEase(Ease.OutQuad));

                _presentationSequence.Join(overlaySequence);
            }
        }

        private void StartFadeOutPresentation()
        {
            KillPresentation();
            _presentationSequence = DOTween.Sequence();

            if (_freezeVolume != null)
            {
                _presentationSequence.Append(DOTween.To(
                    () => _freezeVolume.weight,
                    weight => _freezeVolume.weight = weight,
                    0f,
                    _config.FadeOutDuration).SetEase(Ease.InQuad));
            }

            if (_frostOverlayPresenter != null)
            {
                Sequence overlaySequence = DOTween.Sequence();
                overlaySequence.Append(DOTween.To(
                    () => _frostOverlayPresenter.CanvasGroup.alpha,
                    alpha => _frostOverlayPresenter.SetAlpha(alpha),
                    0f,
                    _config.FadeOutDuration).SetEase(Ease.InQuad));

                _presentationSequence.Join(overlaySequence);
            }
        }

        private void KillPresentation()
        {
            if (_presentationSequence != null && _presentationSequence.IsActive())
            {
                _presentationSequence.Kill();
            }

            _presentationSequence = null;

            if (!_isFrozen)
            {
                if (_freezeVolume != null)
                {
                    _freezeVolume.weight = 0f;
                }

                if (_frostOverlayPresenter != null)
                {
                    _frostOverlayPresenter.ResetOverlay();
                }
            }
        }
    }
}
