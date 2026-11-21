using System;
using System.Collections.Generic;
using Assets.ScriptableObjects.Enemies.Bosses;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Golem.Config;
using Assets.Scripts.GameFlow.Constants;
using Assets.Scripts.Spawners.Enemies;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.GameFlow.Config
{
    [CreateAssetMenu(fileName = "GameLoopProgressionConfig", menuName = "ScriptableObjects/GameFlow/GameLoopProgressionConfig")]
    public class GameLoopProgressionConfigSO : ScriptableObject
    {
        [Tooltip("Ordered list of gameplay tiers for a run (index 0 = Tier 1, index 1 = Tier 2, etc.).")]
        [SerializeField] private List<LoopTierConfig> _tiers = new();

        [Tooltip("Duration in seconds of the DOTween vortex pull/spin/shrink animation on entering NextStagePortal.")]
        [SerializeField, Min(0.1f)] private float _portalVortexDuration = ProgressionConstants.DEFAULT_PORTAL_VORTEX_DURATION;

        [Tooltip("Full 360-degree spins performed by the player car during the portal vortex sequence.")]
        [SerializeField, Min(0f)] private float _portalVortexSpins = ProgressionConstants.DEFAULT_PORTAL_VORTEX_SPINS;

        public IReadOnlyList<LoopTierConfig> Tiers => _tiers;
        public float PortalVortexDuration => _portalVortexDuration;
        public float PortalVortexSpins => _portalVortexSpins;

        public LoopTierConfig GetTier(int tierIndex)
        {
            ValidateConfiguration();

            int clampedIndex = Mathf.Clamp(tierIndex, 0, _tiers.Count - 1);
            return _tiers[clampedIndex];
        }

        public bool TryGetTier(int tierIndex, out LoopTierConfig tierConfig)
        {
            if (_tiers != null && tierIndex >= 0 && tierIndex < _tiers.Count && _tiers[tierIndex] != null)
            {
                tierConfig = _tiers[tierIndex];
                return true;
            }

            tierConfig = null;
            return false;
        }

        public void ValidateConfiguration()
        {
            if (_tiers == null || _tiers.Count == 0)
            {
                throw new InvalidOperationException($"[{nameof(GameLoopProgressionConfigSO)}] '{name}' must define at least one tier.");
            }

            for (int i = 0; i < _tiers.Count; i++)
            {
                LoopTierConfig tier = _tiers[i];
                if (tier == null)
                {
                    throw new InvalidOperationException($"[{nameof(GameLoopProgressionConfigSO)}] '{name}' has a null tier entry at index {i}.");
                }

                if (tier.EnemySpawning == null)
                {
                    throw new InvalidOperationException($"[{nameof(GameLoopProgressionConfigSO)}] '{name}' tier index {i} has null EnemySpawning config.");
                }

                if (tier.BossConfig == null)
                {
                    throw new InvalidOperationException($"[{nameof(GameLoopProgressionConfigSO)}] '{name}' tier index {i} has null BossConfig.");
                }

                if (tier.VisualTheme == null)
                {
                    throw new InvalidOperationException($"[{nameof(GameLoopProgressionConfigSO)}] '{name}' tier index {i} has null VisualTheme.");
                }
            }
        }
    }

    [Serializable]
    public class LoopTierConfig
    {
        [Tooltip("Unique identifier for this tier (e.g. Tier1, Tier2).")]
        [SerializeField] private string _tierId;

        [Tooltip("Player-facing name for this tier.")]
        [SerializeField] private string _displayName;

        [Tooltip("Format string displayed on GolemStatue while towers remain (e.g. 'Towers destroyed ({0}/{1})').")]
        [SerializeField] private string _statueProgressFormat = ProgressionConstants.DEFAULT_STATUE_PROGRESS_FORMAT;

        [Tooltip("Text displayed on GolemStatue once all towers are defeated.")]
        [SerializeField] private string _statueChallengePrompt = ProgressionConstants.DEFAULT_STATUE_CHALLENGE_PROMPT;

        [Tooltip("Optional override to load a different scene for this tier instead of reloading the current gameplay scene.")]
        [SerializeField] private bool _overrideScene;

        [SerializeField] private GameScene _sceneOverride = GameScene.RuinedBloodCity;
        [SerializeField] private TierEnemySpawningConfig _enemySpawning = new();
        [SerializeField] private TierBossConfig _bossConfig = new();
        [SerializeField] private TierVisualThemeConfig _visualTheme = new();

        public string TierId => _tierId;
        public string DisplayName => _displayName;
        public string StatueProgressFormat => _statueProgressFormat;
        public string StatueChallengePrompt => _statueChallengePrompt;
        public bool OverrideScene => _overrideScene;
        public GameScene SceneOverride => _sceneOverride;
        public TierEnemySpawningConfig EnemySpawning => _enemySpawning;
        public TierBossConfig BossConfig => _bossConfig;
        public TierVisualThemeConfig VisualTheme => _visualTheme;
    }

    [Serializable]
    public class TierEnemySpawningConfig
    {
        [Tooltip("Enemy pool entries for this tier. If empty on Tier 1, scene EnemiesSpawner defaults are used.")]
        [SerializeField] private List<EnemySpawnInfo> _enemySpawnInfos = new();

        [Tooltip("Multiplier range reducing active enemy spawn chances as thresholds are reached.")]
        [SerializeField] private FloatValueRange _spawnChanceDecreaseFactor = new(1.5f, 2.5f);

        [SerializeField, Min(0.1f)] private float _waveInitialCountMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float _waveDelayMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float _waveGrowthMultiplier = 1f;

        [Tooltip("Hard upper cap on enemies spawned per wave in this tier to preserve frame budgets.")]
        [SerializeField, Min(1)] private ushort _maxEnemiesInWaveCap = ProgressionConstants.DEFAULT_MAX_ENEMIES_IN_WAVE_CAP;

        [SerializeField, Min(0.1f)] private float _swarmIntervalMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float _swarmSizeMultiplier = 1f;

        public IReadOnlyList<EnemySpawnInfo> EnemySpawnInfos => _enemySpawnInfos;
        public FloatValueRange SpawnChanceDecreaseFactor => _spawnChanceDecreaseFactor;
        public float WaveInitialCountMultiplier => _waveInitialCountMultiplier;
        public float WaveDelayMultiplier => _waveDelayMultiplier;
        public float WaveGrowthMultiplier => _waveGrowthMultiplier;
        public ushort MaxEnemiesInWaveCap => _maxEnemiesInWaveCap;
        public float SwarmIntervalMultiplier => _swarmIntervalMultiplier;
        public float SwarmSizeMultiplier => _swarmSizeMultiplier;
    }

    [Serializable]
    public class TierBossConfig
    {
        [Tooltip("Optional Golem prefab override for this tier. Uses BossEncounterService default if unassigned.")]
        [SerializeField] private GolemBoss _golemPrefabOverride;

        [Tooltip("Optional Golem config override for this tier.")]
        [SerializeField] private GolemBossConfigSO _golemConfigOverride;

        [Tooltip("Optional HUD display name override for the tier's Golem boss.")]
        [SerializeField] private string _golemDisplayNameOverride;

        [Tooltip("Optional Mortar Tower config override for this tier.")]
        [SerializeField] private MortarTowerConfigSO _mortarTowerConfigOverride;

        [Tooltip("Optional Railgun Tower config override for this tier.")]
        [SerializeField] private RailgunTowerConfigSO _railgunTowerConfigOverride;

        [Tooltip("When enabled, applies baseline color/emission tinting to bosses before their 40% HP Enrage phase.")]
        [SerializeField] private bool _applyBaselineBossTint;

        [SerializeField] private Color _baselineBossColor = Color.white;
        [SerializeField, ColorUsage(true, true)] private Color _baselineBossEmissionColor = Color.black;

        public GolemBoss GolemPrefabOverride => _golemPrefabOverride;
        public GolemBossConfigSO GolemConfigOverride => _golemConfigOverride;
        public string GolemDisplayNameOverride => _golemDisplayNameOverride;
        public MortarTowerConfigSO MortarTowerConfigOverride => _mortarTowerConfigOverride;
        public RailgunTowerConfigSO RailgunTowerConfigOverride => _railgunTowerConfigOverride;
        public bool ApplyBaselineBossTint => _applyBaselineBossTint;
        public Color BaselineBossColor => _baselineBossColor;
        public Color BaselineBossEmissionColor => _baselineBossEmissionColor;
    }

    [Serializable]
    public class TierVisualThemeConfig
    {
        [Tooltip("Optional post-processing VolumeProfile applied on scene startup for this tier.")]
        [SerializeField] private VolumeProfile _postProcessProfile;

        [Tooltip("Optional skybox material applied to RenderSettings.skybox for this tier.")]
        [SerializeField] private Material _skyboxMaterial;

        [Tooltip("When enabled, overrides scene Directional Light color, intensity, and rotation.")]
        [SerializeField] private bool _overrideDirectionalLight;

        [SerializeField] private Color _directionalLightColor = Color.white;
        [SerializeField, Min(0f)] private float _directionalLightIntensity = 1f;
        [SerializeField] private Vector3 _directionalLightEulerAngles;

        [Tooltip("Optional shared material applied to large water renderers.")]
        [SerializeField] private Material _waterLargeMaterial;

        [Tooltip("Optional shared material applied to small water renderers.")]
        [SerializeField] private Material _waterSmallMaterial;

        public VolumeProfile PostProcessProfile => _postProcessProfile;
        public Material SkyboxMaterial => _skyboxMaterial;
        public bool OverrideDirectionalLight => _overrideDirectionalLight;
        public Color DirectionalLightColor => _directionalLightColor;
        public float DirectionalLightIntensity => _directionalLightIntensity;
        public Vector3 DirectionalLightEulerAngles => _directionalLightEulerAngles;
        public Material WaterLargeMaterial => _waterLargeMaterial;
        public Material WaterSmallMaterial => _waterSmallMaterial;
    }
}
