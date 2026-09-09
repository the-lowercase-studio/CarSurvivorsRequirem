using System.Collections.Generic;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public enum SkillUpgradeRequestType
    {
        NewSkillChoice,
        UpgradeSkill
    }

    public readonly struct SkillUpgradeRequest
    {
        private SkillUpgradeRequest(
            SkillUpgradeRequestType requestType,
            IReadOnlyList<ISkillBase> skillChoices,
            IUpgradeableSkill upgradeableSkill,
            IReadOnlyList<SkillUpgradeOption> upgradeOptions)
        {
            RequestType = requestType;
            SkillChoices = skillChoices;
            UpgradeableSkill = upgradeableSkill;
            UpgradeOptions = upgradeOptions;
        }

        public SkillUpgradeRequestType RequestType { get; }
        public IReadOnlyList<ISkillBase> SkillChoices { get; }
        public IUpgradeableSkill UpgradeableSkill { get; }
        public IReadOnlyList<SkillUpgradeOption> UpgradeOptions { get; }

        public static SkillUpgradeRequest ForNewSkillChoice(IReadOnlyList<ISkillBase> skillChoices)
        {
            return new SkillUpgradeRequest(SkillUpgradeRequestType.NewSkillChoice, skillChoices, null, null);
        }

        public static SkillUpgradeRequest ForUpgradeSkill(
            IUpgradeableSkill skill,
            IReadOnlyList<SkillUpgradeOption> upgradeOptions)
        {
            return new SkillUpgradeRequest(SkillUpgradeRequestType.UpgradeSkill, null, skill, upgradeOptions);
        }
    }
}

