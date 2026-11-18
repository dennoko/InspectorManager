using InspectorManager.Controllers;
using InspectorManager.Core;
using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;

namespace InspectorManager.UI
{
    /// <summary>
    /// Inspector Manager のバックグラウンド実行基盤（サービス・コントローラー・オーバーレイ）の
    /// ライフサイクルを管理する。
    ///
    /// InspectorManagerWindow が非アクティブタブに隠れた状態でドメインリロード（プレイモード開始や
    /// スクリプト再コンパイル）が起きると、Unity は再ロード後にそのタブの OnEnable() を呼ばない。
    /// ウィンドウの OnEnable/OnDisable にコントローラーの寿命を紐付けていると、
    /// プレイモードから戻った後もローテーションが永久に停止したままになる。
    ///
    /// そこで実行基盤の寿命をウィンドウインスタンスの開閉（HasOpenInstances / OnDestroy）に合わせ、
    /// ドメインリロードやプレイモード復帰時にも自動的に再起動させる。
    /// </summary>
    [InitializeOnLoad]
    internal static class InspectorManagerHost
    {
        internal struct RuntimeContext
        {
            public IPersistenceService PersistenceService;
            public ILocalizationService LocalizationService;
            public IInspectorWindowService InspectorService;
            public IHistoryService HistoryService;
            public IFavoritesService FavoritesService;
            public InspectorManagerSettings Settings;
            public RotationLockController RotationLockController;
            public HistoryController HistoryController;
        }

        private static bool _isStarted;
        private static bool _restoreScheduled;
        private static ServiceInitializer.InitializationResult _services;
        private static RotationLockController _rotationLockController;
        private static HistoryController _historyController;
        private static InspectorOverlayController _overlayController;

        static InspectorManagerHost()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ScheduleRestoreIfWindowOpen();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                ScheduleRestoreIfWindowOpen();
            }
        }

        private static void ScheduleRestoreIfWindowOpen()
        {
            if (_restoreScheduled) return;
            _restoreScheduled = true;

            EditorApplication.delayCall += () =>
            {
                _restoreScheduled = false;
                if (_isStarted) return;
                if (!EditorWindow.HasOpenInstances<InspectorManagerWindow>()) return;

                WindowCloseSuspender.Cancel();
                EnsureStarted();
            };
        }

        public static RuntimeContext EnsureStarted()
        {
            if (!_isStarted)
            {
                _services = ServiceInitializer.InitializeAll();

                _rotationLockController = new RotationLockController(
                    _services.InspectorService,
                    _services.PersistenceService,
                    _services.HistoryService);
                _rotationLockController.ApplySettings(_services.Settings);
                ServiceLocator.Instance.Register(_rotationLockController);

                _historyController = new HistoryController(
                    _services.HistoryService,
                    _services.FavoritesService,
                    _services.Settings);

                _overlayController = new InspectorOverlayController(
                    _services.InspectorService,
                    _services.LocalizationService,
                    _rotationLockController);

                _isStarted = true;
            }

            return new RuntimeContext
            {
                PersistenceService = _services.PersistenceService,
                LocalizationService = _services.LocalizationService,
                InspectorService = _services.InspectorService,
                HistoryService = _services.HistoryService,
                FavoritesService = _services.FavoritesService,
                Settings = _services.Settings,
                RotationLockController = _rotationLockController,
                HistoryController = _historyController,
            };
        }

        public static void UpdateSettings(InspectorManagerSettings settings)
        {
            if (!_isStarted || settings == null) return;
            _services.Settings = settings;
        }

        public static void Shutdown()
        {
            if (!_isStarted) return;
            _isStarted = false;

            _overlayController?.Dispose();
            _overlayController = null;

            _historyController?.Dispose();
            _historyController = null;

            if (_rotationLockController != null)
            {
                if (ReferenceEquals(ServiceLocator.Instance.TryResolve<RotationLockController>(), _rotationLockController))
                {
                    ServiceLocator.Instance.Unregister<RotationLockController>();
                }
                else
                {
                    _rotationLockController.Dispose();
                }
                _rotationLockController = null;
            }

            ServiceLocator.Instance.Clear();
            EventBus.Instance.Clear();
            _services = default;
        }
    }
}
