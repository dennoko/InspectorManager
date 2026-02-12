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

            // オーバーレイ初期化
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

            // ヘッダー：ローテーションロック状態カード
            DrawHeader();

            EditorGUILayout.Space(2);

            // タブ
            DrawTabBar();

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
            var isRotationEnabled = _rotationLockController?.IsEnabled ?? false;
            var inspectorCount = _inspectorService?.GetAllInspectors().Count ?? 0;

            // ヘッダーカード背景
            var headerColor = isRotationEnabled 
                ? Styles.Colors.AccentBlue 
                : new Color(0.22f, 0.22f, 0.22f, 1f);

            var headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(headerRect, headerColor);
            {
                EditorGUILayout.Space(6);

                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Space(8);

                    // ステータスアイコン + テキスト
                    var toggleText = isRotationEnabled 
                        ? _localizationService.GetString("Rotation_On") 
                        : _localizationService.GetString("Rotation_Off");
                    
                    var toggleContent = new GUIContent(
                        toggleText,
                        _localizationService.GetString("Rotation_Tooltip")
                    );

                    var newValue = GUILayout.Toggle(isRotationEnabled, toggleContent, Styles.HeaderToggle);
                    if (newValue != isRotationEnabled && _rotationLockController != null)
                    {
                        _rotationLockController.IsEnabled = newValue;
                    }

                    GUILayout.FlexibleSpace();

                    // Inspector数バッジ
                    var countText = _localizationService.GetString("Inspector_Count", inspectorCount);
                    GUILayout.Label(countText, Styles.HeaderBadge);

                    GUILayout.Space(8);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTabBar()
        {
            var tabNames = new string[] {
                _localizationService.GetString("Tab_Status"),
                _localizationService.GetString("Tab_History"),
                _localizationService.GetString("Tab_Favorites"),
                _localizationService.GetString("Tab_Settings")
            };

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                var isActive = (_selectedTab == i);
                var style = isActive ? Styles.TabActive : Styles.TabInactive;
                if (GUILayout.Button(tabNames[i], style))
                {
                    _selectedTab = i;
                }
            }
            EditorGUILayout.EndHorizontal();

            // タブ下のアクセントライン
            var lineRect = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, Styles.Colors.AccentBlue);
        }

        private void DrawInspectorStatusTab()
        {
            EditorGUILayout.Space(6);
            DrawSectionHeader(_localizationService.GetString("Header_Status"));
            _inspectorStatusView?.Draw();

            EditorGUILayout.Space(8);

            // ローテーション情報
            if (_rotationLockController != null && _rotationLockController.IsEnabled)
            {
                DrawSectionHeader(_localizationService.GetString("Rotation_Active", _rotationLockController.CurrentTargetIndex + 1));

                EditorGUILayout.Space(4);

                if (GUILayout.Button(_localizationService.GetString("Button_ManualRotate"), Styles.ActionButton))
                {
                    _rotationLockController.RotateToNext();
                }
            }

            EditorGUILayout.Space(4);
        }

        private void DrawHistoryTab()
        {
            EditorGUILayout.Space(4);
            _historyListView?.Draw();
        }

        private void DrawFavoritesTab()
        {
            EditorGUILayout.Space(4);
            _favoritesListView?.Draw();
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.Space(6);
            DrawSectionHeader(_localizationService.GetString("Header_Settings"));

            EditorGUI.BeginChangeCheck();

            // ── 言語設定 ──
            EditorGUILayout.Space(4);
            DrawSubSectionHeader(_localizationService.GetString("Setting_Language"));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            var languages = new string[] { "日本語", "English" };
            var currentLangIndex = _settings.Language == "en" ? 1 : 0;
            var newLangIndex = GUILayout.Toolbar(currentLangIndex, languages, Styles.LanguageToolbar);
            if (newLangIndex != currentLangIndex)
            {
                var newLang = newLangIndex == 1 ? "en" : "ja";
                _settings.Language = newLang;
                if (_localizationService is LocalizationService ls)
                {
                    ls.CurrentLanguage = newLang;
                }
                titleContent = new GUIContent(_localizationService.GetString("Window_Title"));
            }
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            // ── ローテーション設定 ──
            EditorGUILayout.Space(8);
            DrawSeparator();
            DrawSubSectionHeader(_localizationService.GetString("Settings_Rotation"));

            EditorGUILayout.Space(2);

            bool newBlockFolderSelection = DrawSettingToggle(
                _localizationService.GetString("Settings_BlockFolder"), _settings.BlockFolderSelection);
            if (newBlockFolderSelection != _settings.BlockFolderSelection)
            {
                _settings.BlockFolderSelection = newBlockFolderSelection;
                if (_rotationLockController != null)
                    _rotationLockController.BlockFolderSelection = newBlockFolderSelection;
            }

            bool newAutoFocus = DrawSettingToggle(
                _localizationService.GetString("Settings_AutoFocus"), _settings.AutoFocusOnUpdate);
            if (newAutoFocus != _settings.AutoFocusOnUpdate)
            {
                _settings.AutoFocusOnUpdate = newAutoFocus;
                if (_rotationLockController != null)
                    _rotationLockController.AutoFocusOnUpdate = newAutoFocus;
            }

            // ── 履歴設定 ──
            EditorGUILayout.Space(8);
            DrawSeparator();
            DrawSubSectionHeader(_localizationService.GetString("Settings_History"));

            EditorGUILayout.Space(2);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            _settings.MaxHistoryCount = EditorGUILayout.IntSlider(
                _localizationService.GetString("Settings_MaxHistory"), _settings.MaxHistoryCount, 10, 200);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            _settings.RecordSceneObjects = DrawSettingToggle(
                _localizationService.GetString("Settings_RecordScene"), _settings.RecordSceneObjects);

            _settings.RecordAssets = DrawSettingToggle(
                _localizationService.GetString("Settings_RecordAssets"), _settings.RecordAssets);

            _settings.AutoCleanInvalidHistory = DrawSettingToggle(
                _localizationService.GetString("Settings_AutoClean"), _settings.AutoCleanInvalidHistory);

            // ── ショートカット ──
            EditorGUILayout.Space(8);
            DrawSeparator();
            DrawSubSectionHeader(_localizationService.GetString("Header_Shortcuts"));
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            EditorGUILayout.HelpBox(
                _localizationService.GetString("Shortcut_Help"),
                MessageType.None);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            // ── メンテナンス ──
            EditorGUILayout.Space(8);
            DrawSeparator();
            DrawSubSectionHeader(_localizationService.GetString("Header_Maintenance"));
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            {
                if (GUILayout.Button(_localizationService.GetString("Button_CleanHistory"), Styles.ActionButton))
                {
                    _historyController?.CleanupAll();
                }
                if (GUILayout.Button(_localizationService.GetString("Button_ResetAll"), Styles.DangerButton))
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
                        _settings.Language = _localizationService.CurrentLanguage;
                        SaveSettings();
                        if (_rotationLockController != null)
                        {
                            _rotationLockController.BlockFolderSelection = _settings.BlockFolderSelection;
                            _rotationLockController.AutoFocusOnUpdate = _settings.AutoFocusOnUpdate;
                        }
                    }
                }
            }
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
            }
        }

        // ── UIヘルパー ──

        private void DrawSectionHeader(string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);
            GUILayout.Label(text, Styles.SectionHeader);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSubSectionHeader(string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(text, Styles.SubSectionHeader);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Styles.Colors.Separator);
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private bool DrawSettingToggle(string label, bool currentValue)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            var newValue = EditorGUILayout.Toggle(label, currentValue);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
            return newValue;
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
