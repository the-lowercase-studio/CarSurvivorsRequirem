using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Constants;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Player;
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
        void NotifyEncounterEngaged(IHealth bossHealth, string displayName);
        void NotifyEncounterDisengaged();
        void NotifyTowerDefeated(IEncounterArenaController towerArena);
        void SpawnGolemBoss(Vector3? spawnPosition = null);
        event Action<int, int> OnTowerDefeated;
        event Action<IGolemBoss> OnGolemSpawned;
        event Action<IGolemBoss> OnGolemDefeated;
    }

    public class BossEncounterService : MonoBehaviour, IBossEncounterService
    {
        [Inject] private readonly IBossHUDPresenter _bossHUDPresenter = null;
        [Inject] private readonly ISwarmFreezer _swarmFreezer = null;
        [Inject] private readonly IPlayerManager _playerManager = null;

        [Header("Prefabs & Configuration")]
        [Tooltip("Prefab for the stage-culmination Ancient Golem boss.")]
        [SerializeField] private GolemBoss _golemBossPrefab;
        [Tooltip("Prefab for the stage progression portal spawned upon Golem defeat.")]
        [SerializeField] private GameObject _nextStagePortalPrefab;
        [Tooltip("Display name shown on the Boss HUD health bar.")]
        [SerializeField] private string _golemDisplayName = BossEncounterConstants.DEFAULT_GOLEM_DISPLAY_NAME;
        [Tooltip("Forward spawn distance ahead of player when no explicit spawn position is provided.")]
        [SerializeField] private float _spawnOffsetDistance = BossEncounterConstants.DEFAULT_SPAWN_OFFSET_DISTANCE;
        [Tooltip("Configured number of towers that must be defeated before GolemBoss spawns.")]
        [SerializeField] private int _requiredTowersForGolemSpawn = BossEncounterConstants.DEFAULT_REQUIRED_TOWERS;
        [Tooltip("Breathing delay in seconds between final tower defeat and GolemBoss spawn.")]
        [SerializeField] private float _delayBeforeGolemSpawn = BossEncounterConstants.DEFAULT_DELAY_BEFORE_GOLEM_SPAWN;

        private readonly HashSet<IEncounterArenaController> _registeredTowers = new();
        private readonly HashSet<IEncounterArenaController> _defeatedTowers = new();
        private GolemBoss _activeGolemInstance;
        private Coroutine _golemSpawnCoroutine;
        private bool _isEncounterEngaged;

        public bool IsAnyBossActive => _isEncounterEngaged || IsGolemActive;

        public bool IsGolemActive => _activeGolemInstance != null && _activeGolemInstance.Health != null && _activeGolemInstance.Health.IsAlive();

        public int RequiredTowersCount => _registeredTowers.Count > 0 ? Mathf.Min(_requiredTowersForGolemSpawn, _registeredTowers.Count) : _requiredTowersForGolemSpawn;

        public int DefeatedTowersCount => _defeatedTowers.Count;

        public int RemainingTowersCount => Mathf.Max(0, RequiredTowersCount - DefeatedTowersCount);

        public event Action<int, int> OnTowerDefeated;
        public event Action<IGolemBoss> OnGolemSpawned;
        public event Action<IGolemBoss> OnGolemDefeated;

        private void OnDisable()
        {
            if (_golemSpawnCoroutine != null)
            {
                StopCoroutine(_golemSpawnCoroutine);
                _golemSpawnCoroutine = null;
            }

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
            if (_golemSpawnCoroutine != null)
            {
                StopCoroutine(_golemSpawnCoroutine);
                _golemSpawnCoroutine = null;
            }

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

        public void NotifyEncounterEngaged(IHealth bossHealth, string displayName)
        {
            _isEncounterEngaged = true;

            if (_bossHUDPresenter != null && bossHealth != null)
            {
                _bossHUDPresenter.Show(bossHealth, displayName);
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
            if (towerArena != null && !_defeatedTowers.Add(towerArena))
            {
                return;
            }

            NotifyEncounterDisengaged();

            OnTowerDefeated?.Invoke(DefeatedTowersCount, RequiredTowersCount);

            if (DefeatedTowersCount >= RequiredTowersCount)
            {
                if (_golemSpawnCoroutine != null)
                {
                    StopCoroutine(_golemSpawnCoroutine);
                }

                _golemSpawnCoroutine = StartCoroutine(DelayedSpawnGolemRoutine());
            }
        }

        public void SpawnGolemBoss(Vector3? spawnPosition = null)
        {
            if (_golemSpawnCoroutine != null)
            {
                StopCoroutine(_golemSpawnCoroutine);
                _golemSpawnCoroutine = null;
            }

            if (IsGolemActive || _golemBossPrefab == null)
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

            _activeGolemInstance = Instantiate(_golemBossPrefab, finalSpawnPos, Quaternion.identity);
            _activeGolemInstance.OnBossDefeated += Golem_OnBossDefeated;

            NotifyEncounterEngaged(_activeGolemInstance.Health, _golemDisplayName);

            OnGolemSpawned?.Invoke(_activeGolemInstance);
        }

        private IEnumerator DelayedSpawnGolemRoutine()
        {
            yield return new WaitForSeconds(_delayBeforeGolemSpawn);
            _golemSpawnCoroutine = null;
            SpawnGolemBoss();
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
                Instantiate(_nextStagePortalPrefab, boss.Transform.position, Quaternion.identity);
            }

            if (_activeGolemInstance != null)
            {
                _activeGolemInstance.OnBossDefeated -= Golem_OnBossDefeated;
                _activeGolemInstance = null;
            }

            OnGolemDefeated?.Invoke(boss);
        }
    }
}
