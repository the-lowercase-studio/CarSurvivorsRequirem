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
            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                return default;
            }

            return JsonUtility.FromJson<T>(json);
        }
    }
}
