using InspectorManager.Controllers;
using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// 設定タブのUI描画を担当
    /// </summary>
    public class SettingsTabView
    {
        private readonly ILocalizationService _localizationService;
        private readonly IHistoryService _historyService;
        private readonly IFavoritesService _favoritesService;
        private readonly HistoryController _historyController;
        private readonly RotationLockController _rotationLockController;

        private InspectorManagerSettings _settings;
        private Vector2 _scrollPosition;
        private bool _blockFilterFoldout = true;

        /// <summary>
        /// 設定が変更された場合に呼び出されるコールバック
        /// </summary>
        public System.Action OnSettingsChanged;

        /// <summary>
        /// タイトルの更新が必要な場合に呼び出されるコールバック
        /// </summary>
        public System.Action<string> OnTitleUpdateRequired;

        public SettingsTabView(
            ILocalizationService localizationService,
            IHistoryService historyService,
            IFavoritesService favoritesService,
            HistoryController historyController,
            RotationLockController rotationLockController,
            InspectorManagerSettings settings)
        {
            _localizationService = localizationService;
            _historyService = historyService;
            _favoritesService = favoritesService;
            _historyController = historyController;
            _rotationLockController = rotationLockController;
            _settings = settings;
        }

        public InspectorManagerSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public void Draw()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(6);
            DrawSectionHeader(_localizationService.GetString("Header_Settings"));

            EditorGUI.BeginChangeCheck();

            DrawLanguageSection();
            DrawRotationSection();
            DrawHistorySection();
            DrawShortcutSection();
            DrawMaintenanceSection();

            EditorGUILayout.Space(8);

            if (EditorGUI.EndChangeCheck())
            {
                OnSettingsChanged?.Invoke();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLanguageSection()
        {
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
                OnTitleUpdateRequired?.Invoke(_localizationService.GetString("Window_Title"));
            }
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRotationSection()
        {
            EditorGUILayout.Space(8);
            DrawSeparator();
            DrawSubSectionHeader(_localizationService.GetString("Settings_Rotation"));
            EditorGUILayout.Space(2);

            bool newAutoFocus = DrawSettingToggle(
                _localizationService.GetString("Settings_AutoFocus"), _settings.AutoFocusOnUpdate);
            if (newAutoFocus != _settings.AutoFocusOnUpdate)
            {
                _settings.AutoFocusOnUpdate = newAutoFocus;
                if (_rotationLockController != null)
                    _rotationLockController.AutoFocusOnUpdate = newAutoFocus;
            }

            EditorGUILayout.Space(4);
            DrawBlockFilterSection();
        }

        private void DrawBlockFilterSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            _blockFilterFoldout = EditorGUILayout.Foldout(_blockFilterFoldout,
                _localizationService.GetString("Settings_BlockFilter"), true);
            EditorGUILayout.EndHorizontal();

            if (!_blockFilterFoldout) return;

            // ── カテゴリA: 操作不可なオブジェクト ──
            DrawBlockCategoryLabel(_localizationService.GetString("Settings_BlockFilter_CategoryA"));

            _settings.BlockFolderSelection = DrawBlockToggle(
                _localizationService.GetString("Block_Folder"), _settings.BlockFolderSelection);
            _settings.BlockDefaultAsset = DrawBlockToggle(
                _localizationService.GetString("Block_DefaultAsset"), _settings.BlockDefaultAsset);
            _settings.BlockAsmDef = DrawBlockToggle(
                _localizationService.GetString("Block_AsmDef"), _settings.BlockAsmDef);
            _settings.BlockNativePlugin = DrawBlockToggle(
                _localizationService.GetString("Block_NativePlugin"), _settings.BlockNativePlugin);

            EditorGUILayout.Space(2);

            // ── カテゴリB: 操作が限定的なオブジェクト ──
            DrawBlockCategoryLabel(_localizationService.GetString("Settings_BlockFilter_CategoryB"));

            _settings.BlockTextAsset = DrawBlockToggle(
                _localizationService.GetString("Block_TextAsset"), _settings.BlockTextAsset);
            _settings.BlockLightingSettings = DrawBlockToggle(
                _localizationService.GetString("Block_LightingSettings"), _settings.BlockLightingSettings);
            _settings.BlockShader = DrawBlockToggle(
                _localizationService.GetString("Block_Shader"), _settings.BlockShader);
            _settings.BlockFont = DrawBlockToggle(
                _localizationService.GetString("Block_Font"), _settings.BlockFont);
        }

        private void DrawBlockCategoryLabel(string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(text, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private bool DrawBlockToggle(string label, bool currentValue)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(36); // インデントを増やす
            var newValue = EditorGUILayout.Toggle(label, currentValue);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
            return newValue;
        }

        private void DrawHistorySection()
        {
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
        }

        private void DrawShortcutSection()
        {
            EditorGUILayout.Space(8);
            DrawSeparator();
            DrawSubSectionHeader(_localizationService.GetString("Header_Shortcuts"));
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            EditorGUILayout.HelpBox(
                _localizationService.GetString("Shortcut_Help"), MessageType.None);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaintenanceSection()
        {
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
                    PerformReset();
                }
            }
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void PerformReset()
        {
            if (!EditorUtility.DisplayDialog(
                _localizationService.GetString("Confirm_Reset_Title"),
                _localizationService.GetString("Confirm_Reset_Message"),
                _localizationService.GetString("Button_Reset"),
                _localizationService.GetString("Button_Cancel")))
            {
                return;
            }

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
            OnSettingsChanged?.Invoke();

            if (_rotationLockController != null)
            {
                _rotationLockController.BlockFolderSelection = _settings.BlockFolderSelection;
                _rotationLockController.AutoFocusOnUpdate = _settings.AutoFocusOnUpdate;
            }
        }

        // ── UIヘルパー（InspectorManagerWindowと共通化可能だが、独立性を保つ）──

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
            GUILayout.Space(24); // インデントを少し増やす
            var newValue = EditorGUILayout.Toggle(label, currentValue);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
            return newValue;
        }
    }
}
