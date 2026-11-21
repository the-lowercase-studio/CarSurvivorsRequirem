using System;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Skills.Constants;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.LandmineTrap
{
    public class LandmineSkill : UpgradeableSkill<LandmineSkillUpgradeableConfigSO>
    {
        [field: SerializeField] public override SkillInfoSO SkillInfo { get; protected set; }
        [field: SerializeField] protected override LandmineSkillUpgradeableConfigSO _config { get; set; }

        [SerializeField] private Landmine _landminePrefab;
        [SerializeField] private Transform _landminesParent;

        private bool _isInitialized;
        private bool _isInitialSpawnPending;
        private float _elapsed;

        private void Update()
        {
            AdvanceSpawnTimer(Time.deltaTime);
        }

        private void OnDisable()
        {
            _elapsed = 0f;
            _isInitialSpawnPending = false;
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

            if (_config == null || _config.SpawnCooldown == null || _landminePrefab == null)
            {
                throw new InvalidOperationException("Landmine skill requires config, spawn cooldown, and prefab.");
            }

            _isInitialized = true;
            _isInitialSpawnPending = true;
            _elapsed = 0f;

            base.Initialize();
        }

        public bool AdvanceSpawnTimer(float deltaTime)
        {
            if (!_isInitialized || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return false;
            }

            float cooldown = _config.SpawnCooldown.Value;
            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown <= 0f)
            {
                throw new InvalidOperationException("Landmine spawn cooldown must be finite and positive.");
            }

            _elapsed += deltaTime;
            if (!_isInitialSpawnPending && _elapsed < cooldown)
            {
                return false;
            }

            _elapsed = 0f;
            _isInitialSpawnPending = false;
            SpawnLandmine();
            return true;
        }

        private void SpawnLandmine()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (Physics.Raycast(transform.position, Vector3.down, SkillConstants.CAN_PLACE_MINE_RAY_DISTANCE, TerrainLayers.Ground))
            {
                Landmine landmine = Instantiate(
                    _landminePrefab,
                    transform.position,
                    Quaternion.identity,
                    _landminesParent
                );

                landmine.Initialize(_config);
            }
        }
    }
}

