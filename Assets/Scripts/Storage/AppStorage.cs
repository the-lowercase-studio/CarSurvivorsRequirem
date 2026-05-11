using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Assets.Scripts.Storage
{
    public static class AppStorage
    {
        private const string DATA_DIRECTORY_NAME = "Data";
        private const string STORAGE_FILE_NAME = "AppStorage.json";
        private const string EDITOR_STORAGE_FILE_NAME = "AppStorage.Editor.json";

        private static readonly string SettingsFilePath = Path.Combine(GetDataDirectoryPath(), GetStorageFileName());

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
            string dataDirectory = GetDataDirectoryPath();
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            _settingsCache[key] = JToken.FromObject(value);
            var json = JsonConvert.SerializeObject(_settingsCache, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }

        private static void Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var obj = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
                _settingsCache = obj ?? new Dictionary<string, JToken>();
            }
            else
            {
                _settingsCache = new Dictionary<string, JToken>();
            }
        }

        private static string GetDataDirectoryPath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, DATA_DIRECTORY_NAME);
#else
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DATA_DIRECTORY_NAME);
#endif
        }

        private static string GetStorageFileName()
        {
#if UNITY_EDITOR
            return EDITOR_STORAGE_FILE_NAME;
#else
            return STORAGE_FILE_NAME;
#endif
        }
    }
}
