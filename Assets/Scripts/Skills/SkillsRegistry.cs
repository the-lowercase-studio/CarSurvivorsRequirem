using Assets.Scripts.Initializers;
using System;
using System.Collections.Generic;
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
    }
}

