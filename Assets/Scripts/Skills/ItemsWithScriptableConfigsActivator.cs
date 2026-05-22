using Assets.Scripts.Initializers;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Skills
{
    public interface IItemsWithScriptableConfigsActivator<TItem, TScriptableConfig>
        where TItem : MonoBehaviour, IInitializableWithScriptableConfig<TScriptableConfig>
        where TScriptableConfig : ScriptableObject
    {
        public TItem InitializeRandom(TScriptableConfig config);

        public TItem InitializeFirst(TScriptableConfig config);

        public void InitializeUntilCount(TScriptableConfig config, int count);

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
            int uninitializedCount = CountUninitialized();

            if (uninitializedCount == 0)
            {
                return null;
            }

            int selectedIndex = Random.Range(0, uninitializedCount);
            int currentIndex = 0;

            foreach (TItem item in _items)
            {
                if (item.IsInitialized())
                {
                    continue;
                }

                if (currentIndex == selectedIndex)
                {
                    item.Initialize(config);
                    return item;
                }

                currentIndex++;
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

        public void InitializeUntilCount(TScriptableConfig config, int count)
        {
            int initializedCount = CountInitialized();

            while (initializedCount < count)
            {
                if (InitializeRandom(config) is null)
                {
                    return;
                }

                initializedCount++;
            }
        }

        public IEnumerable<TItem> GetUninitialized()
        {
            foreach (TItem item in _items)
            {
                if (!item.IsInitialized())
                {
                    yield return item;
                }
            }
        }

        public IEnumerable<TItem> GetInitialized()
        {
            foreach (TItem item in _items)
            {
                if (item.IsInitialized())
                {
                    yield return item;
                }
            }
        }

        private int CountUninitialized()
        {
            int count = 0;

            foreach (TItem item in _items)
            {
                if (!item.IsInitialized())
                {
                    count++;
                }
            }

            return count;
        }

        private int CountInitialized()
        {
            int count = 0;

            foreach (TItem item in _items)
            {
                if (item.IsInitialized())
                {
                    count++;
                }
            }

            return count;
        }
    }
}
