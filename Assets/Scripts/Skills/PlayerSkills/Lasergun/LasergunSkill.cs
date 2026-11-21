using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
using System;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Lasergun
{
    public class LasergunSkill : UpgradeableSkill<LasergunSkillSO>
    {
        [field: SerializeField] public override SkillInfoSO SkillInfo { get; protected set; }
        [field: SerializeField] protected override LasergunSkillSO _config { get; set; }
        [SerializeField] private LasergunTurret[] _turrets;

        private IItemsWithScriptableConfigsActivator<LasergunTurret, TurretConfigSO> _turretsActivator;
        private bool _isInitialized;
        private bool _isInitialShotPending;
        private float _elapsed;

        private void Update()
        {
            AdvanceFireTimer(Time.deltaTime);
        }

        private void OnDisable()
        {
            _elapsed = 0f;
            _isInitialShotPending = false;
        }

        public override bool IsInitialized()
        {
            return _isInitialized && base.IsInitialized();
        }

        public override void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            if (_config == null || _config.DelayBetweenShoots == null || _config.TurretConfig == null || _turrets == null || _turrets.Length == 0)
            {
                throw new InvalidOperationException("Lasergun skill requires config, shoot delay, turret config, and turrets.");
            }

            _isInitialized = true;
            _isInitialShotPending = true;
            _elapsed = 0f;

            base.Initialize();

            _turretsActivator =
                new ItemsWithScriptableConfigsActivator<LasergunTurret, TurretConfigSO>(_turrets);

            InitializeTurretsToConfiguredCount();
            ApplyNumberOfTargetsToInitializedTurrets();

            if (_config.NumberOfTurrets is not null)
            {
                _config.NumberOfTurrets.OnUpgrade -= OnNumberOfTurretsUpgraded;
                _config.NumberOfTurrets.OnUpgrade += OnNumberOfTurretsUpgraded;
            }

            if (_config.NumberOfTargets is not null)
            {
                _config.NumberOfTargets.OnUpgrade -= OnNumberOfTargetsUpgraded;
                _config.NumberOfTargets.OnUpgrade += OnNumberOfTargetsUpgraded;
            }
        }

        public bool AdvanceFireTimer(float deltaTime)
        {
            if (!_isInitialized || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return false;
            }

            float delay = _config.DelayBetweenShoots.Value;
            if (float.IsNaN(delay) || float.IsInfinity(delay) || delay <= 0f)
            {
                throw new InvalidOperationException("Lasergun shoot delay must be finite and positive.");
            }

            _elapsed += deltaTime;
            if (!_isInitialShotPending && _elapsed < delay)
            {
                return false;
            }

            _elapsed = 0f;
            _isInitialShotPending = false;
            ShootFromTurrets();
            return true;
        }

        private void OnDestroy()
        {
            if (_config is null)
            {
                return;
            }

            if (_config.NumberOfTurrets is not null)
            {
                _config.NumberOfTurrets.OnUpgrade -= OnNumberOfTurretsUpgraded;
            }

            if (_config.NumberOfTargets is not null)
            {
                _config.NumberOfTargets.OnUpgrade -= OnNumberOfTargetsUpgraded;
            }
        }

        private void ShootFromTurrets()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            foreach (LasergunTurret turret in _turretsActivator.GetInitialized())
            {
                turret.Shoot(_config.DelayBetweenShoots.MinMaxRange.Min / _config.DelayBetweenShoots.Value);
            }
        }

        private void InitializeTurretsToConfiguredCount()
        {
            _turretsActivator.InitializeUntilCount(_config.TurretConfig, _config.NumberOfTurrets.Value);
            ApplyNumberOfTargetsToInitializedTurrets();
        }

        private void ApplyNumberOfTargetsToInitializedTurrets()
        {
            foreach (LasergunTurret turret in _turretsActivator.GetInitialized())
            {
                turret.SetNumberOfTargets(_config.NumberOfTargets.Value);
            }
        }

        private void OnNumberOfTurretsUpgraded(object sender, EventArgs e)
        {
            InitializeTurretsToConfiguredCount();
        }

        private void OnNumberOfTargetsUpgraded(object sender, EventArgs e)
        {
            ApplyNumberOfTargetsToInitializedTurrets();
        }
    }
}

