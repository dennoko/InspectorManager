using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Services
{
    /// <summary>
    /// EditorPrefsを使用した永続化サービス実装。
    ///
    /// EditorPrefs は Windows ではレジストリへの書き込みになる。
    /// 選択のたびに履歴全体を、スライダー操作のたびに設定全体を書き直すと
    /// 非常に重くなるため、短時間の連続書き込みをまとめてフラッシュする。
    /// 未フラッシュの値も Load/HasKey から見えるので、呼び出し側は
    /// 遅延を意識しなくてよい。
    /// </summary>
    public class EditorPrefsPersistence : IPersistenceService, IDisposable
    {
        private const string KeyPrefix = "InspectorManager_";
        private const double FlushDelaySeconds = 1.0;

        /// <summary>まだ EditorPrefs へ書き出していない値（フルキー → JSON）</summary>
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>();

        private double _flushDueTime;
        private bool _updateSubscribed;
        private bool _disposed;

        public EditorPrefsPersistence()
        {
            // ドメインリロードとエディタ終了で確実に書き出す
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
            EditorApplication.quitting += Flush;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            AssemblyReloadEvents.beforeAssemblyReload -= Flush;
            EditorApplication.quitting -= Flush;
            UnsubscribeUpdate();

            Flush();
        }

        private string GetFullKey(string key)
        {
            return KeyPrefix + key;
        }

        public void Save<T>(string key, T data)
        {
            var fullKey = GetFullKey(key);
            var json = JsonUtility.ToJson(new Wrapper<T> { Value = data });

            _pending[fullKey] = json;
            ScheduleFlush();
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            var fullKey = GetFullKey(key);

            // 未フラッシュの値があればそちらが最新
            if (_pending.TryGetValue(fullKey, out var pendingJson))
            {
                return Deserialize(pendingJson, defaultValue);
            }

            if (!EditorPrefs.HasKey(fullKey))
            {
                return defaultValue;
            }

            return Deserialize(EditorPrefs.GetString(fullKey), defaultValue);
        }

        private static T Deserialize<T>(string json, T defaultValue)
        {
            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
                return wrapper == null ? defaultValue : wrapper.Value;
            }
            catch
            {
                return defaultValue;
            }
        }

        public void Delete(string key)
        {
            var fullKey = GetFullKey(key);
            _pending.Remove(fullKey);
            EditorPrefs.DeleteKey(fullKey);
        }

        public bool HasKey(string key)
        {
            var fullKey = GetFullKey(key);
            return _pending.ContainsKey(fullKey) || EditorPrefs.HasKey(fullKey);
        }

        /// <summary>
        /// 保留中の書き込みを即座に EditorPrefs へ反映する
        /// </summary>
        public void Flush()
        {
            if (_pending.Count == 0)
            {
                UnsubscribeUpdate();
                return;
            }

            foreach (var kvp in _pending)
            {
                EditorPrefs.SetString(kvp.Key, kvp.Value);
            }
            _pending.Clear();

            UnsubscribeUpdate();
        }

        private void ScheduleFlush()
        {
            _flushDueTime = EditorApplication.timeSinceStartup + FlushDelaySeconds;

            if (_updateSubscribed) return;
            _updateSubscribed = true;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _flushDueTime) return;
            Flush();
        }

        private void UnsubscribeUpdate()
        {
            if (!_updateSubscribed) return;
            _updateSubscribed = false;
            EditorApplication.update -= OnEditorUpdate;
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
