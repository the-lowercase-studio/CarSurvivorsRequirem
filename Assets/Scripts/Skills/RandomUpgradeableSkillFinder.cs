using System.Collections.Generic;

namespace Assets.Scripts.Skills
{
    public static class RandomUpgradeableSkillFinder
    {
        public static IUpgradeableSkill Find(ISkillsRegistry skillsRegistry)
        {
            var upgradeableSkills = new List<IUpgradeableSkill>();
            IReadOnlyList<ISkillBase> skills = skillsRegistry.Skills;

            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] is IUpgradeableSkill upgradeableSkill && upgradeableSkill.IsInitialized())
                    {
                        upgradeableSkills.Add(upgradeableSkill);
                    }
                }
            }

            return Find(upgradeableSkills);
        }

        public static IUpgradeableSkill Find(IEnumerable<IUpgradeableSkill> skills)
        {
            if (skills == null)
            {
                return null;
            }

            var upgradeableCandidates = new List<IUpgradeableSkill>();

            foreach (IUpgradeableSkill skill in skills)
            {
                if (skill != null && skill.CanBeUpgraded())
                {
                    upgradeableCandidates.Add(skill);
                }
            }

            if (upgradeableCandidates.Count == 0)
            {
                return null;
            }

            int randomSkillIndex = UnityEngine.Random.Range(0, upgradeableCandidates.Count);

            return upgradeableCandidates[randomSkillIndex];
        }
    }
}

