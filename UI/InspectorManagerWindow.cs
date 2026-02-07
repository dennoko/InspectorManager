using InspectorManager.Controllers;
using InspectorManager.Core;
using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// Inspector Manager メインウィンドウ
    /// </summary>
    public class InspectorManagerWindow : EditorWindow
    {
        // UI Views
        private InspectorStatusView _inspectorStatusView;
        private HistoryListView _historyListView;
        private FavoritesListView _favoritesListView;
        private InspectorOverlayController _overlayController;

        // Controllers
        private RotationLockController _rotationLockController;
        private HistoryController _historyController;

        // Services
        private IInspectorWindowService _inspectorService;
        private IHistoryService _historyService;
        private IFavoritesService _favoritesService;
        private IPersistenceService _persistenceService;

        // Settings
        private InspectorManagerSettings _settings;

        // UI State
        private int _selectedTab;
        private readonly string[] _tabNames = { "Inspector状態", "履歴", "お気に入り", "設定" };
        private bool _isInitialized;

        [MenuItem("Tools/Inspector Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<InspectorManagerWindow>();
            window.titleContent = new GUIContent("Inspector Manager");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }

        private void OnEnable()
        {
            Initialize();

            // イベント購読
            EventBus.Instance.Subscribe<HistoryUpdatedEvent>(OnHistoryUpdated);
            EventBus.Instance.Subscribe<FavoritesUpdatedEvent>(OnFavoritesUpdated);
            EventBus.Instance.Subscribe<InspectorLockChangedEvent>(OnInspectorLockChanged);
            EventBus.Instance.Subscribe<RotationLockStateChangedEvent>(OnRotationLockStateChanged);
        }

        private void OnDisable()
        {
            // イベント購読解除
            EventBus.Instance.Unsubscribe<HistoryUpdatedEvent>(OnHistoryUpdated);
            EventBus.Instance.Unsubscribe<FavoritesUpdatedEvent>(OnFavoritesUpdated);
            EventBus.Instance.Unsubscribe<InspectorLockChangedEvent>(OnInspectorLockChanged);
            EventBus.Instance.Unsubscribe<RotationLockStateChangedEvent>(OnRotationLockStateChanged);
            
            // オーバーレイコントローラの破棄
            _overlayController?.Dispose();
            _overlayController = null;
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            // サービスの初期化と登録
            _persistenceService = new EditorPrefsPersistence();
            ServiceLocator.Instance.Register<IPersistenceService, EditorPrefsPersistence>(
                (EditorPrefsPersistence)_persistenceService);

            _inspectorService = new InspectorWindowService();
            ServiceLocator.Instance.Register<IInspectorWindowService, InspectorWindowService>(
                (InspectorWindowService)_inspectorService);

            _historyService = new HistoryService(_persistenceService);
            ServiceLocator.Instance.Register<IHistoryService, HistoryService>(
                (HistoryService)_historyService);

            _favoritesService = new FavoritesService(_persistenceService);
            ServiceLocator.Instance.Register<IFavoritesService, FavoritesService>(
                (FavoritesService)_favoritesService);

            // 設定の読み込み
            _settings = _persistenceService.Load("Settings", InspectorManagerSettings.CreateDefault());

            // コントローラーの初期化
            _rotationLockController = new RotationLockController(_inspectorService, _persistenceService);
            if (_settings != null)
            {
                _rotationLockController.BlockFolderSelection = _settings.BlockFolderSelection;
            }
            _historyController = new HistoryController(_historyService, _favoritesService, _settings);

            // ビューの初期化
            _inspectorStatusView = new InspectorStatusView(_inspectorService);
            _historyListView = new HistoryListView(_historyService, _favoritesService);
            _favoritesListView = new FavoritesListView(_favoritesService);

            // オーバーレイ初期化（既存があれば破棄してから）
            _overlayController?.Dispose();
            _overlayController = new InspectorOverlayController(_inspectorService);

            _isInitialized = true;
        }

        private void OnGUI()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            // ヘッダー：ローテーションロックトグル
            DrawHeader();

            EditorGUILayout.Space(4);

            // タブ
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);

            EditorGUILayout.Space(4);

            // タブコンテンツ
            switch (_selectedTab)
            {
                case 0:
                    DrawInspectorStatusTab();
                    break;
                case 1:
                    DrawHistoryTab();
                    break;
                case 2:
                    DrawFavoritesTab();
                    break;
                case 3:
                    DrawSettingsTab();
                    break;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // ローテーションロックトグル
                var isRotationEnabled = _rotationLockController?.IsEnabled ?? false;
                var toggleContent = new GUIContent(
                    isRotationEnabled ? "🔄 ローテーション: ON" : "🔄 ローテーション: OFF",
                    "複数Inspectorを自動でローテーションロック"
                );

                var newValue = GUILayout.Toggle(isRotationEnabled, toggleContent, Styles.ToolbarToggle);
                if (newValue != isRotationEnabled && _rotationLockController != null)
                {
                    _rotationLockController.IsEnabled = newValue;
                }

                GUILayout.FlexibleSpace();

                // Inspector数表示
                var inspectorCount = _inspectorService?.GetAllInspectors().Count ?? 0;
                GUILayout.Label($"Inspector: {inspectorCount}", EditorStyles.toolbarButton);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInspectorStatusTab()
        {
            GUILayout.Label("Inspectorウィンドウ状態", Styles.HeaderLabel);
            _inspectorStatusView?.Draw();

            EditorGUILayout.Space(8);

            // ローテーション情報
            if (_rotationLockController != null && _rotationLockController.IsEnabled)
            {
                EditorGUILayout.HelpBox(
                    $"ローテーション有効\n次の更新対象: Inspector {_rotationLockController.CurrentTargetIndex + 1}",
                    MessageType.Info);

                if (GUILayout.Button("手動でローテーション"))
                {
                    _rotationLockController.RotateToNext();
                }
            }
        }

        private void DrawHistoryTab()
        {
            _historyListView?.Draw();
        }

        private void DrawFavoritesTab()
        {
            _favoritesListView?.Draw();
        }

        private void DrawSettingsTab()
        {
            GUILayout.Label("設定", Styles.HeaderLabel);

            EditorGUI.BeginChangeCheck();

            // 履歴設定
            EditorGUILayout.LabelField("履歴", EditorStyles.boldLabel);

            _settings.MaxHistoryCount = EditorGUILayout.IntSlider(
                "最大履歴数", _settings.MaxHistoryCount, 10, 200);

            _settings.RecordSceneObjects = EditorGUILayout.Toggle(
                "シーンオブジェクトを記録", _settings.RecordSceneObjects);

            _settings.RecordAssets = EditorGUILayout.Toggle(
                "アセットを記録", _settings.RecordAssets);

            _settings.AutoCleanInvalidHistory = EditorGUILayout.Toggle(
                "無効なエントリを自動削除", _settings.AutoCleanInvalidHistory);

            bool newBlockFolderSelection = EditorGUILayout.Toggle(
                "フォルダ選択時の更新をブロック", _settings.BlockFolderSelection);
            
            if (newBlockFolderSelection != _settings.BlockFolderSelection)
            {
                _settings.BlockFolderSelection = newBlockFolderSelection;
                if (_rotationLockController != null)
                {
                    _rotationLockController.BlockFolderSelection = newBlockFolderSelection;
                }
            }

            EditorGUILayout.Space(8);

            // ショートカット情報
            EditorGUILayout.LabelField("ショートカット", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Ctrl+L: アクティブInspectorのロック切り替え\n" +
                "Ctrl+Shift+L: 全Inspectorのロック切り替え\n" +
                "Ctrl+[: 履歴を戻る\n" +
                "Ctrl+]: 履歴を進む\n" +
                "Ctrl+D: お気に入りに追加/削除",
                MessageType.None);

            EditorGUILayout.Space(8);

            // メンテナンス
            EditorGUILayout.LabelField("メンテナンス", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("無効なエントリを削除"))
                {
                    _historyController?.CleanupAll();
                }
                if (GUILayout.Button("全データをリセット"))
                {
                    if (EditorUtility.DisplayDialog(
                        "確認",
                        "履歴・お気に入り・設定をすべてリセットしますか？",
                        "リセット", "キャンセル"))
                    {
                        _historyService?.ClearHistory();
                        var favorites = _favoritesService?.GetFavorites();
                        if (favorites != null)
                        {
                            foreach (var fav in favorites)
                            {
                                var obj = fav.GetObject();
                                if (obj != null)
                                {
                                    _favoritesService.RemoveFavorite(obj);
                                }
                            }
                        }
                        _settings = InspectorManagerSettings.CreateDefault();
                        SaveSettings();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            _persistenceService?.Save("Settings", _settings);

            // 履歴サービスに反映
            if (_historyService != null)
            {
                _historyService.MaxHistoryCount = _settings.MaxHistoryCount;
            }
        }

        // イベントハンドラー
        private void OnHistoryUpdated(HistoryUpdatedEvent evt) => Repaint();
        private void OnFavoritesUpdated(FavoritesUpdatedEvent evt) => Repaint();
        private void OnInspectorLockChanged(InspectorLockChangedEvent evt) => Repaint();
        private void OnRotationLockStateChanged(RotationLockStateChangedEvent evt) => Repaint();
    }
}
