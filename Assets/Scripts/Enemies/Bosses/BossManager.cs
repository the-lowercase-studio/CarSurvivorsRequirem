using System;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Player;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.UI.HUD;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses
{
    public interface IBossManager
    {
        bool IsBossActive { get; }
        void SpawnBoss(Vector3? spawnPosition = null);
    }

    public class BossManager : MonoBehaviour, IBossManager
    {
        [Inject] private readonly IBossHUDPresenter _bossHUDPresenter = null;
        [Inject] private readonly ISwarmFreezer _swarmFreezer = null;
        [Inject] private readonly IPlayerManager _playerManager = null;

        [Header("Prefabs & Configuration")]
        [SerializeField] private GolemBoss _golemBossPrefab;
        [SerializeField] private GameObject _nextStagePortalPrefab;
        [SerializeField] private string _bossDisplayName = "ANCIENT GOLEM";
        [SerializeField] private float _spawnOffsetDistance = 15f;

        [Header("Debug")]
        [SerializeField] private KeyCode _debugSpawnKey = KeyCode.P;

        private GolemBoss _activeBossInstance;

        public bool IsBossActive => _activeBossInstance != null && _activeBossInstance.Health != null && _activeBossInstance.Health.IsAlive();

        private void Update()
        {
            if (Input.GetKeyDown(_debugSpawnKey))
            {
                SpawnBoss();
            }
        }

        private void OnDisable()
        {
            if (_activeBossInstance != null)
            {
                _activeBossInstance.OnBossDefeated -= ActiveBoss_OnBossDefeated;
            }
        }

        public void SpawnBoss(Vector3? spawnPosition = null)
        {
            if (IsBossActive || _golemBossPrefab == null)
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
                if (playerForward.sqrMagnitude < 0.01f) playerForward = Vector3.forward;

                finalSpawnPos = playerPos + playerForward.normalized * _spawnOffsetDistance;
            }
            else
            {
                finalSpawnPos = transform.position + Vector3.forward * _spawnOffsetDistance;
            }

            _activeBossInstance = Instantiate(_golemBossPrefab, finalSpawnPos, Quaternion.identity);
            _activeBossInstance.OnBossDefeated += ActiveBoss_OnBossDefeated;

            if (_bossHUDPresenter != null)
            {
                _bossHUDPresenter.Show(_activeBossInstance.Health, _bossDisplayName);
            }

            if (_swarmFreezer != null)
            {
                _swarmFreezer.IsSuppressed = true;
            }
        }

        private void ActiveBoss_OnBossDefeated(IGolemBoss boss)
        {
            if (_swarmFreezer != null)
            {
                _swarmFreezer.IsSuppressed = false;
            }

            if (_nextStagePortalPrefab != null && boss != null)
            {
                Instantiate(_nextStagePortalPrefab, boss.Transform.position, Quaternion.identity);
            }

            if (_activeBossInstance != null)
            {
                _activeBossInstance.OnBossDefeated -= ActiveBoss_OnBossDefeated;
            }
        }
    }
}
