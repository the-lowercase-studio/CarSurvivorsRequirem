using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Skills
{
    public static class RandomUpgradeableSkillFinder
    {
        public static IUpgradeableSkill Find(ISkillsRegistry skillsRegistry)
            => Find(skillsRegistry
                .Skills
                .OfType<IUpgradeableSkill>()
                .Where(skill => skill.IsInitialized()));

        public static IUpgradeableSkill Find(IEnumerable<IUpgradeableSkill> skills)
        {
            var upgradeableSkills = skills
                .Where(skill => skill.CanBeUgraded())
                .ToArray();

            if (upgradeableSkills.Length == 0)
            {
                return null;
            }

            int randomSkillIndex = UnityEngine.Random.Range(0, upgradeableSkills.Length);

            return upgradeableSkills[randomSkillIndex];
        }
    }
}
