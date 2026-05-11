using System.Collections.Generic;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public enum SkillUpgradeRequestType
    {
        NewSkill,
        UpgradeSkill
    }

    public readonly struct SkillUpgradeRequest
    {
        private SkillUpgradeRequest(
            SkillUpgradeRequestType requestType,
            ISkillBase newSkill,
            IUpgradeableSkill upgradeableSkill,
            IReadOnlyList<SkillUpgradeOption> upgradeOptions)
        {
            RequestType = requestType;
            NewSkill = newSkill;
            UpgradeableSkill = upgradeableSkill;
            UpgradeOptions = upgradeOptions;
        }

        public SkillUpgradeRequestType RequestType { get; }
        public ISkillBase NewSkill { get; }
        public IUpgradeableSkill UpgradeableSkill { get; }
        public IReadOnlyList<SkillUpgradeOption> UpgradeOptions { get; }

        public static SkillUpgradeRequest ForNewSkill(ISkillBase skill)
            => new(SkillUpgradeRequestType.NewSkill, skill, null, null);

        public static SkillUpgradeRequest ForUpgradeSkill(
            IUpgradeableSkill skill,
            IReadOnlyList<SkillUpgradeOption> upgradeOptions)
            => new(SkillUpgradeRequestType.UpgradeSkill, null, skill, upgradeOptions);
    }
}
