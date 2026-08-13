using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public static class RandomUninitializedSkillsInitializator
    {
        public static ISkillBase Initialize(ISkillsRegistry skillsRegistry)
        {
            if (skillsRegistry.UninitializedSkillsCount > 0)
            {
                IReadOnlyList<ISkillBase> inactiveSkills = skillsRegistry.GetUninitializedSkills();
                if (inactiveSkills != null && inactiveSkills.Count > 0)
                {
                    int index = Random.Range(0, inactiveSkills.Count);
                    ISkillBase inactiveSkill = inactiveSkills[index];

                    if (inactiveSkill != null)
                    {
                        return skillsRegistry.InitializeSkill(inactiveSkill);
                    }
                }
            }

            return null;
        }
    }
}

