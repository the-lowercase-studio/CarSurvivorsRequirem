using Assets.Scripts.Extensions;
using Assets.Scripts.Initializers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public interface IItemsWithScriptableConfigsActivator<TItem, TScriptableConfig>
        where TItem : MonoBehaviour, IInitializableWithScriptableConfig<TScriptableConfig>
        where TScriptableConfig : ScriptableObject
    {
        public TItem InitializeRandom(TScriptableConfig config);

        public TItem InitializeFirst(TScriptableConfig config);

        public IEnumerable<TItem> GetUninitialized();

        public IEnumerable<TItem> GetInitialized();
    }

    public class ItemsWithScriptableConfigsActivator<TItem, TScriptableConfig> : IItemsWithScriptableConfigsActivator<TItem, TScriptableConfig>
        where TItem : MonoBehaviour, IInitializableWithScriptableConfig<TScriptableConfig>
        where TScriptableConfig : ScriptableObject
    {
        private readonly TItem[] _items;

        public ItemsWithScriptableConfigsActivator(TItem[] items)
        {
            _items = items;
        }

        public TItem InitializeRandom(TScriptableConfig config)
        {
            var inactive = GetUninitialized().ToList();

            if (inactive.Any())
            {
                TItem item = inactive.Shuffle().First();
                item.Initialize(config);
                return item;
            }

            return null;
        }

        public TItem InitializeFirst(TScriptableConfig config)
        {
            if (_items.Length == 0)
            {
                return null;
            }

            if (!_items[0].IsInitialized())
            {
                _items[0].Initialize(config);
            }

            return _items[0];
        }

        public IEnumerable<TItem> GetUninitialized()
        {
            return _items.Where(t => !t.IsInitialized());
        }

        public IEnumerable<TItem> GetInitialized()
        {
            return _items.Where(t => t.IsInitialized());
        }
    }
}
