using System;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Skills.PlayerSkills.Minigun.Constants;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Minigun
{
    public class MinigunSkill : UpgradeableSkill<MinigunSkillUpgradeableConfigSO>
    {
        [Inject] private readonly IPlayerManager _playerManager = null;

        [field: SerializeField] public override SkillInfoSO SkillInfo { get; protected set; }
        [field: SerializeField] protected override MinigunSkillUpgradeableConfigSO _config { get; set; }
        [SerializeField] private MinigunTurret[] _turrets;

        private IItemsWithScriptableConfigsActivator<MinigunTurret, MinigunSkillUpgradeableConfigSO> _turretsActivator;
        private IHealth _playerHealth;
        private bool _isInitialized;
        private bool _isRunning;
        private bool _isSubscribed;
        private bool _isInitialShotPending;
        private float _elapsed;

        private void OnEnable()
        {
            if (_isInitialized && _playerHealth.IsAlive()) { Attach(); }
        }

        private void LateUpdate()
        {
            float delta = Time.deltaTime;
            if (!_isRunning || !_playerHealth.IsAlive() || delta <= 0f) { return; }
            foreach (MinigunTurret turret in _turrets)
            {
                if (turret.IsInitialized() && turret.isActiveAndEnabled)
                {
                    turret.UpdatePresentation(Time.time, delta);
                }
            }
            float delay = _config.ShotDelay.Value;
            if (!MinigunShotResolver.IsFinite(delay) || delay <= 0f)
            {
                throw new InvalidOperationException("Minigun shot delay must be finite and positive.");
            }
            _elapsed += delta;
            if (!_isInitialShotPending && _elapsed < delay) { return; }
            _elapsed = 0f;
            _isInitialShotPending = false;
            foreach (MinigunTurret turret in _turrets)
            {
                if (!_isRunning || !isActiveAndEnabled || !_playerHealth.IsAlive()) { break; }
                if (turret.IsInitialized() && turret.isActiveAndEnabled) { turret.Shoot(); }
            }
        }

        private void OnDisable()
        {
            StopAttack();
        }

        private void OnDestroy()
        {
            StopAttack();
        }

        public override bool IsInitialized()
        {
            return _isInitialized;
        }

        public override void Initialize()
        {
            if (_isInitialized) { return; }
            if (_config == null || _config.TurretConfig == null || _turrets == null || _turrets.Length == 0)
            {
                throw new InvalidOperationException("Minigun requires configuration and authored turret slots.");
            }
            if (_config.NumberOfTurrets.HasUnlimitedMaxValue || _config.NumberOfTurrets.MinMaxRange.Max > _turrets.Length
                || _config.NumberOfTurrets.Value < 1 || _config.NumberOfTurrets.Value > _turrets.Length
                || _config.Piercing.HasUnlimitedMaxValue || _config.Piercing.MinMaxRange.Max >= MinigunConstants.MAX_TARGETS)
            {
                throw new InvalidOperationException("Minigun configured count exceeds authored or resolver capacity.");
            }
            foreach (MinigunTurret turret in _turrets)
            {
                if (turret == null) { throw new InvalidOperationException("Minigun has an empty turret slot."); }
                turret.ValidateReferences();
            }
            _playerHealth = _playerManager.Health;
            _turretsActivator = new ItemsWithScriptableConfigsActivator<MinigunTurret, MinigunSkillUpgradeableConfigSO>(_turrets);
            _isInitialized = true;
            _isInitialShotPending = true;
            base.Initialize();
            InitializeTurretsToConfiguredCount();
            if (_playerHealth.IsAlive()) { Attach(); }
        }

        private void Attach()
        {
            if (_isSubscribed) { return; }
            _config.NumberOfTurrets.OnUpgrade += OnNumberOfTurretsUpgraded;
            _playerHealth.OnNoHealth += OnPlayerDied;
            _isSubscribed = true;
            _isRunning = true;
            InitializeTurretsToConfiguredCount();
        }

        private void StopAttack()
        {
            _isRunning = false;
            _elapsed = 0f;
            _isInitialShotPending = false;
            if (_isSubscribed)
            {
                _config.NumberOfTurrets.OnUpgrade -= OnNumberOfTurretsUpgraded;
                _playerHealth.OnNoHealth -= OnPlayerDied;
                _isSubscribed = false;
            }
            if (_isInitialized)
            {
                foreach (MinigunTurret turret in _turrets) { turret.HideAttackPresentation(); }
            }
        }

        private void InitializeTurretsToConfiguredCount()
        {
            _turretsActivator.InitializeUntilCount(_config, _config.NumberOfTurrets.Value);
        }

        private void OnNumberOfTurretsUpgraded(object sender, EventArgs e)
        {
            InitializeTurretsToConfiguredCount();
        }

        private void OnPlayerDied(object sender, EventArgs e)
        {
            StopAttack();
        }
    }
}
