using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Constants;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Golem.Config;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Interactables;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.ReflexDI;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.UI.HUD;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses
{
    public interface IBossEncounterService
    {
        bool IsAnyBossActive { get; }
        int RequiredTowersCount { get; }
        int DefeatedTowersCount { get; }
        int RemainingTowersCount { get; }
        void RegisterTower(IEncounterArenaController towerArena);
        void UnregisterTower(IEncounterArenaController towerArena);
        void RegisterGolemStatue(IGolemStatue statue);
        void UnregisterGolemStatue(IGolemStatue statue);
        void NotifyEncounterEngaged(IBossHUDSource source, string displayName);
        void NotifyEncounterDisengaged();
        void NotifyTowerDefeated(IEncounterArenaController towerArena);
        bool IsPositionWithinAnyTowerArena(Vector3 worldPosition, float extraBuffer = 0f);
        void SpawnGolemBoss(Vector3? spawnPosition = null);
        int CollectEngagedBosses(List<IBossCombatTarget> buffer);
        event Action<int, int> OnTowerDefeated;
    }

    public class BossEncounterService : MonoBehaviour, IBossEncounterService
    {
        [Inject] private readonly IBossHUDPresenter _bossHUDPresenter = null;
        [Inject] private readonly ISwarmFreezer _swarmFreezer = null;
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;
        [Inject] private readonly Reflex.Core.Container _container = null;

        [Header("Prefabs & Configuration")]
        [Tooltip("Default prefab for the stage-culmination Ancient Golem boss.")]
        [SerializeField] private GolemBoss _golemBossPrefab;
        [Tooltip("Prefab for the stage progression portal spawned upon Golem defeat.")]
        [SerializeField] private GameObject _nextStagePortalPrefab;
        [Tooltip("Default display name shown on the Boss HUD health bar.")]
        [SerializeField] private string _golemDisplayName = BossEncounterConstants.DEFAULT_GOLEM_DISPLAY_NAME;
        [Tooltip("Forward spawn distance ahead of player when no explicit spawn position is provided.")]
        [SerializeField] private float _spawnOffsetDistance = BossEncounterConstants.DEFAULT_SPAWN_OFFSET_DISTANCE;
        [Tooltip("Configured number of towers that must be defeated before GolemStatue unlocks.")]
        [SerializeField] private int _requiredTowersForGolemSpawn = BossEncounterConstants.DEFAULT_REQUIRED_TOWERS;

        private readonly HashSet<IEncounterArenaController> _registeredTowers = new();
        private readonly HashSet<IEncounterArenaController> _defeatedTowers = new();
        private IGolemStatue _registeredStatue;
        private GolemBoss _activeGolemInstance;
        private bool _isEncounterEngaged;

        public bool IsAnyBossActive => _isEncounterEngaged || IsGolemActive;

        public bool IsGolemActive => _activeGolemInstance != null && _activeGolemInstance.Health != null && _activeGolemInstance.Health.IsAlive();

        public int RequiredTowersCount => _registeredTowers.Count > 0 ? Mathf.Min(_requiredTowersForGolemSpawn, _registeredTowers.Count) : _requiredTowersForGolemSpawn;

        public int DefeatedTowersCount => _defeatedTowers.Count;

        public int RemainingTowersCount => Mathf.Max(0, RequiredTowersCount - DefeatedTowersCount);

        public event Action<int, int> OnTowerDefeated;

        private void OnDisable()
        {
            if (_activeGolemInstance != null)
            {
                _activeGolemInstance.OnBossDefeated -= Golem_OnBossDefeated;
            }

            if (_isEncounterEngaged)
            {
                if (_bossHUDPresenter != null)
                {
                    _bossHUDPresenter.Hide();
                }

                if (_swarmFreezer != null)
                {
                    _swarmFreezer.IsSuppressed = false;
                }

                _isEncounterEngaged = false;
            }
        }

        private void OnDestroy()
        {
            if (_activeGolemInstance != null)
            {
                _activeGolemInstance.OnBossDefeated -= Golem_OnBossDefeated;
            }
        }

        public void RegisterTower(IEncounterArenaController towerArena)
        {
            if (towerArena == null)
            {
                return;
            }

            _registeredTowers.Add(towerArena);
        }

        public void UnregisterTower(IEncounterArenaController towerArena)
        {
            if (towerArena == null)
            {
                return;
            }

            if (_defeatedTowers.Contains(towerArena))
            {
                // Retain defeated tower in registered set to keep stage progression count accurate
                return;
            }

            _registeredTowers.Remove(towerArena);
        }

        public void RegisterGolemStatue(IGolemStatue statue)
        {
            _registeredStatue = statue;
        }

        public void UnregisterGolemStatue(IGolemStatue statue)
        {
            if (_registeredStatue == statue)
            {
                _registeredStatue = null;
            }
        }

        public void NotifyEncounterEngaged(IBossHUDSource source, string displayName)
        {
            _isEncounterEngaged = true;

            if (_bossHUDPresenter != null && source != null)
            {
                _bossHUDPresenter.Show(source, displayName);
            }

            UpdateSwarmSuppression();
        }

        public void NotifyEncounterDisengaged()
        {
            _isEncounterEngaged = false;

            if (_bossHUDPresenter != null)
            {
                _bossHUDPresenter.Hide();
            }

            UpdateSwarmSuppression();
        }

        public void NotifyTowerDefeated(IEncounterArenaController towerArena)
        {
            if (towerArena == null || !_defeatedTowers.Add(towerArena))
            {
                return;
            }

            NotifyEncounterDisengaged();

            OnTowerDefeated?.Invoke(DefeatedTowersCount, RequiredTowersCount);
        }

        public bool IsPositionWithinAnyTowerArena(Vector3 worldPosition, float extraBuffer = 0f)
        {
            float clampedBuffer = Mathf.Max(0f, extraBuffer);

            foreach (IEncounterArenaController tower in _registeredTowers)
            {
                if (tower == null || (tower is UnityEngine.Object unityTower && unityTower == null))
                {
                    continue;
                }

                float effectiveRadius = Mathf.Max(0f, tower.ArenaRadius) + clampedBuffer;
                Vector3 center = tower.ArenaCenter;
                float dx = worldPosition.x - center.x;
                float dz = worldPosition.z - center.z;
                if ((dx * dx) + (dz * dz) <= effectiveRadius * effectiveRadius)
                {
                    return true;
                }
            }

            return false;
        }

        public void SpawnGolemBoss(Vector3? spawnPosition = null)
        {
            if (_registeredStatue != null)
            {
                _registeredStatue.DismissForExternalSpawn();
            }

            TierBossConfig tierBossConfig = _runSessionService?.ActiveTierConfig?.BossConfig;
            GolemBoss golemPrefab = tierBossConfig != null && tierBossConfig.GolemPrefabOverride != null
                ? tierBossConfig.GolemPrefabOverride
                : _golemBossPrefab;

            if (IsGolemActive || golemPrefab == null)
            {
                return;
            }

            Vector3 finalSpawnPos;
            if (spawnPosition.HasValue)
            {
                finalSpawnPos = spawnPosition.Value;
            }
            else if (_playerManager != null && _playerManager.GameObject != null)
            {
                Vector3 playerPos = _playerManager.GameObject.transform.position;
                Vector3 playerForward = _playerManager.GameObject.transform.forward;
                playerForward.y = 0f;
                if (playerForward.sqrMagnitude < 0.01f)
                {
                    playerForward = Vector3.forward;
                }

                finalSpawnPos = playerPos + playerForward.normalized * _spawnOffsetDistance;
            }
            else
            {
                finalSpawnPos = transform.position + Vector3.forward * _spawnOffsetDistance;
            }

            _activeGolemInstance = ReflexPrefabInstantiator.InstantiateInjectedObject(
                golemPrefab,
                finalSpawnPos,
                Quaternion.identity,
                null,
                _container);

            GolemBossConfigSO configOverride = tierBossConfig != null ? tierBossConfig.GolemConfigOverride : null;
            _activeGolemInstance.ApplyTierOverride(configOverride, tierBossConfig);
            _activeGolemInstance.OnBossDefeated += Golem_OnBossDefeated;

            string displayName = tierBossConfig != null && !string.IsNullOrEmpty(tierBossConfig.GolemDisplayNameOverride)
                ? tierBossConfig.GolemDisplayNameOverride
                : _golemDisplayName;

            NotifyEncounterEngaged(_activeGolemInstance, displayName);
        }

        public int CollectEngagedBosses(List<IBossCombatTarget> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            if (IsGolemActive && _activeGolemInstance != null)
            {
                buffer.Add(_activeGolemInstance);
            }

            foreach (IEncounterArenaController tower in _registeredTowers)
            {
                if (tower != null && tower.IsEncounterActive && !tower.IsEncounterCompleted)
                {
                    IBossCombatTarget target = tower.BossTarget;
                    if (target != null && target.Health != null && target.Health.IsAlive())
                    {
                        buffer.Add(target);
                    }
                }
            }

            return buffer.Count;
        }

        private void UpdateSwarmSuppression()
        {
            if (_swarmFreezer != null)
            {
                _swarmFreezer.IsSuppressed = IsAnyBossActive;
            }
        }

        private void Golem_OnBossDefeated(IGolemBoss boss)
        {
            NotifyEncounterDisengaged();

            if (_nextStagePortalPrefab != null && boss != null && boss.Transform != null)
            {
                Vector3 spawnPos = boss.Transform.position;
                if (GroundSupportQuery.TryGetSupportedPosition(spawnPos, out Vector3 supportedPos))
                {
                    spawnPos.y = supportedPos.y;
                }

                ReflexPrefabInstantiator.InstantiateInjectedObject(
                    _nextStagePortalPrefab,
                    spawnPos,
                    Quaternion.identity,
                    null,
                    _container);
            }

            if (_activeGolemInstance != null)
            {
                _activeGolemInstance.OnBossDefeated -= Golem_OnBossDefeated;
                _activeGolemInstance = null;
            }
        }
    }
}
