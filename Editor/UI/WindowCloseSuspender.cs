using InspectorManager.Controllers;
using InspectorManager.Services;
using UnityEditor;

namespace InspectorManager.UI
{
    /// <summary>
    /// プレイモード中に Inspector Manager ウィンドウが破棄されたときの後始末を保留する。
    ///
    /// 「Play Maximized」ではメインウィンドウ内のウィンドウが最大化の際に破棄され、
    /// プレイモード終了時に作り直される。これを「閉じた」と扱って即座に
    /// ロックを戻すと、ローテーション状態が失われてしまう。
    /// そこで判断をプレイモード終了後まで遅らせ、猶予時間内にウィンドウが
    /// 作り直されなかった場合（＝本当に閉じられた場合）だけ後始末を行う。
    ///
    /// 保留状態は SessionState に置くため、プレイモード往復時の
    /// ドメインリロードを跨いでも失われない。
    /// </summary>
    [InitializeOnLoad]
    internal static class WindowCloseSuspender
    {
        private const string PendingKey = "InspectorManager.PendingSuspend";

        // 最大化の解除でウィンドウが作り直されるのを待つ時間
        private const double GraceSeconds = 3.0;

        private static double _deadline;
        private static bool _watching;

        static WindowCloseSuspender()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // プレイモード終了時のドメインリロード後は EnteredEditMode より先にここへ来る
            if (IsPending && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StartWatching();
            }
        }

        private static bool IsPending => SessionState.GetBool(PendingKey, false);

        public static void RequestAfterPlayMode()
        {
            SessionState.SetBool(PendingKey, true);
        }

        public static void Cancel()
        {
            SessionState.EraseBool(PendingKey);
            StopWatching();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && IsPending)
            {
                StartWatching();
            }
        }

        private static void StartWatching()
        {
            _deadline = EditorApplication.timeSinceStartup + GraceSeconds;
            if (_watching) return;

            _watching = true;
            EditorApplication.update += OnUpdate;
        }

        private static void StopWatching()
        {
            if (!_watching) return;

            _watching = false;
            EditorApplication.update -= OnUpdate;
        }

        private static void OnUpdate()
        {
            if (!IsPending || EditorWindow.HasOpenInstances<InspectorManagerWindow>())
            {
                Cancel();
                return;
            }

            if (EditorApplication.timeSinceStartup < _deadline) return;

            Cancel();
            RotationLockController.SuspendForWindowClose(new InspectorWindowService());
        }
    }
}
