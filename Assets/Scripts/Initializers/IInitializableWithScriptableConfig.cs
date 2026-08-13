using UnityEngine;

namespace Assets.Scripts.Initializers
{
    public interface IInitializableWithScriptableConfig<TScriptableConfig>
        where TScriptableConfig : ScriptableObject
    {
        void Initialize(TScriptableConfig config);

        bool IsInitialized();
    }
}

