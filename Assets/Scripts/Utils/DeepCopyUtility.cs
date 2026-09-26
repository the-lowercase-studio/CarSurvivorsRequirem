using System;
using Assets.Scripts.Stats;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public static class DeepCopyUtility
    {
        public static T DeepCopy<T>(T obj)
        {
            if (obj == null)
            {
                return default;
            }

            if (obj is ICloneable cloneable)
            {
                return (T)cloneable.Clone();
            }

            if (obj is UpgradeableStat<float> floatStat)
            {
                return (T)(object)floatStat.Clone();
            }

            if (obj is UpgradeableStat<int> intStat)
            {
                return (T)(object)intStat.Clone();
            }

            string json = JsonUtility.ToJson(obj);
            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                return default;
            }

            return JsonUtility.FromJson<T>(json);
        }
    }
}
