using UnityEditor;
using UnityEngine;

namespace InspectorManager.Core
{
    /// <summary>
    /// ドメインリロード・プレイモード遷移時のライフサイクル管理。
    /// [InitializeOnLoad] によりスクリプトリロード後に自動実行される。
    /// </summary>
    [InitializeOnLoad]
    public static class LifecycleManager
    {
        /// <summary>
        /// 初期化済みフラグ（ドメインリロードごとにリセットされる）
        /// </summary>
        private static bool _subscribed;

        static LifecycleManager()
        {
            if (!_subscribed)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                _subscribed = true;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // プレイモード進入前: ローテーション状態等を保存
                    OnBeforePlayMode();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    // プレイモード復帰後: サービスの再初期化を促す
                    OnAfterPlayMode();
                    break;
            }
        }

        /// <summary>
        /// プレイモード進入前の処理
        /// </summary>
        private static void OnBeforePlayMode()
        {
            // ServiceLocator経由でRotationLockControllerの状態を保存
            // （RotationLockControllerは既にEditorPrefsで状態を永続化しているので、
            //   ここでは追加の保存は不要。ただし一時状態のクリーンアップを行う）
            Debug.Log("[InspectorManager] Preparing for Play Mode...");
        }

        /// <summary>
        /// プレイモード復帰後の処理
        /// </summary>
        private static void OnAfterPlayMode()
        {
            Debug.Log("[InspectorManager] Restoring after Play Mode...");

            // ドメインリロード後、ServiceLocator/EventBusのstaticインスタンスは
            // 新しい空のインスタンスになっている。
            // InspectorManagerWindow.OnEnable() が再度呼ばれて再初期化されるので、
            // ここでは古い状態が残っていた場合のクリーンアップのみ行う。
            SafeCleanup();
        }

        /// <summary>
        /// 古いサービス・イベントハンドラが残っている場合のクリーンアップ
        /// </summary>
        public static void SafeCleanup()
        {
            try
            {
                // EventBusのみクリア。ServiceLocatorの所有権はウィンドウのOnDisable()に委ねる。
                // ServiceLocator.Clear()はIDisposableのDisposeも呼ぶため、
                // 他のコンポーネントが所有するオブジェクトを二重Disposeするリスクがある。
                EventBus.Instance.Clear();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Cleanup warning: {ex.Message}");
            }
        }
    }
}
