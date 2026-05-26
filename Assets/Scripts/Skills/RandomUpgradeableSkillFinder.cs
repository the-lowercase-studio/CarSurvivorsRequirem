using System.Linq;

namespace Assets.Scripts.Skills
{
    public static class RandomUpgradeableSkillFinder
    {
        public static IUpgradeableSkill Find(ISkillsRegistry skillsRegistry)
        {
            var upgradeableSkills = skillsRegistry
                .Skills
                .OfType<IUpgradeableSkill>()
                .Where(skill => skill.IsInitialized())
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
