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

namespace Assets.Scripts.PowerUps.Armor
{
    public interface IArmorService
    {
        int CurrentLevel { get; }
        float CurrentArmorHealth { get; }
        bool HasArmor { get; }
        float AbsorbDamage(float incomingDamage);
        void ClearArmorImmediate();
    }

    public class ArmorService : MonoBehaviour, IArmorService
    {
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;
        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField, Tooltip("Authored armor configuration.")]
        private ArmorConfigSO _config;

        [SerializeField, Tooltip("View component managing player armor plate mini-bars.")]
        private ArmorPlatesView _armorPlatesView;

        [SerializeField, Tooltip("Optional VFX played when armor absorbs damage.")]
        private VFXPlayer _damageVfxPlayer;

        [SerializeField, Tooltip("Optional audio player for armor sound effects.")]
        private AudioClipPlayer _audioClipPlayer;

        private readonly float[] _currentPlateHealth = new float[ArmorConstants.MAX_ARMOR_LEVEL];
        private int _currentLevel;

        public int CurrentLevel => _currentLevel;
        public bool HasArmor => _currentLevel > 0;

        public float CurrentArmorHealth
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _currentLevel; i++)
                {
                    total += _currentPlateHealth[i];
                }

                return total;
            }
        }

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException("ArmorService requires an authored ArmorConfigSO.");
            }

            if (_armorPlatesView == null)
            {
                throw new InvalidOperationException("ArmorService requires an authored ArmorPlatesView reference.");
            }

            _config.ValidateConfiguration();
        }

        private void Start()
        {
            _armorPlatesView.Initialize(_config);
            _armorPlatesView.UpdatePlates(_currentLevel, _currentPlateHealth, _config);

            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected += HandlePowerUpCollected;
            }

            if (_playerManager != null && _playerManager.Health != null)
            {
                _playerManager.Health.OnNoHealth += HandlePlayerNoHealth;
            }
        }

        private void OnDisable()
        {
            ClearArmorImmediate();
        }

        private void OnDestroy()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected -= HandlePowerUpCollected;
            }

            if (_playerManager != null && _playerManager.Health != null)
            {
                _playerManager.Health.OnNoHealth -= HandlePlayerNoHealth;
            }

            ClearArmorImmediate();
        }

        public float AbsorbDamage(float incomingDamage)
        {
            if (incomingDamage <= 0f || _currentLevel <= 0)
            {
                return incomingDamage;
            }

            float remainingDamage = incomingDamage;
            bool plateBroken = false;

            for (int index = _currentLevel - 1; index >= 0; index--)
            {
                if (remainingDamage <= 0f)
                {
                    break;
                }

                float currentHp = _currentPlateHealth[index];
                if (currentHp > remainingDamage)
                {
                    _currentPlateHealth[index] -= remainingDamage;
                    remainingDamage = 0f;
                }
                else
                {
                    remainingDamage -= currentHp;
                    _currentPlateHealth[index] = 0f;
                    _currentLevel = index;
                    plateBroken = true;
                }
            }

            if (_armorPlatesView != null)
            {
                _armorPlatesView.UpdatePlates(_currentLevel, _currentPlateHealth, _config);
                _armorPlatesView.PlayDamageShake();
            }

            if (_damageVfxPlayer != null)
            {
                _damageVfxPlayer.Play(new VFXPlayConfig());
            }

            if (_audioClipPlayer != null)
            {
                string sfxKey = plateBroken ? PowerUpConstants.SFX_ARMOR_BREAK : PowerUpConstants.SFX_ARMOR_HIT;
                _audioClipPlayer.PlayOneShot(sfxKey);
            }

            return remainingDamage;
        }

        public void ClearArmorImmediate()
        {
            _currentLevel = 0;
            for (int i = 0; i < _currentPlateHealth.Length; i++)
            {
                _currentPlateHealth[i] = 0f;
            }

            if (_armorPlatesView != null && _config != null)
            {
                _armorPlatesView.UpdatePlates(0, _currentPlateHealth, _config);
            }
        }

        private void HandlePowerUpCollected(PowerUpType type)
        {
            if (type != PowerUpType.Armor)
            {
                return;
            }

            if (_playerManager != null && _playerManager.Health != null && !_playerManager.Health.IsAlive())
            {
                return;
            }

            ApplyArmorPickup();
        }

        private void ApplyArmorPickup()
        {
            _currentLevel = Mathf.Min(_currentLevel + 1, _config.MaxLevel);

            for (int i = 0; i < _config.MaxLevel; i++)
            {
                if (i < _currentLevel)
                {
                    _currentPlateHealth[i] = _config.GetPlateCapacity(i);
                }
                else
                {
                    _currentPlateHealth[i] = 0f;
                }
            }

            if (_armorPlatesView != null)
            {
                _armorPlatesView.UpdatePlates(_currentLevel, _currentPlateHealth, _config);
            }

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(PowerUpConstants.SFX_PICKUP_ARMOR);
            }
        }

        private void HandlePlayerNoHealth(object sender, EventArgs e)
        {
            ClearArmorImmediate();
        }
    }
}
