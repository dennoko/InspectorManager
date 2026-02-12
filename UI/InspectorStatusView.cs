using System.Collections.Generic;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// Inspectorウィンドウの状態表示UI
    /// </summary>
    public class InspectorStatusView
    {
        private readonly IInspectorWindowService _inspectorService;
        private readonly Controllers.RotationLockController _rotationLockController;
        private readonly ILocalizationService _localizationService;
        private Vector2 _scrollPosition;
        
        // ハイライト用
        private EditorWindow _highlightedInspector;
        private double _highlightEndTime;
        private const double HighlightDuration = 1.0;

        public InspectorStatusView(
            IInspectorWindowService inspectorService,
            Controllers.RotationLockController rotationLockController,
            ILocalizationService localizationService)
        {
            _inspectorService = inspectorService;
            _rotationLockController = rotationLockController;
            _localizationService = localizationService;
        }

        public void Flash(EditorWindow inspector)
        {
            _highlightedInspector = inspector;
            _highlightEndTime = EditorApplication.timeSinceStartup + HighlightDuration;
        }

        public void Draw()
        {
            if (!_inspectorService.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    _localizationService.GetString("Error_ReflectionFailed"),
                    MessageType.Error);
                return;
            }

            var inspectors = _inspectorService.GetAllInspectors();

            if (inspectors.Count == 0)
            {
                EditorGUILayout.HelpBox(_localizationService.GetString("Status_NoInspectors"), MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(150));

            for (int i = 0; i < inspectors.Count; i++)
            {
                DrawInspectorRow(inspectors[i], i);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            // 一括操作ボタン
            EditorGUILayout.BeginHorizontal();
            {
                // LocalizationServiceがnullの場合の対策（古いコードとの互換性など）
                var lockAllText = _localizationService?.GetString("Button_LockAll") ?? "Lock All";
                if (GUILayout.Button(lockAllText, Styles.MiniButton))
                {
                    _inspectorService.LockAll();
                }
                var unlockAllText = _localizationService?.GetString("Button_UnlockAll") ?? "Unlock All";
                if (GUILayout.Button(unlockAllText, Styles.MiniButton))
                {
                    _inspectorService.UnlockAll();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInspectorRow(EditorWindow inspector, int index)
        {
            var isLocked = _inspectorService.IsLocked(inspector);
            var inspectedObject = _inspectorService.GetInspectedObject(inspector);
            
            // 除外状態
            var isExcluded = _rotationLockController != null && _rotationLockController.IsExcluded(inspector);
            
            // ハイライト判定
            bool isHighlighted = inspector == _highlightedInspector && EditorApplication.timeSinceStartup < _highlightEndTime;

            // 背景色 (除外時は少し暗くするなど)
            var baseColor = isExcluded 
                ? new Color(0.25f, 0.25f, 0.25f, 1f) // Dark Gray for Excluded
                : (isLocked ? Styles.Colors.LockedBackground : Styles.Colors.UnlockedBackground);

            if (isHighlighted)
            {
                // ハイライト時は緑色を強く
                baseColor = Color.Lerp(baseColor, new Color(0.2f, 0.9f, 0.2f, 0.5f), 0.7f);
            }

            var bgColor = baseColor;

            var rect = EditorGUILayout.BeginHorizontal(Styles.ListItem);
            {
                EditorGUI.DrawRect(rect, bgColor);

                // ロックアイコン＆トグル (除外時は操作不可にするか、アイコンを変えるか)
                // 除外時はロック固定なので、ロックアイコンを表示しつつ、disabledにするのが親切かも
                using (new EditorGUI.DisabledScope(isExcluded))
                {
                    var lockIcon = isLocked ? Styles.LockIcon : Styles.UnlockIcon;
                    if (GUILayout.Button(lockIcon, Styles.IconButton))
                    {
                        _inspectorService.SetLocked(inspector, !isLocked);
                    }
                }

                // Inspector名
                var nameLabel = $"Inspector {index + 1}";
                if (isExcluded)
                {
                     nameLabel += $" ({_localizationService?.GetString("Status_Excluded") ?? "Ex"})";
                }
                GUILayout.Label(nameLabel, GUILayout.Width(100));

                // Rotation Order Badge (if rotation is enabled)
                if (_rotationLockController != null && _rotationLockController.IsEnabled && !isExcluded)
                {
                    int rotationIndex = _rotationLockController.GetRotationOrderIndex(inspector);
                    if (rotationIndex >= 0)
                    {
                        var badgeContent = rotationIndex == 0 ? "NEXT" : (rotationIndex).ToString(); // 0 is Next, 1 is 2nd... or just 1, 2, 3? "Wait 1", "Wait 2"?
                        // Use "NEXT" for 0, and numbers for queue
                        
                        var badgeStyle = new GUIStyle(EditorStyles.miniLabel);
                        badgeStyle.normal.textColor = Color.white;
                        badgeStyle.alignment = TextAnchor.MiddleCenter;
                        badgeStyle.fixedWidth = rotationIndex == 0 ? 36 : 20;

                        var badgeColor = rotationIndex == 0 
                            ? new Color(0.2f, 0.6f, 1f, 1f) // Blue for Next
                            : new Color(0.4f, 0.4f, 0.4f, 1f); // Gray for others
                        
                        var badgeRect = GUILayoutUtility.GetRect(new GUIContent(badgeContent), badgeStyle);
                        var originalColor = GUI.backgroundColor;
                        GUI.backgroundColor = badgeColor;
                        GUI.Box(badgeRect, badgeContent, EditorStyles.helpBox); // Use helpBox style for background
                        GUI.backgroundColor = originalColor;
                    }
                }

                // 表示中のオブジェクト名
                var objectName = inspectedObject != null ? inspectedObject.name : (_localizationService?.GetString("Status_NoObject") ?? "(None)");
                GUILayout.Label(objectName, EditorStyles.label);

                GUILayout.FlexibleSpace();

                // 除外/追加ボタン
                if (_rotationLockController != null && _localizationService != null)
                {
                    var btnIcon = isExcluded ? "Add" : "Excl"; // icon or text
                    var btnTooltip = isExcluded 
                        ? _localizationService.GetString("Tooltip_Include")
                        : _localizationService.GetString("Tooltip_Exclude");
                    
                    var btnContent = new GUIContent(btnIcon, btnTooltip);

                    if (GUILayout.Button(btnContent, Styles.MiniButton, GUILayout.Width(64)))
                    {
                        _rotationLockController.SetExcluded(inspector, !isExcluded);
                    }
                }

                // フォーカスボタン
                if (GUILayout.Button(_localizationService?.GetString("Button_Focus") ?? "Focus", Styles.MiniButton, GUILayout.Width(40)))
                {
                    inspector.Focus();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
