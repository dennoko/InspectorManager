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
        private readonly Controllers.RotationLockController _rotationLockController; // 追加
        private readonly ILocalizationService _localizationService; // 追加
        private Vector2 _scrollPosition;

        public InspectorStatusView(
            IInspectorWindowService inspectorService,
            Controllers.RotationLockController rotationLockController,
            ILocalizationService localizationService)
        {
            _inspectorService = inspectorService;
            _rotationLockController = rotationLockController;
            _localizationService = localizationService;
        }

        public void Draw()
        {
            if (!_inspectorService.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    "Inspector APIへのアクセスに失敗しました。\nこのUnityバージョンはサポートされていない可能性があります。",
                    MessageType.Error);
                return;
            }

            var inspectors = _inspectorService.GetAllInspectors();

            if (inspectors.Count == 0)
            {
                EditorGUILayout.HelpBox("Inspectorウィンドウが見つかりません。", MessageType.Info);
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
                var lockAllText = _localizationService != null ? "全てロック" : "Lock All"; 
                // 本当はこれもローカライズキーに入れるべきだが、今回は省略
                if (GUILayout.Button(lockAllText, Styles.MiniButton))
                {
                    _inspectorService.LockAll();
                }
                var unlockAllText = _localizationService != null ? "全てアンロック" : "Unlock All";
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

            // 背景色 (除外時は少し暗くするなど)
            var bgColor = isExcluded 
                ? new Color(0.25f, 0.25f, 0.25f, 1f) // Dark Gray for Excluded
                : (isLocked ? Styles.Colors.LockedBackground : Styles.Colors.UnlockedBackground);

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

                // 表示中のオブジェクト名
                var objectName = inspectedObject != null ? inspectedObject.name : "(なし)";
                GUILayout.Label(objectName, EditorStyles.label);

                GUILayout.FlexibleSpace();

                // 除外/追加ボタン
                if (_rotationLockController != null && _localizationService != null)
                {
                    var btnIcon = isExcluded ? "➕" : "➖"; // icon or text
                    var btnTooltip = isExcluded 
                        ? _localizationService.GetString("Tooltip_Include")
                        : _localizationService.GetString("Tooltip_Exclude");
                    
                    var btnContent = new GUIContent(btnIcon, btnTooltip);

                    if (GUILayout.Button(btnContent, Styles.MiniButton, GUILayout.Width(24)))
                    {
                        _rotationLockController.SetExcluded(inspector, !isExcluded);
                    }
                }

                // フォーカスボタン
                if (GUILayout.Button("表示", Styles.MiniButton, GUILayout.Width(40)))
                {
                    inspector.Focus();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
