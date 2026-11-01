using System;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Constants;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Shield
{
    public interface IBubbleShield
    {
        bool IsActive { get; }
        void NotifyDamageAbsorbed();
    }

    public class BubbleShieldService : MonoBehaviour, IBubbleShield
    {
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;
        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField] private BubbleShieldConfigSO _config;
        [SerializeField] private BubbleShieldVisualController _visualController;
        [SerializeField] private HealthBar _playerHealthBar;
        [SerializeField] private VFXPlayer _absorbVfxPlayer;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;

        private readonly PowerUpTimer _timer = new PowerUpTimer();

        public bool IsActive => _timer.IsRunning;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException("BubbleShieldService requires an authored BubbleShieldConfigSO.");
            }

            if (_visualController == null)
            {
                throw new InvalidOperationException("BubbleShieldService requires an authored BubbleShieldVisualController.");
            }

            if (_playerHealthBar == null)
            {
                throw new InvalidOperationException("BubbleShieldService requires an authored player HealthBar reference.");
            }

            _config.ValidateConfiguration();
            _visualController.SetColor(_config.ShieldColor);
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
            if (!_timer.IsRunning)
            {
                return;
            }

            if (_playerManager != null && _playerManager.Health != null && !_playerManager.Health.IsAlive())
            {
                DeactivateImmediately();
                return;
            }

            if (_timer.Tick(Time.deltaTime))
            {
                _visualController.Hide(_config.DisappearDuration);
                _playerHealthBar.ClearColorOverride();

                if (_audioClipPlayer != null)
                {
                    _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_SHIELD_EXPIRE);
                }

                return;
            }

            UpdateShieldPresentation();
        }

        private void OnDisable()
        {
            DeactivateImmediately();
        }

        private void OnDestroy()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected -= HandlePowerUpCollected;
            }

            DeactivateImmediately();
        }

        public void NotifyDamageAbsorbed()
        {
            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_SHIELD_ABSORB);
            }

            if (_absorbVfxPlayer != null)
            {
                _absorbVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        private void HandlePowerUpCollected(PowerUpType powerUpType)
        {
            if (powerUpType == PowerUpType.BubbleShield)
            {
                StartOrResetShield();
            }
        }

        private void StartOrResetShield()
        {
            if (_timer.IsRunning)
            {
                _timer.Start(_config.DurationSeconds);
                _visualController.RestoreSteadyState(_config.SteadyAlpha);
                _playerHealthBar.SetColorOverride(_config.ShieldColor, 1f);
                return;
            }

            _timer.Start(_config.DurationSeconds);
            _visualController.Show(_config.AppearDuration, _config.SteadyAlpha);
            _playerHealthBar.SetColorOverride(_config.ShieldColor, 1f);

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_SHIELD_START);
            }
        }

        private void UpdateShieldPresentation()
        {
            if (_timer.IsInWindow(_config.WarningWindowSeconds))
            {
                float cycle = Mathf.Repeat(_timer.Remaining, _config.BlinkPeriodSeconds) / _config.BlinkPeriodSeconds;
                float blink01 = 0.5f * (1f + Mathf.Cos(cycle * Mathf.PI * 2f));
                float alpha = Mathf.Lerp(_config.WarningMinAlpha, _config.SteadyAlpha, blink01);

                _visualController.SetAlpha(alpha);
                _playerHealthBar.SetColorOverride(_config.ShieldColor, blink01);
            }
            else
            {
                _visualController.SetAlpha(_config.SteadyAlpha);
                _playerHealthBar.SetColorOverride(_config.ShieldColor, 1f);
            }
        }

        private void DeactivateImmediately()
        {
            _timer.Stop();

            if (_visualController != null)
            {
                _visualController.HideImmediate();
            }

            if (_playerHealthBar != null)
            {
                _playerHealthBar.ClearColorOverride();
            }
        }
    }
}
