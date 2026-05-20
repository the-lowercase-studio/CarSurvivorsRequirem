using Assets.Scripts.Extensions;
using Assets.Scripts.Stats;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public interface ISkillUpgradeFlow
    {
        void QueueRandomRequest(ISkillsRegistry skillsRegistry);
        bool TryGetNextRequest(ISkillsRegistry skillsRegistry, out SkillUpgradeRequest request);
    }

    public class SkillUpgradeFlow : ISkillUpgradeFlow
    {
        private static readonly byte _maxSkillUpgradeOptions = 3;

        private readonly Queue<ISkillBase> _skillsQueuedForInitialization = new();
        private readonly Queue<IUpgradeableSkill> _skillsQueuedForUpgrade = new();

        public void QueueRandomRequest(ISkillsRegistry skillsRegistry)
        {
            if (_skillsQueuedForInitialization.Count < skillsRegistry.UninitializedSkillsCount)
            {
                ISkillBase skill = skillsRegistry.GetUninitializedSkills().Shuffle().FirstOrDefault();
                if (skill is not null)
                {
                    _skillsQueuedForInitialization.Enqueue(skill);
                }

                return;
            }

            IUpgradeableSkill upgradeableSkill = RandomUpgradeableSkillFinder.Find(skillsRegistry);
            if (upgradeableSkill is not null)
            {
                _skillsQueuedForUpgrade.Enqueue(upgradeableSkill);
            }
        }

        public bool TryGetNextRequest(ISkillsRegistry skillsRegistry, out SkillUpgradeRequest request)
        {
            if (_skillsQueuedForInitialization.Count > 0)
            {
                ISkillBase skill = _skillsQueuedForInitialization.Dequeue();
                skillsRegistry.InitializeSkill(skill);
                request = SkillUpgradeRequest.ForNewSkill(skill);
                return true;
            }

            while (_skillsQueuedForUpgrade.Count > 0)
            {
                IUpgradeableSkill skill = _skillsQueuedForUpgrade.Dequeue();
                if (skill.CanBeUgraded())
                {
                    request = SkillUpgradeRequest.ForUpgradeSkill(skill, CreateUpgradeOptions(skill));
                    return true;
                }
            }

            request = default;
            return false;
        }

        private static IReadOnlyList<SkillUpgradeOption> CreateUpgradeOptions(IUpgradeableSkill upgradeableSkill)
        {
            List<SkillUpgradeOption> options = new();

            foreach (var nameUpgradeableStatPair in upgradeableSkill.Config.GetUpgradeableStatsThatCanBeUpgraded())
            {
                float upgradeValue = nameUpgradeableStatPair.UpgradeableStat.GetUpgradeValueBasedOnUpdateRange();
                IUpgradeableStat upgradeableStat = nameUpgradeableStatPair.UpgradeableStat;

                string changeInfo = upgradeableStat.IsSubstractModeOn ? "Decrease" : "Increase";
                string statName = nameUpgradeableStatPair.Name.PascalCaseToWords();
                string statUnit = upgradeableStat.Unit.ToDisplayString();
                float statValue = upgradeableStat.Unit == StatsUnits.Percentage
                    ? upgradeableStat.GetWhatPercentOfValueIsUpgradeValue(upgradeValue)
                    : upgradeValue;

                options.Add(new SkillUpgradeOption(
                    $"{changeInfo} <b>{statName}</b> by <Color=#F8D61C>{statValue}{statUnit}</Color>",
                    () => upgradeableStat.Upgrade(upgradeValue)));
            }

            return options
                .Shuffle()
                .Take(_maxSkillUpgradeOptions)
                .ToArray();
        }
    }
}
