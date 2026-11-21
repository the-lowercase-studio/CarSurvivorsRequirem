using Assets.Scripts.GameFlow;
using Assets.Scripts.Player.Constants;
using Assets.Scripts.PowerUps.Armor;
using Assets.Scripts.PowerUps.Shield;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Player
{
    public interface IPlayerDamagedHandler
    {
        void SetPortalTransitionInvulnerable(bool isInvulnerable);
    }

    [RequireComponent(typeof(PlayerManager))]
    public class PlayerDamagedHandler : MonoBehaviour, IDamageable, IPlayerDamagedHandler
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IBubbleShield _bubbleShield = null;
        [Inject] private readonly IArmorService _armorService = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;

        [Header("Needed references")]
        [SerializeField] private VFXPlayer _damageVfxPlayer;
        [SerializeField] private GameObject _carVisual;

        [Header("Shake after damage settings")]
        [SerializeField] private float _duration = 0.2f;
        [SerializeField] private float _strength = 0.1f;

        private Tween _shakeTween;
        private Vector3 _originalScale;
        private bool _hasOriginalScale;
        private bool _isPortalTransitionInvulnerable;

        private void Awake()
        {
            _originalScale = _carVisual.transform.localScale;
            _hasOriginalScale = true;
        }

        private void OnDisable()
        {
            KillShakeTween();
        }

        private void OnDestroy()
        {
            KillShakeTween();
        }

        public void SetPortalTransitionInvulnerable(bool isInvulnerable)
        {
            _isPortalTransitionInvulnerable = isInvulnerable;
        }

        public void TakeDamage(float damage)
        {
            if (_isPortalTransitionInvulnerable)
            {
                return;
            }

            if (_bubbleShield != null && _bubbleShield.IsActive)
            {
                _bubbleShield.NotifyDamageAbsorbed();
                return;
            }

            if (_armorService != null && _armorService.HasArmor)
            {
                damage = _armorService.AbsorbDamage(damage);
            }

            if (float.IsNaN(damage) || damage <= 0f)
            {
                return;
            }

            if (!_playerManager.Health.IsAlive())
            {
                return;
            }

            ApplyHealthDamage(damage);
            _playerManager.AudioClipPlayer.PlayOneShot(PlayerAudioConstants.DAMAGED_AUDIO_KEY);
            if (_damageVfxPlayer != null)
            {
                _damageVfxPlayer.Play(new());
            }

            KillShakeTween();
            _shakeTween = _carVisual.transform.DOShakeScale(_duration, _strength);
        }

        public void TakeFullHpDamage()
        {
            if (_isPortalTransitionInvulnerable)
            {
                return;
            }

            if (_armorService != null)
            {
                _armorService.ClearArmorImmediate();
            }

            _playerManager.Health.DecreaseHealth(_playerManager.Health.MaxHealth);
        }

        private void ApplyHealthDamage(float damage)
        {
            if (_runSessionService == null || !_runSessionService.IsGodModeEnabled)
            {
                _playerManager.Health.DecreaseHealth(damage);
                return;
            }

            float minHealth = Mathf.Min(PlayerDamageConstants.GOD_MODE_MIN_HEALTH, _playerManager.Health.MaxHealth);
            float currentHealth = _playerManager.Health.CurrentHealth;
            if (currentHealth <= minHealth)
            {
                return;
            }

            float maxSafeDamage = currentHealth - minHealth;
            float appliedDamage = damage < maxSafeDamage ? damage : maxSafeDamage;
            if (appliedDamage > 0f && currentHealth > appliedDamage)
            {
                _playerManager.Health.DecreaseHealth(appliedDamage);
            }
        }

        private void KillShakeTween()
        {
            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Kill();
            }

            _shakeTween = null;

            if (_hasOriginalScale)
            {
                _carVisual.transform.localScale = _originalScale;
            }
        }
    }
}
