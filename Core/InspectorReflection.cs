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
        private static MethodInfo _forceRebuildMethod;
        private static MethodInfo _setObjectsLockedMethod;
        private static MethodInfo _flushOptimizedGUI;
        private static bool _initialized;
        private static bool _initializationFailed;
        private static bool _directUpdateAvailable;

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

                // ActiveEditorTracker.SetObjectsLockedByThisTracker を取得（直接更新用）
                if (_trackerField != null)
                {
                    var trackerType = typeof(ActiveEditorTracker);
                    _setObjectsLockedMethod = trackerType.GetMethod(
                        "SetObjectsLockedByThisTracker",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        new Type[] { typeof(List<UnityEngine.Object>) },
                        null
                    );
                    _forceRebuildMethod = trackerType.GetMethod(
                        "ForceRebuild",
                        BindingFlags.Instance | BindingFlags.Public
                    );
                }

                // FlushAllOptimizedGUIBlocksIfNeeded（Inspector内部の再描画強制用）
                _flushOptimizedGUI = _inspectorWindowType.GetMethod(
                    "FlushAllOptimizedGUIBlocksIfNeeded",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                _directUpdateAvailable = (_trackerField != null && _setObjectsLockedMethod != null);
                if (_directUpdateAvailable)
                {
                    Debug.Log("[InspectorManager] Direct Inspector update mode available.");
                }
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

        /// <summary>
        /// 直接更新モードが利用可能かどうか
        /// </summary>
        public static bool IsDirectUpdateAvailable
        {
            get
            {
                EnsureInitialized();
                return _directUpdateAvailable;
            }
        }

        /// <summary>
        /// ロック状態のInspectorウィンドウの表示対象を直接変更する。
        /// アンロック/再ロックを行わずに同期的に更新できる。
        /// </summary>
        /// <returns>成功した場合true</returns>
        public static bool SetInspectedObject(EditorWindow inspector, UnityEngine.Object targetObject)
        {
            if (!IsAvailable || inspector == null || targetObject == null) return false;
            if (!_directUpdateAvailable) return false;

            try
            {
                var tracker = _trackerField.GetValue(inspector) as ActiveEditorTracker;
                if (tracker == null) return false;

                // SetObjectsLockedByThisTrackerでロック中のInspectorに
                // 新しいオブジェクトを強制的に設定する
                var objectsList = new List<UnityEngine.Object> { targetObject };
                _setObjectsLockedMethod.Invoke(tracker, new object[] { objectsList });

                // TrackerのForceRebuildで即時にEditorを再構築
                if (_forceRebuildMethod != null)
                {
                    _forceRebuildMethod.Invoke(tracker, null);
                }

                // GUI最適化ブロックのフラッシュ（表示の即時更新）
                if (_flushOptimizedGUI != null)
                {
                    _flushOptimizedGUI.Invoke(inspector, null);
                }

                inspector.Repaint();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Direct update failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 新しいInspectorウィンドウを生成する
        /// </summary>
        /// <returns>生成されたInspectorウィンドウ。失敗時はnull</returns>
        public static EditorWindow CreateNewInspector()
        {
            EnsureInitialized();
            if (_inspectorWindowType == null) return null;

            try
            {
                var inspector = ScriptableObject.CreateInstance(_inspectorWindowType) as EditorWindow;
                if (inspector != null)
                {
                    inspector.Show();
                }
                return inspector;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] Failed to create Inspector: {ex.Message}");
                return null;
            }
        }
    }
}
