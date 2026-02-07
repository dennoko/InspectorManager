using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Core
{
    /// <summary>
    /// Unity内部のInspectorWindow APIへのリフレクションアクセスを提供する。
    /// </summary>
    public static class InspectorReflection
    {
        private static Type _inspectorWindowType;
        private static PropertyInfo _isLockedProperty;
        private static FieldInfo _trackerField;
        private static bool _initialized;
        private static bool _initializationFailed;

        /// <summary>
        /// InspectorWindowの型を取得
        /// </summary>
        public static Type InspectorWindowType
        {
            get
            {
                EnsureInitialized();
                return _inspectorWindowType;
            }
        }

        /// <summary>
        /// リフレクションが正常に初期化されたかどうか
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return !_initializationFailed;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // UnityEditor.InspectorWindow型を取得
                _inspectorWindowType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                if (_inspectorWindowType == null)
                {
                    Debug.LogError("[InspectorManager] InspectorWindow type not found");
                    _initializationFailed = true;
                    return;
                }

                // isLockedプロパティを取得
                _isLockedProperty = _inspectorWindowType.GetProperty(
                    "isLocked",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (_isLockedProperty == null)
                {
                    Debug.LogError("[InspectorManager] isLocked property not found");
                    _initializationFailed = true;
                    return;
                }

                // trackerフィールドを取得（表示中オブジェクト取得用）
                _trackerField = _inspectorWindowType.GetField(
                    "m_Tracker",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] Reflection initialization failed: {ex.Message}");
                _initializationFailed = true;
            }
        }

        /// <summary>
        /// 現在開いているすべてのInspectorウィンドウを取得
        /// </summary>
        public static List<EditorWindow> GetAllInspectorWindows()
        {
            var result = new List<EditorWindow>();

            if (!IsAvailable) return result;

            var allWindows = Resources.FindObjectsOfTypeAll(_inspectorWindowType);
            foreach (var window in allWindows)
            {
                if (window is EditorWindow editorWindow)
                {
                    result.Add(editorWindow);
                }
            }

            return result;
        }

        /// <summary>
        /// 指定したInspectorウィンドウのロック状態を取得
        /// </summary>
        public static bool GetLockedState(EditorWindow inspector)
        {
            if (!IsAvailable || inspector == null) return false;

            try
            {
                return (bool)_isLockedProperty.GetValue(inspector);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Failed to get locked state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 指定したInspectorウィンドウのロック状態を設定
        /// </summary>
        public static void SetLockedState(EditorWindow inspector, bool locked)
        {
            if (!IsAvailable || inspector == null) return;

            try
            {
                _isLockedProperty.SetValue(inspector, locked);
                inspector.Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Failed to set locked state: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウがInspectorWindowかどうかを判定
        /// </summary>
        public static bool IsInspectorWindow(EditorWindow window)
        {
            if (!IsAvailable || window == null) return false;
            return _inspectorWindowType.IsInstanceOfType(window);
        }

        /// <summary>
        /// 指定したInspectorウィンドウが表示しているオブジェクトを取得
        /// </summary>
        public static UnityEngine.Object GetInspectedObject(EditorWindow inspector)
        {
            if (!IsAvailable || inspector == null) return null;

            try
            {
                // ActiveEditorTrackerを使用して表示中のオブジェクトを取得
                if (_trackerField != null)
                {
                    var tracker = _trackerField.GetValue(inspector) as ActiveEditorTracker;
                    if (tracker != null && tracker.activeEditors.Length > 0)
                    {
                        return tracker.activeEditors[0].target;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Failed to get inspected object: {ex.Message}");
            }

            return null;
        }
    }
}
