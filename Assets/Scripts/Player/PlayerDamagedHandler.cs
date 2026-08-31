using Assets.Scripts.Player.Constants;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Player
{
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerDamagedHandler : MonoBehaviour, IDamageable
    {
        [Inject] private readonly IPlayerManager _playerManager = null;

        [Header("Needed references")]
        [SerializeField] private VFXPlayer _damageVfxPlayer;
        [SerializeField] private GameObject _carVisual;

        [Header("Shake after damage settings")]
        [SerializeField] private float _duration = 0.2f;
        [SerializeField] private float _strength = 0.1f;

        private Tween _shakeTween;
        private Vector3 _originalScale;
        private bool _hasOriginalScale;

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

        public void TakeDamage(float damage)
        {
            _playerManager.Health.DecreaseHealth(damage);
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
            _playerManager.Health.DecreaseHealth(_playerManager.Health.MaxHealth);
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
