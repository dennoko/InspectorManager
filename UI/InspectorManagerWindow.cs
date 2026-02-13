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
        private SettingsTabView _settingsTabView;
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
            EventBus.Instance.Subscribe<RotationPauseChangedEvent>(OnRotationPauseChanged);
            
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
            EventBus.Instance.Unsubscribe<RotationPauseChangedEvent>(OnRotationPauseChanged);
            
            if (_localizationService != null)
                _localizationService.OnLanguageChanged -= Repaint;

            // コントローラーの確実な破棄（Unregister内でDisposeも呼ばれる）
            ServiceLocator.Instance.Unregister<RotationLockController>();
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

            // サービスの初期化
            var result = ServiceInitializer.InitializeAll();
            _persistenceService = result.PersistenceService;
            _localizationService = result.LocalizationService;
            _inspectorService = result.InspectorService;
            _historyService = result.HistoryService;
            _favoritesService = result.FavoritesService;
            _settings = result.Settings;

            // ウィンドウタイトル更新
            titleContent = new GUIContent(_localizationService.GetString("Window_Title"));

            // コントローラーの初期化
            _rotationLockController = new RotationLockController(_inspectorService, _persistenceService);
            if (_settings != null)
            {
                _rotationLockController.BlockFolderSelection = _settings.BlockFolderSelection;
                _rotationLockController.AutoFocusOnUpdate = _settings.AutoFocusOnUpdate;
                _rotationLockController.FilterSettings = _settings;
            }
            // HotkeyControllerからアクセスできるようServiceLocatorに登録
            ServiceLocator.Instance.Register(_rotationLockController);
            _historyController = new HistoryController(_historyService, _favoritesService, _settings);

            // ビューの初期化
            _inspectorStatusView = new InspectorStatusView(
                _inspectorService, 
                _rotationLockController,
                _localizationService);
            _historyListView = new HistoryListView(_historyService, _favoritesService, _localizationService);
            _favoritesListView = new FavoritesListView(_favoritesService, _localizationService);
            _settingsTabView = new SettingsTabView(
                _localizationService,
                _historyService,
                _favoritesService,
                _historyController,
                _rotationLockController,
                _settings);
            _settingsTabView.OnSettingsChanged = SaveSettings;
            _settingsTabView.OnTitleUpdateRequired = title => titleContent = new GUIContent(title);

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

            if (_inspectorService == null || !_inspectorService.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    _localizationService?.GetString("Error_ReflectionFailed") ?? "Inspector reflection not available.",
                    MessageType.Error);
                return;
            }

            DrawHeader();
            DrawSeparator();
            DrawTabBar();
            DrawSeparator();

            switch (_selectedTab)
            {
                case 0: DrawInspectorStatusTab(); break;
                case 1: _historyListView?.Draw(); break;
                case 2: _favoritesListView?.Draw(); break;
                case 3: _settingsTabView?.Draw(); break;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Space(8);

                // ローテーションロック状態表示（3状態: OFF / ON / Paused）
                bool isRotationEnabled = _rotationLockController?.IsEnabled ?? false;
                bool isPaused = isRotationEnabled && (_rotationLockController?.IsPaused ?? false);

                string statusIcon;
                string statusText;
                Color headerBg;
                Color barColor;

                if (!isRotationEnabled)
                {
                    statusIcon = "TestNormal";
                    statusText = _localizationService.GetString("Status_RotationOff");
                    headerBg = new Color(0.22f, 0.22f, 0.22f, 1f);
                    barColor = Styles.Colors.TextMuted;
                }
                else if (isPaused)
                {
                    statusIcon = "TestInconclusive";
                    statusText = _localizationService.GetString("Status_RotationPaused");
                    headerBg = new Color(0.36f, 0.30f, 0.14f, 1f);
                    barColor = Styles.Colors.WarningOrange;
                }
                else
                {
                    statusIcon = "TestPassed";
                    statusText = _localizationService.GetString("Status_RotationOn");
                    headerBg = new Color(0.16f, 0.36f, 0.24f, 1f);
                    barColor = Styles.Colors.StatusGreen;
                }

                // カード風ヘッダー
                var headerRect = EditorGUILayout.BeginHorizontal(Styles.ListItem, GUILayout.Height(36));
                {
                    EditorGUI.DrawRect(headerRect, headerBg);

                    // 左バー
                    var leftBar = new Rect(headerRect.x, headerRect.y, 3, headerRect.height);
                    EditorGUI.DrawRect(leftBar, barColor);

                    GUILayout.Space(8);
                    GUILayout.Label(EditorGUIUtility.IconContent(statusIcon), GUILayout.Width(20), GUILayout.Height(20));
                    GUILayout.Label(statusText, Styles.SectionHeader);
                    GUILayout.FlexibleSpace();

                    // 一時停止ボタン（ローテーションON時のみ表示）
                    if (isRotationEnabled && _rotationLockController != null)
                    {
                        var pauseIcon = isPaused ? "PlayButton" : "PauseButton";
                        var pauseTooltip = isPaused
                            ? _localizationService.GetString("Tooltip_ResumeRotation")
                            : _localizationService.GetString("Tooltip_PauseRotation");
                        if (GUILayout.Button(
                            new GUIContent(EditorGUIUtility.IconContent(pauseIcon).image, pauseTooltip),
                            Styles.IconButton, GUILayout.Width(22), GUILayout.Height(22)))
                        {
                            _rotationLockController.IsPaused = !isPaused;
                        }
                        GUILayout.Space(4);
                    }

                    // ON/OFFトグル
                    var newEnabled = GUILayout.Toggle(isRotationEnabled, "", GUILayout.Width(16));
                    if (newEnabled != isRotationEnabled && _rotationLockController != null)
                    {
                        _rotationLockController.IsEnabled = newEnabled;
                        // ONにした時は一時停止を解除
                        if (newEnabled) _rotationLockController.IsPaused = false;
                    }

                    GUILayout.Space(6);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);

            string[] tabLabels = {
                _localizationService.GetString("Tab_Status"),
                _localizationService.GetString("Tab_History"),
                _localizationService.GetString("Tab_Favorites"),
                _localizationService.GetString("Tab_Settings")
            };

            for (int i = 0; i < tabLabels.Length; i++)
            {
                var style = i == _selectedTab ? Styles.TabActive : Styles.TabInactive;
                if (GUILayout.Button(tabLabels[i], style))
                {
                    _selectedTab = i;
                }
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();

            // アクセントライン
            var accentRect = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(accentRect, Styles.Colors.AccentBlue);
        }

        private void DrawInspectorStatusTab()
        {
            _inspectorStatusView?.Draw();
        }

        private void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Styles.Colors.Separator);
        }

        private void SaveSettings()
        {
            _settings = _settingsTabView?.Settings ?? _settings;
            _persistenceService?.Save("Settings", _settings);

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
        private void OnRotationPauseChanged(RotationPauseChangedEvent evt) => Repaint();
        
        private void OnRotationUpdateCompleted(RotationUpdateCompletedEvent evt)
        {
            _inspectorStatusView?.Flash(evt.UpdatedInspector);
            Repaint();
        }
    }
}
