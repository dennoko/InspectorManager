using UnityEditor;
using UnityEngine;

namespace InspectorManager.Services
{
    /// <summary>
    /// EditorPrefsを使用した永続化サービス実装
    /// </summary>
    public class EditorPrefsPersistence : IPersistenceService
    {
        private const string KeyPrefix = "InspectorManager_";

        private string GetFullKey(string key)
        {
            return KeyPrefix + key;
        }

        public void Save<T>(string key, T data)
        {
            var fullKey = GetFullKey(key);
            var json = JsonUtility.ToJson(new Wrapper<T> { Value = data });
            EditorPrefs.SetString(fullKey, json);
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            var fullKey = GetFullKey(key);

            if (!EditorPrefs.HasKey(fullKey))
            {
                return defaultValue;
            }

            var json = EditorPrefs.GetString(fullKey);
            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
                return wrapper.Value;
            }
            catch
            {
                return defaultValue;
            }
        }

        public void Delete(string key)
        {
            var fullKey = GetFullKey(key);
            EditorPrefs.DeleteKey(fullKey);
        }

        public bool HasKey(string key)
        {
            var fullKey = GetFullKey(key);
            return EditorPrefs.HasKey(fullKey);
        }

        /// <summary>
        /// JsonUtilityでジェネリック型をシリアライズするためのラッパー
        /// </summary>
        [System.Serializable]
        private class Wrapper<T>
        {
            public T Value;
        }
    }
}
