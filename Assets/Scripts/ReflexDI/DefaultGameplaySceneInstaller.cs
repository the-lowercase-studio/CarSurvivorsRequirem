using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Player;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.UI.Death;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.UI.Level;
using Assets.Scripts.UI.Skills;
using Assets.Scripts.Waves;
using Reflex.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.ReflexDI
{
    public class DefaultGameplaySceneInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private PlayerManager _playerManager;
        [SerializeField] private PlayerDeathPresenter _playerDeathPresenter;
        [SerializeField] private PlayerLevelPresenter _playerLevelPresenter;
        [SerializeField] private SkillsVisualPresenter _skillsVisualPresenter;
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private EnemiesSpawner _enemiesSpawner;
        [SerializeField] private TimerPresenter _timerPresenter;
        [SerializeField] private ExpParticleSpawner _expParticleSpawner;
        [SerializeField] private CollectibleDropNotifier _collectibleDropNotifier;
        [SerializeField] private DropAnimationConfiguration _dropAnimationConfiguration;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private SwarmNotificationPresenter _swarmNotificationPresenter;
        [SerializeField] private SwarmSpawner _swarmSpawner;
        [SerializeField] private BossHUDPresenter _bossHUDPresenter;
        [SerializeField] private BossManager _bossManager;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Volume _postProcessVolume;

        public Camera MainCamera => _mainCamera;

        public void InstallBindings(ContainerBuilder builder)
        {
            //Camera
            builder.AddSingleton(_mainCamera);

            //Player
            builder.AddSingleton(_playerManager, typeof(IPlayerManager));

            //UI Presenters
            builder.AddSingleton(_playerDeathPresenter, typeof(IPlayerDeathPresenter));
            builder.AddSingleton(_playerLevelPresenter, typeof(IPlayerLevelPresenter));
            builder.AddSingleton(_skillsVisualPresenter, typeof(ISkillsVisualPresenter));
            builder.AddSingleton(_timerPresenter, typeof(ITimerPresenter));
            if (_bossHUDPresenter != null)
            {
                builder.AddSingleton(_bossHUDPresenter, typeof(IBossHUDPresenter));
            }

            //Grid System
            builder.AddSingleton(_gridManager, typeof(IGridManager));

            //Skill Upgrade Flow
            builder.AddSingleton(typeof(SkillUpgradeFlow), typeof(ISkillUpgradeFlow));

            //Spawners
            builder.AddSingleton(_enemiesSpawner, typeof(IOnRandomGridPosSpawner<EnemiesSpawner>));
            builder.AddSingleton(_enemiesSpawner, typeof(ISwarmEnemySpawner));
            builder.AddSingleton(_enemiesSpawner, typeof(IEnemySpawnDifficultyController));
            builder.AddSingleton(_collectibleDropNotifier, typeof(ICollectibleDropNotifier));
            builder.AddSingleton(_dropAnimationConfiguration);
            builder.AddSingleton(_expParticleSpawner, typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>));

            //Waves & Swarms
            builder.AddSingleton(_waveManager, typeof(IWaveFreezer));
            if (_swarmSpawner != null)
            {
                builder.AddSingleton(_swarmSpawner, typeof(ISwarmFreezer));
            }

            //Boss Management
            if (_bossManager != null)
            {
                builder.AddSingleton(_bossManager, typeof(IBossManager));
            }

            //Swarm UI
            builder.AddSingleton(_swarmNotificationPresenter, typeof(ISwarmNotificationPresenter));

            //Post Processing
            builder.AddSingleton(_postProcessVolume);
        }
    }
}
