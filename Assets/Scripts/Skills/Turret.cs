using Assets.Scripts.Initializers;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public abstract class Turret<ConfigSO> : MonoBehaviour, IInitializableWithScriptableConfig<ConfigSO>
        where ConfigSO : ScriptableObject
    {
        [SerializeField] protected ConfigSO _config;
        [SerializeField] protected Transform _gunTip;

        public abstract void Shoot(float shootPreparingAnimationSpeed = 1f);

        public virtual void Initialize(ConfigSO config)
        {
            _config = config;
            gameObject.SetActive(true);
        }

        public bool IsInitialized()
        {
            return gameObject.activeSelf && _config != null;
        }
    }
}

