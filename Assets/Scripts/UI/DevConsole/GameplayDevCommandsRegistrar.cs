using System;
using System.Globalization;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.UI.DevConsole.Constants;
using Assets.Scripts.UI.Level;
using Assets.Scripts.UI.Skills;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.UI.DevConsole
{
    public interface IGameplayDevCommandsRegistrar
    {
    }

    public class GameplayDevCommandsRegistrar : MonoBehaviour, IGameplayDevCommandsRegistrar
    {
        [Inject] private readonly IDevConsoleService _devConsoleService = null;
        [Inject] private readonly IBossEncounterService _bossEncounterService = null;
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly ISwarmEnemySpawner _swarmEnemySpawner = null;
        [Inject] private readonly ISkillUpgradePresenter _skillUpgradePresenter = null;
        [Inject] private readonly IPlayerLevelPresenter _playerLevelPresenter = null;
        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;

        [Header("Collectible Prefabs")]
        [SerializeField] private GameObject _nukePrefab;
        [SerializeField] private GameObject _freezeTimerPrefab;
        [SerializeField] private GameObject _bubbleShieldPrefab;
        [SerializeField] private GameObject _armorPrefab;
        [SerializeField] private GameObject _fireTailsPrefab;
        [SerializeField] private GameObject _skillCratePrefab;

        private static bool IsConsoleSupported
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (!IsConsoleSupported)
            {
                enabled = false;
            }
        }

        private void Start()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            RegisterCommands();
        }

        private void OnDestroy()
        {
            if (!IsConsoleSupported)
            {
                return;
            }

            UnregisterCommands();
        }

        private void RegisterCommands()
        {
            if (!IsConsoleSupported || _devConsoleService == null)
            {
                return;
            }

            _devConsoleService.RegisterCommand(
                "golem",
                "golem",
                "Spawns the Ancient Golem boss encounter.",
                HandleGolemCommand
            );

            _devConsoleService.RegisterCommand(
                "spawn",
                "spawn <entity> [count]",
                "Spawns accessible entities (golem, barrel, zombie [1-4], crawling [2-3]) ahead of the player.",
                HandleSpawnCommand
            );

            _devConsoleService.RegisterCommand(
                "exp",
                "exp <amount>[k|m|b] [--auto]",
                "Awards EXP to player with optional automated upgrade resolution (e.g. 'exp 220 --auto').",
                HandleExpCommand
            );

            _devConsoleService.RegisterCommand(
                DevConsoleConstants.POWERUP_COMMAND_NAME,
                DevConsoleConstants.POWERUP_COMMAND_SYNTAX,
                DevConsoleConstants.POWERUP_COMMAND_DESCRIPTION,
                HandlePowerUpCommand
            );
        }

        private void UnregisterCommands()
        {
            if (!IsConsoleSupported || _devConsoleService == null)
            {
                return;
            }

            _devConsoleService.UnregisterCommand("golem");
            _devConsoleService.UnregisterCommand("spawn");
            _devConsoleService.UnregisterCommand("exp");
            _devConsoleService.UnregisterCommand(DevConsoleConstants.POWERUP_COMMAND_NAME);
        }

        private void HandleGolemCommand(string[] args)
        {
            SpawnGolem(DevConsoleConstants.DEFAULT_SPAWN_COUNT);
        }

        private void HandleSpawnCommand(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                _devConsoleService.LogError("Usage: spawn <entity> [count]. Supported entities: golem, barrel, zombie [1-4], crawling [2-3].");
                return;
            }

            if (_playerManager == null || _playerManager.GameObject == null)
            {
                _devConsoleService.LogError("Player is not available in the current scene.");
                return;
            }

            // Tower boss exclusion check
            for (int i = 0; i < args.Length; i++)
            {
                string token = args[i];
                if (string.Equals(token, "tower", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "mortar", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "mortartower", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "mortal", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "mortaltower", StringComparison.OrdinalIgnoreCase))
                {
                    _devConsoleService.LogError("Tower bosses cannot be spawned; they are pre-placed arena encounters on the map.");
                    return;
                }
            }

            if (TryParseGolemSpawn(args, out int golemCount))
            {
                SpawnGolem(golemCount);
                return;
            }

            if (!TryResolveSwarmTarget(args, out string targetPrefabName, out int spawnCount))
            {
                _devConsoleService.LogError($"Unknown entity '{args[0]}'. Usage: spawn <entity> [count]. Supported entities: golem, barrel, zombie [1-4], crawling [2-3].");
                return;
            }

            SpawnSwarmEntities(targetPrefabName, spawnCount);
        }

        private bool TryParseGolemSpawn(string[] args, out int count)
        {
            count = DevConsoleConstants.DEFAULT_SPAWN_COUNT;

            if (string.Equals(args[0], "golem", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "ancient_golem", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "ancientgolem", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
                {
                    count = parsedCount;
                }
                return true;
            }

            if (string.Equals(args[0], "ancient", StringComparison.OrdinalIgnoreCase) &&
                args.Length > 1 &&
                string.Equals(args[1], "golem", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length > 2 && int.TryParse(args[2], out int parsedCount))
                {
                    count = parsedCount;
                }
                return true;
            }

            return false;
        }

        private void SpawnGolem(int count)
        {
            if (_bossEncounterService == null)
            {
                _devConsoleService.LogError("BossEncounterService is not available in the current scene.");
                return;
            }

            if (_bossEncounterService.IsAnyBossActive)
            {
                _devConsoleService.LogWarning("A boss encounter is already active. Forcing Ancient Golem spawn...");
            }

            int clampedCount = Mathf.Clamp(count, 1, DevConsoleConstants.MAX_SPAWN_COUNT);
            for (int i = 0; i < clampedCount; i++)
            {
                _bossEncounterService.SpawnGolemBoss();
            }

            if (clampedCount == 1)
            {
                _devConsoleService.Log("Ancient Golem boss spawned successfully.");
            }
            else
            {
                _devConsoleService.Log($"Spawned {clampedCount} Ancient Golem bosses successfully.");
            }
        }

        private bool TryResolveSwarmTarget(string[] args, out string targetPrefabName, out int spawnCount)
        {
            targetPrefabName = null;
            spawnCount = DevConsoleConstants.DEFAULT_SPAWN_COUNT;

            string first = args[0];

            // Barrel
            if (string.Equals(first, "barrel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "barrelenemy", StringComparison.OrdinalIgnoreCase))
            {
                targetPrefabName = "BarrelEnemy";
                if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
                {
                    spawnCount = parsedCount;
                }
                return true;
            }

            // Standing Zombie (levels 1-4)
            if (string.Equals(first, "zombie", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "standing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "standingzombie", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "standing_zombie", StringComparison.OrdinalIgnoreCase))
            {
                int argIndex = 1;
                if (argIndex < args.Length && string.Equals(args[argIndex], "zombie", StringComparison.OrdinalIgnoreCase))
                {
                    argIndex++;
                }
                if (argIndex < args.Length && string.Equals(args[argIndex], "level", StringComparison.OrdinalIgnoreCase))
                {
                    argIndex++;
                }

                int level = 1;
                if (argIndex < args.Length && int.TryParse(args[argIndex], out int parsedNum))
                {
                    if (parsedNum >= 1 && parsedNum <= 4)
                    {
                        level = parsedNum;
                        argIndex++;
                        if (argIndex < args.Length && int.TryParse(args[argIndex], out int parsedCount))
                        {
                            spawnCount = parsedCount;
                        }
                    }
                    else
                    {
                        spawnCount = parsedNum;
                    }
                }

                targetPrefabName = $"StandingZombieLevel{level}";
                return true;
            }

            // Crawling Zombie (levels 2-3)
            if (string.Equals(first, "crawling", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "crawlingzombie", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "crawling_zombie", StringComparison.OrdinalIgnoreCase))
            {
                int argIndex = 1;
                if (argIndex < args.Length && string.Equals(args[argIndex], "zombie", StringComparison.OrdinalIgnoreCase))
                {
                    argIndex++;
                }
                if (argIndex < args.Length && string.Equals(args[argIndex], "level", StringComparison.OrdinalIgnoreCase))
                {
                    argIndex++;
                }

                int level = 2;
                if (argIndex < args.Length && int.TryParse(args[argIndex], out int parsedNum))
                {
                    if (parsedNum >= 2 && parsedNum <= 3)
                    {
                        level = parsedNum;
                        argIndex++;
                        if (argIndex < args.Length && int.TryParse(args[argIndex], out int parsedCount))
                        {
                            spawnCount = parsedCount;
                        }
                    }
                    else
                    {
                        spawnCount = parsedNum;
                    }
                }

                targetPrefabName = $"CrawlingZombieLevel{level}";
                return true;
            }

            // Direct prefab fallback
            if (_swarmEnemySpawner != null && _swarmEnemySpawner.EnemyConfigs != null)
            {
                for (int i = 0; i < _swarmEnemySpawner.EnemyConfigs.Count; i++)
                {
                    EnemySpawnInfo config = _swarmEnemySpawner.EnemyConfigs[i];
                    if (config != null && config.EnemyPrefab != null &&
                        string.Equals(config.EnemyPrefab.name, first, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPrefabName = config.EnemyPrefab.name;
                        if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
                        {
                            spawnCount = parsedCount;
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        private void SpawnSwarmEntities(string targetPrefabName, int count)
        {
            if (_swarmEnemySpawner == null)
            {
                _devConsoleService.LogError("SwarmEnemySpawner is not available in the current scene.");
                return;
            }

            EnemySpawnInfo targetInfo = null;
            if (_swarmEnemySpawner.EnemyConfigs != null)
            {
                for (int i = 0; i < _swarmEnemySpawner.EnemyConfigs.Count; i++)
                {
                    EnemySpawnInfo info = _swarmEnemySpawner.EnemyConfigs[i];
                    if (info != null && info.EnemyPrefab != null &&
                        string.Equals(info.EnemyPrefab.name, targetPrefabName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetInfo = info;
                        break;
                    }
                }
            }

            if (targetInfo == null)
            {
                _devConsoleService.LogError($"Enemy config for '{targetPrefabName}' not found in active spawner.");
                return;
            }

            int clampedCount = Mathf.Clamp(count, 1, DevConsoleConstants.MAX_SPAWN_COUNT);
            Transform playerTransform = _playerManager.GameObject.transform;
            Vector3 playerPos = playerTransform.position;
            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 right = playerTransform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            Vector3 center = playerPos + forward * DevConsoleConstants.DEFAULT_SPAWN_FORWARD_DISTANCE;
            int spawnedCount = 0;

            for (int i = 0; i < clampedCount; i++)
            {
                float lateralOffset = (clampedCount == 1) ? 0f : (i - (clampedCount - 1) * 0.5f) * DevConsoleConstants.DEFAULT_SPAWN_LATERAL_SPACING;
                Vector3 spawnPos = center + right * lateralOffset;

                Vector3 rayOrigin = spawnPos + Vector3.up * 5f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, TerrainLayers.Walkable))
                {
                    spawnPos = hit.point;
                }

                if (_swarmEnemySpawner.TrySpawnEnemyAt(targetInfo, spawnPos, out _))
                {
                    spawnedCount++;
                }
            }

            _devConsoleService.Log($"Spawned {spawnedCount} {targetInfo.EnemyPrefab.name} ahead of player.");
        }

        private void HandleExpCommand(string[] args)
        {
            if (args.Length == 0)
            {
                _devConsoleService.LogError("Usage: exp <amount>[k|m|b] [--auto] (e.g. 'exp 20', 'exp 220 --auto', 'exp 22b -a')");
                return;
            }

            if (_playerManager == null || _playerManager.LevelController == null)
            {
                _devConsoleService.LogError("Player LevelController is not available in the current scene.");
                return;
            }

            bool isAuto = false;
            string amountToken = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, DevConsoleConstants.AUTO_FLAG_LONG, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, DevConsoleConstants.AUTO_FLAG_SHORT, StringComparison.OrdinalIgnoreCase))
                {
                    isAuto = true;
                }
                else if (amountToken == null)
                {
                    amountToken = arg;
                }
            }

            if (string.IsNullOrEmpty(amountToken) || !TryParseExpAmount(amountToken, out float amount))
            {
                _devConsoleService.LogError($"Invalid EXP amount: '{amountToken ?? string.Empty}'. Examples: 'exp 20', 'exp 220', 'exp 22b'.");
                return;
            }

            if (amount <= 0f)
            {
                _devConsoleService.LogError("EXP amount must be greater than zero.");
                return;
            }

            if (isAuto)
            {
                if (_skillUpgradePresenter != null)
                {
                    _skillUpgradePresenter.IsAutoUpgradeEnabled = true;
                }

                try
                {
                    _playerManager.LevelController.AddExp(amount);
                    if (_playerLevelPresenter != null)
                    {
                        _playerLevelPresenter.FastForward();
                    }
                    if (_skillUpgradePresenter != null)
                    {
                        _skillUpgradePresenter.AutoResolvePendingUpgrades();
                    }
                }
                finally
                {
                    if (_skillUpgradePresenter != null)
                    {
                        _skillUpgradePresenter.IsAutoUpgradeEnabled = false;
                    }
                }
            }
            else
            {
                _playerManager.LevelController.AddExp(amount);
            }

            LevelData currentData = _playerManager.LevelController.LevelData;
            string modeSuffix = isAuto ? " [auto-resolved]" : string.Empty;
            _devConsoleService.Log($"Awarded {amount:N0} EXP{modeSuffix}. Current Level: {currentData.Lvl}, EXP: {currentData.Exp:N0}/{currentData.MaxExp:N0}");
        }

        private bool TryParseExpAmount(string rawInput, out float amount)
        {
            amount = 0f;
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return false;
            }

            string trimmed = rawInput.Trim();
            float multiplier = 1f;

            if (trimmed.EndsWith("b", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = DevConsoleConstants.EXP_BILLION_MULTIPLIER;
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }
            else if (trimmed.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = DevConsoleConstants.EXP_MILLION_MULTIPLIER;
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }
            else if (trimmed.EndsWith("k", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = DevConsoleConstants.EXP_THOUSAND_MULTIPLIER;
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedBase))
            {
                amount = parsedBase * multiplier;
                return true;
            }

            return false;
        }

        private void HandlePowerUpCommand(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                _devConsoleService.LogError($"Usage: {DevConsoleConstants.POWERUP_COMMAND_SYNTAX}.");
                return;
            }

            if (_playerManager == null || _playerManager.GameObject == null)
            {
                _devConsoleService.LogError("Player is not available in the current scene.");
                return;
            }

            if (_collectibleDropNotifier == null)
            {
                _devConsoleService.LogError("CollectibleDropNotifier is not available in the current scene.");
                return;
            }

            if (!TryResolveCollectiblePrefab(args[0], out GameObject prefab, out string displayName))
            {
                _devConsoleService.LogError($"Unknown power-up type '{args[0]}'. Usage: {DevConsoleConstants.POWERUP_COMMAND_SYNTAX}.");
                return;
            }

            if (prefab == null)
            {
                _devConsoleService.LogError($"Prefab for power-up '{displayName}' is not assigned.");
                return;
            }

            Transform playerTransform = _playerManager.GameObject.transform;
            Vector3 playerPos = playerTransform.position;
            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 spawnOrigin = playerPos + Vector3.up * DevConsoleConstants.POWERUP_SPAWN_Y_OFFSET;
            Vector3 targetPos = playerPos + forward * DevConsoleConstants.DEFAULT_SPAWN_FORWARD_DISTANCE;

            Vector3 rayOrigin = targetPos + Vector3.up * 5f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, TerrainLayers.Walkable))
            {
                targetPos = hit.point + Vector3.up * DevConsoleConstants.POWERUP_SPAWN_Y_OFFSET;
            }
            else
            {
                targetPos.y = playerPos.y + DevConsoleConstants.POWERUP_SPAWN_Y_OFFSET;
            }

            _collectibleDropNotifier.SpawnCollectible(prefab, spawnOrigin, targetPos);
            _devConsoleService.Log($"Spawned {displayName} ahead of player.");
        }

        private bool TryResolveCollectiblePrefab(string token, out GameObject prefab, out string displayName)
        {
            prefab = null;
            displayName = null;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (string.Equals(token, "nuke", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Nuke";
                prefab = _nukePrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DevConsoleConstants.NUKE_PREFAB_PATH);
                }
#endif
                return true;
            }

            if (string.Equals(token, "freeze", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "freezetimer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "ice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "timefreeze", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Freeze Timer";
                prefab = _freezeTimerPrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DevConsoleConstants.FREEZE_TIMER_PREFAB_PATH);
                }
#endif
                return true;
            }

            if (string.Equals(token, "shield", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "bubbleshield", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "bubble", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Bubble Shield";
                prefab = _bubbleShieldPrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DevConsoleConstants.BUBBLE_SHIELD_PREFAB_PATH);
                }
#endif
                return true;
            }

            if (string.Equals(token, "armor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "armour", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "plate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "plates", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Armor";
                prefab = _armorPrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DevConsoleConstants.ARMOR_PREFAB_PATH);
                }
#endif
                return true;
            }

            if (string.Equals(token, "firetails", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "fire", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "tails", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "napalm", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Fire Tails";
                prefab = _fireTailsPrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DevConsoleConstants.FIRE_TAILS_PREFAB_PATH);
                }
#endif
                return true;
            }

            if (string.Equals(token, "crate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "skillcrate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "skill", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Skill Crate";
                prefab = _skillCratePrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DevConsoleConstants.SKILL_CRATE_PREFAB_PATH);
                }
#endif
                return true;
            }

            return false;
        }
    }
}
