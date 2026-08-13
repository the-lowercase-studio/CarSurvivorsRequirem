using UnityEngine;
using Assets.ScriptableObjects.Player.Skills;
using Assets.ScriptableObjects.Skills;

namespace Assets.Scripts.Skills
{
    public interface IUpgradeableSkill : ISkillBase
    {
        public bool CanBeUpgraded();

        public ISkillUpgradeableStatsConfig Config { get; }
    }

    public abstract class UpgradeableSkill<TUpgradeableConfig> : MonoBehaviour, IUpgradeableSkill
        where TUpgradeableConfig : ISkillUpgradeableStatsConfig
    {
        public abstract SkillInfoSO SkillInfo { get; protected set; }
        protected abstract TUpgradeableConfig _config { get; set; }
        public ISkillUpgradeableStatsConfig Config => _config;

        public virtual bool CanBeUpgraded()
        {
            if (_config is null)
            {
                return false;
            }

            using var enumerator = _config.GetUpgradeableStatsThatCanBeUpgraded().GetEnumerator();
            return enumerator.MoveNext();
        }

        public virtual void Initialize()
        {
            if (_config is not null)
            {
                gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"Skill {SkillInfo?.name} is not initialized. Please assign a valid config.", this);
            }
        }

        public virtual bool IsInitialized()
        {
            return gameObject.activeSelf && _config is not null;
        }
    }
}

