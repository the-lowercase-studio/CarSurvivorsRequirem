using System;
using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Player;
using Assets.Scripts.Skills;
using UnityEngine;

namespace Assets.Scripts.GameFlow
{
    public readonly struct SavedStatValue
    {
        public string StatName { get; }
        public double Value { get; }
        public bool CanBeUpgraded { get; }

        public SavedStatValue(string statName, double value, bool canBeUpgraded)
        {
            StatName = statName;
            Value = value;
            CanBeUpgraded = canBeUpgraded;
        }
    }

    public sealed class SavedSkillSnapshot
    {
        public string SkillName { get; }
        public IReadOnlyList<SavedStatValue> Stats { get; }

        public SavedSkillSnapshot(string skillName, IReadOnlyList<SavedStatValue> stats)
        {
            SkillName = skillName;
            Stats = stats;
        }
    }

    public sealed class RunSessionSnapshot
    {
        public byte Level { get; }
        public float Exp { get; }
        public uint ElapsedSeconds { get; }
        public List<SavedSkillSnapshot> InitializedSkills { get; }

        public RunSessionSnapshot(byte level, float exp, uint elapsedSeconds, List<SavedSkillSnapshot> initializedSkills)
        {
            Level = level;
            Exp = exp;
            ElapsedSeconds = elapsedSeconds;
            InitializedSkills = initializedSkills ?? new List<SavedSkillSnapshot>();
        }
    }

    public interface IRunSessionService
    {
        int CurrentTierIndex { get; }
        int TotalTiersCount { get; }
        bool IsFinalTier { get; }
        LoopTierConfig ActiveTierConfig { get; }
        GameLoopProgressionConfigSO ProgressionConfig { get; }
        bool HasPendingRestorationSnapshot { get; }
        uint AccumulatedRunSeconds { get; }
        bool IsGodModeEnabled { get; }

        void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds);
        void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds);
        void CaptureFinalRunTimer(uint elapsedSeconds);
        bool TryGetRestoredLevel(out byte level, out float exp);
        bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills);
        void SetGodModeEnabled(bool isEnabled);
        void ResetSession();
        void ResetSessionToTier(int tierIndex);
    }

    public class RunSessionService : IRunSessionService
    {
        private static bool IsDevCheatsSupported
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

        private readonly GameLoopProgressionConfigSO _progressionConfig;
        private RunSessionSnapshot _snapshot;
        private bool _isGodModeEnabled;

        public int CurrentTierIndex { get; private set; }
        public int TotalTiersCount => _progressionConfig.Tiers.Count;
        public bool IsFinalTier => CurrentTierIndex >= TotalTiersCount - 1;
        public LoopTierConfig ActiveTierConfig => _progressionConfig.GetTier(CurrentTierIndex);
        public GameLoopProgressionConfigSO ProgressionConfig => _progressionConfig;
        public bool HasPendingRestorationSnapshot => _snapshot != null;
        public uint AccumulatedRunSeconds { get; private set; }
        public bool IsGodModeEnabled
        {
            get
            {
                return IsDevCheatsSupported && _isGodModeEnabled;
            }
        }

        public RunSessionService(GameLoopProgressionConfigSO progressionConfig)
        {
            if (progressionConfig == null)
            {
                throw new ArgumentNullException(nameof(progressionConfig));
            }

            progressionConfig.ValidateConfiguration();
            _progressionConfig = progressionConfig;
            ResetSession();
        }

        public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds)
        {
            CaptureRunSnapshot(playerManager, elapsedSeconds);

            if (!IsFinalTier)
            {
                CurrentTierIndex++;
            }
        }

        public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds)
        {
            ValidateTierIndex(tierIndex);
            CaptureRunSnapshot(playerManager, elapsedSeconds);
            CurrentTierIndex = tierIndex;
        }

        public void CaptureFinalRunTimer(uint elapsedSeconds)
        {
            AccumulatedRunSeconds = elapsedSeconds;
        }

        public bool TryGetRestoredLevel(out byte level, out float exp)
        {
            if (_snapshot == null)
            {
                level = 1;
                exp = 0f;
                return false;
            }

            level = _snapshot.Level;
            exp = _snapshot.Exp;
            return true;
        }

        public bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills)
        {
            if (_snapshot == null || _snapshot.InitializedSkills == null)
            {
                skills = null;
                return false;
            }

            skills = _snapshot.InitializedSkills;
            return true;
        }

        public void SetGodModeEnabled(bool isEnabled)
        {
            _isGodModeEnabled = IsDevCheatsSupported && isEnabled;
        }

        public void ResetSession()
        {
            ResetSessionToTier(0);
        }

        public void ResetSessionToTier(int tierIndex)
        {
            ValidateTierIndex(tierIndex);
            CurrentTierIndex = tierIndex;
            AccumulatedRunSeconds = 0;
            _snapshot = null;
            _isGodModeEnabled = false;
        }

        private void ValidateTierIndex(int tierIndex)
        {
            if (tierIndex < 0 || tierIndex >= TotalTiersCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tierIndex),
                    tierIndex,
                    $"Tier index must be within [0, {TotalTiersCount - 1}].");
            }
        }

        private void CaptureRunSnapshot(IPlayerManager playerManager, uint elapsedSeconds)
        {
            if (playerManager == null)
            {
                throw new ArgumentNullException(nameof(playerManager));
            }

            if (playerManager.LevelController == null)
            {
                throw new InvalidOperationException($"[{nameof(RunSessionService)}] PlayerManager.LevelController is null when capturing run snapshot.");
            }

            byte level = playerManager.LevelController.LevelData.Lvl;
            float exp = playerManager.LevelController.LevelData.Exp;

            List<SavedSkillSnapshot> savedSkills = CaptureSkillsSnapshot(playerManager.SkillsRegistry);
            _snapshot = new RunSessionSnapshot(level, exp, elapsedSeconds, savedSkills);
            AccumulatedRunSeconds = elapsedSeconds;
        }

        private static List<SavedSkillSnapshot> CaptureSkillsSnapshot(ISkillsRegistry skillsRegistry)
        {
            var result = new List<SavedSkillSnapshot>();
            if (skillsRegistry == null)
            {
                return result;
            }

            IReadOnlyList<ISkillBase> initializedSkills = skillsRegistry.GetInitializedSkills();
            if (initializedSkills == null)
            {
                return result;
            }

            var statsBuffer = new List<NameUpgradableStatPair>();

            for (int i = 0; i < initializedSkills.Count; i++)
            {
                ISkillBase skill = initializedSkills[i];
                if (skill == null || skill.SkillInfo == null)
                {
                    continue;
                }

                string skillKey = string.IsNullOrEmpty(skill.SkillInfo.Name)
                    ? skill.SkillInfo.name
                    : skill.SkillInfo.Name;

                var savedStats = new List<SavedStatValue>();
                if (skill is IUpgradeableSkill upgradeableSkill && upgradeableSkill.Config != null)
                {
                    statsBuffer.Clear();
                    upgradeableSkill.Config.AppendStatsForDisplay(statsBuffer);

                    for (int j = 0; j < statsBuffer.Count; j++)
                    {
                        NameUpgradableStatPair pair = statsBuffer[j];
                        if (pair.UpgradeableStat != null)
                        {
                            savedStats.Add(new SavedStatValue(
                                pair.Name,
                                pair.UpgradeableStat.CurrentValue,
                                pair.UpgradeableStat.CanBeUpgraded));
                        }
                    }
                }

                result.Add(new SavedSkillSnapshot(skillKey, savedStats));
            }

            return result;
        }
    }
}
