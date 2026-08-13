using Assets.Scripts.Extensions;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Stats;
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public interface ISkillUpgradeFlow
    {
        event EventHandler OnRequestQueued;
        bool QueueRandomNewSkillRequest(ISkillsRegistry skillsRegistry);
        void QueueRandomSkillUpgradeRequest(ISkillsRegistry skillsRegistry);
        bool TryGetNextRequest(ISkillsRegistry skillsRegistry, out SkillUpgradeRequest request);
    }

    public class SkillUpgradeFlow : ISkillUpgradeFlow
    {
        public event EventHandler OnRequestQueued;

        private readonly Queue<QueuedSkillRewardRequest> _queuedRequests = new();
        private readonly HashSet<ISkillBase> _skillsQueuedForInitialization = new();

        public bool QueueRandomNewSkillRequest(ISkillsRegistry skillsRegistry)
        {
            IReadOnlyList<ISkillBase> uninitializedSkills = skillsRegistry.GetUninitializedSkills();
            var candidates = new List<ISkillBase>();

            if (uninitializedSkills != null)
            {
                for (int i = 0; i < uninitializedSkills.Count; i++)
                {
                    ISkillBase skill = uninitializedSkills[i];
                    if (skill != null && !_skillsQueuedForInitialization.Contains(skill))
                    {
                        candidates.Add(skill);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Shuffle();
            ISkillBase selectedSkill = candidates[0];

            _skillsQueuedForInitialization.Add(selectedSkill);
            _queuedRequests.Enqueue(QueuedSkillRewardRequest.ForNewSkill(selectedSkill));
            OnRequestQueued?.Invoke(this, EventArgs.Empty);

            return true;
        }

        public void QueueRandomSkillUpgradeRequest(ISkillsRegistry skillsRegistry)
        {
            IUpgradeableSkill upgradeableSkill = RandomUpgradeableSkillFinder.Find(GetUpgradeableSkillCandidates(skillsRegistry));
            if (upgradeableSkill is not null)
            {
                _queuedRequests.Enqueue(QueuedSkillRewardRequest.ForUpgradeSkill(upgradeableSkill));
                OnRequestQueued?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                QueueRandomNewSkillRequest(skillsRegistry);
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
                if (upgradeableSkill != null && upgradeableSkill.CanBeUpgraded())
                {
                    request = SkillUpgradeRequest.ForUpgradeSkill(upgradeableSkill, CreateUpgradeOptions(upgradeableSkill));
                    return true;
                }
            }

            request = default;
            return false;
        }

        private IEnumerable<IUpgradeableSkill> GetUpgradeableSkillCandidates(ISkillsRegistry skillsRegistry)
        {
            var candidates = new List<IUpgradeableSkill>();
            IReadOnlyList<ISkillBase> skills = skillsRegistry.Skills;

            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] is IUpgradeableSkill upgradeableSkill)
                    {
                        if (upgradeableSkill.IsInitialized() || _skillsQueuedForInitialization.Contains(upgradeableSkill))
                        {
                            candidates.Add(upgradeableSkill);
                        }
                    }
                }
            }

            return candidates;
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
                SkillUpgradeRarity rarity = nameUpgradeableStatPair.RarityOverride
                    ?? SkillUpgradeRarityCalculator.Calculate(upgradeableStat, upgradeValue);

                options.Add(new SkillUpgradeOption(
                    $"{changeInfo} <b>{statName}</b> by <Color=#F8D61C>{statValue}{statUnit}</Color>",
                    () => upgradeableStat.Upgrade(upgradeValue),
                    rarity,
                    upgradeableStat.Icon));
            }

            options.Shuffle();
            int count = Math.Min(options.Count, SkillConstants.MAX_SKILL_UPGRADE_OPTIONS);
            SkillUpgradeOption[] result = new SkillUpgradeOption[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = options[i];
            }

            return result;
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
            {
                return new QueuedSkillRewardRequest(SkillUpgradeRequestType.NewSkill, skill, null);
            }

            public static QueuedSkillRewardRequest ForUpgradeSkill(IUpgradeableSkill skill)
            {
                return new QueuedSkillRewardRequest(SkillUpgradeRequestType.UpgradeSkill, null, skill);
            }
        }
    }
}

