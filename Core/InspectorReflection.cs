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
        private static PropertyInfo _trackerProperty;
        private static FieldInfo _trackerField;
        private static MethodInfo _forceRebuildMethod;
        private static MethodInfo _setObjectsLockedMethod;
        private static MethodInfo _setObjectsLockedOnWindow;
        private static MethodInfo _getObjectsLockedOnWindow;
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

                // PropertyEditor.tracker（public）。ゲッターが CreateTracker() を呼ぶため、
                // まだトラッカーが生成されていないInspectorでも確実に取得できる。
                _trackerProperty = _inspectorWindowType.GetProperty(
                    "tracker",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                // m_Tracker はプロパティが見つからない場合のフォールバック。
                // こちらは未生成だと null を返すので単独では使わない。
                _trackerField = _inspectorWindowType.GetField(
                    "m_Tracker",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                // InspectorWindow.SetObjectsLocked(List<Object>) は Unity 自身が使う内部APIで、
                // 「ロック状態にする」と「表示対象を差し替える」を一括で行う。
                // トラッカーを直接叩くとロックの復旧が漏れるため、こちらを優先する。
                _setObjectsLockedOnWindow = _inspectorWindowType.GetMethod(
                    "SetObjectsLocked",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new Type[] { typeof(List<UnityEngine.Object>) },
                    null
                );

                _getObjectsLockedOnWindow = _inspectorWindowType.GetMethod(
                    "GetObjectsLocked",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new Type[] { typeof(List<UnityEngine.Object>) },
                    null
                );

                // ActiveEditorTracker 側のAPI（SetObjectsLocked が無い場合のフォールバック）
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

                // FlushAllOptimizedGUIBlocksIfNeeded（Inspector内部の再描画強制用）。
                // static メソッドなので Instance で引くと取得できない。
                _flushOptimizedGUI = _inspectorWindowType.GetMethod(
                    "FlushAllOptimizedGUIBlocksIfNeeded",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
                );

                bool trackerAccessible = _trackerProperty != null || _trackerField != null;
                _directUpdateAvailable = _setObjectsLockedOnWindow != null
                    || (trackerAccessible && _setObjectsLockedMethod != null);

                if (!_directUpdateAvailable)
                {
                    // 直接更新が使えないのは想定外の状況なので警告する。
                    // 使える場合は正常系であり、ドメインリロードのたびに
                    // Console を汚さないよう何も出力しない。
                    Debug.LogWarning(
                        "[InspectorManager] Direct Inspector update is unavailable on this Unity version. " +
                        "Falling back to the lock/unlock method.");
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
                var tracker = GetTracker(inspector);
                if (tracker != null && tracker.activeEditors.Length > 0)
                {
                    return tracker.activeEditors[0].target;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Failed to get inspected object: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// InspectorのActiveEditorTrackerを取得する。
        ///
        /// m_Tracker フィールドを直接読むと、まだ CreateTracker() が呼ばれていない
        /// Inspector では null が返り、そのInspectorだけ更新に失敗する。
        /// tracker プロパティはゲッター内で CreateTracker() を呼ぶため、
        /// 必ずこちらを優先する。
        /// </summary>
        private static ActiveEditorTracker GetTracker(EditorWindow inspector)
        {
            if (inspector == null) return null;

            if (_trackerProperty != null)
            {
                var fromProperty = _trackerProperty.GetValue(inspector) as ActiveEditorTracker;
                if (fromProperty != null) return fromProperty;
            }

            return _trackerField?.GetValue(inspector) as ActiveEditorTracker;
        }

        /// <summary>
        /// Inspectorがロック対象として保持しているオブジェクトを取得する。
        /// ドメインリロード後など、こちらの記録と実際の表示がずれた場合の
        /// 突き合わせに使う。取得できない場合は false。
        /// </summary>
        public static bool TryGetLockedObjects(EditorWindow inspector, List<UnityEngine.Object> result)
        {
            if (!IsAvailable || inspector == null || result == null) return false;

            result.Clear();

            // activeEditors はコンポーネントのEditorまで含むため代用できない。
            // ロック対象そのものを返す内部APIが無ければ諦める。
            if (_getObjectsLockedOnWindow == null) return false;

            try
            {
                _getObjectsLockedOnWindow.Invoke(inspector, new object[] { result });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Failed to read locked objects: {ex.Message}");
                return false;
            }
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
            if (targetObject == null) return false;
            return SetInspectedObjects(inspector, new[] { targetObject });
        }

        /// <summary>
        /// ロック状態のInspectorウィンドウの表示対象を複数まとめて設定する。
        /// SetObjectsLockedByThisTracker はリストを受け取るため、
        /// Unity標準と同じマルチ編集表示を再現できる。
        /// </summary>
        /// <returns>成功した場合true</returns>
        public static bool SetInspectedObjects(EditorWindow inspector, IList<UnityEngine.Object> targetObjects)
        {
            if (!IsAvailable || inspector == null) return false;
            if (targetObjects == null || targetObjects.Count == 0) return false;
            if (!_directUpdateAvailable) return false;

            var objectsList = new List<UnityEngine.Object>(targetObjects.Count);
            for (int i = 0; i < targetObjects.Count; i++)
            {
                if (targetObjects[i] != null) objectsList.Add(targetObjects[i]);
            }
            if (objectsList.Count == 0) return false;

            try
            {
                if (_setObjectsLockedOnWindow != null)
                {
                    // Unity内部の SetObjectsLocked は isLocked=true の設定と
                    // 表示対象の差し替えを一括で行う。
                    // 何らかの理由でロックが外れていた場合もここで復旧するため、
                    // 「アンロック状態のInspectorに書き込んで即座に選択で上書きされる」
                    // という取りこぼしが起きない。
                    _setObjectsLockedOnWindow.Invoke(inspector, new object[] { objectsList });
                }
                else
                {
                    var fallbackTracker = GetTracker(inspector);
                    if (fallbackTracker == null) return false;

                    // トラッカー経由の場合はロックを自分で立てる必要がある
                    fallbackTracker.isLocked = true;
                    _setObjectsLockedMethod.Invoke(fallbackTracker, new object[] { objectsList });
                }

                // ForceRebuildで即時にEditorを再構築
                var tracker = GetTracker(inspector);
                if (tracker != null && _forceRebuildMethod != null)
                {
                    _forceRebuildMethod.Invoke(tracker, null);
                }

                // GUI最適化ブロックのフラッシュ（表示の即時更新）。staticメソッド。
                if (_flushOptimizedGUI != null)
                {
                    _flushOptimizedGUI.Invoke(null, null);
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
