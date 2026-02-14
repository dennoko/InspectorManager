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

            // UI描画前にリストを最新の状態に同期
            if (_rotationLockController != null)
            {
                _rotationLockController.SyncInspectorList();
            }

            // ローテーション有効かどうかで表示モードを切り替え
            bool isRotationEnabled = _rotationLockController != null && _rotationLockController.IsEnabled;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (isRotationEnabled)
            {
                DrawRotationEnabledView();
            }
            else
            {
                DrawStandardView();
            }

            EditorGUILayout.EndScrollView();

            // ── 「＋ Inspectorを追加」行 ──
            DrawAddInspectorRow(isRotationEnabled);

            EditorGUILayout.Space(6);

            // 一括操作ボタン
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            {
                var lockAllText = _localizationService.GetString("Button_LockAll");
                if (GUILayout.Button(lockAllText, Styles.ActionButton))
                {
                    _inspectorService.LockAll();
                }
                var unlockAllText = _localizationService.GetString("Button_UnlockAll");
                if (GUILayout.Button(unlockAllText, Styles.ActionButton))
                {
                    _inspectorService.UnlockAll();
                }
            }
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRotationEnabledView()
        {
            _rotationLockController.GetInspectorLists(out var rotationList, out var excludedList, out var unmanagedList);

            // ── ローテーション順序セクション ──
            if (rotationList.Count > 0 || unmanagedList.Count > 0)
            {
                EditorGUILayout.LabelField(_localizationService.GetString("Section_RotationOrder"), EditorStyles.boldLabel);
                
                // ローテーション中のInspector
                for (int i = 0; i < rotationList.Count; i++)
                {
                    DrawDetailedInspectorRow(rotationList[i], i, true, false);
                }

                // 未管理のInspector（新規追加など）
                for (int i = 0; i < unmanagedList.Count; i++)
                {
                    // ローテーション末尾に表示するが、ドラッグ＆ドロップや役割ラベルは無し
                    DrawDetailedInspectorRow(unmanagedList[i], rotationList.Count + i, false, false);
                }
                EditorGUILayout.Space(8);
            }

            // ── 除外中セクション ──
            if (excludedList.Count > 0)
            {
                EditorGUILayout.LabelField(_localizationService.GetString("Section_Excluded"), EditorStyles.boldLabel);
                for (int i = 0; i < excludedList.Count; i++)
                {
                    DrawDetailedInspectorRow(excludedList[i], i, false, true);
                }
            }
            
            if (rotationList.Count == 0 && excludedList.Count == 0 && unmanagedList.Count == 0)
            {
                DrawEmptyMessage();
            }
        }

        private void DrawStandardView()
        {
            var inspectors = _inspectorService.GetAllInspectors();
            if (inspectors.Count == 0)
            {
                DrawEmptyMessage();
                return;
            }

            for (int i = 0; i < inspectors.Count; i++)
            {
                // ローテーション無効時は単純なリスト表示（D&Dなし、順序番号なし）
                DrawDetailedInspectorRow(inspectors[i], i, false, false, isStandardView: true);
            }
        }

        private void DrawEmptyMessage()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            EditorGUILayout.HelpBox(_localizationService.GetString("Status_NoInspectors"), MessageType.Info);
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddInspectorRow(bool autoAddToRotation)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            var addLabel = _localizationService?.GetString("Button_AddInspector") ?? "+ Add Inspector";
            if (GUILayout.Button(addLabel, Styles.ActionButton))
            {
                var newInspector = InspectorReflection.CreateNewInspector();
                if (newInspector != null && autoAddToRotation)
                {
                    EditorApplication.delayCall += () =>
                    {
                        _rotationLockController.AddManagedInspector(newInspector);
                    };
                }
            }

            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 詳細なInspector行描画
        /// </summary>
        /// <param name="inspector">対象Inspector</param>
        /// <param name="index">リスト内のインデックス</param>
        /// <param name="isRotationItem">ローテーション順序リスト内の項目か</param>
        /// <param name="isExcludedItem">除外リスト内の項目か</param>
        /// <param name="isStandardView">ローテーションOFF時の通常表示か</param>
        private void DrawDetailedInspectorRow(EditorWindow inspector, int index, bool isRotationItem, bool isExcludedItem, bool isStandardView = false)
        {
            // 固定番号（ウィンドウインデックス）を取得
            int fixedIndex = -1;
            if (_rotationLockController != null)
            {
                fixedIndex = _rotationLockController.GetWindowIndex(inspector);
            }
            else
            {
                // Fallback for standard view without controller
                var all = _inspectorService.GetAllInspectors();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == inspector) { fixedIndex = i + 1; break; }
                }
                if (fixedIndex == -1) fixedIndex = index + 1;
            }

            bool isLocked = _inspectorService.IsLocked(inspector);
            var inspectedObject = _inspectorService.GetInspectedObject(inspector);
            
            // ハイライト判定
            bool isHighlighted = inspector == _highlightedInspector && EditorApplication.timeSinceStartup < _highlightEndTime;

            // ドラッグオーバー判定 (ローテーション項目のみ)
            bool isDragOver = isRotationItem && _dragFromIndex >= 0 && _dragOverIndex == index && _dragFromIndex != index;

            // 背景色
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
            else if (isExcludedItem)
            {
                bgColor = new Color(0.18f, 0.18f, 0.18f, 1f); // Darker for excluded
            }
            else
            {
                bgColor = index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd;
            }

            var rect = EditorGUILayout.BeginHorizontal(Styles.ListItem);
            {
                // 行背景
                EditorGUI.DrawRect(rect, bgColor);

                // D&D インジケーター
                if (isDragOver)
                {
                    var lineRect = new Rect(rect.x, rect.y - 1, rect.width, 2);
                    EditorGUI.DrawRect(lineRect, Styles.Colors.AccentGreen);
                }

                // ── ハンバーガーアイコン (ローテーション項目のみ) ──
                if (isRotationItem)
                {
                    var dragHandleContent = new GUIContent("≡", _localizationService.GetString("Tooltip_DragReorder"));
                    var dragHandleRect = GUILayoutUtility.GetRect(dragHandleContent, Styles.IconButton, GUILayout.Width(20));
                    EditorGUIUtility.AddCursorRect(dragHandleRect, MouseCursor.Pan);
                    GUI.Label(dragHandleRect, dragHandleContent, Styles.IconButton);
                    
                    HandleRowDragAndDrop(dragHandleRect, rect, index);
                }
                else
                {
                    GUILayout.Space(24); // インデント合わせ
                }

                // ── ロック状態バー ──
                var barRect = new Rect(rect.x, rect.y + 2, 3, rect.height - 4);
                if (isExcludedItem) EditorGUI.DrawRect(barRect, Styles.Colors.TextMuted);
                else if (isLocked) EditorGUI.DrawRect(barRect, Styles.Colors.DangerRed);
                else EditorGUI.DrawRect(barRect, Styles.Colors.AccentGreen);

                // ── 固定番号表示 (#1, #2...) ──
                // ローテーション順ではなく、ウィンドウ固定番号を表示
                if (isRotationItem)
                {
                    // 更新対象（リスト先頭）の場合、矢印付加
                    if (index == 0)
                    {
                        GUILayout.Label($"▶ #{fixedIndex}", Styles.BadgeNext, GUILayout.Width(42));
                    }
                    else
                    {
                        GUILayout.Label($"   #{fixedIndex}", Styles.BadgeOrder, GUILayout.Width(42));
                    }
                }
                else if (isExcludedItem)
                {
                    // 除外中でも固定番号は維持
                    GUILayout.Label($"   #{fixedIndex}", Styles.BadgeExcluded, GUILayout.Width(42));
                }
                else
                {
                    // Standard View / Unmanaged
                    GUILayout.Label($"   #{fixedIndex}", Styles.BadgeOrder, GUILayout.Width(42));
                }

                // ── ロックアイコン ──
                using (new EditorGUI.DisabledScope(isExcludedItem))
                {
                    var lockIcon = isLocked ? Styles.LockIcon : Styles.UnlockIcon;
                    if (GUILayout.Button(lockIcon, Styles.IconButton))
                    {
                        _inspectorService.SetLocked(inspector, !isLocked);
                    }
                }

                // ── 履歴モード役割ラベル (History Mode Only) ──
                if (isRotationItem && _rotationLockController.Mode == Controllers.RotationMode.History)
                {
                    string roleKey = index == 0 ? "History_Latest" : "History_Previous";
                    string roleLabel = index == 0 
                        ? _localizationService.GetString(roleKey)
                        : string.Format(_localizationService.GetString(roleKey), index);
                    
                    GUILayout.Label($"[{roleLabel}]", EditorStyles.miniLabel, GUILayout.Width(60));
                }
                
                // ── オブジェクト名 ──
                GUILayout.Space(4);
                var objectName = inspectedObject != null 
                    ? inspectedObject.name 
                    : (_localizationService?.GetString("Status_NoObject") ?? "(None)");

                using (new EditorGUI.DisabledScope(isExcludedItem))
                {
                    GUILayout.Label(objectName, EditorStyles.label);
                }
                
                GUILayout.FlexibleSpace();

                // ── 除外/追加・閉じるボタン ──
                if (!isStandardView) // Rotation ON時のみ除外操作を表示
                {
                    var btnText = isExcludedItem ? "＋" : "－";
                    var btnTooltip = isExcludedItem 
                        ? _localizationService.GetString("Tooltip_Include")
                        : _localizationService.GetString("Tooltip_Exclude");

                    if (GUILayout.Button(new GUIContent(btnText, btnTooltip), Styles.MiniButton, GUILayout.Width(28)))
                    {
                        _rotationLockController.SetExcluded(inspector, !isExcludedItem);
                    }
                }

                // 閉じるボタン
                var closeContent = new GUIContent("✕", _localizationService.GetString("Tooltip_CloseInspector"));
                if (GUILayout.Button(closeContent, Styles.MiniButton, GUILayout.Width(28)))
                {
                    inspector.Close();
                }

                // フォーカスボタン
                var focusContent = new GUIContent(_localizationService.GetString("Button_Focus"));
                if (GUILayout.Button(focusContent, Styles.MiniButton, GUILayout.Width(44)))
                {
                    inspector.Focus();
                }

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();
        }

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
