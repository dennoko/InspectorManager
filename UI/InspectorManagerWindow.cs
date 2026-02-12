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
        private ILocalizationService _localizationService;

        // Settings
        private InspectorManagerSettings _settings;

        // UI State
        private int _selectedTab;
        // タブ名は都度取得するように変更するため削除
        private bool _isInitialized;

        [MenuItem("dennokoworks/Inspector Manager")]
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
            EventBus.Instance.Subscribe<RotationUpdateCompletedEvent>(OnRotationUpdateCompleted);
            
            if (_localizationService != null)
                _localizationService.OnLanguageChanged += Repaint;
        }

        private void OnDisable()
        {
            // イベント購読解除
            EventBus.Instance.Unsubscribe<HistoryUpdatedEvent>(OnHistoryUpdated);
            EventBus.Instance.Unsubscribe<FavoritesUpdatedEvent>(OnFavoritesUpdated);
            EventBus.Instance.Unsubscribe<InspectorLockChangedEvent>(OnInspectorLockChanged);
            EventBus.Instance.Unsubscribe<RotationLockStateChangedEvent>(OnRotationLockStateChanged);
            EventBus.Instance.Unsubscribe<RotationUpdateCompletedEvent>(OnRotationUpdateCompleted);
            
            if (_localizationService != null)
                _localizationService.OnLanguageChanged -= Repaint;

            // コントローラーの確実な破棄
            _rotationLockController?.Dispose();
            _rotationLockController = null;

            _historyController?.Dispose();
            _historyController = null;

            // オーバーレイコントローラの破棄
            _overlayController?.Dispose();
            _overlayController = null;

            _isInitialized = false;
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            // 既存サービスが残っていれば先にクリア（ドメインリロード後の二重登録防止）
            ServiceLocator.Instance.Clear();
            EventBus.Instance.Clear();

            // サービスの初期化と登録
            _persistenceService = new EditorPrefsPersistence();
            ServiceLocator.Instance.Register<IPersistenceService, EditorPrefsPersistence>(
                (EditorPrefsPersistence)_persistenceService);

            _localizationService = new LocalizationService(_persistenceService);
            ServiceLocator.Instance.Register<ILocalizationService, LocalizationService>(
                (LocalizationService)_localizationService);

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
            
            // 言語設定の反映
            ((LocalizationService)_localizationService).Initialize(_settings.Language);
            
            // ウィンドウタイトル更新
            titleContent = new GUIContent(_localizationService.GetString("Window_Title"));

            // コントローラーの初期化
            _rotationLockController = new RotationLockController(_inspectorService, _persistenceService);
            if (_settings != null)
            {
                _rotationLockController.BlockFolderSelection = _settings.BlockFolderSelection;
                _rotationLockController.AutoFocusOnUpdate = _settings.AutoFocusOnUpdate;
            }
            _historyController = new HistoryController(_historyService, _favoritesService, _settings);

            // ビューの初期化
            _inspectorStatusView = new InspectorStatusView(
                _inspectorService, 
                _rotationLockController,
                _localizationService);
            _historyListView = new HistoryListView(_historyService, _favoritesService, _localizationService);
            _favoritesListView = new FavoritesListView(_favoritesService, _localizationService);

            // オーバーレイ初期化（既存があれば破棄してから）
            _overlayController?.Dispose();
            _overlayController?.Dispose();
            _overlayController = new InspectorOverlayController(
                _inspectorService, 
                _localizationService,
                _rotationLockController);

            _isInitialized = true;
        }

        private void OnGUI()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            // 初期化失敗時は中断
            if (_localizationService == null) return;

            // ヘッダー：ローテーションロックトグル
            DrawHeader();

            EditorGUILayout.Space(4);

            // タブ
            var tabNames = new string[] {
                _localizationService.GetString("Tab_Status"),
                _localizationService.GetString("Tab_History"),
                _localizationService.GetString("Tab_Favorites"),
                _localizationService.GetString("Tab_Settings")
            };
            _selectedTab = GUILayout.Toolbar(_selectedTab, tabNames);

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
                var toggleText = isRotationEnabled 
                    ? _localizationService.GetString("Rotation_On") 
                    : _localizationService.GetString("Rotation_Off");
                    
                var toggleContent = new GUIContent(
                    toggleText,
                    _localizationService.GetString("Rotation_Tooltip")
                );

                var newValue = GUILayout.Toggle(isRotationEnabled, toggleContent, Styles.ToolbarToggle);
                if (newValue != isRotationEnabled && _rotationLockController != null)
                {
                    _rotationLockController.IsEnabled = newValue;
                }

                GUILayout.FlexibleSpace();

                // Inspector数表示
                var inspectorCount = _inspectorService?.GetAllInspectors().Count ?? 0;
                GUILayout.Label(_localizationService.GetString("Inspector_Count", inspectorCount), EditorStyles.toolbarButton);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInspectorStatusTab()
        {
            GUILayout.Label(_localizationService.GetString("Header_Status"), Styles.HeaderLabel);
            _inspectorStatusView?.Draw();

            EditorGUILayout.Space(8);

            // ローテーション情報
            if (_rotationLockController != null && _rotationLockController.IsEnabled)
            {
                EditorGUILayout.HelpBox(
                    _localizationService.GetString("Rotation_Active", _rotationLockController.CurrentTargetIndex + 1),
                    MessageType.Info);

                if (GUILayout.Button(_localizationService.GetString("Button_ManualRotate")))
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
            GUILayout.Label(_localizationService.GetString("Header_Settings"), Styles.HeaderLabel);

            EditorGUI.BeginChangeCheck();

            // 言語設定
            GUILayout.Label(_localizationService.GetString("Setting_Language"), EditorStyles.boldLabel);
            var languages = new string[] { "日本語", "English" };
            var currentLangIndex = _settings.Language == "en" ? 1 : 0;
            var newLangIndex = GUILayout.Toolbar(currentLangIndex, languages);
            if (newLangIndex != currentLangIndex)
            {
                var newLang = newLangIndex == 1 ? "en" : "ja";
                _settings.Language = newLang;
                if (_localizationService is LocalizationService ls)
                {
                    ls.CurrentLanguage = newLang;
                }
                // タイトル即時更新
                titleContent = new GUIContent(_localizationService.GetString("Window_Title"));
            }

            EditorGUILayout.Space(8);

            // 履歴設定
            EditorGUILayout.LabelField(_localizationService.GetString("Settings_History"), EditorStyles.boldLabel);

            _settings.MaxHistoryCount = EditorGUILayout.IntSlider(
                _localizationService.GetString("Settings_MaxHistory"), _settings.MaxHistoryCount, 10, 200);

            _settings.RecordSceneObjects = EditorGUILayout.Toggle(
                _localizationService.GetString("Settings_RecordScene"), _settings.RecordSceneObjects);

            _settings.RecordAssets = EditorGUILayout.Toggle(
                _localizationService.GetString("Settings_RecordAssets"), _settings.RecordAssets);

            _settings.AutoCleanInvalidHistory = EditorGUILayout.Toggle(
                _localizationService.GetString("Settings_AutoClean"), _settings.AutoCleanInvalidHistory);

            bool newBlockFolderSelection = EditorGUILayout.Toggle(
                _localizationService.GetString("Settings_BlockFolder"), _settings.BlockFolderSelection);
            
            if (newBlockFolderSelection != _settings.BlockFolderSelection)
            {
                _settings.BlockFolderSelection = newBlockFolderSelection;
                if (_rotationLockController != null)
                {
                    _rotationLockController.BlockFolderSelection = newBlockFolderSelection;
                }
            }

            bool newAutoFocus = EditorGUILayout.Toggle(
                _localizationService.GetString("Settings_AutoFocus"), _settings.AutoFocusOnUpdate);
            
            if (newAutoFocus != _settings.AutoFocusOnUpdate)
            {
                _settings.AutoFocusOnUpdate = newAutoFocus;
                if (_rotationLockController != null)
                {
                    _rotationLockController.AutoFocusOnUpdate = newAutoFocus;
                }
            }

            EditorGUILayout.Space(8);

            // ショートカット情報
            EditorGUILayout.LabelField(_localizationService.GetString("Header_Shortcuts"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _localizationService.GetString("Shortcut_Help"),
                MessageType.None);

            EditorGUILayout.Space(8);

            // メンテナンス
            EditorGUILayout.LabelField(_localizationService.GetString("Header_Maintenance"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(_localizationService.GetString("Button_CleanHistory")))
                {
                    _historyController?.CleanupAll();
                }
                if (GUILayout.Button(_localizationService.GetString("Button_ResetAll")))
                {
                    if (EditorUtility.DisplayDialog(
                        _localizationService.GetString("Confirm_Reset_Title"),
                        _localizationService.GetString("Confirm_Reset_Message"),
                        _localizationService.GetString("Button_Reset"),
                        _localizationService.GetString("Button_Cancel")))
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
                        // 念のため言語設定は維持する
                        _settings.Language = _localizationService.CurrentLanguage;
                        
                        SaveSettings();
                        
                        // 設定リセット後の再反映
                         if (_rotationLockController != null)
                        {
                            _rotationLockController.BlockFolderSelection = _settings.BlockFolderSelection;
                        }
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
        
        private void OnRotationUpdateCompleted(RotationUpdateCompletedEvent evt)
        {
            _inspectorStatusView?.Flash(evt.UpdatedInspector);
            Repaint();
        }
    }
}
