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
        private int _pendingNewSkillChoicesCount = 0;

        public bool QueueRandomNewSkillRequest(ISkillsRegistry skillsRegistry)
        {
            if (skillsRegistry.InitializedSkillsCount + _pendingNewSkillChoicesCount >= SkillConstants.MAX_ACTIVE_SKILLS)
            {
                return false;
            }

            IReadOnlyList<ISkillBase> uninitializedSkills = skillsRegistry.GetUninitializedSkills();
            if (uninitializedSkills == null || uninitializedSkills.Count == 0)
            {
                return false;
            }

            _pendingNewSkillChoicesCount++;
            _queuedRequests.Enqueue(QueuedSkillRewardRequest.ForNewSkillChoice());
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
            else if (skillsRegistry.InitializedSkillsCount + _pendingNewSkillChoicesCount < SkillConstants.MAX_ACTIVE_SKILLS)
            {
                QueueRandomNewSkillRequest(skillsRegistry);
            }
        }

        public bool TryGetNextRequest(ISkillsRegistry skillsRegistry, out SkillUpgradeRequest request)
        {
            while (_queuedRequests.Count > 0)
            {
                QueuedSkillRewardRequest queuedRequest = _queuedRequests.Dequeue();

                if (queuedRequest.RequestType == SkillUpgradeRequestType.NewSkillChoice)
                {
                    _pendingNewSkillChoicesCount--;

                    if (skillsRegistry.InitializedSkillsCount < SkillConstants.MAX_ACTIVE_SKILLS)
                    {
                        IReadOnlyList<ISkillBase> uninitializedSkills = skillsRegistry.GetUninitializedSkills();
                        if (uninitializedSkills != null && uninitializedSkills.Count > 0)
                        {
                            var candidates = new List<ISkillBase>(uninitializedSkills);
                            candidates.ShuffleInPlace();

                            int choiceCount = Math.Min(candidates.Count, SkillConstants.NEW_SKILL_CHOICE_COUNT);
                            var selectedChoices = new List<ISkillBase>(choiceCount);
                            for (int i = 0; i < choiceCount; i++)
                            {
                                selectedChoices.Add(candidates[i]);
                            }

                            request = SkillUpgradeRequest.ForNewSkillChoice(selectedChoices);
                            return true;
                        }
                    }

                    IUpgradeableSkill fallbackSkill = RandomUpgradeableSkillFinder.Find(GetUpgradeableSkillCandidates(skillsRegistry));
                    if (fallbackSkill != null && fallbackSkill.CanBeUpgraded())
                    {
                        request = SkillUpgradeRequest.ForUpgradeSkill(fallbackSkill, CreateUpgradeOptions(fallbackSkill));
                        return true;
                    }

                    continue;
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
                    if (skills[i] is IUpgradeableSkill upgradeableSkill && upgradeableSkill.IsInitialized())
                    {
                        candidates.Add(upgradeableSkill);
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

            options.ShuffleInPlace();
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
                IUpgradeableSkill upgradeableSkill)
            {
                RequestType = requestType;
                UpgradeableSkill = upgradeableSkill;
            }

            public SkillUpgradeRequestType RequestType { get; }
            public IUpgradeableSkill UpgradeableSkill { get; }

            public static QueuedSkillRewardRequest ForNewSkillChoice()
            {
                return new QueuedSkillRewardRequest(SkillUpgradeRequestType.NewSkillChoice, null);
            }

            public static QueuedSkillRewardRequest ForUpgradeSkill(IUpgradeableSkill skill)
            {
                return new QueuedSkillRewardRequest(SkillUpgradeRequestType.UpgradeSkill, skill);
            }
        }
    }
}

