using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Storage.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Assets.Scripts.Storage
{
    public static class AppStorage
    {
        private static readonly string _settingsFilePath = Path.Combine(GetDataDirectoryPath(), GetStorageFileName());

        private static Dictionary<string, JToken> _settingsCache;

        static AppStorage()
        {
            Load();
        }

        public static bool TryGetValue<T>(string key, out T value)
        {
            if (_settingsCache.TryGetValue(key, out var result))
            {
                try
                {
                    value = result.ToObject<T>();
                    return true;
                }
                catch (Exception)
                {
                }
            }

            value = default;
            return false;
        }

        public static void SetValue<T>(string key, T value)
        {
            try
            {
                string dataDirectory = GetDataDirectoryPath();
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                _settingsCache[key] = JToken.FromObject(value);
                var json = JsonConvert.SerializeObject(_settingsCache, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppStorage] Failed to set value for key '{key}': {ex.Message}");
            }
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
                    _settingsCache = obj ?? new Dictionary<string, JToken>();
                }
                else
                {
                    _settingsCache = new Dictionary<string, JToken>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppStorage] Failed to load settings file at '{_settingsFilePath}': {ex.Message}");
                _settingsCache = new Dictionary<string, JToken>();
            }
        }

        private static string GetDataDirectoryPath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, StorageConstants.DATA_DIRECTORY_NAME);
#else
            return Path.Combine(GetBuildRootDirectoryPath(), StorageConstants.DATA_DIRECTORY_NAME);
#endif
        }

        private static string GetBuildRootDirectoryPath()
        {
            var dataDirectoryInfo = Directory.GetParent(Application.dataPath);

            if (dataDirectoryInfo != null)
            {
                return dataDirectoryInfo.FullName;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetStorageFileName()
        {
#if UNITY_EDITOR
            return StorageConstants.EDITOR_STORAGE_FILE_NAME;
#else
            return StorageConstants.STORAGE_FILE_NAME;
#endif
        }
    }
}
