using Assets.Scripts.Initializers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public interface ISkillsRegistry
    {
        public IReadOnlyList<ISkillBase> Skills { get; }
        public int UninitializedSkillsCount { get; }

        public IEnumerable<ISkillBase> GetUninitializedSkills();

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

            UninitializedSkillsCount = GetUninitializedSkills().Count();

            InitializeSkill(Skills[0]);
        }

        public IEnumerable<ISkillBase> GetUninitializedSkills()
        {
            return Skills
                    .Select(skill => skill as IInitializable)
                    .Where(skill => !skill.IsInitialized())
                    .Select(skill => skill as ISkillBase)
                    .ToArray();
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
            foreach (IUpgradeableSkill skill in Skills.OfType<IUpgradeableSkill>())
            {
                skill.Config?.ResetRuntimeState();
            }
        }
    }
}
