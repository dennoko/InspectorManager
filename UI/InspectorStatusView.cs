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
        private Vector2 _scrollPosition;

        public InspectorStatusView(IInspectorWindowService inspectorService)
        {
            _inspectorService = inspectorService;
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
                if (GUILayout.Button("全てロック", Styles.MiniButton))
                {
                    _inspectorService.LockAll();
                }
                if (GUILayout.Button("全てアンロック", Styles.MiniButton))
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

            // 背景色
            var bgColor = isLocked ? Styles.Colors.LockedBackground : Styles.Colors.UnlockedBackground;

            var rect = EditorGUILayout.BeginHorizontal(Styles.ListItem);
            {
                EditorGUI.DrawRect(rect, bgColor);

                // ロックアイコン＆トグル
                var lockIcon = isLocked ? Styles.LockIcon : Styles.UnlockIcon;
                if (GUILayout.Button(lockIcon, Styles.IconButton))
                {
                    _inspectorService.SetLocked(inspector, !isLocked);
                }

                // Inspector名
                GUILayout.Label($"Inspector {index + 1}", GUILayout.Width(80));

                // 表示中のオブジェクト名
                var objectName = inspectedObject != null ? inspectedObject.name : "(なし)";
                GUILayout.Label(objectName, EditorStyles.label);

                GUILayout.FlexibleSpace();

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
