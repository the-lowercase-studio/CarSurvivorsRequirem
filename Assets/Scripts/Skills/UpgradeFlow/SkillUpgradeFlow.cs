using Assets.Scripts.Extensions;
using Assets.Scripts.Stats;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public interface ISkillUpgradeFlow
    {
        bool QueueRandomNewSkillRequest(ISkillsRegistry skillsRegistry);
        void QueueRandomSkillUpgradeRequest(ISkillsRegistry skillsRegistry);
        bool TryGetNextRequest(ISkillsRegistry skillsRegistry, out SkillUpgradeRequest request);
    }

    public class SkillUpgradeFlow : ISkillUpgradeFlow
    {
        private const int MAX_SKILL_UPGRADE_OPTIONS = 3;

        private readonly Queue<QueuedSkillRewardRequest> _queuedRequests = new();
        private readonly HashSet<ISkillBase> _skillsQueuedForInitialization = new();

        public bool QueueRandomNewSkillRequest(ISkillsRegistry skillsRegistry)
        {
            ISkillBase skill = skillsRegistry
                .GetUninitializedSkills()
                .Where(skill => !_skillsQueuedForInitialization.Contains(skill))
                .Shuffle()
                .FirstOrDefault();

            if (skill is null)
            {
                return false;
            }

            _skillsQueuedForInitialization.Add(skill);
            _queuedRequests.Enqueue(QueuedSkillRewardRequest.ForNewSkill(skill));

            return true;
        }

        public void QueueRandomSkillUpgradeRequest(ISkillsRegistry skillsRegistry)
        {
            IUpgradeableSkill upgradeableSkill = RandomUpgradeableSkillFinder.Find(GetUpgradeableSkillCandidates(skillsRegistry));
            if (upgradeableSkill is not null)
            {
                _queuedRequests.Enqueue(QueuedSkillRewardRequest.ForUpgradeSkill(upgradeableSkill));
            }
        }

        public bool TryGetNextRequest(ISkillsRegistry skillsRegistry, out SkillUpgradeRequest request)
        {
            while (_queuedRequests.Count > 0)
            {
                QueuedSkillRewardRequest queuedRequest = _queuedRequests.Dequeue();

                if (queuedRequest.RequestType == SkillUpgradeRequestType.NewSkill)
                {
                    _skillsQueuedForInitialization.Remove(queuedRequest.NewSkill);
                    ISkillBase skill = skillsRegistry.InitializeSkill(queuedRequest.NewSkill) ?? queuedRequest.NewSkill;
                    request = SkillUpgradeRequest.ForNewSkill(skill);
                    return true;
                }

                IUpgradeableSkill upgradeableSkill = queuedRequest.UpgradeableSkill;
                if (upgradeableSkill.CanBeUgraded())
                {
                    request = SkillUpgradeRequest.ForUpgradeSkill(upgradeableSkill, CreateUpgradeOptions(upgradeableSkill));
                    return true;
                }
            }

            request = default;
            return false;
        }

        private IEnumerable<IUpgradeableSkill> GetUpgradeableSkillCandidates(ISkillsRegistry skillsRegistry)
            => skillsRegistry
                .Skills
                .OfType<IUpgradeableSkill>()
                .Where(skill => skill.IsInitialized() || _skillsQueuedForInitialization.Contains(skill));

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
                SkillUpgradeRarity rarity = nameUpgradeableStatPair.RarityOverride
                    ?? SkillUpgradeRarityCalculator.Calculate(upgradeableStat, upgradeValue);

                options.Add(new SkillUpgradeOption(
                    $"{changeInfo} <b>{statName}</b> by <Color=#F8D61C>{statValue}{statUnit}</Color>",
                    () => upgradeableStat.Upgrade(upgradeValue),
                    rarity));
            }

            return options
                .Shuffle()
                .Take(MAX_SKILL_UPGRADE_OPTIONS)
                .ToArray();
        }

        private readonly struct QueuedSkillRewardRequest
        {
            private QueuedSkillRewardRequest(
                SkillUpgradeRequestType requestType,
                ISkillBase newSkill,
                IUpgradeableSkill upgradeableSkill)
            {
                RequestType = requestType;
                NewSkill = newSkill;
                UpgradeableSkill = upgradeableSkill;
            }

            public SkillUpgradeRequestType RequestType { get; }
            public ISkillBase NewSkill { get; }
            public IUpgradeableSkill UpgradeableSkill { get; }

            public static QueuedSkillRewardRequest ForNewSkill(ISkillBase skill)
                => new(SkillUpgradeRequestType.NewSkill, skill, null);

            public static QueuedSkillRewardRequest ForUpgradeSkill(IUpgradeableSkill skill)
                => new(SkillUpgradeRequestType.UpgradeSkill, null, skill);
        }
    }
}
