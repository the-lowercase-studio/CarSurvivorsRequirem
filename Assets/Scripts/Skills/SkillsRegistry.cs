using System;
using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Initializers;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public interface ISkillsRegistry
    {
        IReadOnlyList<ISkillBase> Skills { get; }
        int UninitializedSkillsCount { get; }
        int InitializedSkillsCount { get; }

        IReadOnlyList<ISkillBase> GetInitializedSkills();
        IReadOnlyList<ISkillBase> GetUninitializedSkills();

        ISkillBase InitializeSkill(ISkillBase skill);
        event Action<ISkillBase> OnSkillInitialized;
    }

    public class SkillsRegistry : MonoBehaviour, ISkillsRegistry
    {
        [Inject] private readonly IRunSessionService _runSessionService = null;

        public event Action<ISkillBase> OnSkillInitialized;

        public IReadOnlyList<ISkillBase> Skills { get; private set; }
        public int UninitializedSkillsCount { get; private set; }
        public int InitializedSkillsCount { get; private set; }

        private void Awake()
        {
            RegisterAllSkills();
        }

        private void Start()
        {
            ResetUpgradeableSkillConfigs();

            UninitializedSkillsCount = GetUninitializedSkills().Count;
            InitializedSkillsCount = GetInitializedSkills().Count;

            if (_runSessionService != null
                && _runSessionService.TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> restoredSkills)
                && restoredSkills.Count > 0)
            {
                RestoreSkillsFromSnapshot(restoredSkills);
                if (InitializedSkillsCount > 0)
                {
                    return;
                }
            }

            if (Skills != null && Skills.Count > 0)
            {
                InitializeSkill(Skills[0]);
            }
        }

        public IReadOnlyList<ISkillBase> GetInitializedSkills()
        {
            var initializedSkills = new List<ISkillBase>();

            if (Skills != null)
            {
                for (int i = 0; i < Skills.Count; i++)
                {
                    ISkillBase skill = Skills[i];
                    if (skill is IInitializable initializableSkill && initializableSkill.IsInitialized())
                    {
                        initializedSkills.Add(skill);
                    }
                }
            }

            return initializedSkills;
        }

        public IReadOnlyList<ISkillBase> GetUninitializedSkills()
        {
            var uninitializedSkills = new List<ISkillBase>();

            if (Skills != null)
            {
                for (int i = 0; i < Skills.Count; i++)
                {
                    ISkillBase skill = Skills[i];
                    if (skill is IInitializable initializableSkill && !initializableSkill.IsInitialized())
                    {
                        uninitializedSkills.Add(skill);
                    }
                }
            }

            return uninitializedSkills;
        }

        public ISkillBase InitializeSkill(ISkillBase skill)
        {
            if (skill is IInitializable initializableSkill && !initializableSkill.IsInitialized())
            {
                initializableSkill.Initialize();

                if (UninitializedSkillsCount - 1 >= 0)
                {
                    UninitializedSkillsCount--;
                }

                InitializedSkillsCount++;

                OnSkillInitialized?.Invoke(skill);

                return skill;
            }

            return null;
        }

        private void RegisterAllSkills()
        {
            var skills = new List<ISkillBase>();

            foreach (Transform skillChild in transform)
            {
                if (skillChild.gameObject.TryGetComponent(out ISkillBase skill))
                {
                    skills.Add(skill);
                }
            }

            Skills = skills;
        }

        private void ResetUpgradeableSkillConfigs()
        {
            if (Skills == null)
            {
                return;
            }

            for (int i = 0; i < Skills.Count; i++)
            {
                if (Skills[i] is IUpgradeableSkill upgradeableSkill)
                {
                    upgradeableSkill.Config?.ResetRuntimeState();
                }
            }
        }

        private void RestoreSkillsFromSnapshot(IReadOnlyList<SavedSkillSnapshot> restoredSkills)
        {
            if (Skills == null || restoredSkills == null)
            {
                return;
            }

            var statsBuffer = new List<NameUpgradableStatPair>();

            for (int i = 0; i < restoredSkills.Count; i++)
            {
                SavedSkillSnapshot snapshot = restoredSkills[i];
                if (snapshot == null || string.IsNullOrEmpty(snapshot.SkillName))
                {
                    continue;
                }

                ISkillBase matchingSkill = FindSkillByName(snapshot.SkillName);
                if (matchingSkill == null)
                {
                    continue;
                }

                if (matchingSkill is IUpgradeableSkill upgradeableSkill
                    && upgradeableSkill.Config != null
                    && snapshot.Stats != null)
                {
                    statsBuffer.Clear();
                    upgradeableSkill.Config.AppendStatsForDisplay(statsBuffer);

                    for (int s = 0; s < snapshot.Stats.Count; s++)
                    {
                        SavedStatValue savedStat = snapshot.Stats[s];
                        for (int p = 0; p < statsBuffer.Count; p++)
                        {
                            NameUpgradableStatPair pair = statsBuffer[p];
                            if (pair.UpgradeableStat != null && string.Equals(pair.Name, savedStat.StatName, StringComparison.Ordinal))
                            {
                                pair.UpgradeableStat.RestoreRuntimeState(savedStat.Value, savedStat.CanBeUpgraded);
                                break;
                            }
                        }
                    }

                    upgradeableSkill.Config.SyncDerivedRuntimeState();
                }

                InitializeSkill(matchingSkill);
            }
        }

        private ISkillBase FindSkillByName(string skillName)
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                ISkillBase candidate = Skills[i];
                if (candidate == null || candidate.SkillInfo == null)
                {
                    continue;
                }

                string candidateKey = string.IsNullOrEmpty(candidate.SkillInfo.Name)
                    ? candidate.SkillInfo.name
                    : candidate.SkillInfo.Name;

                if (string.Equals(candidateKey, skillName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}

