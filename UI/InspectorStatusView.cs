using System.Collections.Generic;
using InspectorManager.Core;
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
        private const double HighlightDuration = 1.2;

        // ドラッグ&ドロップ用
        private int _dragFromIndex = -1;
        private int _dragOverIndex = -1;

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
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(_localizationService.GetString("Status_NoInspectors"), MessageType.Info);
                GUILayout.Space(12);
                EditorGUILayout.EndHorizontal();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < inspectors.Count; i++)
            {
                DrawInspectorRow(inspectors[i], i);
            }

            EditorGUILayout.EndScrollView();

            // ── 「＋ Inspectorを追加」行 ──
            DrawAddInspectorRow();

            EditorGUILayout.Space(6);

            // 一括操作ボタン
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            {
                var lockAllText = _localizationService?.GetString("Button_LockAll") ?? "Lock All";
                if (GUILayout.Button(lockAllText, Styles.ActionButton))
                {
                    _inspectorService.LockAll();
                }
                var unlockAllText = _localizationService?.GetString("Button_UnlockAll") ?? "Unlock All";
                if (GUILayout.Button(unlockAllText, Styles.ActionButton))
                {
                    _inspectorService.UnlockAll();
                }
            }
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddInspectorRow()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            var addLabel = _localizationService?.GetString("Button_AddInspector") ?? "+ Add Inspector";
            if (GUILayout.Button(addLabel, Styles.ActionButton))
            {
                var newInspector = InspectorReflection.CreateNewInspector();
                if (newInspector != null && _rotationLockController != null && _rotationLockController.IsEnabled)
                {
                    // Manager経由で追加 → ローテーションに自動参加
                    EditorApplication.delayCall += () =>
                    {
                        _rotationLockController.AddManagedInspector(newInspector);
                    };
                }
            }

            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInspectorRow(EditorWindow inspector, int index)
        {
            var isLocked = _inspectorService.IsLocked(inspector);
            var inspectedObject = _inspectorService.GetInspectedObject(inspector);
            var isExcluded = _rotationLockController != null && _rotationLockController.IsExcluded(inspector);

            // ハイライト判定
            bool isHighlighted = inspector == _highlightedInspector && EditorApplication.timeSinceStartup < _highlightEndTime;

            // ドラッグオーバー判定
            bool isDragOver = _dragFromIndex >= 0 && _dragOverIndex == index && _dragFromIndex != index;

            // 背景色の計算
            Color bgColor;
            if (isHighlighted)
            {
                float remaining = (float)(_highlightEndTime - EditorApplication.timeSinceStartup);
                float t = Mathf.Clamp01(remaining / (float)HighlightDuration);
                bgColor = Color.Lerp(
                    index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd,
                    Styles.Colors.FlashHighlight,
                    t * 0.8f);
            }
            else if (isExcluded)
            {
                bgColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            }
            else
            {
                bgColor = index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd;
            }

            var rect = EditorGUILayout.BeginHorizontal(Styles.ListItem);
            {
                // 行背景の描画
                EditorGUI.DrawRect(rect, bgColor);

                // ── ドラッグオーバーインジケーター ──
                if (isDragOver)
                {
                    var lineRect = new Rect(rect.x, rect.y - 1, rect.width, 2);
                    EditorGUI.DrawRect(lineRect, Styles.Colors.AccentGreen);
                }

                // ── ハンバーガーアイコン（ドラッグハンドル）──
                var dragHandleContent = new GUIContent("≡", 
                    _localizationService?.GetString("Tooltip_DragReorder") ?? "Drag to reorder");
                var dragHandleRect = GUILayoutUtility.GetRect(dragHandleContent, Styles.IconButton, GUILayout.Width(20));
                
                EditorGUIUtility.AddCursorRect(dragHandleRect, MouseCursor.Pan);
                GUI.Label(dragHandleRect, dragHandleContent, Styles.IconButton);

                // D&Dハンドリング
                HandleRowDragAndDrop(dragHandleRect, rect, index);

                // ── ロック状態左バー ──
                var barRect = new Rect(rect.x, rect.y + 2, 3, rect.height - 4);
                if (isExcluded)
                    EditorGUI.DrawRect(barRect, Styles.Colors.TextMuted);
                else if (isLocked)
                    EditorGUI.DrawRect(barRect, Styles.Colors.DangerRed);
                else
                    EditorGUI.DrawRect(barRect, Styles.Colors.AccentGreen);

                // ── ロックアイコン＆トグル ──
                using (new EditorGUI.DisabledScope(isExcluded))
                {
                    var lockIcon = isLocked ? Styles.LockIcon : Styles.UnlockIcon;
                    if (GUILayout.Button(lockIcon, Styles.IconButton))
                    {
                        _inspectorService.SetLocked(inspector, !isLocked);
                    }
                }

                // ── Inspector名 ──
                var nameLabel = $"Inspector {index + 1}";
                GUILayout.Label(nameLabel, GUILayout.Width(80));

                // ── ローテーションバッジ ──
                if (_rotationLockController != null && _rotationLockController.IsEnabled)
                {
                    if (isExcluded)
                    {
                        GUILayout.Label("(Ex)", Styles.BadgeExcluded, GUILayout.Width(30));
                    }
                    else
                    {
                        int rotationIndex = _rotationLockController.GetRotationOrderIndex(inspector);
                        if (rotationIndex >= 0)
                        {
                            if (rotationIndex == 0)
                            {
                                GUILayout.Label("▶ NEXT", Styles.BadgeNext, GUILayout.Width(50));
                            }
                            else
                            {
                                GUILayout.Label($"{rotationIndex + 1}.", Styles.BadgeOrder, GUILayout.Width(24));
                            }
                        }
                    }
                }

                // ── 表示中のオブジェクト名 ──
                GUILayout.Space(4);
                var objectName = inspectedObject != null 
                    ? inspectedObject.name 
                    : (_localizationService?.GetString("Status_NoObject") ?? "(None)");
                
                using (new EditorGUI.DisabledScope(isExcluded))
                {
                    GUILayout.Label(objectName, EditorStyles.label);
                }

                GUILayout.FlexibleSpace();

                // ── 除外/追加ボタン ──
                if (_rotationLockController != null && _localizationService != null && _rotationLockController.IsEnabled)
                {
                    var btnText = isExcluded 
                        ? "＋" 
                        : "－";
                    var btnTooltip = isExcluded 
                        ? _localizationService.GetString("Tooltip_Include")
                        : _localizationService.GetString("Tooltip_Exclude");
                    
                    var btnContent = new GUIContent(btnText, btnTooltip);

                    if (GUILayout.Button(btnContent, Styles.MiniButton, GUILayout.Width(24)))
                    {
                        _rotationLockController.SetExcluded(inspector, !isExcluded);
                    }
                }

                // ── 閉じるボタン（除外状態のInspectorのみ、またはローテーションOFF時） ──
                bool showCloseButton = isExcluded || 
                    (_rotationLockController == null || !_rotationLockController.IsEnabled);
                if (showCloseButton)
                {
                    var closeContent = new GUIContent("✕", 
                        _localizationService?.GetString("Tooltip_CloseInspector") ?? "Close this Inspector");
                    if (GUILayout.Button(closeContent, Styles.MiniButton, GUILayout.Width(24)))
                    {
                        inspector.Close();
                    }
                }

                // ── フォーカスボタン ──
                var focusContent = new GUIContent(
                    _localizationService?.GetString("Button_Focus") ?? "Focus",
                    "Focus this Inspector window");
                if (GUILayout.Button(focusContent, Styles.MiniButton, GUILayout.Width(44)))
                {
                    inspector.Focus();
                }

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 行ごとのドラッグ&ドロップ処理
        /// </summary>
        private void HandleRowDragAndDrop(Rect handleRect, Rect rowRect, int index)
        {
            if (_rotationLockController == null || !_rotationLockController.IsEnabled) return;

            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.MouseDrag:
                    if (handleRect.Contains(evt.mousePosition))
                    {
                        _dragFromIndex = index;
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.SetGenericData("InspectorReorderIndex", index);
                        DragAndDrop.StartDrag("Reorder Inspector");
                        evt.Use();
                    }
                    break;

                case EventType.DragUpdated:
                    if (rowRect.Contains(evt.mousePosition) && _dragFromIndex >= 0 && _dragFromIndex != index)
                    {
                        _dragOverIndex = index;
                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (rowRect.Contains(evt.mousePosition) && _dragFromIndex >= 0 && _dragFromIndex != index)
                    {
                        DragAndDrop.AcceptDrag();
                        _rotationLockController.ReorderInspector(_dragFromIndex, index);
                        _dragFromIndex = -1;
                        _dragOverIndex = -1;
                        evt.Use();
                    }
                    break;

                case EventType.DragExited:
                    _dragFromIndex = -1;
                    _dragOverIndex = -1;
                    break;
            }
        }
    }
}
