using Assets.Scripts.Stats;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
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

            string json = JsonUtility.ToJson(obj);
            if (!string.IsNullOrEmpty(json) && json != "{}")
            {
                return JsonUtility.FromJson<T>(json);
            }

            using (var ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                T result = (T)formatter.Deserialize(ms);

                if (obj is IUpgradeableStat sourceStat && result is IUpgradeableStat resultStat)
                {
                    resultStat.SetIcon(sourceStat.Icon);
                }

                return result;
            }
        }
    }
}
