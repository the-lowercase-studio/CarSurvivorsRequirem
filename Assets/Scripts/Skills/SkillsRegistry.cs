using Assets.Scripts.Initializers;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public interface ISkillsRegistry
    {
        public IReadOnlyList<ISkillBase> Skills { get; }
        public int UninitializedSkillsCount { get; }

        public IReadOnlyList<ISkillBase> GetUninitializedSkills();

        public ISkillBase InitializeSkill(ISkillBase skill);
    }

    public class SkillsRegistry : MonoBehaviour, ISkillsRegistry
    {
        public IReadOnlyList<ISkillBase> Skills { get; private set; }
        public int UninitializedSkillsCount { get; private set; }

        private void Awake()
        {
            RegisterAllSkills();
        }

        private void Start()
        {
            ResetUpgradeableSkillConfigs();

            UninitializedSkillsCount = GetUninitializedSkills().Count;

            if (Skills != null && Skills.Count > 0)
            {
                InitializeSkill(Skills[0]);
            }
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

